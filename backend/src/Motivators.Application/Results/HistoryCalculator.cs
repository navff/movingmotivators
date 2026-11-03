using Motivators.Domain;

namespace Motivators.Application.Results;

public static class HistoryCalculator
{
    /// <summary>Сетка «категория × волна» по закрытым HC, волны по дате закрытия.</summary>
    public static HealthCheckHistory HealthCheck(
        MethodologyVersionContent catalog,
        IEnumerable<(SurveyRef Survey, HealthCheckResult Result)> waves)
    {
        var list = waves.OrderBy(w => w.Survey.ClosedAt).ToList();
        var used = list.SelectMany(w => w.Result.Categories.Select(c => c.Key)).ToHashSet();
        var keys = catalog.HealthCheckCategories.Select(c => c.Key).Where(used.Contains).ToList();
        return new HealthCheckHistory(keys, list.Select(w => new HealthCheckWave(
            w.Survey.Id, w.Survey.Title, w.Survey.ClosedAt, w.Result.ResponseCount, w.Result.OverallAvg,
            w.Result.Categories.ToDictionary(c => c.Key, c => new HealthCheckWaveCell(c.AvgScore, c.Color, c.Trend))
        )).ToList());
    }

    /// <summary>Сдвиг среднего ранга (B − A); неанонимные — сдвиг по участнику при совпадении имени без учёта регистра.</summary>
    public static MotivatorsComparison CompareMotivators(
        SurveyRef refA, MotivatorsResult a, bool aAnonymous,
        SurveyRef refB, MotivatorsResult b, bool bAnonymous)
    {
        var avgA = a.Cards.ToDictionary(c => c.Key, c => c.AvgRank);
        var cards = b.Cards
            .Select(c => new CompareCard(c.Key, avgA.GetValueOrDefault(c.Key), c.AvgRank,
                MotivatorsResultsCalculator.Round(c.AvgRank - avgA.GetValueOrDefault(c.Key))))
            .ToList();

        List<CompareParticipant>? participants = null;
        if (!aAnonymous && !bAnonymous && a.Participants != null && b.Participants != null)
        {
            static string Norm(string s) => s.Trim().ToLowerInvariant();
            var byName = a.Participants.GroupBy(p => Norm(p.Name)).ToDictionary(g => g.Key, g => g.First());
            participants = b.Participants
                .Where(p => byName.ContainsKey(Norm(p.Name)))
                .Select(p =>
                {
                    var prev = byName[Norm(p.Name)];
                    var shift = p.Ranking.Select((key, i) => (key, rank: i + 1))
                        .ToDictionary(x => x.key, x => x.rank - (prev.Ranking.ToList().IndexOf(x.key) + 1));
                    return new CompareParticipant(p.Name, shift);
                })
                .ToList();
        }

        return new MotivatorsComparison(refA, refB, cards, participants);
    }
}
