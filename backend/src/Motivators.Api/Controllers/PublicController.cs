using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Motivators.Application.Responses;

namespace Motivators.Api.Controllers;

/// <summary>Участник — без авторизации. IP здесь не логируется и не сохраняется.</summary>
[ApiController]
[Route("api/public/surveys/{code}")]
public class PublicController(PublicSurveyService service) : ControllerBase
{
    [HttpGet]
    public Task<PublicSurveyDto> Get(string code, CancellationToken ct) => service.GetAsync(code, ct);

    [HttpPost("responses")]
    [EnableRateLimiting(RateLimitPolicies.PublicSubmit)]
    public async Task<IActionResult> Submit(string code, SubmitResponseRequest req, CancellationToken ct)
    {
        var result = await service.SubmitAsync(code, req, ct);
        return result.Replaced ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Ключ — в теле, а не в query, чтобы не попадал в логи прокси.</summary>
    [HttpPost("responses/mine")]
    [EnableRateLimiting(RateLimitPolicies.PublicSubmit)]
    public async Task<IActionResult> Mine(string code, MineRequest req, CancellationToken ct)
    {
        var mine = await service.MineAsync(code, req.RespondentKey, ct);
        return mine == null ? NoContent() : Ok(mine);
    }
}
