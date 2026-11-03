using Microsoft.EntityFrameworkCore;
using Motivators.Application.Common;
using Motivators.Domain;

namespace Motivators.Application.Admin;

public record DayStats(DateOnly Day, int Registrations, int Surveys, int Responses);

public record AdminStats(
    DateOnly From,
    DateOnly To,
    int TotalUsers,
    int TotalTeams,
    int TotalSurveys,
    int TotalResponses,
    int SurveysOverDeclared,
    IReadOnlyList<DayStats> Days);

public record CountItem(string Key, int Count);

public record AdminFunnel(
    DateOnly From,
    DateOnly To,
    int FreeSurveys,
    int PaidRequiredSurveys,
    int CheckoutViewed,
    int PaymentMethodSelected,
    int PaymentClicked,
    int CheckoutAbandoned,
    /// <summary>payment_clicked / survey_draft_paid_required, 0..1.</summary>
    double ConversionFromDraft,
    /// <summary>payment_clicked / checkout_viewed, 0..1.</summary>
    double ConversionFromCheckout,
    IReadOnlyList<CountItem> ByMethod,
    IReadOnlyList<CountItem> BySize);

public record AdminUser(Guid Id, string Email, string? DisplayName, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt, int Teams, int Surveys);

public class AdminService(IAppDbContext db, TimeProvider clock)
{
    public (DateOnly From, DateOnly To, DateTimeOffset FromTs, DateTimeOffset ToTs) Range(DateOnly? from, DateOnly? to)
    {
        var t = to ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var f = from ?? t.AddDays(-29);
        if (f > t) (f, t) = (t, f);
        return (f, t, new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(t.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
    }

    public async Task<AdminStats> StatsAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (f, t, fts, tts) = Range(from, to);
        var regs = await db.Users.Where(u => u.CreatedAt >= fts && u.CreatedAt < tts).Select(u => u.CreatedAt).ToListAsync(ct);
        var surveys = await db.Surveys.Where(s => s.CreatedAt >= fts && s.CreatedAt < tts).Select(s => s.CreatedAt).ToListAsync(ct);
        var responses = await db.Responses.Where(r => r.SubmittedOn >= f && r.SubmittedOn <= t)
            .GroupBy(r => r.SubmittedOn).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

        var days = new List<DayStats>();
        for (var d = f; d <= t; d = d.AddDays(1))
        {
            var day = d;
            days.Add(new DayStats(day,
                regs.Count(x => DateOnly.FromDateTime(x.UtcDateTime) == day),
                surveys.Count(x => DateOnly.FromDateTime(x.UtcDateTime) == day),
                responses.FirstOrDefault(x => x.Key == day)?.Count ?? 0));
        }

        return new AdminStats(f, t,
            await db.Users.CountAsync(ct),
            await db.Teams.CountAsync(ct),
            await db.Surveys.CountAsync(ct),
            await db.Responses.CountAsync(ct),
            await db.Surveys.CountAsync(s => s.ResponsesOverDeclared, ct),
            days);
    }

    public async Task<AdminFunnel> FunnelAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (f, t, fts, tts) = Range(from, to);
        var now = clock.GetUtcNow();
        var events = await db.FunnelEvents.AsNoTracking()
            .Where(e => e.At >= fts && e.At < tts)
            .Select(e => new { e.SurveyId, e.Event, e.Props, e.At })
            .ToListAsync(ct);

        var freeSurveys = await db.Surveys.CountAsync(s => s.CreatedAt >= fts && s.CreatedAt < tts && s.PriceRub == 0, ct);

        int Distinct(string evt) => events.Where(e => e.Event == evt).Select(e => e.SurveyId).Distinct().Count();
        var draft = Distinct(FunnelEvents.SurveyDraftPaidRequired);
        var viewed = Distinct(FunnelEvents.CheckoutViewed);
        var selected = Distinct(FunnelEvents.PaymentMethodSelected);
        var clicked = Distinct(FunnelEvents.PaymentClicked);

        // checkout_abandoned: был checkout_viewed, нет payment_clicked в течение 24 ч
        var clicks = events.Where(e => e.Event == FunnelEvents.PaymentClicked).ToList();
        var abandoned = events.Where(e => e.Event == FunnelEvents.CheckoutViewed)
            .GroupBy(e => e.SurveyId)
            .Select(g => (SurveyId: g.Key, FirstView: g.Min(e => e.At)))
            .Count(x => x.FirstView.AddHours(24) <= now &&
                        !clicks.Any(c => c.SurveyId == x.SurveyId && c.At <= x.FirstView.AddHours(24)));

        var byMethod = clicks
            .Select(c => Json.Deserialize<Dictionary<string, object>>(c.Props).GetValueOrDefault("method")?.ToString() ?? "unknown")
            .GroupBy(m => m).Select(g => new CountItem(g.Key, g.Count())).OrderByDescending(x => x.Count).ToList();

        var sizes = await db.Surveys.Where(s => s.CreatedAt >= fts && s.CreatedAt < tts)
            .Select(s => s.DeclaredParticipants).ToListAsync(ct);
        var bySize = sizes.GroupBy(SizeBucket)
            .Select(g => new CountItem(g.Key, g.Count()))
            .OrderBy(x => BucketOrder.IndexOf(x.Key)).ToList();

        return new AdminFunnel(f, t, freeSurveys, draft, viewed, selected, clicked, abandoned,
            draft == 0 ? 0 : Math.Round((double)clicked / draft, 3),
            viewed == 0 ? 0 : Math.Round((double)clicked / viewed, 3),
            byMethod, bySize);
    }

    private static readonly List<string> BucketOrder = ["2–8", "9–15", "16–30", "31–50", "51+"];

    private static string SizeBucket(int n) => n switch
    {
        <= 8 => "2–8",
        <= 15 => "9–15",
        <= 30 => "16–30",
        <= 50 => "31–50",
        _ => "51+",
    };

    public async Task<IReadOnlyList<AdminUser>> UsersAsync(string? q, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email.Contains(term) || (u.DisplayName != null && u.DisplayName.ToLower().Contains(term)));
        }
        return await query.OrderByDescending(u => u.CreatedAt).Take(100)
            .Select(u => new AdminUser(u.Id, u.Email, u.DisplayName, u.CreatedAt, u.LastLoginAt,
                u.Teams.Count, u.Teams.SelectMany(t => t.Surveys).Count()))
            .ToListAsync(ct);
    }
}
