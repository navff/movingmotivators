using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Motivators.Application.Catalog;
using Motivators.Application.Common;
using Motivators.Application.Pricing;

namespace Motivators.Api.Controllers;

[ApiController]
[Route("api")]
public class CatalogController(CatalogService catalog, IPricingService pricing, IOptions<LegalOptions> legal, IOptions<AppOptions> app)
    : ControllerBase
{
    [HttpGet("catalog")]
    [ResponseCache(Duration = 300)]
    public CatalogDto Catalog() => catalog.Get();

    [HttpGet("pricing")]
    public PricingDto Pricing() => pricing.Get();

    /// <summary>Реквизиты и название — для футера и юр. страниц.</summary>
    [HttpGet("site")]
    [ResponseCache(Duration = 300)]
    public object Site()
    {
        var l = legal.Value;
        return new
        {
            productName = app.Value.ProductName,
            baseUrl = app.Value.BaseUrl,
            legal = new { l.Name, l.ShortName, l.Inn, l.Ogrnip, l.Address, l.Email, l.OrganizerConsentVersion, l.ParticipantConsentVersion },
        };
    }
}
