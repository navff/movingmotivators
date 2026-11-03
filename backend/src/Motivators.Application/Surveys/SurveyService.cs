using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Motivators.Application.Common;
using Motivators.Application.Payments;
using Motivators.Application.Pricing;
using Motivators.Application.Results;
using Motivators.Domain;

namespace Motivators.Application.Surveys;

public class SurveyService(
    IAppDbContext db,
    Ownership ownership,
    IPricingService pricing,
    ICheckoutService checkout,
    SurveyClosingService closing,
    ResultsService results,
    Tracking tracking,
    IOptions<AppOptions> appOptions,
    IOptions<LegalOptions> legalOptions,
    IOptions<SurveyOptions> surveyOptions,
    TimeProvider clock)
{
    private static readonly CreateSurveyValidator CreateValidator = new();
    private static readonly UpdateSurveyValidator UpdateValidator = new();
    private static readonly string[] Months =
        ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];

    private readonly SurveyOptions _opts = surveyOptions.Value;

    public async Task<CreateSurveyResponse> CreateAsync(Guid userId, Guid teamId, CreateSurveyRequest req, CancellationToken ct)
    {
        var team = await ownership.TeamAsync(userId, teamId, ct);
        CreateValidator.ValidateOrThrow(req);
        var now = clock.GetUtcNow();
        var catalog = MethodologyCatalog.Current;

        var settings = new SurveySettings();
        if (req.Type == SurveyTypes.MovingMotivators)
        {
            settings.Changes = (req.Changes ?? [])
                .Select((text, i) => new SurveyChange { Id = $"c{i + 1}", Text = text.Trim() })
                .ToList();
        }
        else
        {
            settings.Categories = NormalizeCategories(req.Categories, catalog);
        }

        var closesAt = req.ClosesAt ?? now.AddDays(_opts.DefaultDurationDays);
        if (closesAt <= now) throw Field("closesAt", "Срок закрытия должен быть в будущем");

        var price = pricing.GetSurveyPrice(req.DeclaredParticipants);
        var survey = new Survey
        {
            TeamId = team.Id,
            Team = team,
            Type = req.Type,
            MethodologyVersion = catalog.Version,
            Title = string.IsNullOrWhiteSpace(req.Title) ? DefaultTitle(req.Type, now) : req.Title.Trim(),
            IsAnonymous = req.IsAnonymous,
            DeclaredParticipants = req.DeclaredParticipants,
            PriceRub = price,
            PublicCode = await NewPublicCodeAsync(ct),
            RespondentSalt = Crypto.NewSalt(),
            WelcomeMessage = string.IsNullOrWhiteSpace(req.WelcomeMessage) ? null : req.WelcomeMessage.Trim(),
            Settings = settings,
            AutoCloseOnFull = req.AutoCloseOnFull,
            ClosesAt = closesAt,
            CreatedAt = now,
        };

        if (price > 0)
        {
            survey.Status = SurveyStatus.Draft;
            tracking.Funnel(userId, survey.Id, FunnelEvents.SurveyDraftPaidRequired,
                new { participants = req.DeclaredParticipants, priceRub = price, type = req.Type });
        }
        else
        {
            SurveyLifecycle.Open(survey, now, _opts);
        }

        db.Surveys.Add(survey);
        tracking.Audit(userId, "create", "survey", survey.Id);
        await db.SaveChangesAsync(ct);
        return new CreateSurveyResponse(ToDto(survey, team, 0), price > 0, price);
    }

    public async Task<SurveyDto> GetAsync(Guid userId, Guid surveyId, CancellationToken ct)
    {
        var survey = await ownership.SurveyAsync(userId, surveyId, ct);
        return await DtoAsync(survey, ct);
    }

    public async Task<SurveyDto> UpdateAsync(Guid userId, Guid surveyId, UpdateSurveyRequest req, CancellationToken ct)
    {
        var survey = await ownership.SurveyAsync(userId, surveyId, ct);
        UpdateValidator.ValidateOrThrow(req);
        var count = await db.Responses.CountAsync(r => r.SurveyId == survey.Id, ct);
        var now = clock.GetUtcNow();

        if (req.Title != null) survey.Title = req.Title.Trim();
        if (req.WelcomeMessage != null)
            survey.WelcomeMessage = string.IsNullOrWhiteSpace(req.WelcomeMessage) ? null : req.WelcomeMessage.Trim();
        if (req.AutoCloseOnFull != null) survey.AutoCloseOnFull = req.AutoCloseOnFull.Value;

        if (req.ClosesAt != null)
        {
            if (survey.Status == SurveyStatus.Closed) throw AppException.Conflict(ErrorCodes.InvalidState, "Опрос закрыт — используйте «Переоткрыть»");
            if (req.ClosesAt <= now) throw Field("closesAt", "Срок закрытия должен быть в будущем");
            survey.ClosesAt = req.ClosesAt;
        }

        if (req.IsAnonymous != null && req.IsAnonymous != survey.IsAnonymous)
        {
            if (count > 0) throw AppException.Conflict(ErrorCodes.Locked, "Режим анонимности нельзя изменить после первого ответа");
            survey.IsAnonymous = req.IsAnonymous.Value;
        }

        if (req.Changes != null)
        {
            if (survey.Type != SurveyTypes.MovingMotivators) throw Field("changes", "Изменения доступны только для «Движущих мотиваторов»");
            survey.Settings = new SurveySettings { Changes = MergeChanges(survey.Settings.Changes ?? [], req.Changes, locked: count > 0) };
        }

        if (req.Categories != null)
        {
            if (survey.Type != SurveyTypes.HealthCheck) throw Field("categories", "Категории доступны только для Health Check");
            var normalized = NormalizeCategories(req.Categories, MethodologyCatalog.Get(survey.MethodologyVersion));
            if (count > 0 && !normalized.SequenceEqual(survey.Settings.Categories ?? []))
                throw AppException.Conflict(ErrorCodes.Locked, "Категории нельзя менять после первого ответа");
            survey.Settings = new SurveySettings { Categories = normalized };
        }

        await db.SaveChangesAsync(ct);
        return await DtoAsync(survey, ct);
    }

    public async Task<CheckoutSummary> ViewCheckoutAsync(Guid userId, Guid surveyId, CancellationToken ct)
    {
        var survey = await RequireCheckoutAsync(userId, surveyId, ct);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var now = clock.GetUtcNow();

        if (!await db.Checkouts.AnyAsync(c => c.SurveyId == survey.Id && c.Status == CheckoutStatus.Created, ct))
            db.Checkouts.Add(new Motivators.Domain.Checkout
            {
                SurveyId = survey.Id, UserId = userId, AmountRub = survey.PriceRub, Mode = checkout.Mode, CreatedAt = now,
            });
        tracking.Funnel(userId, survey.Id, FunnelEvents.CheckoutViewed,
            new { amountRub = survey.PriceRub, participants = survey.DeclaredParticipants });
        await db.SaveChangesAsync(ct);

        var legal = legalOptions.Value;
        return new CheckoutSummary(survey.Id, survey.Type, survey.Title, survey.Team.Name, survey.DeclaredParticipants,
            survey.PriceRub, user.Email, legal.ShortName, legal.Inn);
    }

    public async Task SelectPaymentMethodAsync(Guid userId, Guid surveyId, string method, CancellationToken ct)
    {
        var survey = await RequireCheckoutAsync(userId, surveyId, ct);
        if (!PaymentMethods.IsValid(method)) throw Field("method", "Неизвестный способ оплаты");
        tracking.Funnel(userId, survey.Id, FunnelEvents.PaymentMethodSelected, new { method });
        await db.SaveChangesAsync(ct);
    }

    public async Task<CheckoutResponse> CheckoutAsync(Guid userId, Guid surveyId, string method, CancellationToken ct)
    {
        var survey = await RequireCheckoutAsync(userId, surveyId, ct);
        if (!PaymentMethods.IsValid(method)) throw Field("method", "Выберите способ оплаты");
        var result = await checkout.CheckoutAsync(survey, userId, method, ct);
        return new CheckoutResponse(result.Status, result.ConfirmationUrl, await DtoAsync(survey, ct));
    }

    public async Task<SurveyDto> CloseAsync(Guid userId, Guid surveyId, CancellationToken ct)
    {
        var survey = await ownership.SurveyAsync(userId, surveyId, ct);
        await closing.CloseAsync(survey, ct);
        return await DtoAsync(survey, ct);
    }

    public async Task<SurveyDto> ReopenAsync(Guid userId, Guid surveyId, DateTimeOffset closesAt, CancellationToken ct)
    {
        var survey = await ownership.SurveyAsync(userId, surveyId, ct);
        var now = clock.GetUtcNow();
        if (!CanReopen(survey, now)) throw AppException.Conflict(ErrorCodes.InvalidState,
            $"Переоткрыть можно закрытый опрос в течение {_opts.ReopenWindowDays} дней");
        if (closesAt <= now) throw Field("closesAt", "Срок закрытия должен быть в будущем");
        survey.Status = SurveyStatus.Open;
        survey.ClosesAt = closesAt;
        survey.ClosedAt = null;
        await db.SaveChangesAsync(ct);
        return await DtoAsync(survey, ct);
    }

    public async Task DeleteAsync(Guid userId, Guid surveyId, CancellationToken ct)
    {
        var survey = await ownership.SurveyAsync(userId, surveyId, ct);
        db.Surveys.Remove(survey);
        tracking.Audit(userId, "delete", "survey", survey.Id);
        await db.SaveChangesAsync(ct);
    }

    public async Task<string> ResultsJsonAsync(Guid userId, Guid surveyId, CancellationToken ct)
    {
        var survey = await ownership.SurveyAsync(userId, surveyId, ct);
        return await results.GetJsonAsync(survey, ct);
    }

    public async Task<MotivatorsComparison> CompareAsync(Guid userId, Guid a, Guid b, CancellationToken ct)
    {
        var sa = await ownership.SurveyAsync(userId, a, ct);
        var sb = await ownership.SurveyAsync(userId, b, ct);
        if (sa.TeamId != sb.TeamId || sa.Type != SurveyTypes.MovingMotivators || sb.Type != SurveyTypes.MovingMotivators)
            throw AppException.Validation("Сравнивать можно два опроса «Движущие мотиваторы» одной команды");
        // хронологически: A — раньше
        if ((sa.OpenedAt ?? sa.CreatedAt) > (sb.OpenedAt ?? sb.CreatedAt)) (sa, sb) = (sb, sa);
        var ra = await results.GetAsync<MotivatorsResult>(sa, ct);
        var rb = await results.GetAsync<MotivatorsResult>(sb, ct);
        return HistoryCalculator.CompareMotivators(
            new SurveyRef(sa.Id, sa.Title, sa.ClosedAt, ra.ResponseCount), ra, sa.IsAnonymous,
            new SurveyRef(sb.Id, sb.Title, sb.ClosedAt, rb.ResponseCount), rb, sb.IsAnonymous);
    }

    // ---------- helpers ----------

    private async Task<Survey> RequireCheckoutAsync(Guid userId, Guid surveyId, CancellationToken ct)
    {
        var survey = await ownership.SurveyAsync(userId, surveyId, ct);
        if (survey.Status != SurveyStatus.Draft || survey.PriceRub == 0)
            throw AppException.Conflict(ErrorCodes.InvalidState, "Опрос уже запущен");
        return survey;
    }

    private bool CanReopen(Survey s, DateTimeOffset now) =>
        s.Status == SurveyStatus.Closed && s.ClosedAt > now.AddDays(-_opts.ReopenWindowDays);

    private async Task<SurveyDto> DtoAsync(Survey survey, CancellationToken ct)
    {
        var count = await db.Responses.CountAsync(r => r.SurveyId == survey.Id, ct);
        return ToDto(survey, survey.Team, count);
    }

    private SurveyDto ToDto(Survey s, Team team, int responseCount) => new(
        s.Id, s.TeamId, team.Name, s.Type, s.Title, s.IsAnonymous, s.DeclaredParticipants, s.PriceRub,
        s.PublicCode, appOptions.Value.Url($"/s/{s.PublicCode}"), s.WelcomeMessage,
        new SurveySettingsDto(s.Settings.Changes ?? [], s.Settings.Categories ?? []),
        s.AutoCloseOnFull, s.Status, s.OpenedAt, s.ClosesAt, s.ClosedAt, s.CreatedAt,
        responseCount, s.ResponsesOverDeclared,
        s.Status == SurveyStatus.Draft && s.PriceRub > 0,
        CanReopen(s, clock.GetUtcNow()));

    public static SurveyListItem ToListItem(Survey s, int responseCount) => new(
        s.Id, s.Type, s.Title, s.Status, s.IsAnonymous, s.DeclaredParticipants, s.PriceRub, responseCount,
        s.CreatedAt, s.ClosesAt, s.ClosedAt, s.Status == SurveyStatus.Draft && s.PriceRub > 0);

    public static string DefaultTitle(string type, DateTimeOffset now) =>
        $"{(type == SurveyTypes.HealthCheck ? "Health Check" : "Движущие мотиваторы")} · {Months[now.Month - 1]} {now.Year}";

    private static List<string> NormalizeCategories(List<string>? requested, MethodologyVersionContent catalog)
    {
        var all = catalog.HealthCheckCategories.Select(c => c.Key).ToList();
        if (requested == null) return all;
        if (requested.Any(k => !all.Contains(k))) throw Field("categories", "Неизвестная категория");
        var set = requested.ToHashSet();
        if (set.Count < SurveyLimits.MinCategories)
            throw Field("categories", $"Оставьте минимум {SurveyLimits.MinCategories} категории");
        return all.Where(set.Contains).ToList();
    }

    /// <summary>
    /// До первого ответа — свободная правка (новые получают следующий id).
    /// После — только текст: тот же набор id в том же порядке.
    /// </summary>
    private static List<SurveyChange> MergeChanges(List<SurveyChange> current, List<ChangeInput> requested, bool locked)
    {
        if (locked)
        {
            if (requested.Count != current.Count || requested.Where((r, i) => r.Id != current[i].Id).Any())
                throw AppException.Conflict(ErrorCodes.Locked, "После первого ответа можно менять только текст изменений");
            return current.Select((c, i) => new SurveyChange { Id = c.Id, Text = requested[i].Text.Trim() }).ToList();
        }

        var known = current.Select(c => c.Id).ToHashSet();
        var next = current.Select(c => int.TryParse(c.Id.TrimStart('c'), out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        var result = new List<SurveyChange>();
        foreach (var r in requested)
        {
            var id = r.Id != null && known.Contains(r.Id) && result.All(x => x.Id != r.Id) ? r.Id : $"c{next++}";
            result.Add(new SurveyChange { Id = id, Text = r.Text.Trim() });
        }
        return result;
    }

    private async Task<string> NewPublicCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = Crypto.NewPublicCode();
            if (!await db.Surveys.AnyAsync(s => s.PublicCode == code, ct)) return code;
        }
    }

    private static AppException Field(string field, string message) =>
        AppException.Validation(message, new Dictionary<string, string[]> { [field] = [message] });
}
