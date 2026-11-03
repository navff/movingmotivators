using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Motivators.Application.Common;
using Motivators.Application.Surveys;
using Motivators.Domain;

namespace Motivators.Application.Responses;

public record PublicMotivatorCard(string Key, string Name, string? EnglishName, string Statement, string AccentColor);

public record PublicCategory(string Key, string Name, string Awesome, string Awful);

public record PublicSurveyDto(
    string Code,
    string Status,
    string? Type = null,
    string? Title = null,
    string? TeamName = null,
    string? OrganizerName = null,
    string? WelcomeMessage = null,
    bool? IsAnonymous = null,
    DateTimeOffset? ClosesAt = null,
    IReadOnlyList<PublicMotivatorCard>? Cards = null,
    IReadOnlyList<SurveyChange>? Changes = null,
    IReadOnlyList<PublicCategory>? Categories = null,
    string? ConsentVersion = null);

public record SubmitResponseRequest(
    string RespondentKey,
    string? ParticipantName,
    JsonElement Payload,
    string? Comment,
    bool? Consent,
    string? Honeypot,
    long StartedAt);

public record SubmitResponseResult(bool Replaced);

public record MineRequest(string RespondentKey);

public record MyResponseDto(string? ParticipantName, JsonElement Payload, string? Comment, int UpdatedCount);

public static class ResponseLimits
{
    public const int CommentMax = 1000;
    public const int NameMax = 60;
}

public class PublicSurveyService(
    IAppDbContext db,
    SurveyClosingService closing,
    IOptions<AppOptions> appOptions,
    IOptions<LegalOptions> legalOptions,
    IOptions<SurveyOptions> surveyOptions,
    TimeProvider clock)
{
    private readonly SurveyOptions _opts = surveyOptions.Value;

    public async Task<PublicSurveyDto> GetAsync(string code, CancellationToken ct)
    {
        var survey = await FindAsync(code, ct);
        if (!IsAcceptingResponses(survey)) return new PublicSurveyDto(code, SurveyStatus.Closed);

        var catalog = MethodologyCatalog.Get(survey.MethodologyVersion);
        var owner = survey.Team.Owner;
        var showEnglish = appOptions.Value.ShowEnglishCardNames;
        var isMm = survey.Type == SurveyTypes.MovingMotivators;
        var enabled = survey.Settings.Categories ?? [];

        return new PublicSurveyDto(
            code,
            SurveyStatus.Open,
            survey.Type,
            survey.Title,
            survey.Team.Name,
            owner.DisplayName ?? owner.Email,
            survey.WelcomeMessage,
            survey.IsAnonymous,
            survey.ClosesAt,
            isMm ? catalog.Motivators.Select(m => new PublicMotivatorCard(m.Key, m.Name, showEnglish ? m.EnglishName : null, m.Statement, m.AccentColor)).ToList() : null,
            isMm ? survey.Settings.Changes ?? [] : null,
            isMm ? null : catalog.HealthCheckCategories.Where(c => enabled.Contains(c.Key))
                .Select(c => new PublicCategory(c.Key, c.Name, c.Awesome, c.Awful)).ToList(),
            survey.IsAnonymous ? null : legalOptions.Value.ParticipantConsentVersion);
    }

    public async Task<SubmitResponseResult> SubmitAsync(string code, SubmitResponseRequest req, CancellationToken ct)
    {
        var survey = await FindAsync(code, ct);
        if (!IsAcceptingResponses(survey))
            throw new AppException(410, ErrorCodes.SurveyClosed, "Опрос завершён");

        // honeypot: делаем вид, что всё хорошо, ничего не сохраняя
        if (!string.IsNullOrEmpty(req.Honeypot)) return new SubmitResponseResult(false);

        var now = clock.GetUtcNow();
        if (now.ToUnixTimeMilliseconds() - req.StartedAt < _opts.MinFillSeconds * 1000L)
            throw new AppException(400, ErrorCodes.TooFast, "Слишком быстро. Пожалуйста, пройдите опрос внимательно.");

        if (string.IsNullOrWhiteSpace(req.RespondentKey) || req.RespondentKey.Length > 100)
            throw AppException.Validation("respondentKey обязателен");

        var catalog = MethodologyCatalog.Get(survey.MethodologyVersion);
        var payloadJson = survey.Type == SurveyTypes.HealthCheck
            ? Json.Serialize(PayloadValidator.ValidateHealthCheck(req.Payload, catalog, survey.Settings))
            : Json.Serialize(PayloadValidator.ValidateMotivators(req.Payload, catalog, survey.Settings));

        var comment = string.IsNullOrWhiteSpace(req.Comment) ? null : req.Comment.Trim();
        if (comment?.Length > ResponseLimits.CommentMax)
            throw AppException.Validation($"Комментарий — не длиннее {ResponseLimits.CommentMax} символов");

        string? name = null;
        if (!survey.IsAnonymous)
        {
            name = req.ParticipantName?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > ResponseLimits.NameMax)
                throw AppException.Validation($"Укажите имя (до {ResponseLimits.NameMax} символов)",
                    new Dictionary<string, string[]> { ["participantName"] = ["Укажите имя"] });
            if (req.Consent != true)
                throw AppException.Validation("Нужно согласие на обработку персональных данных",
                    new Dictionary<string, string[]> { ["consent"] = ["Нужно согласие"] });
        }

        var hash = Crypto.RespondentHash(survey.RespondentSalt, req.RespondentKey);
        var existing = await db.Responses.FirstOrDefaultAsync(r => r.SurveyId == survey.Id && r.RespondentKeyHash == hash, ct);
        var response = existing;
        if (response == null)
        {
            var count = await db.Responses.CountAsync(r => r.SurveyId == survey.Id, ct);
            if (count >= _opts.MaxResponses)
                throw AppException.Conflict(ErrorCodes.ResponseLimit, "Достигнут лимит ответов на опрос");
            response = new Response { SurveyId = survey.Id, RespondentKeyHash = hash };
            db.Responses.Add(response);
            if (count + 1 > survey.DeclaredParticipants) survey.ResponsesOverDeclared = true;
        }
        else
        {
            response.UpdatedCount++;
        }

        response.Payload = payloadJson;
        response.Comment = comment;
        response.ParticipantName = name;
        // анонимный: дата — только с точностью до дня, без времени
        response.SubmittedOn = DateOnly.FromDateTime(now.UtcDateTime);
        response.SubmittedAt = survey.IsAnonymous ? null : now;
        response.ConsentVersion = survey.IsAnonymous ? null : legalOptions.Value.ParticipantConsentVersion;
        await db.SaveChangesAsync(ct);

        if (existing == null && survey.AutoCloseOnFull)
        {
            var total = await db.Responses.CountAsync(r => r.SurveyId == survey.Id, ct);
            if (total >= survey.DeclaredParticipants) await closing.CloseAsync(survey, ct);
        }

        return new SubmitResponseResult(existing != null);
    }

    public async Task<MyResponseDto?> MineAsync(string code, string respondentKey, CancellationToken ct)
    {
        var survey = await FindAsync(code, ct);
        if (!IsAcceptingResponses(survey) || string.IsNullOrEmpty(respondentKey)) return null;
        var hash = Crypto.RespondentHash(survey.RespondentSalt, respondentKey);
        var r = await db.Responses.AsNoTracking().FirstOrDefaultAsync(x => x.SurveyId == survey.Id && x.RespondentKeyHash == hash, ct);
        return r == null ? null : new MyResponseDto(r.ParticipantName, JsonDocument.Parse(r.Payload).RootElement, r.Comment, r.UpdatedCount);
    }

    private async Task<Survey> FindAsync(string code, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(code) || code.Length > 16) throw AppException.NotFound();
        var survey = await db.Surveys.Include(s => s.Team).ThenInclude(t => t.Owner)
            .FirstOrDefaultAsync(s => s.PublicCode == code, ct);
        // черновик неотличим от несуществующего
        if (survey == null || survey.Status == SurveyStatus.Draft) throw AppException.NotFound();
        return survey;
    }

    private bool IsAcceptingResponses(Survey s) =>
        s.Status == SurveyStatus.Open && (s.ClosesAt == null || s.ClosesAt > clock.GetUtcNow());
}
