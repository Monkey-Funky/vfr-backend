using Application.Behaviors;
using Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Tests.Unit.Application.Behaviors;

public sealed class LoggingBehaviorTests
{
    public sealed record TestRequest : IRequest<string>;
    private readonly Mock<ILogger<LoggingBehavior<TestRequest, string>>> _loggerMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly LoggingBehavior<TestRequest, string> _sut;

    public LoggingBehaviorTests()
    {
        _sut = new LoggingBehavior<TestRequest, string>(_loggerMock.Object, _userServiceMock.Object);
    }

    [Fact]
    public async Task Handle_Success_LogsStartAndEnd()
    {
        // Arrange
        _userServiceMock.SetupGet(x => x.RetailerId).Returns(Guid.NewGuid());
        
        // Act
        await _sut.Handle(new TestRequest(), (_) => Task.FromResult("Success"), default);

        // Assert
        _loggerMock.VerifyLog(LogLevel.Information, "Handling TestRequest", Times.Once());
        _loggerMock.VerifyLog(LogLevel.Information, "Handled TestRequest", Times.Once());
    }

    [Fact]
    public async Task Handle_Exception_LogsErrorAndRethrows()
    {
        // Arrange
        var ex = new Exception("Boom");

        // Act
        var act = () => _sut.Handle(new TestRequest(), (_) => throw ex, default);

        // Assert
        await act.Should().ThrowAsync<Exception>();
        _loggerMock.VerifyLog(LogLevel.Error, "Error handling TestRequest", Times.Once());
    }
}

public sealed class PerformanceBehaviorTests
{
    public sealed record TestRequest : IRequest<string>;
    private readonly Mock<ILogger<PerformanceBehavior<TestRequest, string>>> _loggerMock = new();
    private readonly Mock<ICurrentUserService> _userServiceMock = new();
    private readonly PerformanceBehavior<TestRequest, string> _sut;

    public PerformanceBehaviorTests()
    {
        _sut = new PerformanceBehavior<TestRequest, string>(_loggerMock.Object, _userServiceMock.Object);
    }

    [Fact]
    public async Task Handle_FastRequest_NoWarningLogged()
    {
        // Act
        await _sut.Handle(new TestRequest(), (_) => Task.FromResult("Success"), default);

        // Assert
        _loggerMock.VerifyLog(LogLevel.Warning, "Slow request detected", Times.Never());
    }

    [Fact]
    public async Task Handle_SlowRequest_WarningLogged()
    {
        // Act
        await _sut.Handle(new TestRequest(), async (_) =>
        {
            await Task.Delay(600);
            return "Success";
        }, default);

        // Assert
        _loggerMock.VerifyLog(LogLevel.Warning, "Slow request detected", Times.Once());
    }
}

public static class LoggerExtensions
{
    public static void VerifyLog<T>(this Mock<ILogger<T>> loggerMock, LogLevel level, string messageContains, Times times)
    {
        loggerMock.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messageContains)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
