// tests/Tests.Unit/Application/Behaviors/LoggingBehaviorTests.cs
using Application.Behaviors;
using Application.Interfaces.Services;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Unit.Application.Behaviors;

public sealed class LoggingBehaviorTests
{
    private sealed record TestRequest(string Data) : IRequest<string>;

    // ── ICurrentUserService stub ──────────────────────────────────────────────
    // LoggingBehavior requires ICurrentUserService to enrich log entries with RetailerId.
    // In unit tests we supply a mock that returns a fixed tenant ID so log output is
    // deterministic and assertions never depend on a real HTTP context.
    private static Mock<ICurrentUserService> BuildCurrentUserMock(Guid? retailerId = null)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(c => c.RetailerId).Returns(retailerId ?? Guid.NewGuid());
        return mock;
    }

    // ── ILogger helper: captures log calls to track execution order ──────────
    private static (Mock<ILogger<LoggingBehavior<TestRequest, string>>>, List<string>) BuildLogCapture()
    {
        var logMock = new Mock<ILogger<LoggingBehavior<TestRequest, string>>>();
        var callLog = new List<string>();

        logMock.Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Delegate>(
                (level, _, state, _, formatter) =>
                    callLog.Add($"{level}:{formatter.DynamicInvoke(state, null)}"));

        return (logMock, callLog);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AlwaysLogsBeforeAndAfterInnerHandler()
    {
        // Arrange
        var (logMock, _) = BuildLogCapture();
        var currentUserMock = BuildCurrentUserMock();

        // FIX #1: LoggingBehavior requires ICurrentUserService as second constructor arg.
        var behavior = new LoggingBehavior<TestRequest, string>(
            logMock.Object,
            currentUserMock.Object);

        var executionOrder = new List<string>();

        // Intercept log calls to track ordering relative to the inner handler
        logMock.Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Delegate>(
                (_, _, _, _, _) => executionOrder.Add("log"));

        // FIX #2: RequestHandlerDelegate<TResponse> in MediatR 13 is Func<CancellationToken, Task<TResponse>>.
        // The lambda must accept a CancellationToken parameter even when it is not used.
        RequestHandlerDelegate<string> next = _ =>
        {
            executionOrder.Add("handler");
            return Task.FromResult("response");
        };

        // Act
        string result = await behavior.Handle(new TestRequest("data"), next, CancellationToken.None);

        // Assert
        result.Should().Be("response");
        executionOrder.Should().HaveCountGreaterThanOrEqualTo(2);

        int firstLog = executionOrder.IndexOf("log");
        int handlerIndex = executionOrder.IndexOf("handler");
        int lastLog = executionOrder.LastIndexOf("log");

        firstLog.Should().BeLessThan(handlerIndex,
            "a log entry must appear BEFORE the handler executes");
        lastLog.Should().BeGreaterThan(handlerIndex,
            "a log entry must appear AFTER the handler executes");
    }

    [Fact]
    public async Task Handle_WhenInnerHandlerThrows_StillLogsAndPropagatesException()
    {
        // Arrange
        var (logMock, _) = BuildLogCapture();
        var currentUserMock = BuildCurrentUserMock();

        var behavior = new LoggingBehavior<TestRequest, string>(
            logMock.Object,
            currentUserMock.Object);

        int logCallCount = 0;
        logMock.Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => logCallCount++);

        // FIX #2: 1-arg lambda matching RequestHandlerDelegate<string> = Func<CancellationToken, Task<string>>
        RequestHandlerDelegate<string> next = _ =>
            throw new InvalidOperationException("Handler boom!");

        // Act
        Func<Task> act = () => behavior.Handle(new TestRequest("x"), next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Handler boom!");

        logCallCount.Should().BeGreaterThanOrEqualTo(1,
            "the behavior must log at least the start message before re-throwing");
    }
}
