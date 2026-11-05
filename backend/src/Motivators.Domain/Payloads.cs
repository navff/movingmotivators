namespace Motivators.Domain;

/// <summary>Ответ по ММ. <c>Ranking</c>: 10 ключей, последний — самый важный.</summary>
public class MotivatorsPayload
{
    public List<string> Ranking { get; set; } = [];
    /// <summary>Ключ карточки → оценка 1..5: насколько мотиватор развит в команде. null — методика v1.</summary>
    public Dictionary<string, int>? Ratings { get; set; }
    /// <summary>Необязательные комментарии к оценкам: ключ карточки → текст. Пустые не хранятся.</summary>
    public Dictionary<string, string>? RatingComments { get; set; }
    /// <summary>changeId → (ключ карточки → 1 | 0 | -1). null — шаг влияния выключен.</summary>
    public Dictionary<string, Dictionary<string, int>>? Impact { get; set; }
}

public class HealthCheckAnswer
{
    public int Score { get; set; }
    public int Trend { get; set; }
    public string? Comment { get; set; }
}

public class HealthCheckPayload
{
    public Dictionary<string, HealthCheckAnswer> Answers { get; set; } = [];
}
