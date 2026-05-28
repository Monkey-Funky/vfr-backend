using Application.Behaviors;
using Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Tests.Unit.Helpers;

namespace Tests.Unit.ApplicationTests.Behaviors;

public sealed class PerformanceBehaviorTests
{
    public sealed record TestQuery : IRequest<string>;

    private readonly Mock<ILogger<PerformanceBehavior<TestQuery, string>>> _loggerMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly PerformanceBehavior<TestQuery, string> _sut;

    public PerformanceBehaviorTests()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(Guid.NewGuid());

        _sut = new PerformanceBehavior<TestQuery, string>(
            _loggerMock.Object,
            _currentUserServiceMock.Object);
    }

    [Fact]
    public async Task Handle_FastRequest_DoesNotLogWarning()
    {
        var result = await _sut.Handle(
            new TestQuery(),
            _ => Task.FromResult("ok"),
            default);

        result.Should().Be("ok");
        _loggerMock.VerifyLog(LogLevel.Warning, "Slow request detected", Times.Never());
    }

    [Fact]
    public async Task Handle_SlowRequest_LogsWarning()
    {
        await _sut.Handle(new TestQuery(), async _ =>
        {
            await Task.Delay(510);
            return "ok";
        }, default);

        _loggerMock.VerifyLog(LogLevel.Warning, "Slow request detected", Times.Once());
    }

    [Fact]
    public async Task Handle_WarningContainsRequestName()
    {
        await _sut.Handle(new TestQuery(), async _ =>
        {
            await Task.Delay(510);
            return "ok";
        }, default);

        _loggerMock.VerifyLog(LogLevel.Warning, nameof(TestQuery), Times.Once());
    }

    [Fact]
    public async Task Handle_WarningContainsElapsedTime()
    {
        await _sut.Handle(new TestQuery(), async _ =>
        {
            await Task.Delay(510);
            return "ok";
        }, default);

        _loggerMock.VerifyLog(LogLevel.Warning, "ms", Times.Once());
    }

    [Fact]
    public async Task Handle_ThresholdIsConfigurable()
    {
        await _sut.Handle(new TestQuery(), async _ =>
        {
            await Task.Delay(200);
            return "fast";
        }, default);

        _loggerMock.VerifyLog(LogLevel.Warning, "Slow request detected", Times.Never());

        await _sut.Handle(new TestQuery(), async _ =>
        {
            await Task.Delay(510);
            return "slow";
        }, default);

        _loggerMock.VerifyLog(LogLevel.Warning, "Slow request detected", Times.Once());
    }
}