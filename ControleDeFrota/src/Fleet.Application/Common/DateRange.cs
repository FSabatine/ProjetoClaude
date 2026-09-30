using FluentValidation;

namespace Fleet.Application.Common;

public static class DateRangeRules
{
    public const int MaxPeriodDays = 366 * 5;

    /// <summary>"from ≤ to" for optional list filters.</summary>
    public static void ValidPeriod<T>(this AbstractValidator<T> validator, Func<T, DateOnly?> from, Func<T, DateOnly?> to, string field) =>
        validator.RuleFor(x => to(x))
            .Must((x, end) => from(x) is not { } start || end is not { } e || start <= e)
            .WithName(field)
            .OverridePropertyName(field)
            .WithMessage("A data final deve ser igual ou posterior à data inicial.");

}
