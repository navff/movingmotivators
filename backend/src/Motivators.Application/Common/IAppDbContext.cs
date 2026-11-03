using Microsoft.EntityFrameworkCore;
using Motivators.Domain;

namespace Motivators.Application.Common;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<LoginToken> LoginTokens { get; }
    DbSet<Session> Sessions { get; }
    DbSet<Team> Teams { get; }
    DbSet<Survey> Surveys { get; }
    DbSet<Response> Responses { get; }
    DbSet<SurveyResultSnapshot> SurveyResultSnapshots { get; }
    DbSet<Checkout> Checkouts { get; }
    DbSet<FunnelEvent> FunnelEvents { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
