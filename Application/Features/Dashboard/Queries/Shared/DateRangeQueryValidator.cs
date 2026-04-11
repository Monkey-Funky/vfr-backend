

namespace Application.Features.Dashboard.Queries.Shared;

/// <summary>
/// Shared validator base for all dashboard date-range queries.
/// Subclass validators call ApplyDateRangeRules(fromExpr, toExpr) from their constructor.
///
/// Rules:
///   1. From is required (not default DateOnly).
///   2. To   is required (not default DateOnly).
///   3. From ≤ To.
///   4. Range ≤ 365 days.
///   5. From not more than 2 years in the past.
/// </summary>
public abstract class DateRangeQueryValidator<T> : AbstractValidator<T>
{
    protected void ApplyDateRangeRules(
        System.Linq.Expressions.Expression<Func<T, DateOnly>> fromExpr,
        System.Linq.Expressions.Expression<Func<T, DateOnly>> toExpr)
    {
        RuleFor(fromExpr)
            .NotEqual(default(DateOnly))
            .WithMessage("From date is required.");

        RuleFor(toExpr)
            .NotEqual(default(DateOnly))
            .WithMessage("To date is required.");

        RuleFor(x => x)
            .Must(x =>
            {
                DateOnly from = fromExpr.Compile()(x);
                DateOnly to = toExpr.Compile()(x);
                return from <= to;
            })
            .WithName("DateRange")
            .WithMessage("From date must be earlier than or equal to To date.");

        RuleFor(x => x)
            .Must(x =>
            {
                DateOnly from = fromExpr.Compile()(x);
                DateOnly to = toExpr.Compile()(x);
                return (to.DayNumber - from.DayNumber) <= 365;
            })
            .WithName("DateRange")
            .WithMessage("Date range cannot exceed 365 days (1 year).");

        RuleFor(fromExpr)
            .Must(from =>
            {
                DateOnly twoYearsAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2));
                return from >= twoYearsAgo;
            })
            .WithMessage("From date cannot be more than 2 years in the past.");
    }
}