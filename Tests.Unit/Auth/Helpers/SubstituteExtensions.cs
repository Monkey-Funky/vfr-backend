// tests/Tests.Unit/Auth/Helpers/SubstituteExtensions.cs
using Application.Interfaces;
using Domain.Common;
using Domain.Entities.Retailer;
using NSubstitute;
using System.Linq.Expressions;

namespace Tests.Unit.Auth.Helpers;

/// <summary>
/// NSubstitute wiring helpers for Auth unit tests.
///
/// IMPORTANT — why UpdateAsync and AddAsync have no .Returns() setup here:
///
///   • UpdateAsync returns plain Task (void-equivalent). You cannot call
///     .Returns(someEntity) on it — the compiler rejects it because
///     RetailerAccount cannot be converted to Func&lt;CallInfo, Task&gt;.
///     NSubstitute returns Task.CompletedTask by default. No setup needed.
///
///   • AddAsync returns Task&lt;T&gt;, but all handlers in this project discard
///     the return value (they only await the call). NSubstitute's default
///     Task.FromResult(default(T)) is correct. Setting up .Returns() would
///     also cause CS4014 "not awaited" warnings in the setup call chain.
///
///   Bottom line: only set up methods whose return value is actually used
///   by the code under test.
/// </summary>
public static class SubstituteExtensions
{
    /// <summary>
    /// Wires the unit-of-work mock:
    ///   • Repository&lt;RetailerAccount&gt;() → accountRepo
    ///   • Repository&lt;NotificationPreference&gt;() → prefRepo (optional)
    ///   • SaveChangesAsync → returns 1
    ///   • ExecuteInTransactionAsync → executes the lambda inline (no real DB transaction)
    /// </summary>
    public static void SetupRepositories(
        this IUnitOfWork unitOfWork,
        IRepository<RetailerAccount> accountRepo,
        IRepository<NotificationPreference>? prefRepo = null)
    {
        unitOfWork.Repository<RetailerAccount>().Returns(accountRepo);

        if (prefRepo is not null)
            unitOfWork.Repository<NotificationPreference>().Returns(prefRepo);

        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        // Inline execution simulates a transaction without a real database
        unitOfWork.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var operation = callInfo.Arg<Func<CancellationToken, Task>>();
                return operation(CancellationToken.None);
            });
    }

    /// <summary>
    /// Configures FirstOrDefaultAsync to return <paramref name="entity"/> (may be null)
    /// for any predicate expression.
    ///
    /// FIX: Parameter type is T? (nullable) and we use Task.FromResult(entity) to
    /// produce Task&lt;T?&gt;, which matches FirstOrDefaultAsync's exact return type.
    /// This eliminates the nullability mismatch warning from the previous version.
    /// </summary>
    public static void ReturnsForAnyPredicate<T>(
        this IRepository<T> repo,
        T? entity)
        where T : BaseEntity
    {
        repo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<T, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(entity));   // Task<T?> — exact match
    }

    /// <summary>Configures AnyAsync to return <paramref name="result"/> for any predicate.</summary>
    public static void AnyReturns<T>(
        this IRepository<T> repo,
        bool result)
        where T : BaseEntity
    {
        repo.AnyAsync(
                Arg.Any<Expression<Func<T, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(result);
    }
}