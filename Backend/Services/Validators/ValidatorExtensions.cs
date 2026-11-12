using FluentValidation;
using FluentValidation.Results;
using ValidationException = Common.Exceptions.ValidationException;

namespace Services.Validators;

/// <summary>
/// Runs a FluentValidation validator from a service (spec 11.4) and turns failures into
/// <see cref="ValidationException"/> (→ 400 ProblemDetails with "errors" by field name).
/// </summary>
public static class ValidatorExtensions
{
    /// <exception cref="ValidationException">The instance is invalid.</exception>
    public static async Task EnsureValidAsync<T>(this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (!result.IsValid)
        {
            throw new ValidationException(errors: ToErrors(result.Errors));
        }
    }

    /// <summary>Messages grouped by field, keys in camelCase as in the JSON body ("firstName", "roles[0]").</summary>
    public static IReadOnlyDictionary<string, string[]> ToErrors(IEnumerable<ValidationFailure> failures) =>
        failures
            .GroupBy(f => ToCamelCasePath(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

    internal static string ToCamelCasePath(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(segment =>
            segment.Length == 0 ? segment : char.ToLowerInvariant(segment[0]) + segment[1..]));
}
