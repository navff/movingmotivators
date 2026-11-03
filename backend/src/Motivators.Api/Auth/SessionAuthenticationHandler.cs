using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Motivators.Application.Auth;
using Motivators.Application.Common;

namespace Motivators.Api.Auth;

/// <summary>Аутентификация по cookie-сессии (хеш id сессии хранится в таблице sessions).</summary>
public class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AuthService auth,
    SessionCookie cookie,
    IOptions<AppOptions> app)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Session";
    public const string AdminRole = "admin";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = cookie.Read(Context);
        if (string.IsNullOrEmpty(token)) return AuthenticateResult.NoResult();

        var result = await auth.ValidateSessionAsync(token, Context.RequestAborted);
        if (result == null)
        {
            cookie.Clear(Context);
            return AuthenticateResult.NoResult();
        }

        var (user, expiresAt, renewed) = result.Value;
        if (renewed) cookie.Write(Context, token, expiresAt);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
        };
        if (app.Value.IsAdmin(user.Email)) claims.Add(new Claim(ClaimTypes.Role, AdminRole));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        Problem(StatusCodes.Status401Unauthorized, "unauthorized", "Требуется вход");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        Problem(StatusCodes.Status403Forbidden, "forbidden", "Недостаточно прав");

    private Task Problem(int status, string code, string title)
    {
        Response.StatusCode = status;
        Response.ContentType = "application/problem+json";
        return Response.WriteAsJsonAsync(new { type = "about:blank", title, status, errorCode = code },
            options: null, contentType: "application/problem+json");
    }
}

public class SessionCookie(IOptions<AuthOptions> options)
{
    private string Name => options.Value.CookieName;

    public string? Read(HttpContext ctx) => ctx.Request.Cookies[Name];

    public void Write(HttpContext ctx, string token, DateTimeOffset expiresAt) =>
        ctx.Response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true,
        });

    public void Clear(HttpContext ctx) =>
        ctx.Response.Cookies.Delete(Name, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/" });
}

public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Нет пользователя"));
}
