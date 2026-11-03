using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Motivators.Application.Common;
using Npgsql;

namespace Motivators.Api.Infrastructure;

/// <summary>Ошибки → application/problem+json (RFC 9457) с errorCode.</summary>
public class AppExceptionHandler(IProblemDetailsService problems, ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception exception, CancellationToken ct)
    {
        var (status, code, title, errors) = exception switch
        {
            AppException app => (app.Status, app.ErrorCode, app.Message, app.Errors),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (409, "conflict", "Конфликт данных, повторите запрос", null),
            BadHttpRequestException bad => (bad.StatusCode, ErrorCodes.ValidationFailed, "Некорректный запрос", null),
            OperationCanceledException when ct.IsCancellationRequested => (499, "canceled", "Запрос отменён", null),
            _ => (500, "internal_error", "Внутренняя ошибка сервера", (IDictionary<string, string[]>?)null),
        };
        if (status >= 500) logger.LogError(exception, "Необработанная ошибка");

        ctx.Response.StatusCode = status;
        var details = new ProblemDetails { Status = status, Title = title };
        details.Extensions["errorCode"] = code;
        if (errors != null) details.Extensions["errors"] = errors;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = ctx, ProblemDetails = details, Exception = exception });
    }
}
