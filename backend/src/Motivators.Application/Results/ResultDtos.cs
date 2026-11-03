using Motivators.Domain;

namespace Motivators.Application.Results;

/// <summary>Вход калькулятора: один ответ без привязки к БД.</summary>
public record ResponseInput<TPayload>(Guid Id, string? Name, TPayload Payload, string? Comment, DateOnly SubmittedOn);

public static class PromptPriority
{
    public const string High = "high";
    public const string Normal = "normal";
}

public static class PromptKinds
{
    public const string TopCardThreatened = "top_card_threatened";
    public const string TopCard = "top_card";
    public const string DividedOpinions = "divided_opinions";
    public const string NegativeImpact = "negative_impact";
    public const string SplitImpact = "split_impact";
    public const string FocusCategory = "focus_category";
}

public record DiscussionPrompt(
    string Priority,
    string Kind,
    string Text,
    string? CardKey = null,
    string? ChangeId = null,
    IReadOnlyList<string>? Questions = null);

public record CommentDto(string Text, string? Name = null, DateOnly? SubmittedOn = null, string? CategoryKey = null);

// ---------- Движущие мотиваторы ----------

public static class Consensus
{
    public const string Unanimous = "unanimous";
    public const string Mixed = "mixed";
    public const string Divided = "divided";
}

public record MotivatorCardStats(
    string Key,
    double AvgRank,
    double StdRank,
    string Consensus,
    /// <summary>Distribution[i] — сколько человек поставили карточку на позицию i+1 (10 — самая важная).</summary>
    int[] Distribution);

public record ChangeCardImpact(string Key, int Up, int Neutral, int Down, double Index);

public record ChangeImpactResult(
    string Id,
    string Text,
    int ResponseCount,
    /// <summary>В порядке среднего ранга команды (самые важные сначала).</summary>
    IReadOnlyList<ChangeCardImpact> Cards,
    /// <summary>Средний индекс, взвешенный по важности карточек.</summary>
    double Overall);

public record MotivatorsParticipant(
    string Name,
    IReadOnlyList<string> Ranking,
    Dictionary<string, Dictionary<string, int>>? Impact,
    string? Comment);

public record MotivatorsResult(
    int ResponseCount,
    IReadOnlyList<MotivatorCardStats> Cards,
    IReadOnlyList<string> Top3,
    IReadOnlyList<string> Bottom3,
    IReadOnlyList<ChangeImpactResult> Changes,
    string? MostPainfulChangeId,
    string? MostPositiveChangeId,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<DiscussionPrompt> Prompts,
    IReadOnlyList<MotivatorsParticipant>? Participants)
{
    public string Type => SurveyTypes.MovingMotivators;
}

// ---------- Health Check ----------

public static class TrafficLight
{
    public const string Green = "green";
    public const string Yellow = "yellow";
    public const string Red = "red";
}

public record HealthCheckCategoryStats(
    string Key,
    int Green,
    int Yellow,
    int Red,
    double? AvgScore,
    string? Color,
    int TrendUp,
    int TrendSame,
    int TrendDown,
    /// <summary>Преобладающий тренд: 1, 0, -1; null — нет ответов.</summary>
    int? Trend,
    IReadOnlyList<CommentDto> Comments);

public record HealthCheckParticipant(string Name, Dictionary<string, HealthCheckAnswer> Answers, string? Comment);

public record HealthCheckResult(
    int ResponseCount,
    IReadOnlyList<HealthCheckCategoryStats> Categories,
    double? OverallAvg,
    IReadOnlyList<string> Focus,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<DiscussionPrompt> Prompts,
    IReadOnlyList<HealthCheckParticipant>? Participants)
{
    public string Type => SurveyTypes.HealthCheck;
}

// ---------- История и сравнение ----------

public record HealthCheckWaveCell(double? AvgScore, string? Color, int? Trend);

public record HealthCheckWave(
    Guid SurveyId,
    string Title,
    DateTimeOffset? ClosedAt,
    int ResponseCount,
    double? OverallAvg,
    Dictionary<string, HealthCheckWaveCell> Categories);

public record HealthCheckHistory(IReadOnlyList<string> CategoryKeys, IReadOnlyList<HealthCheckWave> Waves);

public record CompareCard(string Key, double AvgA, double AvgB, double Delta);

public record CompareParticipant(string Name, Dictionary<string, int> Shift);

public record SurveyRef(Guid Id, string Title, DateTimeOffset? ClosedAt, int ResponseCount);

public record MotivatorsComparison(
    SurveyRef A,
    SurveyRef B,
    IReadOnlyList<CompareCard> Cards,
    IReadOnlyList<CompareParticipant>? Participants);
