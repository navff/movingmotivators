using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Motivators.Application.Common;
using Motivators.Application.Email;
using Motivators.Application.Results;
using Motivators.Domain;

namespace Motivators.Application.Surveys;

/// <summary>Закрытие опроса: статус, снимок результатов, письмо организатору.</summary>
public class SurveyClosingService(
    IAppDbContext db,
    ResultsService results,
    IEmailSender email,
    IEmailRenderer renderer,
    IOptions<AppOptions> app,
    TimeProvider clock,
    ILogger<SurveyClosingService> logger)
{
    public async Task CloseAsync(Survey survey, CancellationToken ct)
    {
        if (survey.Status != SurveyStatus.Open) throw AppException.Conflict(ErrorCodes.InvalidState, "Опрос не открыт");
        var now = clock.GetUtcNow();
        survey.Status = SurveyStatus.Closed;
        survey.ClosedAt = now;
        var result = await results.UpsertSnapshotAsync(survey, now, ct);
        await db.SaveChangesAsync(ct);

        try
        {
            await SendClosedEmailAsync(survey, result, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось отправить письмо о закрытии опроса {SurveyId}", survey.Id);
        }
    }

    private async Task SendClosedEmailAsync(Survey survey, object result, CancellationToken ct)
    {
        var team = await db.Teams.AsNoTracking().Include(t => t.Owner).FirstAsync(t => t.Id == survey.TeamId, ct);
        var catalog = MethodologyCatalog.Get(survey.MethodologyVersion);
        var o = app.Value;

        object Card(string key)
        {
            var m = catalog.FindMotivator(key)!;
            return new { name = m.Name, color = m.AccentColor, textColor = Colors.TextOn(m.AccentColor) };
        }

        var model = new Dictionary<string, object?>
        {
            ["productName"] = o.ProductName,
            ["surveyTitle"] = survey.Title,
            ["teamName"] = team.Name,
            ["responseCount"] = result is MotivatorsResult mr ? mr.ResponseCount : ((HealthCheckResult)result).ResponseCount,
            ["reportUrl"] = o.Url($"/app/surveys/{survey.Id}"),
            ["isMotivators"] = survey.Type == SurveyTypes.MovingMotivators,
        };

        if (result is MotivatorsResult m)
        {
            model["top3"] = m.Top3.Select(Card).ToList();
            model["bottom3"] = m.Bottom3.Select(Card).ToList();
            var painful = m.Changes.FirstOrDefault(c => c.Id == m.MostPainfulChangeId);
            model["painfulChange"] = painful == null ? null : new { text = painful.Text, overall = painful.Overall.ToString("+0.00;−0.00;0") };
        }
        else if (result is HealthCheckResult h)
        {
            model["categories"] = h.Categories.Select(c => new
            {
                name = catalog.FindCategory(c.Key)!.Name,
                avg = c.AvgScore?.ToString("0.0") ?? "—",
                color = Colors.TrafficHex(c.Color),
                label = Colors.TrafficLabel(c.Color),
                trend = c.Trend switch { 1 => "↑", -1 => "↓", 0 => "→", _ => "" },
            }).ToList();
            model["focus"] = h.Focus.Select(k => catalog.FindCategory(k)!.Name).ToList();
        }

        var (html, text) = renderer.Render(EmailTemplates.SurveyClosed, model);
        await email.SendAsync(new EmailMessage(team.Owner.Email,
            $"Опрос «{survey.Title}» команды «{team.Name}» завершён", html, text), ct);
    }
}

public static class Colors
{
    /// <summary>
    /// Цвет текста на фоне accentColor по относительной яркости (WCAG): тёмный для светлых фонов.
    /// Порог 0.55 — та же формула во frontend (textOn в lib/colors.ts).
    /// </summary>
    public static string TextOn(string hex)
    {
        var h = hex.TrimStart('#');
        double Lin(int i)
        {
            var c = Convert.ToInt32(h.Substring(i, 2), 16) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        var l = 0.2126 * Lin(0) + 0.7152 * Lin(2) + 0.0722 * Lin(4);
        return l > 0.55 ? "#1A1A1A" : "#FFFFFF";
    }

    public static string TrafficHex(string? color) => color switch
    {
        TrafficLight.Green => "#22A559",
        TrafficLight.Yellow => "#F2C230",
        TrafficLight.Red => "#E5484D",
        _ => "#BBBBBB",
    };

    public static string TrafficLabel(string? color) => color switch
    {
        TrafficLight.Green => "Отлично",
        TrafficLight.Yellow => "Есть проблемы",
        TrafficLight.Red => "Плохо",
        _ => "Нет данных",
    };
}
