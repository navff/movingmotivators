using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Motivators.Api.Auth;
using Motivators.Application.Results;
using Motivators.Application.Surveys;

namespace Motivators.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/surveys")]
public class SurveysController(SurveyService surveys) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public Task<SurveyDto> Get(Guid id, CancellationToken ct) => surveys.GetAsync(User.UserId(), id, ct);

    [HttpPatch("{id:guid}")]
    public Task<SurveyDto> Update(Guid id, UpdateSurveyRequest req, CancellationToken ct) =>
        surveys.UpdateAsync(User.UserId(), id, req, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await surveys.DeleteAsync(User.UserId(), id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/checkout/view")]
    public Task<CheckoutSummary> ViewCheckout(Guid id, CancellationToken ct) => surveys.ViewCheckoutAsync(User.UserId(), id, ct);

    [HttpPost("{id:guid}/checkout/method")]
    public async Task<IActionResult> SelectMethod(Guid id, CheckoutMethodRequest req, CancellationToken ct)
    {
        await surveys.SelectPaymentMethodAsync(User.UserId(), id, req.Method, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/checkout")]
    public Task<CheckoutResponse> Checkout(Guid id, CheckoutRequest req, CancellationToken ct) =>
        surveys.CheckoutAsync(User.UserId(), id, req.Method, ct);

    [HttpPost("{id:guid}/close")]
    public Task<SurveyDto> Close(Guid id, CancellationToken ct) => surveys.CloseAsync(User.UserId(), id, ct);

    [HttpPost("{id:guid}/reopen")]
    public Task<SurveyDto> Reopen(Guid id, ReopenRequest req, CancellationToken ct) =>
        surveys.ReopenAsync(User.UserId(), id, req.ClosesAt, ct);

    [HttpGet("{id:guid}/results")]
    public async Task<ContentResult> Results(Guid id, CancellationToken ct) =>
        Content(await surveys.ResultsJsonAsync(User.UserId(), id, ct), "application/json");

    [HttpGet("compare")]
    public Task<MotivatorsComparison> Compare([FromQuery] Guid a, [FromQuery] Guid b, CancellationToken ct) =>
        surveys.CompareAsync(User.UserId(), a, b, ct);
}
