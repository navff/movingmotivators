using FluentValidation;

namespace Motivators.Application.Common;

public static class ValidationExtensions
{
    public static void ValidateOrThrow<T>(this IValidator<T> validator, T instance)
    {
        var result = validator.Validate(instance);
        if (result.IsValid) return;
        var errors = result.Errors
            .GroupBy(e => char.ToLowerInvariant(e.PropertyName.FirstOrDefault()) + e.PropertyName[1..])
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        throw AppException.Validation(result.Errors[0].ErrorMessage, errors);
    }
}
