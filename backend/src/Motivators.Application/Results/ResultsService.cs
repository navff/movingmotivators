using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Motivators.Application.Common;
using Motivators.Domain;

namespace Motivators.Application.Results;

/// <summary>Результаты опроса: закрытый — из снимка, открытый — пересчёт на лету.</summary>
public class ResultsService(IAppDbContext db, IOptions<ResultsOptions> options)
{
    private readonly ResultsOptions _opts = options.Value;

    public async Task<object> ComputeAsync(Survey survey, CancellationToken ct)
    {
        var responses = await db.Responses.AsNoTracking()
            .Where(r => r.SurveyId == survey.Id)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);
        return Compute(survey, responses);
    }

    public object Compute(Survey survey, IReadOnlyList<Response> responses)
    {
        var catalog = MethodologyCatalog.Get(survey.MethodologyVersion);
        return survey.Type == SurveyTypes.HealthCheck
            ? HealthCheckResultsCalculator.Calculate(catalog, survey.Settings, survey.IsAnonymous,
                responses.Select(r => Input(r, Json.Deserialize<HealthCheckPayload>(r.Payload))).ToList(), _opts)
            : MotivatorsResultsCalculator.Calculate(catalog, survey.Settings, survey.IsAnonymous,
                responses.Select(r => Input(r, Json.Deserialize<MotivatorsPayload>(r.Payload))).ToList(), _opts);
    }

    /// <summary>JSON результатов (тот же DTO, что хранится в снимке).</summary>
    public async Task<string> GetJsonAsync(Survey survey, CancellationToken ct)
    {
        if (survey.Status == SurveyStatus.Closed)
        {
            var snapshot = await db.SurveyResultSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(s => s.SurveyId == survey.Id, ct);
            if (snapshot != null) return snapshot.Result;
        }
        return Json.Serialize(await ComputeAsync(survey, ct));
    }

    public async Task<T> GetAsync<T>(Survey survey, CancellationToken ct) =>
        Json.Deserialize<T>(await GetJsonAsync(survey, ct));

    /// <summary>Пересчитывает и сохраняет снимок (в контекст, без SaveChanges).</summary>
    public async Task<object> UpsertSnapshotAsync(Survey survey, DateTimeOffset now, CancellationToken ct)
    {
        var responses = await db.Responses.AsNoTracking()
            .Where(r => r.SurveyId == survey.Id).OrderBy(r => r.Id).ToListAsync(ct);
        var result = Compute(survey, responses);
        var snapshot = await db.SurveyResultSnapshots.FirstOrDefaultAsync(s => s.SurveyId == survey.Id, ct);
        if (snapshot == null)
        {
            snapshot = new SurveyResultSnapshot { SurveyId = survey.Id };
            db.SurveyResultSnapshots.Add(snapshot);
        }
        snapshot.ComputedAt = now;
        snapshot.ResponseCount = responses.Count;
        snapshot.Result = Json.Serialize(result);
        return result;
    }

    private static ResponseInput<T> Input<T>(Response r, T payload) =>
        new(r.Id, r.ParticipantName, payload, r.Comment, r.SubmittedOn);
}
