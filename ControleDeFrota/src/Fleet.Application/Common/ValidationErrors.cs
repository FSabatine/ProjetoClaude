using FluentValidation;
using FluentValidation.Results;

namespace Fleet.Application.Common;

public static class ValidationErrors
{
    /// <summary>Field-level error for rules that need data unavailable to the validator (e.g. the user's e-mail).</summary>
    public static ValidationException ForField(string field, string message) =>
        new([new ValidationFailure(field, message)]);
}
