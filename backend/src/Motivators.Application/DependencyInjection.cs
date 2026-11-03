using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Motivators.Application.Admin;
using Motivators.Application.Auth;
using Motivators.Application.Catalog;
using Motivators.Application.Common;
using Motivators.Application.Payments;
using Motivators.Application.Pricing;
using Motivators.Application.Responses;
using Motivators.Application.Results;
using Motivators.Application.Surveys;
using Motivators.Application.Teams;

namespace Motivators.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<AppOptions>(config.GetSection(AppOptions.Section));
        services.Configure<PricingOptions>(config.GetSection(PricingOptions.Section));
        services.Configure<LegalOptions>(config.GetSection(LegalOptions.Section));
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.Section));
        services.Configure<ResultsOptions>(config.GetSection(ResultsOptions.Section));
        services.Configure<SurveyOptions>(config.GetSection(SurveyOptions.Section));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPricingService, PricingService>();
        services.AddSingleton<CatalogService>();

        services.AddScoped<Ownership>();
        services.AddScoped<Tracking>();
        services.AddScoped<ResultsService>();
        services.AddScoped<SurveyClosingService>();
        services.AddScoped<AuthService>();
        services.AddScoped<TeamService>();
        services.AddScoped<SurveyService>();
        services.AddScoped<PublicSurveyService>();
        services.AddScoped<AdminService>();
        services.AddScoped<MaintenanceService>();

        var mode = config[$"{PricingOptions.Section}:{nameof(PricingOptions.CheckoutMode)}"] ?? "fake";
        switch (mode)
        {
            case "fake":
                services.AddScoped<ICheckoutService, FakeCheckoutService>();
                break;
            default:
                // YooKassaCheckoutService — фаза 1.5
                throw new InvalidOperationException($"Pricing:CheckoutMode={mode} не поддерживается в MVP");
        }
        return services;
    }
}
