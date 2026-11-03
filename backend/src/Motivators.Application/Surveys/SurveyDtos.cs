using FluentValidation;
using Motivators.Domain;

namespace Motivators.Application.Surveys;

public record SurveySettingsDto(IReadOnlyList<SurveyChange> Changes, IReadOnlyList<string> Categories);

public record SurveyDto(
    Guid Id,
    Guid TeamId,
    string TeamName,
    string Type,
    string Title,
    bool IsAnonymous,
    int DeclaredParticipants,
    int PriceRub,
    string PublicCode,
    string PublicUrl,
    string? WelcomeMessage,
    SurveySettingsDto Settings,
    bool AutoCloseOnFull,
    string Status,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? ClosesAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    int ResponseCount,
    bool ResponsesOverDeclared,
    bool CheckoutRequired,
    bool CanReopen);

public record SurveyListItem(
    Guid Id,
    string Type,
    string Title,
    string Status,
    bool IsAnonymous,
    int DeclaredParticipants,
    int PriceRub,
    int ResponseCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosesAt,
    DateTimeOffset? ClosedAt,
    bool CheckoutRequired);

public record CreateSurveyRequest(
    string Type,
    string? Title,
    int DeclaredParticipants,
    bool IsAnonymous,
    List<string>? Changes,
    List<string>? Categories,
    DateTimeOffset? ClosesAt,
    string? WelcomeMessage,
    bool AutoCloseOnFull);

public record CreateSurveyResponse(SurveyDto Survey, bool CheckoutRequired, int PriceRub);

public record ChangeInput(string? Id, string Text);

public record UpdateSurveyRequest(
    string? Title,
    DateTimeOffset? ClosesAt,
    string? WelcomeMessage,
    bool? IsAnonymous,
    List<ChangeInput>? Changes,
    List<string>? Categories,
    bool? AutoCloseOnFull);

public record ReopenRequest(DateTimeOffset ClosesAt);

public record CheckoutSummary(
    Guid SurveyId,
    string SurveyType,
    string SurveyTitle,
    string TeamName,
    int Participants,
    int AmountRub,
    string Email,
    string LegalName,
    string LegalInn);

public record CheckoutMethodRequest(string Method);

public record CheckoutRequest(string Method);

public record CheckoutResponse(string Status, string? ConfirmationUrl, SurveyDto Survey);

public static class SurveyLimits
{
    public const int TitleMax = 200;
    public const int WelcomeMax = 500;
    public const int ChangeTextMax = 300;
    public const int MaxChanges = 5;
    public const int MinCategories = 3;
    public const int MinParticipants = 2;
    public const int MaxParticipants = 200;
}

public class CreateSurveyValidator : AbstractValidator<CreateSurveyRequest>
{
    public CreateSurveyValidator()
    {
        RuleFor(x => x.Type).Must(SurveyTypes.IsValid).WithMessage("Неизвестный тип опроса");
        RuleFor(x => x.Title).MaximumLength(SurveyLimits.TitleMax);
        RuleFor(x => x.DeclaredParticipants)
            .InclusiveBetween(SurveyLimits.MinParticipants, SurveyLimits.MaxParticipants)
            .WithMessage($"Число участников — от {SurveyLimits.MinParticipants} до {SurveyLimits.MaxParticipants}");
        RuleFor(x => x.WelcomeMessage).MaximumLength(SurveyLimits.WelcomeMax);
        RuleFor(x => x.Changes).Must(c => c == null || c.Count <= SurveyLimits.MaxChanges)
            .WithMessage($"Не более {SurveyLimits.MaxChanges} изменений");
        RuleForEach(x => x.Changes).NotEmpty().WithMessage("Текст изменения обязателен")
            .MaximumLength(SurveyLimits.ChangeTextMax);
        RuleFor(x => x.Changes).Null().When(x => x.Type == SurveyTypes.HealthCheck)
            .WithMessage("Изменения доступны только для «Движущих мотиваторов»");
        RuleFor(x => x.Categories).Null().When(x => x.Type == SurveyTypes.MovingMotivators)
            .WithMessage("Категории доступны только для Health Check");
    }
}

public class UpdateSurveyValidator : AbstractValidator<UpdateSurveyRequest>
{
    public UpdateSurveyValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(SurveyLimits.TitleMax).When(x => x.Title != null);
        RuleFor(x => x.WelcomeMessage).MaximumLength(SurveyLimits.WelcomeMax);
        RuleFor(x => x.Changes).Must(c => c == null || c.Count <= SurveyLimits.MaxChanges)
            .WithMessage($"Не более {SurveyLimits.MaxChanges} изменений");
        RuleForEach(x => x.Changes).ChildRules(c =>
            c.RuleFor(x => x.Text).NotEmpty().WithMessage("Текст изменения обязателен").MaximumLength(SurveyLimits.ChangeTextMax));
    }
}
