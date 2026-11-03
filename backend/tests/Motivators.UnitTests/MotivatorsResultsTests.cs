using Motivators.Application.Common;
using Motivators.Application.Results;
using Motivators.Domain;

namespace Motivators.UnitTests;

public class MotivatorsResultsTests
{
    private static readonly MethodologyVersionContent Catalog = MethodologyCatalog.Current;
    private static readonly ResultsOptions Opts = new();
    private static readonly string[] Keys = Catalog.Motivators.Select(m => m.Key).ToArray();

    /// <summary>Ранжирование: последний — самый важный.</summary>
    private static ResponseInput<MotivatorsPayload> R(string[] ranking, Dictionary<string, Dictionary<string, int>>? impact = null,
        string? name = null, string? comment = null) =>
        new(Guid.CreateVersion7(), name, new MotivatorsPayload { Ranking = ranking.ToList(), Impact = impact }, comment,
            new DateOnly(2026, 11, 3));

    private static Dictionary<string, int> Marks(params (string Key, int Value)[] marks)
    {
        var d = Keys.ToDictionary(k => k, _ => 0);
        foreach (var (k, v) in marks) d[k] = v;
        return d;
    }

    private static MotivatorsResult Calc(SurveySettings settings, bool anonymous, params ResponseInput<MotivatorsPayload>[] rs) =>
        MotivatorsResultsCalculator.Calculate(Catalog, settings, anonymous, rs, Opts);

    [Fact]
    public void Empty_survey_has_no_top_and_no_prompts()
    {
        var r = Calc(new SurveySettings { Changes = [] }, true);
        Assert.Equal(0, r.ResponseCount);
        Assert.Empty(r.Top3);
        Assert.Empty(r.Bottom3);
        Assert.Empty(r.Prompts);
        Assert.Equal(10, r.Cards.Count);
    }

    [Fact]
    public void Single_response_ranks_follow_positions()
    {
        var r = Calc(new SurveySettings { Changes = [] }, true, R(Keys));
        // Keys[9] — последний — самый важный (ранг 10)
        Assert.Equal(new[] { Keys[9], Keys[8], Keys[7] }, r.Top3);
        Assert.Equal(new[] { Keys[0], Keys[1], Keys[2] }, r.Bottom3);
        var top = r.Cards[0];
        Assert.Equal(10, top.AvgRank);
        Assert.Equal(0, top.StdRank);
        Assert.Equal(Consensus.Unanimous, top.Consensus);
        Assert.Equal(1, top.Distribution[9]);
        Assert.Null(r.Participants);
    }

    [Fact]
    public void Average_std_and_heatmap_over_several_responses()
    {
        var reversed = Keys.Reverse().ToArray();
        var r = Calc(new SurveySettings { Changes = [] }, true, R(Keys), R(reversed));
        // каждая карточка: ранги i+1 и 10-i → среднее 5.5
        Assert.All(r.Cards, c => Assert.Equal(5.5, c.AvgRank));
        var first = r.Cards.Single(c => c.Key == Keys[0]); // ранги 1 и 10
        Assert.Equal(4.5, first.StdRank);
        Assert.Equal(Consensus.Divided, first.Consensus);
        Assert.Equal(1, first.Distribution[0]);
        Assert.Equal(1, first.Distribution[9]);
        Assert.Equal(2, first.Distribution.Sum());
        Assert.Contains(r.Prompts, p => p.Kind == PromptKinds.DividedOpinions && p.CardKey == Keys[0]);
    }

    [Fact]
    public void Impact_index_and_weighted_overall()
    {
        var settings = new SurveySettings
        {
            Changes = [new SurveyChange { Id = "c1", Text = "Офис 3 дня" }, new SurveyChange { Id = "c2", Text = "Новая оргструктура" }],
        };
        var freedom = "freedom";
        var curiosity = "curiosity";
        // freedom — самый важный у обоих, curiosity — наименее важный
        var ranking = new[] { curiosity }.Concat(Keys.Where(k => k != freedom && k != curiosity)).Append(freedom).ToArray();

        var r = Calc(settings, true,
            R(ranking, new() { ["c1"] = Marks((freedom, -1), (curiosity, 1)), ["c2"] = Marks((freedom, 1)) }),
            R(ranking, new() { ["c1"] = Marks((freedom, -1)), ["c2"] = Marks((freedom, -1)) }));

        var c1 = r.Changes.Single(c => c.Id == "c1");
        var c1Freedom = c1.Cards.Single(c => c.Key == freedom);
        Assert.Equal(0, c1Freedom.Up);
        Assert.Equal(2, c1Freedom.Down);
        Assert.Equal(-1, c1Freedom.Index);
        Assert.Equal(0.5, c1.Cards.Single(c => c.Key == curiosity).Index);

        // взвешенный итог: (−1·10/10 + 0.5·1/10) / (55/10) = (−1 + 0.05) / 5.5
        Assert.Equal(Math.Round(-0.95 / 5.5, 2), c1.Overall);

        var c2 = r.Changes.Single(c => c.Id == "c2");
        var c2Freedom = c2.Cards.Single(c => c.Key == freedom);
        Assert.Equal(1, c2Freedom.Up);
        Assert.Equal(1, c2Freedom.Down);
        Assert.Equal(0, c2Freedom.Index);
        Assert.Equal(0, c2.Overall);

        Assert.Equal("c1", r.MostPainfulChangeId);
        Assert.Null(r.MostPositiveChangeId);
        // карточки изменения — в порядке важности для команды
        Assert.Equal(freedom, c1.Cards[0].Key);
    }

    [Fact]
    public void Weighted_overall_gives_more_weight_to_important_cards()
    {
        var settings = new SurveySettings { Changes = [new SurveyChange { Id = "c1", Text = "X" }] };
        // +1 у самого важного и −1 у наименее важного → итог положительный
        var r = Calc(settings, true, R(Keys, new() { ["c1"] = Marks((Keys[9], 1), (Keys[0], -1)) }));
        Assert.True(r.Changes[0].Overall > 0);
        Assert.Equal(Math.Round((1 * 1.0 - 1 * 0.1) / 5.5, 2), r.Changes[0].Overall);
        Assert.Equal("c1", r.MostPositiveChangeId);
    }

    [Fact]
    public void Threat_to_top_card_is_first_high_priority_prompt()
    {
        var settings = new SurveySettings { Changes = [new SurveyChange { Id = "c1", Text = "Возврат в офис" }] };
        var top = Keys[9];
        var r = Calc(settings, true,
            R(Keys, new() { ["c1"] = Marks((top, -1)) }),
            R(Keys, new() { ["c1"] = Marks((top, -1)) }));

        var first = r.Prompts[0];
        Assert.Equal(PromptPriority.High, first.Priority);
        Assert.Equal(PromptKinds.TopCardThreatened, first.Kind);
        Assert.Equal(top, first.CardKey);
        Assert.Equal("c1", first.ChangeId);
        // та же пара (изменение, карточка) не дублируется обычной подсказкой
        Assert.DoesNotContain(r.Prompts, p => p.Kind == PromptKinds.NegativeImpact && p.CardKey == top);
    }

    [Fact]
    public void Negative_impact_prompt_for_non_top_card()
    {
        var settings = new SurveySettings { Changes = [new SurveyChange { Id = "c1", Text = "X" }] };
        var r = Calc(settings, true, R(Keys, new() { ["c1"] = Marks((Keys[0], -1)) }));
        Assert.Contains(r.Prompts, p => p.Kind == PromptKinds.NegativeImpact && p.CardKey == Keys[0] && p.ChangeId == "c1");
        Assert.DoesNotContain(r.Prompts, p => p.Priority == PromptPriority.High);
    }

    [Fact]
    public void Split_votes_prompt_when_30_percent_up_and_down()
    {
        var settings = new SurveySettings { Changes = [new SurveyChange { Id = "c1", Text = "X" }] };
        var r = Calc(settings, true,
            R(Keys, new() { ["c1"] = Marks((Keys[5], 1)) }),
            R(Keys, new() { ["c1"] = Marks((Keys[5], -1)) }),
            R(Keys, new() { ["c1"] = Marks() }));
        Assert.Contains(r.Prompts, p => p.Kind == PromptKinds.SplitImpact && p.ChangeId == "c1");
    }

    [Fact]
    public void Top_card_prompts_include_catalog_questions()
    {
        var r = Calc(new SurveySettings { Changes = [] }, true, R(Keys));
        var topPrompts = r.Prompts.Where(p => p.Kind == PromptKinds.TopCard).ToList();
        Assert.Equal(3, topPrompts.Count);
        Assert.All(topPrompts, p => Assert.InRange(p.Questions!.Count, 2, 3));
    }

    [Fact]
    public void Anonymous_hides_participants_and_comment_metadata()
    {
        var r = Calc(new SurveySettings { Changes = [] }, true, R(Keys, name: "Иван", comment: "Хочу больше свободы"));
        Assert.Null(r.Participants);
        var c = Assert.Single(r.Comments);
        Assert.Null(c.Name);
        Assert.Null(c.SubmittedOn);
    }

    [Fact]
    public void Named_survey_exposes_participants()
    {
        var r = Calc(new SurveySettings { Changes = [] }, false, R(Keys, name: "Мария", comment: "ok"), R(Keys, name: "Алексей"));
        Assert.Equal(new[] { "Алексей", "Мария" }, r.Participants!.Select(p => p.Name));
        Assert.Equal("Мария", r.Comments.Single().Name);
    }

    [Fact]
    public void All_cards_have_discussion_prompts()
    {
        Assert.All(Catalog.Motivators, m => Assert.InRange(m.DiscussionPrompts.Count, 2, 3));
        Assert.Contains("Где сейчас вам не хватает самостоятельности?", Catalog.FindMotivator("freedom")!.DiscussionPrompts);
    }
}
