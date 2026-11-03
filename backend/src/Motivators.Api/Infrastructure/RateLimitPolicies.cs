using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Motivators.Api;

public static class RateLimitPolicies
{
    public const string AuthLink = "auth-link";
    public const string AuthVerify = "auth-verify";
    public const string PublicSubmit = "public-submit";

    public static IServiceCollection AddRateLimits(this IServiceCollection services, IConfiguration config)
    {
        var s = config.GetSection("RateLimits");
        var linkPerHour = s.GetValue("AuthLinkPerIpPerHour", 20);
        var verifyPerMinute = s.GetValue("AuthVerifyPerIpPerMinute", 30);
        var submitPerMinute = s.GetValue("PublicSubmitPerIpPerMinute", 10);

        static string Ip(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.ContentType = "application/problem+json";
                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "about:blank", title = "Слишком много запросов, попробуйте позже", status = 429, errorCode = "rate_limited",
                }, options: null, contentType: "application/problem+json", ct);
            };
            o.AddPolicy(AuthLink, ctx => RateLimitPartition.GetFixedWindowLimiter(Ip(ctx),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = linkPerHour, Window = TimeSpan.FromHours(1) }));
            o.AddPolicy(AuthVerify, ctx => RateLimitPartition.GetFixedWindowLimiter(Ip(ctx),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = verifyPerMinute, Window = TimeSpan.FromMinutes(1) }));
            // IP используется только в памяти как ключ лимита и нигде не сохраняется
            o.AddPolicy(PublicSubmit, ctx => RateLimitPartition.GetFixedWindowLimiter(Ip(ctx),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = submitPerMinute, Window = TimeSpan.FromMinutes(1) }));
        });
        return services;
    }
}
