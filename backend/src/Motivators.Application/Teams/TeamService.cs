using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Motivators.Application.Common;
using Motivators.Application.Results;
using Motivators.Application.Surveys;
using Motivators.Domain;

namespace Motivators.Application.Teams;

public record LastSurveyDto(Guid Id, string Title, string Type, string Status, DateTimeOffset CreatedAt);

public record TeamDto(
    Guid Id,
    string Name,
    int Size,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    int SurveyCount,
    int OpenSurveyCount,
    LastSurveyDto? LastSurvey);

public record TeamDetailsDto(
    Guid Id,
    string Name,
    int Size,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SurveyListItem> Surveys);

public record CreateTeamRequest(string Name, int Size);

public record UpdateTeamRequest(string? Name, int? Size, bool? Archived);

public static class TeamLimits
{
    public const int NameMax = 100;
    public const int MinSize = 2;
    public const int MaxSize = 200;
}

public class CreateTeamValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Укажите название команды")
            .MaximumLength(TeamLimits.NameMax).WithMessage($"Название — не длиннее {TeamLimits.NameMax} символов");
        RuleFor(x => x.Size).InclusiveBetween(TeamLimits.MinSize, TeamLimits.MaxSize)
            .WithMessage($"Размер команды — от {TeamLimits.MinSize} до {TeamLimits.MaxSize} человек");
    }
}

public class UpdateTeamValidator : AbstractValidator<UpdateTeamRequest>
{
    public UpdateTeamValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(TeamLimits.NameMax).When(x => x.Name != null)
            .WithMessage($"Название — от 1 до {TeamLimits.NameMax} символов");
        RuleFor(x => x.Size).InclusiveBetween(TeamLimits.MinSize, TeamLimits.MaxSize).When(x => x.Size != null)
            .WithMessage($"Размер команды — от {TeamLimits.MinSize} до {TeamLimits.MaxSize} человек");
    }
}

public class TeamService(
    IAppDbContext db,
    Ownership ownership,
    ResultsService results,
    Tracking tracking,
    TimeProvider clock)
{
    private static readonly CreateTeamValidator CreateValidator = new();
    private static readonly UpdateTeamValidator UpdateValidator = new();

    public async Task<IReadOnlyList<TeamDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        var teams = await db.Teams.AsNoTracking()
            .Where(t => t.OwnerId == userId)
            .OrderBy(t => t.ArchivedAt != null).ThenByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                Team = t,
                SurveyCount = t.Surveys.Count,
                OpenCount = t.Surveys.Count(s => s.Status == SurveyStatus.Open),
                Last = t.Surveys.OrderByDescending(s => s.CreatedAt)
                    .Select(s => new LastSurveyDto(s.Id, s.Title, s.Type, s.Status, s.CreatedAt))
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);
        return teams.Select(x => new TeamDto(x.Team.Id, x.Team.Name, x.Team.Size, x.Team.ArchivedAt,
            x.Team.CreatedAt, x.SurveyCount, x.OpenCount, x.Last)).ToList();
    }

    public async Task<TeamDto> CreateAsync(Guid userId, CreateTeamRequest req, CancellationToken ct)
    {
        CreateValidator.ValidateOrThrow(req);
        var team = new Team { OwnerId = userId, Name = req.Name.Trim(), Size = req.Size, CreatedAt = clock.GetUtcNow() };
        db.Teams.Add(team);
        tracking.Audit(userId, "create", "team", team.Id);
        await db.SaveChangesAsync(ct);
        return new TeamDto(team.Id, team.Name, team.Size, null, team.CreatedAt, 0, 0, null);
    }

    public async Task<TeamDetailsDto> GetAsync(Guid userId, Guid teamId, CancellationToken ct)
    {
        var team = await ownership.TeamAsync(userId, teamId, ct);
        var surveys = await db.Surveys.AsNoTracking()
            .Where(s => s.TeamId == team.Id)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { Survey = s, Count = s.Responses.Count })
            .ToListAsync(ct);
        return new TeamDetailsDto(team.Id, team.Name, team.Size, team.ArchivedAt, team.CreatedAt,
            surveys.Select(x => SurveyService.ToListItem(x.Survey, x.Count)).ToList());
    }

    public async Task<TeamDetailsDto> UpdateAsync(Guid userId, Guid teamId, UpdateTeamRequest req, CancellationToken ct)
    {
        var team = await ownership.TeamAsync(userId, teamId, ct);
        UpdateValidator.ValidateOrThrow(req);
        if (req.Name != null) team.Name = req.Name.Trim();
        if (req.Size != null) team.Size = req.Size.Value;
        if (req.Archived != null) team.ArchivedAt = req.Archived.Value ? team.ArchivedAt ?? clock.GetUtcNow() : null;
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, teamId, ct);
    }

    public async Task DeleteAsync(Guid userId, Guid teamId, CancellationToken ct)
    {
        var team = await ownership.TeamAsync(userId, teamId, ct);
        db.Teams.Remove(team);
        tracking.Audit(userId, "delete", "team", team.Id);
        await db.SaveChangesAsync(ct);
    }

    public async Task<HealthCheckHistory> HealthCheckHistoryAsync(Guid userId, Guid teamId, CancellationToken ct)
    {
        var team = await ownership.TeamAsync(userId, teamId, ct);
        var surveys = await db.Surveys.AsNoTracking()
            .Where(s => s.TeamId == team.Id && s.Type == SurveyTypes.HealthCheck && s.Status == SurveyStatus.Closed)
            .OrderBy(s => s.ClosedAt)
            .ToListAsync(ct);
        var waves = new List<(SurveyRef, HealthCheckResult)>();
        foreach (var s in surveys)
        {
            var r = await results.GetAsync<HealthCheckResult>(s, ct);
            waves.Add((new SurveyRef(s.Id, s.Title, s.ClosedAt, r.ResponseCount), r));
        }
        return HistoryCalculator.HealthCheck(MethodologyCatalog.Current, waves);
    }
}
