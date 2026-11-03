using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Motivators.Api;
using Motivators.Api.Auth;
using Motivators.Api.Infrastructure;
using Motivators.Application;
using Motivators.Application.Common;
using Motivators.Infrastructure;
using Motivators.Infrastructure.Data;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);
var monium = MoniumOptions.From(builder.Configuration);

builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
        // исходящие HTTP (ЮKassa и т. п.) пишут по 4 записи на запрос — оставляем только проблемы
        .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning);
    if (ctx.Configuration.GetValue("Logging:Json", true))
        cfg.WriteTo.Console(new RenderedCompactJsonFormatter());
    else
        cfg.WriteTo.Console();
    cfg.WriteToMonium(monium);
});
builder.Services.AddMoniumTracing(monium);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        var d = Json.Options;
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = d.DefaultIgnoreCondition;
        o.JsonSerializerOptions.Encoder = d.Encoder;
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        // ошибки биндинга (битый JSON, неверные типы) — тоже с errorCode
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var problem = new ValidationProblemDetails(ctx.ModelState)
            {
                Status = 400, Title = "Некорректные данные запроса",
            };
            problem.Extensions["errorCode"] = ErrorCodes.ValidationFailed;
            return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
        };
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();

builder.Services.AddSingleton<SessionCookie>();
builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services.AddRateLimits(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // api доступен только из docker-сети за nginx/Traefik
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    o.ForwardLimit = 2;
});

builder.Services.AddHealthChecks().AddCheck<DbHealthCheck>("db");

if (builder.Configuration.GetValue("Background:Enabled", true))
    builder.Services.AddHostedService<MaintenanceWorker>();

var app = builder.Build();

// одна строка при старте: куда идёт телеметрия — чтобы не искать данные «не в том кластере»
if (monium.IsActive)
    app.Logger.LogInformation("Monium: отправка включена, проект {Project}, кластер {Cluster}, сервис {Service}",
        monium.Project, monium.Cluster, monium.Service);
else if (monium.Enabled)
    app.Logger.LogWarning("Monium: отправка выключена — {Problem}", monium.Problem());

if (app.Configuration.GetValue("Db:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseSerilogRequestLogging(o =>
{
    // без IP, тел и query: только метод, путь-шаблон, статус и время
    o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} за {Elapsed:0} мс";
    o.GetLevel = (ctx, _, ex) => ex != null || ctx.Response.StatusCode >= 500
        ? Serilog.Events.LogEventLevel.Error
        : ctx.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose : Serilog.Events.LogEventLevel.Information;
});
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseMiddleware<OriginCheckMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: 404, title: "Не найдено",
    extensions: new Dictionary<string, object?> { ["errorCode"] = "not_found" }));

app.Run();

public partial class Program;

namespace Motivators.Api
{
    public class DbHealthCheck(AppDbContext db) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
            await db.Database.CanConnectAsync(ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("БД недоступна");
    }
}
