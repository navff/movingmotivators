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
        var cs = config.GetConnectionString("Default")
                 ?? throw new InvalidOperationException("ConnectionStrings:Default не задан");
        services.AddDbContext<AppDbContext>(o => ConfigureDb(o, cs));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<SmtpOptions>(config.GetSection(SmtpOptions.Section));
        services.AddSingleton<IEmailRenderer, FluidEmailRenderer>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        return services;
    }

    public static void ConfigureDb(DbContextOptionsBuilder o, string connectionString) =>
        o.UseNpgsql(connectionString, n => n.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();
}
