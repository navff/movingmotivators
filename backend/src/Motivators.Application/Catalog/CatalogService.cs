using Microsoft.Extensions.Options;
using Motivators.Application.Common;
using Motivators.Domain;

namespace Motivators.Application.Catalog;

public record CatalogDto(
    int Version,
    bool ShowEnglishCardNames,
    IReadOnlyList<MotivatorCard> Motivators,
    IReadOnlyList<HealthCheckCategory> HealthCheckCategories);

public class CatalogService(IOptions<AppOptions> app)
{
    public CatalogDto Get()
    {
        var c = MethodologyCatalog.Current;
        return new CatalogDto(c.Version, app.Value.ShowEnglishCardNames, c.Motivators, c.HealthCheckCategories);
    }
}
