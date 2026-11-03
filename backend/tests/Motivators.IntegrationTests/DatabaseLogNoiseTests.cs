using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Motivators.Infrastructure.Data;

namespace Motivators.IntegrationTests;

/// <summary>
/// Успешные SQL-команды не должны попадать в лог на Information и выше:
/// фоновые задачи опрашивают БД по таймеру, и такие записи забивают логи и Monium.
/// Проверяем на настоящем PostgreSQL, с логгером без собственных фильтров.
/// </summary>
[Collection(ApiCollection.Name)]
public class DatabaseLogNoiseTests(ApiFactory api)
{
    [Fact]
    public async Task Successful_commands_are_not_logged_at_information()
    {
        var provider = new CapturingLoggerProvider();
        using var scope = api.Services.CreateScope();
        var options = new DbContextOptionsBuilder<AppDbContext>(
                scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>())
            .UseLoggerFactory(LoggerFactory.Create(b => b.AddProvider(provider).SetMinimumLevel(LogLevel.Trace)))
            .Options;

        await using (var db = new AppDbContext(options))
        {
            for (var i = 0; i < 5; i++) await db.Surveys.CountAsync();
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>("select 1 as \"Value\"").SingleAsync());
        }

        var noisy = provider.Entries
            .Where(e => e.Category.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) && e.Level >= LogLevel.Information)
            .Select(e => $"{e.Level} {e.Category}: {e.Message}")
            .ToList();
        Assert.Empty(noisy);
        // сами команды по-прежнему доступны на Debug — при отладке их можно включить
        Assert.Contains(provider.Entries, e => e.Category == "Microsoft.EntityFrameworkCore.Database.Command" && e.Level == LogLevel.Debug);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(string Category, LogLevel Level, string Message)> Entries { get; } = new();
        public ILogger CreateLogger(string category) => new Logger(category, Entries);
        public void Dispose() { }

        private sealed class Logger(string category, ConcurrentQueue<(string, LogLevel, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> fmt) =>
                entries.Enqueue((category, level, fmt(state, ex)));
        }
    }
}
