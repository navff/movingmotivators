using Motivators.Domain;

namespace Motivators.Application.Common;

/// <summary>Аудит и события воронки. Записи добавляются в контекст, сохраняет вызывающий сценарий.</summary>
public class Tracking(IAppDbContext db, TimeProvider clock)
{
    public void Audit(Guid? userId, string action, string entity, Guid? entityId) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = action, Entity = entity, EntityId = entityId, At = clock.GetUtcNow(),
        });

    public void Funnel(Guid userId, Guid? surveyId, string evt, object? props = null) =>
        db.FunnelEvents.Add(new FunnelEvent
        {
            UserId = userId, SurveyId = surveyId, Event = evt,
            Props = Json.Serialize(props ?? new { }), At = clock.GetUtcNow(),
        });
}
