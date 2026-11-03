using Motivators.Application.Common;
using Motivators.Domain;

namespace Motivators.Application.Results;

public static class MotivatorsResultsCalculator
{
    public static MotivatorsResult Calculate(
        MethodologyVersionContent catalog,
        SurveySettings settings,
        bool isAnonymous,
        IReadOnlyList<ResponseInput<MotivatorsPayload>> responses,
        ResultsOptions opts)
    {
        var keys = catalog.Motivators.Select(m => m.Key).ToList();
        var n = responses.Count;

        // rank: позиция в ranking + 1 (10 = самая важная)
        var ranks = keys.ToDictionary(k => k, _ => new List<int>(n));
        foreach (var r in responses)
            for (var i = 0; i < r.Payload.Ranking.Count; i++)
                if (ranks.TryGetValue(r.Payload.Ranking[i], out var list)) list.Add(i + 1);

        var cards = keys.Select(k =>
            {
                var list = ranks[k];
                var avg = list.Count == 0 ? 0 : list.Average();
                var std = list.Count == 0 ? 0 : Math.Sqrt(list.Sum(x => (x - avg) * (x - avg)) / list.Count);
                var dist = new int[keys.Count];
                foreach (var rank in list) dist[rank - 1]++;
                var consensus = std > opts.MmDividedStd ? Consensus.Divided
                    : std <= opts.MmUnanimousStd ? Consensus.Unanimous
                    : Consensus.Mixed;
                return new MotivatorCardStats(k, Round(avg), Round(std), consensus, dist);
            })
            // при равенстве — порядок каталога (стабильная сортировка)
            .OrderByDescending(c => c.AvgRank)
            .ToList();

        var top3 = n == 0 ? [] : cards.Take(3).Select(c => c.Key).ToList();
        var bottom3 = n == 0 ? [] : cards.TakeLast(3).Reverse().Select(c => c.Key).ToList();
        var avgByKey = cards.ToDictionary(c => c.Key, c => c.AvgRank);

        var changes = (settings.Changes ?? []).Select(change =>
        {
            var withImpact = responses
                .Select(r => r.Payload.Impact?.GetValueOrDefault(change.Id))
                .Where(x => x != null)
                .Select(x => x!)
                .ToList();
            var cn = withImpact.Count;
            var cardImpacts = cards.Select(c =>
            {
                int up = 0, down = 0, neutral = 0;
                foreach (var imp in withImpact)
                {
                    switch (imp.GetValueOrDefault(c.Key))
                    {
                        case > 0: up++; break;
                        case < 0: down++; break;
                        default: neutral++; break;
                    }
                }
                var index = cn == 0 ? 0 : (double)(up - down) / cn;
                return new ChangeCardImpact(c.Key, up, neutral, down, Round(index));
            }).ToList();

            // вес карточки = средний ранг команды / 10
            var weightSum = cardImpacts.Sum(ci => avgByKey[ci.Key] / 10.0);
            var overall = cn == 0 || weightSum == 0
                ? 0
                : cardImpacts.Sum(ci => ci.Index * avgByKey[ci.Key] / 10.0) / weightSum;
            return new ChangeImpactResult(change.Id, change.Text, cn, cardImpacts, Round(overall));
        }).ToList();

        var withData = changes.Where(c => c.ResponseCount > 0).ToList();
        var mostPainful = withData.Where(c => c.Overall < 0).OrderBy(c => c.Overall).FirstOrDefault()?.Id;
        var mostPositive = withData.Where(c => c.Overall > 0).OrderByDescending(c => c.Overall).FirstOrDefault()?.Id;

        var comments = CommentOrdering.Apply(
            responses.Where(r => !string.IsNullOrWhiteSpace(r.Comment))
                .Select(r => (r.Id, Dto: isAnonymous
                    ? new CommentDto(r.Comment!)
                    : new CommentDto(r.Comment!, r.Name, r.SubmittedOn))),
            isAnonymous);

        var participants = isAnonymous
            ? null
            : responses
                .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(r => new MotivatorsParticipant(r.Name ?? "", r.Payload.Ranking, r.Payload.Impact, r.Comment))
                .ToList();

        var prompts = n == 0 ? [] : BuildPrompts(catalog, cards, top3, changes, opts);

        return new MotivatorsResult(n, cards, top3, bottom3, changes, mostPainful, mostPositive, comments, prompts, participants);
    }

    private static List<DiscussionPrompt> BuildPrompts(
        MethodologyVersionContent catalog,
        List<MotivatorCardStats> cards,
        List<string> top3,
        List<ChangeImpactResult> changes,
        ResultsOptions opts)
    {
        string Name(string key) => catalog.FindMotivator(key)?.Name ?? key;
        var high = new List<DiscussionPrompt>();
        var normal = new List<DiscussionPrompt>();
        var reportedNegative = new HashSet<(string, string)>();

        // 1. Главный мотиватор под угрозой — с повышенным приоритетом, первыми
        foreach (var key in top3)
            foreach (var change in changes)
            {
                var ci = change.Cards.First(c => c.Key == key);
                if (change.ResponseCount == 0 || ci.Index >= opts.MmNegativeImpact) continue;
                reportedNegative.Add((change.Id, key));
                high.Add(new DiscussionPrompt(PromptPriority.High, PromptKinds.TopCardThreatened,
                    $"Под угрозой один из главных мотиваторов команды: «{Name(key)}». Изменение „{change.Text}“ может ударить по нему.",
                    key, change.Id));
            }

        // 2. Вопросы по топ-3
        foreach (var key in top3)
            normal.Add(new DiscussionPrompt(PromptPriority.Normal, PromptKinds.TopCard,
                $"«{Name(key)}» — один из самых важных мотиваторов команды.",
                key, Questions: catalog.FindMotivator(key)?.DiscussionPrompts ?? []));

        // 3. Мнения разделились
        foreach (var c in cards.Where(c => c.StdRank > opts.MmDividedStd))
            normal.Add(new DiscussionPrompt(PromptPriority.Normal, PromptKinds.DividedOpinions,
                $"«{Name(c.Key)}»: мнения разделились — обсудите, почему для одних это важно, а для других нет.",
                c.Key));

        foreach (var change in changes.Where(c => c.ResponseCount > 0))
        {
            // 4. Изменение бьёт по мотиватору
            foreach (var ci in change.Cards.Where(ci => ci.Index < opts.MmNegativeImpact))
            {
                if (reportedNegative.Contains((change.Id, ci.Key))) continue;
                normal.Add(new DiscussionPrompt(PromptPriority.Normal, PromptKinds.NegativeImpact,
                    $"Изменение „{change.Text}“ может ударить по мотиватору «{Name(ci.Key)}».",
                    ci.Key, change.Id));
            }

            // 5. Голоса разделились
            var split = change.Cards
                .Where(ci => (double)ci.Up / change.ResponseCount >= opts.MmSplitShare
                          && (double)ci.Down / change.ResponseCount >= opts.MmSplitShare)
                .Select(ci => Name(ci.Key))
                .ToList();
            if (split.Count > 0)
                normal.Add(new DiscussionPrompt(PromptPriority.Normal, PromptKinds.SplitImpact,
                    $"Изменение „{change.Text}“ воспринимается по-разному ({string.Join(", ", split.Select(s => $"«{s}»"))}) — обсудите, кому оно поможет, а кому помешает.",
                    ChangeId: change.Id));
        }

        return [.. high, .. normal];
    }

    internal static double Round(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

internal static class CommentOrdering
{
    /// <summary>
    /// Анонимный режим — псевдослучайный, но стабильный порядок (по хешу id), без времени.
    /// Неанонимный — по дате отправки.
    /// </summary>
    public static List<CommentDto> Apply(IEnumerable<(Guid Id, CommentDto Dto)> items, bool anonymous) =>
        anonymous
            ? items.OrderBy(x => Convert.ToHexString(Crypto.Sha256(x.Id.ToString()))).Select(x => x.Dto).ToList()
            : items.OrderBy(x => x.Id).Select(x => x.Dto).ToList();
}
