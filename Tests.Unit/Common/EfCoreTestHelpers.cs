// tests/Tests.Unit/Common/EfCoreTestHelpers.cs
using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Moq;

namespace Tests.Unit.Common;

// ── Async Enumerable wrapper for EF Core mock DbSets ──────────────────────────

internal sealed class TestAsyncEnumerable<T> : IEnumerable<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    private readonly IQueryable<T> _inner;
    private readonly IEnumerable<T> _enumerable;

    public TestAsyncEnumerable(IEnumerable<T> enumerable)
    {
        _enumerable = enumerable;
        _inner = enumerable.AsQueryable();
    }

    public TestAsyncEnumerable(Expression expression)
    {
        var eq = new EnumerableQuery<T>(expression);
        _enumerable = eq;
        _inner = eq;
    }

    public Type ElementType => _inner.ElementType;
    public Expression Expression => _inner.Expression;
    public IQueryProvider Provider => new TestAsyncQueryProvider<T>(_inner.Provider);

    public IEnumerator<T> GetEnumerator() => _enumerable.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _enumerable.GetEnumerator();

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(_enumerable.GetEnumerator());
}

internal sealed class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;
    public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;
    public T Current => _inner.Current;
    public ValueTask DisposeAsync() { _inner.Dispose(); return ValueTask.CompletedTask; }
    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_inner.MoveNext());
}

internal sealed class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;
    public TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(Expression expression)
        => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression) => _inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        Type resultType = typeof(TResult).GetGenericArguments()[0];

        object? executionResult;
        try
        {
            executionResult = _inner.Execute(expression);
        }
        catch
        {
            executionResult = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
        }

        return (TResult)typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, new[] { executionResult })!;
    }
}

internal static class MockDbSetFactory
{
    public static Mock<Microsoft.EntityFrameworkCore.DbSet<T>> Create<T>(IList<T> data)
        where T : class
    {
        var queryable = new TestAsyncEnumerable<T>(data);

        var mockSet = new Mock<Microsoft.EntityFrameworkCore.DbSet<T>>(MockBehavior.Loose);

        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => queryable.GetEnumerator());
        mockSet.As<IAsyncEnumerable<T>>()
               .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
               .Returns(new TestAsyncEnumerator<T>(data.GetEnumerator()));

        return mockSet;
    }
}