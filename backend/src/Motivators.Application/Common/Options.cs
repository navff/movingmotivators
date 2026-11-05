namespace Motivators.Application.Common;

public class AppOptions
{
    public const string Section = "App";
    public string ProductName { get; set; } = "Движущие Мотиваторы: онлайн-тест";
    public string BaseUrl { get; set; } = "https://movingmotivators.ru";
    /// <summary>Список email админов через запятую или массивом.</summary>
    public List<string> AdminEmails { get; set; } = [];
    public bool ShowEnglishCardNames { get; set; }

    public bool IsAdmin(string email) =>
        AdminEmails.SelectMany(e => e.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase));

    public string Url(string relative) => BaseUrl.TrimEnd('/') + relative;
}

public class PricingOptions
{
    public const string Section = "Pricing";
    public int FreeMaxParticipants { get; set; } = 8;
    public int SurveyPriceRub { get; set; } = 299;
    /// <summary>fake | yookassa</summary>
    public string CheckoutMode { get; set; } = "fake";
}

public class LegalOptions
{
    public const string Section = "Legal";
    public string Name { get; set; } = "ИП Варнавский Владимир Николаевич";
    public string ShortName { get; set; } = "ИП Варнавский В. Н.";
    public string Inn { get; set; } = "352826429326";
    public string Ogrnip { get; set; } = "308352816800157";
    public string Address { get; set; } = "162642, Вологодская обл., Череповецкий р-н, д. Шайма, ул. Вологодская, д. 11";
    public string BankAccount { get; set; } = "40802810100000025438 в АО «ТИНЬКОФФ БАНК»";
    public string CorrAccount { get; set; } = "30101810145250000974";
    public string Bik { get; set; } = "044525974";
    public string Email { get; set; } = "support@movingmotivators.ru";
    public string OrganizerConsentVersion { get; set; } = "org-1";
    public string ParticipantConsentVersion { get; set; } = "part-1";
}

public class AuthOptions
{
    public const string Section = "Auth";
    public string CookieName { get; set; } = "__Host-session";
    public int LoginTokenTtlMinutes { get; set; } = 15;
    public int SessionDays { get; set; } = 30;
    public int LinksPerEmailPerHour { get; set; } = 5;
}

public class ResultsOptions
{
    public const string Section = "Results";
    /// <summary>std ранга выше — «мнения сильно расходятся».</summary>
    public double MmDividedStd { get; set; } = 2.5;
    /// <summary>std ранга не выше — «команда единодушна».</summary>
    public double MmUnanimousStd { get; set; } = 1.5;
    /// <summary>Индекс влияния ниже — изменение бьёт по мотиватору.</summary>
    public double MmNegativeImpact { get; set; } = -0.3;
    /// <summary>Доля ↑ и доля ↓ не меньше — голоса разделились.</summary>
    public double MmSplitShare { get; set; } = 0.3;
    /// <summary>Средняя оценка развитости (1..5) ниже — мотиватор из топ-3 развит слабо.</summary>
    public double MmLowRating { get; set; } = 3.0;
    public double HcGreenMin { get; set; } = 2.5;
    public double HcYellowMin { get; set; } = 1.75;
}

public class SurveyOptions
{
    public const string Section = "Surveys";
    public int MaxResponses { get; set; } = 200;
    public int MinFillSeconds { get; set; } = 15;
    public int DefaultDurationDays { get; set; } = 7;
    public int ReopenWindowDays { get; set; } = 30;
    public int DraftRetentionDays { get; set; } = 30;
}
