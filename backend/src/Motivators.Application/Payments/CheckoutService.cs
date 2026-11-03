using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Motivators.Application.Common;
using Motivators.Domain;

namespace Motivators.Application.Payments;

public record CheckoutResult(string Status, string? ConfirmationUrl);

public static class CheckoutResultStatus
{
    /// <summary>Режим раннего доступа: опрос запущен, деньги не списываются.</summary>
    public const string LaunchedFree = "launched_free";
    /// <summary>Реальный платёж: нужно перейти по confirmationUrl.</summary>
    public const string PaymentPending = "payment_pending";
}

/// <summary>
/// Шаг «draft → checkout → open». В MVP — <see cref="FakeCheckoutService"/>;
/// после MVP — YooKassaCheckoutService (выбор по <c>Pricing:CheckoutMode</c>).
/// </summary>
public interface ICheckoutService
{
    string Mode { get; }
    Task<CheckoutResult> CheckoutAsync(Survey survey, Guid userId, string method, CancellationToken ct);
}

public class FakeCheckoutService(
    IAppDbContext db,
    Tracking tracking,
    IOptions<SurveyOptions> surveyOptions,
    TimeProvider clock) : ICheckoutService
{
    public string Mode => "fake";

    public async Task<CheckoutResult> CheckoutAsync(Survey survey, Guid userId, string method, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var checkout = await db.Checkouts
            .Where(c => c.SurveyId == survey.Id && c.Status == CheckoutStatus.Created)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (checkout == null)
        {
            checkout = new Motivators.Domain.Checkout
            {
                SurveyId = survey.Id, UserId = userId, AmountRub = survey.PriceRub, Mode = Mode, CreatedAt = now,
            };
            db.Checkouts.Add(checkout);
        }
        checkout.Method = method;
        checkout.Status = CheckoutStatus.Clicked;
        checkout.ClickedAt = now;

        tracking.Funnel(userId, survey.Id, FunnelEvents.PaymentClicked,
            new { method, amountRub = survey.PriceRub, participants = survey.DeclaredParticipants });
        tracking.Audit(userId, "checkout", "survey", survey.Id);

        SurveyLifecycle.Open(survey, now, surveyOptions.Value);
        await db.SaveChangesAsync(ct);
        return new CheckoutResult(CheckoutResultStatus.LaunchedFree, null);
    }
}

public static class SurveyLifecycle
{
    public static void Open(Survey survey, DateTimeOffset now, SurveyOptions opts)
    {
        survey.Status = SurveyStatus.Open;
        survey.OpenedAt ??= now;
        if (survey.ClosesAt == null || survey.ClosesAt <= now)
            survey.ClosesAt = now.AddDays(opts.DefaultDurationDays);
    }
}
