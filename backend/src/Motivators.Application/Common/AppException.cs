namespace Motivators.Application.Common;

/// <summary>Ошибка сценария, превращается в problem+json с <see cref="ErrorCode"/>.</summary>
public class AppException(int status, string errorCode, string message, IDictionary<string, string[]>? errors = null)
    : Exception(message)
{
    public int Status { get; } = status;
    public string ErrorCode { get; } = errorCode;
    public IDictionary<string, string[]>? Errors { get; } = errors;

    public static AppException NotFound() => new(404, "not_found", "Не найдено");
    public static AppException Validation(string message, IDictionary<string, string[]>? errors = null) =>
        new(400, "validation_failed", message, errors);
    public static AppException Conflict(string code, string message) => new(409, code, message);
}

public static class ErrorCodes
{
    public const string SurveyClosed = "survey_closed";
    public const string CheckoutRequired = "checkout_required";
    public const string ValidationFailed = "validation_failed";
    public const string TooFast = "too_fast";
    public const string ResponseLimit = "response_limit";
    public const string InvalidToken = "invalid_token";
    public const string RateLimited = "rate_limited";
    public const string Locked = "survey_locked";
    public const string InvalidState = "invalid_state";
}
