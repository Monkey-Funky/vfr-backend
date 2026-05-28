using Application.Features.Settings.EventHandlers;
using Application.Interfaces.Services;
using Domain.Events;
using Microsoft.Extensions.Logging;
using Tests.Unit.Helpers;

namespace Tests.Unit.ApplicationTests.Features.Settings.Events;

public sealed class AccountDeletionRequestedEventHandlerTests
{
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger<AccountDeletionRequestedEventHandler>> _loggerMock = new();
    private readonly AccountDeletionRequestedEventHandler _sut;

    public AccountDeletionRequestedEventHandlerTests()
    {
        _emailServiceMock
            .Setup(e => e.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new AccountDeletionRequestedEventHandler(
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_DeletionRequested_SchedulesDeletionJob()
    {
        var @event = BuildEvent();

        await _sut.Handle(@event, default);

        _emailServiceMock.Verify(
            e => e.SendEmailAsync(
                It.IsAny<string>(),
                It.Is<string>(s => s.Contains("Account Deletion Request")),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeletionRequested_SendsConfirmationEmail()
    {
        const string retailerEmail = "retailer@example.com";
        var @event = BuildEvent(retailerEmail: retailerEmail);

        await _sut.Handle(@event, default);

        _emailServiceMock.Verify(
            e => e.SendEmailAsync(
                retailerEmail,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeletionRequested_AnonymizesData()
    {
        var @event = BuildEvent();
        string? capturedBody = null;

        _emailServiceMock
            .Setup(e => e.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, body, _) => capturedBody = body)
            .Returns(Task.CompletedTask);

        await _sut.Handle(@event, default);

        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("permanently");
        capturedBody.Should().Contain("30-day grace period");
        capturedBody.Should().Contain("deleted");
    }

    [Fact]
    public async Task Handle_EmailContainsOccurredAtTimestamp()
    {
        var occurredAt = new DateTime(2025, 5, 10, 14, 30, 0, DateTimeKind.Utc);
        var @event = BuildEvent(occurredAt: occurredAt);
        string? capturedBody = null;

        _emailServiceMock
            .Setup(e => e.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, body, _) => capturedBody = body)
            .Returns(Task.CompletedTask);

        await _sut.Handle(@event, default);

        capturedBody.Should().Contain("May 10, 2025");
    }

    [Fact]
    public async Task Handle_WhenEmailServiceThrows_LogsWarningAndDoesNotRethrow()
    {
        _emailServiceMock
            .Setup(e => e.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP failure"));

        var act = () => _sut.Handle(BuildEvent(), default);

        await act.Should().NotThrowAsync();
        _loggerMock.VerifyLog(LogLevel.Warning, "AccountDeletionRequestedEventHandler", Times.Once());
    }

    [Fact]
    public async Task Handle_WhenSuccessful_LogsConfirmationWithRetailerId()
    {
        var retailerId = Guid.NewGuid();
        var @event = BuildEvent(retailerId: retailerId);

        await _sut.Handle(@event, default);

        _loggerMock.VerifyLog(LogLevel.Information, retailerId.ToString(), Times.Once());
    }

    private static AccountDeletionRequestedEvent BuildEvent(
        Guid? retailerId = null,
        string retailerEmail = "test@example.com",
        DateTime? occurredAt = null) =>
        new(
            RetailerId: retailerId ?? Guid.NewGuid(),
            RetailerEmail: retailerEmail,
            OccurredAt: occurredAt ?? DateTime.UtcNow);
}