using Motivators.Application.Common;
using Motivators.Application.Results;
using Motivators.Domain;

namespace Motivators.UnitTests;

public class HealthCheckResultsTests
{
    private static readonly MethodologyVersionContent Catalog = MethodologyCatalog.Current;
    private static readonly ResultsOptions Opts = new();
    private static readonly List<string> Cats = ["delivery", "process", "speed", "fun"];

    private static ResponseInput<HealthCheckPayload> R(Dictionary<string, (int Score, int Trend)> answers, string? name = null) =>
        new(Guid.CreateVersion7(), name, new HealthCheckPayload
        {
            Answers = answers.ToDictionary(a => a.Key, a => new HealthCheckAnswer { Score = a.Value.Score, Trend = a.Value.Trend }),
        }, null, new DateOnly(2026, 11, 3));

    private static Dictionary<string, (int, int)> All(int score, int trend) => Cats.ToDictionary(c => c, _ => (score, trend));

    private static HealthCheckResult Calc(params ResponseInput<HealthCheckPayload>[] rs) =>
        HealthCheckResultsCalculator.Calculate(Catalog, new SurveySettings { Categories = Cats }, true, rs, Opts);

    [Theory]
    [InlineData(3.0, TrafficLight.Green)]
    [InlineData(2.5, TrafficLight.Green)]
    [InlineData(2.49, TrafficLight.Yellow)]
    [InlineData(1.75, TrafficLight.Yellow)]
    [InlineData(1.74, TrafficLight.Red)]
    [InlineData(1.0, TrafficLight.Red)]
    public void Color_thresholds(double avg, string color) =>
        Assert.Equal(color, HealthCheckResultsCalculator.ColorOf(avg, Opts));

    [Fact]
    public void Distribution_average_and_trend()
    {
        var a = All(3, 1); a["delivery"] = (1, -1);
        var b = All(3, 1); b["delivery"] = (2, -1);
        var c = All(2, 0); c["delivery"] = (1, 1);
        var r = Calc(R(a), R(b), R(c));

        var delivery = r.Categories.Single(x => x.Key == "delivery");
        Assert.Equal(0, delivery.Green);
        Assert.Equal(1, delivery.Yellow);
        Assert.Equal(2, delivery.Red);
        Assert.Equal(1.33, delivery.AvgScore);
        Assert.Equal(TrafficLight.Red, delivery.Color);
        Assert.Equal(-1, delivery.Trend);

        var fun = r.Categories.Single(x => x.Key == "fun");
        Assert.Equal(2.67, fun.AvgScore);
        Assert.Equal(TrafficLight.Green, fun.Color);
        Assert.Equal(1, fun.Trend);
    }

    [Fact]
    public void Categories_follow_catalog_order_and_only_enabled()
    {
        var r = Calc(R(All(2, 0)));
        Assert.Equal(new[] { "delivery", "process", "speed", "fun" }, r.Categories.Select(c => c.Key));
    }

    [Fact]
    public void Focus_picks_lowest_and_declining()
    {
        var a = All(3, 0); a["process"] = (1, 0); a["fun"] = (3, -1);
        var b = All(3, 0); b["process"] = (2, 0); b["fun"] = (3, -1);
        var r = Calc(R(a), R(b));
        // process — низкий балл, fun — зелёный, но ухудшается у большинства
        Assert.Equal("process", r.Focus[0]);
        Assert.Contains("fun", r.Focus);
        Assert.InRange(r.Focus.Count, 2, 3);
        Assert.Equal(r.Focus.Count, r.Prompts.Count);
    }

    [Fact]
    public void Third_focus_only_if_not_green()
    {
        var r = Calc(R(All(3, 0)));
        Assert.Equal(2, r.Focus.Count);
    }

    [Fact]
    public void Empty_results()
    {
        var r = Calc();
        Assert.Null(r.OverallAvg);
        Assert.Empty(r.Focus);
        Assert.All(r.Categories, c => Assert.Null(c.Color));
    }

    [Fact]
    public void History_grid_orders_waves_by_close_date()
    {
        var q1 = Calc(R(All(1, 0)));
        var q2 = Calc(R(All(3, 1)));
        var h = HistoryCalculator.HealthCheck(Catalog,
        [
            (new SurveyRef(Guid.NewGuid(), "Q2", new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero), 1), q2),
            (new SurveyRef(Guid.NewGuid(), "Q1", new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero), 1), q1),
        ]);
        Assert.Equal(new[] { "Q1", "Q2" }, h.Waves.Select(w => w.Title));
        Assert.Equal(TrafficLight.Red, h.Waves[0].Categories["delivery"].Color);
        Assert.Equal(TrafficLight.Green, h.Waves[1].Categories["delivery"].Color);
        Assert.Equal(Cats, h.CategoryKeys);
    }
}
