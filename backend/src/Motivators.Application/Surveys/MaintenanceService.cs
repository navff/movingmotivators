using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Motivators.Application.Common;
using Motivators.Domain;

namespace Motivators.Application.Surveys;

/// <summary>Фоновые задачи: закрытие по сроку, очистка токенов/сессий, удаление старых черновиков.</summary>
public class MaintenanceService(
    IAppDbContext db,
    SurveyClosingService closing,
    IOptions<SurveyOptions> options,
    TimeProvider clock,
    ILogger<MaintenanceService> logger)
{
    public async Task<int> CloseExpiredAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var expired = await db.Surveys
            .Where(s => s.Status == SurveyStatus.Open && s.ClosesAt != null && s.ClosesAt <= now)
            .OrderBy(s => s.ClosesAt)
            .Take(50)
            .ToListAsync(ct);
        foreach (var s in expired)
        {
            try
            {
                await closing.CloseAsync(s, ct);
                logger.LogInformation("Опрос {SurveyId} закрыт по сроку", s.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Не удалось закрыть опрос {SurveyId}", s.Id);
            }
        }
        return expired.Count;
    }

    public async Task CleanupAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var tokens = await db.LoginTokens.Where(t => t.ExpiresAt < now.AddDays(-1)).ExecuteDeleteAsync(ct);
        var sessions = await db.Sessions.Where(s => s.ExpiresAt < now).ExecuteDeleteAsync(ct);
        var drafts = await db.Surveys
            .Where(s => s.Status == SurveyStatus.Draft && s.CreatedAt < now.AddDays(-options.Value.DraftRetentionDays))
            .ExecuteDeleteAsync(ct);
        if (tokens + sessions + drafts > 0)
            logger.LogInformation("Очистка: токенов {Tokens}, сессий {Sessions}, черновиков {Drafts}", tokens, sessions, drafts);
    }
}
