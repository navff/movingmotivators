using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Serilog.Debugging;

namespace Motivators.Api.Infrastructure;

/// <summary>
/// Делает видимыми ошибки отправки телеметрии. Без этого экспортёры молчат: при отказе ингеста
/// (нет роли, неверный проект) в логе приложения не появляется ничего.
/// <list type="bullet">
/// <item>трейсы — слушаем EventSource OpenTelemetry SDK и OTLP-экспортёра;</item>
/// <item>логи — Serilog SelfLog (ошибки sink'а нельзя писать через сам Serilog — будет рекурсия).</item>
/// </list>
/// Одинаковый текст — не чаще раза в минуту, чтобы не забить лог при постоянном отказе.
/// </summary>
public sealed class MoniumDiagnostics(ILogger<MoniumDiagnostics> logger) : IHostedService
{
    private static readonly TimeSpan Throttle = TimeSpan.FromMinutes(1);
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastSeen = new();
    private Listener? _listener;

    public Task StartAsync(CancellationToken ct)
    {
        _listener = new Listener((name, message) =>
        {
            if (ShouldReport(message)) logger.LogWarning("Телеметрия {Event}: {Detail}", name, message);
        });
        SelfLog.Enable(message =>
        {
            if (ShouldReport(message)) Console.Error.WriteLine($"warn: Телеметрия (логи): {message.Trim()}");
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _listener?.Dispose();
        SelfLog.Disable();
        return Task.CompletedTask;
    }

    internal static bool ShouldReport(string message)
    {
        // «2026-…Z …failed emitting a batch (Temporary, 14 events)» — время и счётчики разные,
        // а ошибка одна: сравниваем текст без них
        var key = Volatile.Replace(message, "#");
        var now = DateTimeOffset.UtcNow;
        var last = LastSeen.GetOrAdd(key, DateTimeOffset.MinValue);
        if (now - last < Throttle) return false;
        LastSeen[key] = now;
        return true;
    }

    private static readonly System.Text.RegularExpressions.Regex Volatile =
        new(@"^\S+Z\s+|\d+", System.Text.RegularExpressions.RegexOptions.Compiled);

    private sealed class Listener(Action<string, string> report) : EventListener
    {
        protected override void OnEventSourceCreated(EventSource source)
        {
            // OpenTelemetry-Sdk, OpenTelemetry-Exporter-OpenTelemetryProtocol и т. п.
            if (source.Name.StartsWith("OpenTelemetry", StringComparison.Ordinal))
                EnableEvents(source, EventLevel.Warning);
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (e.Level > EventLevel.Warning) return;
            string text;
            try
            {
                text = e.Message != null && e.Payload is { Count: > 0 }
                    ? string.Format(e.Message, [.. e.Payload])
                    : e.Message ?? string.Join("; ", e.Payload ?? []);
            }
            catch (FormatException)
            {
                text = string.Join("; ", e.Payload ?? []);
            }
            report(e.EventName ?? e.EventSource.Name, text);
        }
    }
}
