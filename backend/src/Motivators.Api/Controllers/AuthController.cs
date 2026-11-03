using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Motivators.Api.Auth;
using Motivators.Application.Auth;

namespace Motivators.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, SessionCookie cookie) : ControllerBase
{
    [HttpPost("request-link")]
    [EnableRateLimiting(RateLimitPolicies.AuthLink)]
    public async Task<IActionResult> RequestLink(RequestLinkRequest req, CancellationToken ct)
    {
        await auth.RequestLinkAsync(req, ct);
        return Accepted(new { message = "Если адрес корректный, мы отправили письмо со ссылкой для входа." });
    }

    [HttpPost("verify")]
    [EnableRateLimiting(RateLimitPolicies.AuthVerify)]
    public async Task<IActionResult> Verify(VerifyRequest req, CancellationToken ct)
    {
        var result = await auth.VerifyAsync(req.Token, ct);
        cookie.Write(HttpContext, result.SessionToken, result.SessionExpiresAt);
        return Ok(new { user = result.User, isNew = result.IsNew, returnUrl = result.ReturnUrl });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(cookie.Read(HttpContext), ct);
        cookie.Clear(HttpContext);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await auth.LogoutAllAsync(User.UserId(), ct);
        cookie.Clear(HttpContext);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController(AuthService auth, SessionCookie cookie) : ControllerBase
{
    [HttpGet]
    public Task<UserDto> Get(CancellationToken ct) => auth.GetMeAsync(User.UserId(), ct);

    [HttpPatch]
    public Task<UserDto> Update(UpdateMeRequest req, CancellationToken ct) => auth.UpdateMeAsync(User.UserId(), req, ct);

    [HttpPost("delete-request")]
    [EnableRateLimiting(RateLimitPolicies.AuthLink)]
    public async Task<IActionResult> DeleteRequest(CancellationToken ct)
    {
        await auth.RequestDeleteAsync(User.UserId(), ct);
        return Accepted();
    }

    /// <summary>Подтверждение по ссылке из письма; сессия не обязательна — токен самодостаточен.</summary>
    [AllowAnonymous]
    [HttpPost("delete-confirm")]
    public async Task<IActionResult> DeleteConfirm(VerifyRequest req, CancellationToken ct)
    {
        await auth.ConfirmDeleteAsync(req.Token, ct);
        cookie.Clear(HttpContext);
        return NoContent();
    }
}
