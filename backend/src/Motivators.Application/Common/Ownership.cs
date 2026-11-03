using Microsoft.EntityFrameworkCore;
using Motivators.Domain;

namespace Motivators.Application.Common;

/// <summary>
/// Централизованная проверка владения: чужой ресурс неотличим от несуществующего (404).
/// </summary>
public class Ownership(IAppDbContext db)
{
    public async Task<Team> TeamAsync(Guid userId, Guid teamId, CancellationToken ct) =>
        await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId && t.OwnerId == userId, ct)
        ?? throw AppException.NotFound();

    public async Task<Survey> SurveyAsync(Guid userId, Guid surveyId, CancellationToken ct) =>
        await db.Surveys.Include(s => s.Team)
            .FirstOrDefaultAsync(s => s.Id == surveyId && s.Team.OwnerId == userId, ct)
        ?? throw AppException.NotFound();
}
