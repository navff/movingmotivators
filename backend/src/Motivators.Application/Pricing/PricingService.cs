using Microsoft.Extensions.Options;
using Motivators.Application.Common;

namespace Motivators.Application.Pricing;

public record PricingDto(int FreeMaxParticipants, int SurveyPriceRub);

public interface IPricingService
{
    /// <summary>Цена опроса в рублях; 0 — бесплатно.</summary>
    int GetSurveyPrice(int participants);
    PricingDto Get();
}

public class PricingService(IOptions<PricingOptions> options) : IPricingService
{
    private readonly PricingOptions _o = options.Value;

    public int GetSurveyPrice(int participants) => participants > _o.FreeMaxParticipants ? _o.SurveyPriceRub : 0;

    public PricingDto Get() => new(_o.FreeMaxParticipants, _o.SurveyPriceRub);
}
