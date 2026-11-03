using System.Diagnostics;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace Motivators.Api.Infrastructure;

/// <summary>
/// Monium (Yandex Cloud): логи и трейсы по OTLP/HTTP.
/// https://yandex.cloud/ru/docs/monium/collector/otlp-protocol
/// </summary>
public class MoniumOptions
{
    public const string Section = "Monium";
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "https://ingest.monium.yandex.cloud/otlp";
    /// <summary>API-ключ сервисного аккаунта с ролью monium.telemetry.writer.</summary>
    public string? ApiKey { get; set; }
    /// <summary>Проект Monium (заголовок x-monium-project).</summary>
    public string? Project { get; set; }
    public string Cluster { get; set; } = "production";
    public string Service { get; set; } = "api";

    public bool IsActive => Enabled && Problem() == null;

    /// <summary>Почему при Enabled=true отправка всё же выключена; null — всё в порядке.</summary>
    public string? Problem()
    {
        if (string.IsNullOrWhiteSpace(ApiKey)) return "не задан Monium:ApiKey (MONIUM__APIKEY)";
        if (string.IsNullOrWhiteSpace(Project)) return "не задан Monium:Project (MONIUM__PROJECT)";
        // Ингест отвечает «200 Hello! This is Monium.» на любой путь без /otlp — экспортёр
        // считает это успехом, и телеметрия молча пропадает. Поэтому неверный путь — ошибка конфигурации.
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) || !uri.AbsolutePath.TrimEnd('/').EndsWith("/otlp", StringComparison.Ordinal))
            return $"Monium:Endpoint должен оканчиваться на /otlp (сейчас {Endpoint})";
        return null;
    }

    public Dictionary<string, string> Headers() => new()
    {
        ["Authorization"] = $"Api-Key {ApiKey}",
        ["x-monium-project"] = Project!,
        ["x-monium-cluster"] = Cluster,
        ["x-monium-service"] = Service,
    };

    public static MoniumOptions From(IConfiguration config) =>
        config.GetSection(Section).Get<MoniumOptions>() ?? new MoniumOptions();
}

public static class MoniumExtensions
{
    /// <summary>Источник спанов приложения (фоновые задачи и т. п.).</summary>
    public static readonly ActivitySource Source = new("Motivators");

    /// <summary>
    /// Атрибуты, которые инструментирование ASP.NET Core пишет по умолчанию, но которые нам нельзя отправлять:
    /// IP и user-agent участников (§6 SPEC), query-строки.
    /// </summary>
    private static readonly string[] PrivateTags =
        ["client.address", "client.port", "network.peer.address", "network.peer.port", "user_agent.original", "url.query"];

    public static void WriteToMonium(this LoggerConfiguration cfg, MoniumOptions o)
    {
        if (!o.IsActive) return;
        cfg.WriteTo.OpenTelemetry(s =>
        {
            s.Endpoint = null;
            s.LogsEndpoint = $"{o.Endpoint.TrimEnd('/')}/v1/logs";
            s.Protocol = OtlpProtocol.HttpProtobuf;
            s.Headers = o.Headers();
            s.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = o.Service,
                ["deployment.name"] = o.Cluster,
            };
        });
    }

    public static IServiceCollection AddMoniumTracing(this IServiceCollection services, MoniumOptions o)
    {
        if (!o.IsActive) return services;

        services.AddHostedService<MoniumDiagnostics>();
        var ingestHost = new Uri(o.Endpoint).Host;
        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(o.Service)
                .AddAttributes([new("deployment.name", o.Cluster)]))
            .WithTracing(t => t
                .AddSource(Source.Name)
                .AddAspNetCoreInstrumentation(a =>
                {
                    a.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                    a.RecordException = true;
                })
                // своих исходящих HTTP пока нет (появятся с ЮKassa); отправку логов в Monium не трейсим
                .AddHttpClientInstrumentation(h => h.FilterHttpRequestMessage = req => req.RequestUri?.Host != ingestHost)
                .AddNpgsql()
                .AddProcessor(new PrivacyProcessor())
                .AddOtlpExporter(e =>
                {
                    e.Endpoint = new Uri($"{o.Endpoint.TrimEnd('/')}/v1/traces");
                    e.Protocol = OtlpExportProtocol.HttpProtobuf;
                    // заголовки — через HttpClient: значение «Api-Key …» с пробелом надёжнее, чем строка OTEL headers
                    e.HttpClientFactory = () =>
                    {
                        var client = new HttpClient();
                        foreach (var (k, v) in o.Headers()) client.DefaultRequestHeaders.TryAddWithoutValidation(k, v);
                        return client;
                    };
                }));
        return services;
    }

    private sealed class PrivacyProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            foreach (var tag in PrivateTags) activity.SetTag(tag, null);
        }
    }
}
