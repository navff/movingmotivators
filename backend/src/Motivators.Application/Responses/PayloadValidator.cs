using System.Text.Json;
using Motivators.Application.Common;
using Motivators.Domain;

namespace Motivators.Application.Responses;

/// <summary>Строгая валидация payload ответа по методике и настройкам опроса.</summary>
public static class PayloadValidator
{
    public const int HcCommentMaxLength = 500;

    public static MotivatorsPayload ValidateMotivators(JsonElement payload, MethodologyVersionContent catalog, SurveySettings settings)
    {
        if (payload.ValueKind != JsonValueKind.Object) throw Fail("payload должен быть объектом");
        var keys = catalog.Motivators.Select(m => m.Key).ToHashSet();

        if (!payload.TryGetProperty("ranking", out var rankingEl) || rankingEl.ValueKind != JsonValueKind.Array)
            throw Fail("ranking обязателен");
        var ranking = new List<string>();
        foreach (var el in rankingEl.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.String) throw Fail("ranking: ожидаются строки");
            ranking.Add(el.GetString()!);
        }
        if (ranking.Count != keys.Count || ranking.Distinct().Count() != keys.Count || !ranking.All(keys.Contains))
            throw Fail($"ranking: ровно {keys.Count} уникальных ключей карточек");

        var ratings = ValidateRatings(payload, catalog, keys);

        var changes = settings.Changes ?? [];
        var hasImpact = payload.TryGetProperty("impact", out var impactEl) && impactEl.ValueKind != JsonValueKind.Null;
        if (changes.Count == 0)
        {
            if (hasImpact) throw Fail("impact не ожидается: в опросе нет изменений");
            return new MotivatorsPayload { Ranking = ranking, Ratings = ratings, Impact = null };
        }

        if (!hasImpact || impactEl.ValueKind != JsonValueKind.Object) throw Fail("impact обязателен");
        var impact = new Dictionary<string, Dictionary<string, int>>();
        foreach (var changeProp in impactEl.EnumerateObject())
        {
            if (changeProp.Value.ValueKind != JsonValueKind.Object) throw Fail("impact: ожидается объект");
            var marks = new Dictionary<string, int>();
            foreach (var markProp in changeProp.Value.EnumerateObject())
            {
                if (!keys.Contains(markProp.Name)) throw Fail($"impact: неизвестная карточка {markProp.Name}");
                if (markProp.Value.ValueKind != JsonValueKind.Number || !markProp.Value.TryGetInt32(out var v) || v is < -1 or > 1)
                    throw Fail("impact: значения -1, 0 или 1");
                marks[markProp.Name] = v;
            }
            if (marks.Count != keys.Count) throw Fail($"impact: для каждого изменения ровно {keys.Count} карточек");
            if (!impact.TryAdd(changeProp.Name, marks)) throw Fail("impact: дубли изменений");
        }
        var expected = changes.Select(c => c.Id).ToHashSet();
        if (!impact.Keys.ToHashSet().SetEquals(expected)) throw Fail("impact: набор изменений не совпадает с опросом");

        return new MotivatorsPayload { Ranking = ranking, Ratings = ratings, Impact = impact };
    }

    /// <summary>С версии 2 оценка обязательна: ровно все карточки, значения 1..5. В версии 1 её нет.</summary>
    private static Dictionary<string, int>? ValidateRatings(JsonElement payload, MethodologyVersionContent catalog, HashSet<string> keys)
    {
        var present = payload.TryGetProperty("ratings", out var el) && el.ValueKind != JsonValueKind.Null;
        if (!catalog.HasRatings)
        {
            if (present) throw Fail("ratings не ожидается в этой версии методики");
            return null;
        }

        if (!present || el.ValueKind != JsonValueKind.Object) throw Fail("ratings обязателен");
        var ratings = new Dictionary<string, int>();
        foreach (var prop in el.EnumerateObject())
        {
            if (!keys.Contains(prop.Name)) throw Fail($"ratings: неизвестная карточка {prop.Name}");
            if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetInt32(out var v) || v < 1 || v > MethodologyCatalog.RatingLevels)
                throw Fail($"ratings: значения от 1 до {MethodologyCatalog.RatingLevels}");
            if (!ratings.TryAdd(prop.Name, v)) throw Fail("ratings: дубли карточек");
        }
        if (ratings.Count != keys.Count) throw Fail($"ratings: нужны оценки всех {keys.Count} карточек");
        return ratings;
    }

    public static HealthCheckPayload ValidateHealthCheck(JsonElement payload, MethodologyVersionContent catalog, SurveySettings settings)
    {
        if (payload.ValueKind != JsonValueKind.Object) throw Fail("payload должен быть объектом");
        if (!payload.TryGetProperty("answers", out var answersEl) || answersEl.ValueKind != JsonValueKind.Object)
            throw Fail("answers обязателен");

        var enabled = (settings.Categories ?? catalog.HealthCheckCategories.Select(c => c.Key).ToList()).ToHashSet();
        var answers = new Dictionary<string, HealthCheckAnswer>();
        foreach (var prop in answersEl.EnumerateObject())
        {
            if (!enabled.Contains(prop.Name)) throw Fail($"answers: категория {prop.Name} не включена в опрос");
            var a = prop.Value;
            if (a.ValueKind != JsonValueKind.Object) throw Fail("answers: ожидается объект");
            if (!a.TryGetProperty("score", out var s) || s.ValueKind != JsonValueKind.Number || !s.TryGetInt32(out var score) || score is < 1 or > 3)
                throw Fail("score: 1, 2 или 3");
            if (!a.TryGetProperty("trend", out var t) || t.ValueKind != JsonValueKind.Number || !t.TryGetInt32(out var trend) || trend is < -1 or > 1)
                throw Fail("trend: -1, 0 или 1");
            string? comment = null;
            if (a.TryGetProperty("comment", out var c) && c.ValueKind != JsonValueKind.Null)
            {
                if (c.ValueKind != JsonValueKind.String) throw Fail("comment: строка");
                comment = c.GetString()!.Trim();
                if (comment.Length > HcCommentMaxLength) throw Fail($"comment: не более {HcCommentMaxLength} символов");
                if (comment.Length == 0) comment = null;
            }
            if (!answers.TryAdd(prop.Name, new HealthCheckAnswer { Score = score, Trend = trend, Comment = comment }))
                throw Fail("answers: дубли категорий");
        }
        if (!answers.Keys.ToHashSet().SetEquals(enabled)) throw Fail("answers: нужны ответы ровно по включённым категориям");

        return new HealthCheckPayload { Answers = answers };
    }

    private static AppException Fail(string message) =>
        AppException.Validation(message, new Dictionary<string, string[]> { ["payload"] = [message] });
}
