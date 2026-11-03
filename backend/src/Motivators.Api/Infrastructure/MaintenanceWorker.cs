using Motivators.Application.Surveys;

namespace Motivators.Api.Infrastructure;

/// <summary>Раз в минуту закрывает просроченные опросы, раз в час чистит токены, сессии и старые черновики.</summary>
public class MaintenanceWorker(IServiceScopeFactory scopes, ILogger<MaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        var lastCleanup = DateTimeOffset.MinValue;
        do
        {
            using var activity = MoniumExtensions.Source.StartActivity("maintenance");
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var maintenance = scope.ServiceProvider.GetRequiredService<MaintenanceService>();
                await maintenance.CloseExpiredAsync(ct);
                if (DateTimeOffset.UtcNow - lastCleanup > TimeSpan.FromHours(1))
                {
                    await maintenance.CleanupAsync(ct);
                    lastCleanup = DateTimeOffset.UtcNow;
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Ошибка фоновой задачи");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
