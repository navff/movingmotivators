using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Motivators.Application.Common;
using Motivators.Application.Email;
using Motivators.Infrastructure.Data;
using Motivators.Infrastructure.Email;

namespace Motivators.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // пустое значение приходит из compose, если переменная не задана в Coolify
        var cs = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionStrings:Default не задан (в Docker — переменная DB_CONNECTION_STRING)");
        services.AddDbContext<AppDbContext>(o => ConfigureDb(o, cs));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddOptions<SmtpOptions>()
            .Bind(config.GetSection(SmtpOptions.Section))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Smtp:Host не задан (в Docker — переменная SMTP_HOST)")
            .ValidateOnStart();
        services.AddSingleton<IEmailRenderer, FluidEmailRenderer>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        return services;
    }

    public static void ConfigureDb(DbContextOptionsBuilder o, string connectionString) =>
        o.UseNpgsql(connectionString, n => n.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();
}
