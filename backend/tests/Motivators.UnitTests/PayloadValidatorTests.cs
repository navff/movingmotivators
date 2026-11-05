using System.Text.Json;
using Motivators.Application.Common;
using Motivators.Application.Responses;
using Motivators.Domain;

namespace Motivators.UnitTests;

public class PayloadValidatorTests
{
    // v1 — без шага оценки: старые опросы должны продолжать приниматься
    private static readonly MethodologyVersionContent Catalog = MethodologyCatalog.Get(1);
    private static readonly string[] Keys = Catalog.Motivators.Select(m => m.Key).ToArray();
    private static readonly SurveySettings NoChanges = new() { Changes = [] };
    private static readonly SurveySettings TwoChanges = new()
    {
        Changes = [new SurveyChange { Id = "c1", Text = "A" }, new SurveyChange { Id = "c2", Text = "B" }],
    };

    private static JsonElement J(object o) => JsonSerializer.SerializeToElement(o);
    private static Dictionary<string, int> Marks(int v = 0) => Keys.ToDictionary(k => k, _ => v);

    [Fact]
    public void Valid_ranking_without_changes()
    {
        var p = PayloadValidator.ValidateMotivators(J(new { ranking = Keys }), Catalog, NoChanges);
        Assert.Equal(Keys, p.Ranking);
        Assert.Null(p.Impact);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(11)]
    public void Ranking_must_have_exactly_10(int count)
    {
        var ranking = Keys.Concat(Keys).Take(count).ToArray();
        Assert.Throws<AppException>(() => PayloadValidator.ValidateMotivators(J(new { ranking }), Catalog, NoChanges));
    }

    [Fact]
    public void Ranking_rejects_duplicates_and_unknown_keys()
    {
        var dup = Keys.Take(9).Append(Keys[0]).ToArray();
        Assert.Throws<AppException>(() => PayloadValidator.ValidateMotivators(J(new { ranking = dup }), Catalog, NoChanges));
        var unknown = Keys.Take(9).Append("money").ToArray();
        Assert.Throws<AppException>(() => PayloadValidator.ValidateMotivators(J(new { ranking = unknown }), Catalog, NoChanges));
    }

    [Fact]
    public void Impact_not_allowed_without_changes()
    {
        Assert.Throws<AppException>(() =>
            PayloadValidator.ValidateMotivators(J(new { ranking = Keys, impact = new { c1 = Marks() } }), Catalog, NoChanges));
    }

    [Fact]
    public void Impact_required_and_exact_set_of_changes()
    {
        Assert.Throws<AppException>(() => PayloadValidator.ValidateMotivators(J(new { ranking = Keys }), Catalog, TwoChanges));
        Assert.Throws<AppException>(() =>
            PayloadValidator.ValidateMotivators(J(new { ranking = Keys, impact = new { c1 = Marks() } }), Catalog, TwoChanges));
        Assert.Throws<AppException>(() =>
            PayloadValidator.ValidateMotivators(J(new { ranking = Keys, impact = new { c1 = Marks(), c2 = Marks(), c3 = Marks() } }), Catalog, TwoChanges));

        var ok = PayloadValidator.ValidateMotivators(J(new { ranking = Keys, impact = new { c1 = Marks(1), c2 = Marks(-1) } }), Catalog, TwoChanges);
        Assert.Equal(2, ok.Impact!.Count);
        Assert.All(ok.Impact["c1"].Values, v => Assert.Equal(1, v));
    }

    [Fact]
    public void Impact_values_and_card_set()
    {
        var bad = Marks(); bad["freedom"] = 2;
        Assert.Throws<AppException>(() =>
            PayloadValidator.ValidateMotivators(J(new { ranking = Keys, impact = new { c1 = bad, c2 = Marks() } }), Catalog, TwoChanges));
        var missing = Marks(); missing.Remove("freedom");
        Assert.Throws<AppException>(() =>
            PayloadValidator.ValidateMotivators(J(new { ranking = Keys, impact = new { c1 = missing, c2 = Marks() } }), Catalog, TwoChanges));
    }

    private static Dictionary<string, int> Ratings(int v = 3) => Keys.ToDictionary(k => k, _ => v);

    [Fact]
    public void V2_requires_rating_for_every_card()
    {
        var current = MethodologyCatalog.Current;
        Assert.True(current.HasRatings);

        var ok = PayloadValidator.ValidateMotivators(J(new { ranking = Keys, ratings = Ratings(5) }), current, NoChanges);
        Assert.Equal(10, ok.Ratings!.Count);
        Assert.All(ok.Ratings.Values, v => Assert.Equal(5, v));

        Assert.Throws<AppException>(() => PayloadValidator.ValidateMotivators(J(new { ranking = Keys }), current, NoChanges));

        var missing = Ratings(); missing.Remove("freedom");
        Assert.Throws<AppException>(() => PayloadValidator.ValidateMotivators(J(new { ranking = Keys, ratings = missing }), current, NoChanges));

        var unknown = Ratings(); unknown.Remove("freedom"); unknown["money"] = 3;
        Assert.Throws<AppException>(() => PayloadValidator.ValidateMotivators(J(new { ranking = Keys, ratings = unknown }), current, NoChanges));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void V2_rating_range_is_1_to_5(int value)
    {
        var bad = Ratings(); bad["freedom"] = value;
        Assert.Throws<AppException>(() =>
            PayloadValidator.ValidateMotivators(J(new { ranking = Keys, ratings = bad }), MethodologyCatalog.Current, NoChanges));
    }

    [Fact]
    public void V1_keeps_working_without_ratings_and_rejects_them()
    {
        var old = MethodologyCatalog.Get(1);
        Assert.False(old.HasRatings);
        Assert.Null(PayloadValidator.ValidateMotivators(J(new { ranking = Keys }), old, NoChanges).Ratings);
        Assert.Throws<AppException>(() => PayloadValidator.ValidateMotivators(J(new { ranking = Keys, ratings = Ratings() }), old, NoChanges));
    }

    [Fact]
    public void Health_check_requires_exactly_enabled_categories()
    {
        var settings = new SurveySettings { Categories = ["delivery", "fun", "speed"] };
        var answers = new Dictionary<string, object>
        {
            ["delivery"] = new { score = 3, trend = 1 },
            ["fun"] = new { score = 1, trend = -1, comment = "  тяжело  " },
            ["speed"] = new { score = 2, trend = 0, comment = (string?)null },
        };
        var p = PayloadValidator.ValidateHealthCheck(J(new { answers }), Catalog, settings);
        Assert.Equal("тяжело", p.Answers["fun"].Comment);

        answers.Remove("speed");
        Assert.Throws<AppException>(() => PayloadValidator.ValidateHealthCheck(J(new { answers }), Catalog, settings));

        answers["speed"] = new { score = 2, trend = 0 };
        answers["mission"] = new { score = 2, trend = 0 };
        Assert.Throws<AppException>(() => PayloadValidator.ValidateHealthCheck(J(new { answers }), Catalog, settings));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(2, 2)]
    [InlineData(2, -2)]
    public void Health_check_score_and_trend_ranges(int score, int trend)
    {
        var settings = new SurveySettings { Categories = ["delivery", "fun", "speed"] };
        var answers = new Dictionary<string, object>
        {
            ["delivery"] = new { score, trend },
            ["fun"] = new { score = 1, trend = 0 },
            ["speed"] = new { score = 1, trend = 0 },
        };
        Assert.Throws<AppException>(() => PayloadValidator.ValidateHealthCheck(J(new { answers }), Catalog, settings));
    }

    [Fact]
    public void Health_check_comment_length()
    {
        var settings = new SurveySettings { Categories = ["delivery", "fun", "speed"] };
        var answers = new Dictionary<string, object>
        {
            ["delivery"] = new { score = 1, trend = 0, comment = new string('x', 501) },
            ["fun"] = new { score = 1, trend = 0 },
            ["speed"] = new { score = 1, trend = 0 },
        };
        Assert.Throws<AppException>(() => PayloadValidator.ValidateHealthCheck(J(new { answers }), Catalog, settings));
    }
}
