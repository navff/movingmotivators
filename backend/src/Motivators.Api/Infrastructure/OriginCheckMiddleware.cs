using Microsoft.Extensions.Options;
using Motivators.Application.Common;

namespace Motivators.Api.Infrastructure;

/// <summary>
/// Anti-CSRF для организаторских мутаций: вместе с SameSite=Lax проверяем, что Origin — наш сайт.
/// Запросы без Origin (не браузер) пропускаем: cookie-сессию они не получают от чужого сайта.
/// </summary>
public class OriginCheckMiddleware(RequestDelegate next, IOptions<AppOptions> app)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var req = ctx.Request;
        if (IsUnsafe(req.Method)
            && req.Path.StartsWithSegments("/api")
            && !req.Path.StartsWithSegments("/api/public")
            && req.Headers.Origin is { Count: > 0 } origins)
        {
            var origin = origins.ToString().TrimEnd('/');
            var own = $"{req.Scheme}://{req.Host}";
            var configured = new Uri(app.Value.BaseUrl).GetLeftPart(UriPartial.Authority);
            if (!string.Equals(origin, own, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(origin, configured, StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(
                    new { type = "about:blank", title = "Недопустимый источник запроса", status = 403, errorCode = "bad_origin" },
                    options: null, contentType: "application/problem+json");
                return;
            }
        }
        await next(ctx);
    }

    private static bool IsUnsafe(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));
}
