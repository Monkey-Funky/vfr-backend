// tests/Tests.Unit/Application/Behaviors/PerformanceBehaviorTests.cs
using Application.Behaviors;
using Application.Interfaces;
using Application.Interfaces.Services;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Unit.Application.Behaviors;

public sealed class PerformanceBehaviorTests
{
    private sealed record TestRequest(string Data) : IRequest<bool>;

    // ── Shared helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a strict ICurrentUserService mock with a fixed RetailerId.
    /// PerformanceBehavior reads RetailerId to enrich the warning log message.
    /// </summary>
    private static Mock<ICurrentUserService> BuildCurrentUserMock()
    {
        var mock = new Mock<ICurrentUserService>(MockBehavior.Strict);
        // RetailerId may be null when called from an anonymous context (e.g. a public endpoint).
        // Returning a real Guid exercises the happy-path log formatting.
        mock.SetupGet(c => c.RetailerId).Returns(Guid.NewGuid());
        return mock;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenHandlerExceedsThreshold_EmitsWarningLog()
    {
        // Arrange
        // FIX: PerformanceBehavior uses Stopwatch internally — NOT IDateTime.
        //      The constructor signature is (ILogger, ICurrentUserService), not (ILogger, IDateTime).
        //      To trigger the 500 ms warning threshold we introduce a real delay in the next delegate.
        var logMock = new Mock<ILogger<PerformanceBehavior<TestRequest, bool>>>();
        var currentUserMock = BuildCurrentUserMock();

        bool warningLogged = false;
        logMock.Setup(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => warningLogged = true);

        // FIX: Constructor now takes ICurrentUserService, not IDateTime.
        var behavior = new PerformanceBehavior<TestRequest, bool>(
            logMock.Object,
            currentUserMock.Object);

        // FIX: RequestHandlerDelegate<TResponse> = Func<CancellationToken, Task<TResponse>>.
        //      The lambda must accept a CancellationToken.
        //      A 510 ms delay guarantees the 500 ms threshold is exceeded on any machine.
        RequestHandlerDelegate<bool> next = async ct =>
        {
            await Task.Delay(510, ct);
            return true;
        };

        // Act
        await behavior.Handle(new TestRequest("slow"), next, CancellationToken.None);

        // Assert
        warningLogged.Should().BeTrue(
            "a warning must be logged when handler execution exceeds 500 ms");
    }

    [Fact]
    public async Task Handle_WhenHandlerIsFast_DoesNotEmitWarningLog()
    {
        // Arrange
        var logMock = new Mock<ILogger<PerformanceBehavior<TestRequest, bool>>>();
        var currentUserMock = BuildCurrentUserMock();

        bool warningLogged = false;
        logMock.Setup(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => warningLogged = true);

        var behavior = new PerformanceBehavior<TestRequest, bool>(
            logMock.Object,
            currentUserMock.Object);

        // FIX: 1-arg lambda — returns immediately, well under the 500 ms threshold.
        RequestHandlerDelegate<bool> next = _ => Task.FromResult(true);

        // Act
        await behavior.Handle(new TestRequest("fast"), next, CancellationToken.None);

        // Assert
        warningLogged.Should().BeFalse(
            "no warning must be logged when execution is within the 500 ms threshold");
    }
}