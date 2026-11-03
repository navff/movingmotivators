namespace Motivators.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Email { get; set; } = "";
    public string? DisplayName { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public string? ConsentVersion { get; set; }
    public DateTimeOffset? ConsentAt { get; set; }

    public List<Team> Teams { get; set; } = [];
}

public static class TokenPurpose
{
    public const string Login = "login";
    public const string DeleteAccount = "delete_account";
}

public class LoginToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public byte[] TokenHash { get; set; } = [];
    public string Purpose { get; set; } = TokenPurpose.Login;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? ReturnUrl { get; set; }
}

public class Session
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public byte[] SessionHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}

public class Team
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public string Name { get; set; } = "";
    public int Size { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<Survey> Surveys { get; set; } = [];
}

public static class SurveyTypes
{
    public const string MovingMotivators = "moving_motivators";
    public const string HealthCheck = "health_check";

    public static bool IsValid(string? type) => type is MovingMotivators or HealthCheck;
}

public static class SurveyStatus
{
    public const string Draft = "draft";
    public const string Open = "open";
    public const string Closed = "closed";
}

public class Survey
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public string Type { get; set; } = SurveyTypes.MovingMotivators;
    public int MethodologyVersion { get; set; } = MethodologyCatalog.CurrentVersion;
    public string Title { get; set; } = "";
    public bool IsAnonymous { get; set; }
    public int DeclaredParticipants { get; set; }
    public int PriceRub { get; set; }
    public string PublicCode { get; set; } = "";
    /// <summary>Соль для хеша respondentKey (уникальна для опроса).</summary>
    public byte[] RespondentSalt { get; set; } = [];
    public string? WelcomeMessage { get; set; }
    public SurveySettings Settings { get; set; } = new();
    public bool AutoCloseOnFull { get; set; }
    public bool ResponsesOverDeclared { get; set; }
    public string Status { get; set; } = SurveyStatus.Draft;
    public DateTimeOffset? OpenedAt { get; set; }
    public DateTimeOffset? ClosesAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<Response> Responses { get; set; } = [];
    public SurveyResultSnapshot? Snapshot { get; set; }
}

public class SurveySettings
{
    /// <summary>ММ: изменения для оценки влияния. Пустой список — шаг выключен.</summary>
    public List<SurveyChange>? Changes { get; set; }
    /// <summary>HC: включённые категории.</summary>
    public List<string>? Categories { get; set; }
}

public class SurveyChange
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
}

public class Response
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SurveyId { get; set; }
    public Survey Survey { get; set; } = null!;
    public byte[] RespondentKeyHash { get; set; } = [];
    public string? ParticipantName { get; set; }
    /// <summary>jsonb: MotivatorsPayload или HealthCheckPayload.</summary>
    public string Payload { get; set; } = "{}";
    public string? Comment { get; set; }
    public DateOnly SubmittedOn { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public string? ConsentVersion { get; set; }
    public int UpdatedCount { get; set; }
}

public class SurveyResultSnapshot
{
    public Guid SurveyId { get; set; }
    public Survey Survey { get; set; } = null!;
    public DateTimeOffset ComputedAt { get; set; }
    public int ResponseCount { get; set; }
    public string Result { get; set; } = "{}";
}

public static class CheckoutStatus
{
    public const string Created = "created";
    public const string Clicked = "clicked";
    public const string Succeeded = "succeeded";
    public const string Canceled = "canceled";
}

public static class PaymentMethods
{
    public const string Card = "card";
    public const string Sbp = "sbp";
    public const string SberPay = "sberpay";

    public static bool IsValid(string? m) => m is Card or Sbp or SberPay;
}

public class Checkout
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SurveyId { get; set; }
    public Survey Survey { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int AmountRub { get; set; }
    public string? Method { get; set; }
    public string Mode { get; set; } = "fake";
    public string Status { get; set; } = CheckoutStatus.Created;
    public string? ProviderPaymentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClickedAt { get; set; }
}

public static class FunnelEvents
{
    public const string SurveyDraftPaidRequired = "survey_draft_paid_required";
    public const string CheckoutViewed = "checkout_viewed";
    public const string PaymentMethodSelected = "payment_method_selected";
    public const string PaymentClicked = "payment_clicked";
}

public class FunnelEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid? SurveyId { get; set; }
    public string Event { get; set; } = "";
    public string Props { get; set; } = "{}";
    public DateTimeOffset At { get; set; }
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Без FK: запись переживает удаление аккаунта.</summary>
    public Guid? UserId { get; set; }
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public Guid? EntityId { get; set; }
    public DateTimeOffset At { get; set; }
}
