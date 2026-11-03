using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Motivators.Api.Auth;
using Motivators.Application.Admin;

namespace Motivators.Api.Controllers;

[ApiController]
[Authorize(Roles = SessionAuthenticationHandler.AdminRole)]
[Route("api/admin")]
public class AdminController(AdminService admin) : ControllerBase
{
    [HttpGet("stats")]
    public Task<AdminStats> Stats([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        admin.StatsAsync(from, to, ct);

    [HttpGet("funnel")]
    public Task<AdminFunnel> Funnel([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        admin.FunnelAsync(from, to, ct);

    [HttpGet("users")]
    public Task<IReadOnlyList<AdminUser>> Users([FromQuery] string? q, CancellationToken ct) => admin.UsersAsync(q, ct);
}
