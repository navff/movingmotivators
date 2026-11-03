using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Motivators.Api.Auth;
using Motivators.Application.Common;
using Motivators.Application.Results;
using Motivators.Application.Surveys;
using Motivators.Application.Teams;
using Motivators.Domain;

namespace Motivators.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/teams")]
public class TeamsController(TeamService teams, SurveyService surveys) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TeamDto>> List(CancellationToken ct) => teams.ListAsync(User.UserId(), ct);

    [HttpPost]
    public async Task<IActionResult> Create(CreateTeamRequest req, CancellationToken ct)
    {
        var team = await teams.CreateAsync(User.UserId(), req, ct);
        return Created($"/api/teams/{team.Id}", team);
    }

    [HttpGet("{id:guid}")]
    public Task<TeamDetailsDto> Get(Guid id, CancellationToken ct) => teams.GetAsync(User.UserId(), id, ct);

    [HttpPatch("{id:guid}")]
    public Task<TeamDetailsDto> Update(Guid id, UpdateTeamRequest req, CancellationToken ct) =>
        teams.UpdateAsync(User.UserId(), id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await teams.DeleteAsync(User.UserId(), id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/history")]
    public async Task<HealthCheckHistory> History(Guid id, [FromQuery] string type = SurveyTypes.HealthCheck, CancellationToken ct = default)
    {
        if (type != SurveyTypes.HealthCheck) throw AppException.Validation("История поддерживается для health_check; для ММ — /api/surveys/compare");
        return await teams.HealthCheckHistoryAsync(User.UserId(), id, ct);
    }

    [HttpPost("{id:guid}/surveys")]
    public async Task<IActionResult> CreateSurvey(Guid id, CreateSurveyRequest req, CancellationToken ct)
    {
        var result = await surveys.CreateAsync(User.UserId(), id, req, ct);
        return Created($"/api/surveys/{result.Survey.Id}", result);
    }
}
