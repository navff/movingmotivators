using Motivators.Application.Common;
using Motivators.Domain;

namespace Motivators.Application.Results;

public static class HealthCheckResultsCalculator
{
    public static HealthCheckResult Calculate(
        MethodologyVersionContent catalog,
        SurveySettings settings,
        bool isAnonymous,
        IReadOnlyList<ResponseInput<HealthCheckPayload>> responses,
        ResultsOptions opts)
    {
        var enabled = settings.Categories ?? catalog.HealthCheckCategories.Select(c => c.Key).ToList();
        // в порядке каталога
        var keys = catalog.HealthCheckCategories.Select(c => c.Key).Where(enabled.Contains).ToList();

        var categories = keys.Select(key =>
        {
            var answers = responses
                .Select(r => (r.Id, r.Name, r.SubmittedOn, Answer: r.Payload.Answers.GetValueOrDefault(key)))
                .Where(x => x.Answer != null)
                .ToList();
            int green = 0, yellow = 0, red = 0, up = 0, same = 0, down = 0;
            foreach (var (_, _, _, a) in answers)
            {
                switch (a!.Score) { case 3: green++; break; case 2: yellow++; break; default: red++; break; }
                switch (a.Trend) { case > 0: up++; break; case < 0: down++; break; default: same++; break; }
            }
            double? avg = answers.Count == 0 ? null : MotivatorsResultsCalculator.Round(answers.Average(x => x.Answer!.Score));
            var comments = CommentOrdering.Apply(
                answers.Where(x => !string.IsNullOrWhiteSpace(x.Answer!.Comment))
                    .Select(x => (x.Id, Dto: isAnonymous
                        ? new CommentDto(x.Answer!.Comment!, CategoryKey: key)
                        : new CommentDto(x.Answer!.Comment!, x.Name, x.SubmittedOn, key))),
                isAnonymous);
            return new HealthCheckCategoryStats(key, green, yellow, red, avg, ColorOf(avg, opts),
                up, same, down, PredominantTrend(up, same, down, answers.Count), comments);
        }).ToList();

        var withData = categories.Where(c => c.AvgScore != null).ToList();
        double? overall = withData.Count == 0 ? null : MotivatorsResultsCalculator.Round(withData.Average(c => c.AvgScore!.Value));

        var focus = SelectFocus(withData, opts);

        var generalComments = CommentOrdering.Apply(
            responses.Where(r => !string.IsNullOrWhiteSpace(r.Comment))
                .Select(r => (r.Id, Dto: isAnonymous ? new CommentDto(r.Comment!) : new CommentDto(r.Comment!, r.Name, r.SubmittedOn))),
            isAnonymous);

        var prompts = focus.Select(key =>
        {
            var cat = catalog.FindCategory(key)!;
            var stats = categories.First(c => c.Key == key);
            var why = stats.Trend == -1 && stats.TrendDown * 2 > stats.TrendUp + stats.TrendSame + stats.TrendDown
                ? "большинство отмечает ухудшение"
                : $"средний балл {stats.AvgScore:0.##} из 3";
            return new DiscussionPrompt(PromptPriority.Normal, PromptKinds.FocusCategory,
                $"«{cat.Name}» — {why}.", key, Questions: cat.DiscussionPrompts);
        }).ToList();

        var participants = isAnonymous
            ? null
            : responses.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(r => new HealthCheckParticipant(r.Name ?? "", r.Payload.Answers, r.Comment)).ToList();

        return new HealthCheckResult(responses.Count, categories, overall, focus, generalComments, prompts, participants);
    }

    public static string? ColorOf(double? avg, ResultsOptions opts) =>
        avg is null ? null
        : avg >= opts.HcGreenMin ? TrafficLight.Green
        : avg >= opts.HcYellowMin ? TrafficLight.Yellow
        : TrafficLight.Red;

    private static int? PredominantTrend(int up, int same, int down, int total)
    {
        if (total == 0) return null;
        if (down > up && down > same) return -1;
        if (up > down && up > same) return 1;
        return 0;
    }

    /// <summary>
    /// «Обсудите в первую очередь»: 2–3 категории с минимальным баллом и/или трендом ↓ у большинства.
    /// Третья берётся, только если она не зелёная или ухудшается.
    /// </summary>
    private static List<string> SelectFocus(List<HealthCheckCategoryStats> withData, ResultsOptions opts)
    {
        static bool DownMajority(HealthCheckCategoryStats c) =>
            c.TrendDown * 2 > c.TrendUp + c.TrendSame + c.TrendDown;

        var ordered = withData
            .OrderBy(c => c.AvgScore!.Value - (DownMajority(c) ? 0.75 : 0))
            .ThenByDescending(c => c.TrendDown)
            .ToList();
        var result = ordered.Take(2).Select(c => c.Key).ToList();
        if (ordered.Count > 2 && (ordered[2].Color != TrafficLight.Green || DownMajority(ordered[2])))
            result.Add(ordered[2].Key);
        return result;
    }
}
