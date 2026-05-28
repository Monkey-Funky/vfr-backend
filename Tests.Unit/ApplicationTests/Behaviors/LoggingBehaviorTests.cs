using Application.Behaviors;
using Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Tests.Unit.Helpers;

namespace Tests.Unit.ApplicationTests.Behaviors;

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
        _userServiceMock.SetupGet(x => x.RetailerId).Returns(Guid.NewGuid());

        await _sut.Handle(new TestRequest(), _ => Task.FromResult("Success"), default);

        _loggerMock.VerifyLog(LogLevel.Information, "Handling TestRequest", Times.Once());
        _loggerMock.VerifyLog(LogLevel.Information, "Handled TestRequest", Times.Once());
    }

    [Fact]
    public async Task Handle_Exception_LogsErrorAndRethrows()
    {
        var ex = new Exception("Boom");

        var act = () => _sut.Handle(new TestRequest(), _ => throw ex, default);

        await act.Should().ThrowAsync<Exception>();
        _loggerMock.VerifyLog(LogLevel.Error, "Error handling TestRequest", Times.Once());
    }
}