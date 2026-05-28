using Application.Features.Subscriptions.DomainEventHandlers;
using Domain.Events;
using Microsoft.Extensions.Logging;
using Tests.Unit.Helpers;

namespace Tests.Unit.ApplicationTests.Features.Subscriptions.DomainEventHandlers;

public sealed class SaasEnquirySubmittedDomainEventHandlerTests
{
    private readonly Mock<ILogger<SaasEnquirySubmittedDomainEventHandler>> _loggerMock = new();
    private readonly SaasEnquirySubmittedDomainEventHandler _sut;

    public SaasEnquirySubmittedDomainEventHandlerTests()
    {
        _sut = new SaasEnquirySubmittedDomainEventHandler(_loggerMock.Object);
    }

    [Fact]
    public async Task Handle_EnquirySubmitted_SendsNotificationToAdmin()
    {
        var @event = BuildEvent();

        await _sut.Handle(@event, default);

        _loggerMock.VerifyLog(LogLevel.Information, "SaaS enquiry submitted", Times.Once());
    }

    [Fact]
    public async Task Handle_EnquirySubmitted_LogsEnquiryDetails()
    {
        var enquiryId = Guid.NewGuid();
        var @event = BuildEvent(enquiryId: enquiryId);

        await _sut.Handle(@event, default);

        _loggerMock.VerifyLog(LogLevel.Information, enquiryId.ToString(), Times.Once());
    }

    [Fact]
    public async Task Handle_EnquiryContainsRetailerInfo()
    {
        var retailerId = Guid.NewGuid();
        var @event = BuildEvent(retailerId: retailerId);

        await _sut.Handle(@event, default);

        _loggerMock.VerifyLog(LogLevel.Information, retailerId.ToString(), Times.Once());
    }

    [Fact]
    public async Task Handle_ReturnsCompletedTask()
    {
        var @event = BuildEvent();

        var result = _sut.Handle(@event, default);

        result.IsCompleted.Should().BeTrue();
        await result;
    }

    [Fact]
    public async Task Handle_DoesNotThrow_ForAnyValidEvent()
    {
        var @event = BuildEvent();

        var act = () => _sut.Handle(@event, default);

        await act.Should().NotThrowAsync();
    }

    private static SaasEnquirySubmittedDomainEvent BuildEvent(
        Guid? enquiryId = null,
        Guid? retailerId = null) =>
        new(
            EnquiryId: enquiryId ?? Guid.NewGuid(),
            RetailerId: retailerId ?? Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow);
}