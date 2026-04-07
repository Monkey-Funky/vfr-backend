

namespace Application.Features.Dashboard.Queries.Shared;

/// <summary>
/// Shared validator base for all dashboard date-range queries.
///
/// Rules enforced (all return HTTP 422 with descriptive messages):
///   1. From must be provided (not default DateOnly).
///   2. To must be provided (not default DateOnly).
///   3. From must be less than or equal to To.
///   4. Range must not exceed 365 days (1 calendar year).
///   5. From must not be more than 2 years in the past (prevents excessively
///      wide queries that bypass cache effectiveness and saturate the DB).
///
/// All five rules are applied via ApplyDateRangeRules(fromExpr, toExpr).
/// Subclass validators call this method from their constructor once and are done.
/// </summary>
public abstract class DateRangeQueryValidator<T> : AbstractValidator<T>
{
    protected void ApplyDateRangeRules(
        System.Linq.Expressions.Expression<Func<T, DateOnly>> fromExpr,
        System.Linq.Expressions.Expression<Func<T, DateOnly>> toExpr)
    {
        // ── Rule 1: From must be provided ────────────────────────────────────
        RuleFor(fromExpr)
            .NotEqual(default(DateOnly))
            .WithMessage("From date is required.");

        // ── Rule 2: To must be provided ──────────────────────────────────────
        RuleFor(toExpr)
            .NotEqual(default(DateOnly))
            .WithMessage("To date is required.");

        // ── Rule 3: From <= To ────────────────────────────────────────────────
        RuleFor(x => x)
            .Must(x =>
            {
                DateOnly from = fromExpr.Compile()(x);
                DateOnly to = toExpr.Compile()(x);
                return from <= to;
            })
            .WithName("DateRange")
            .WithMessage("From date must be earlier than or equal to To date.");

        // ── Rule 4: Range ≤ 365 days ──────────────────────────────────────────
        RuleFor(x => x)
            .Must(x =>
            {
                DateOnly from = fromExpr.Compile()(x);
                DateOnly to = toExpr.Compile()(x);
                return (to.DayNumber - from.DayNumber) <= 365;
            })
            .WithName("DateRange")
            .WithMessage("Date range cannot exceed 365 days (1 year).");

        // ── Rule 5: From not more than 2 years in the past ───────────────────
        RuleFor(fromExpr)
            .Must(from =>
            {
                DateOnly twoYearsAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2));
                return from >= twoYearsAgo;
            })
            .WithMessage("From date cannot be more than 2 years in the past.");
    }
}