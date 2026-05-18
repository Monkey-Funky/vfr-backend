using Application.Features.Dashboard.Commands.GenerateReport;
using Application.Features.Dashboard.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Analytics;
using Domain.Enums.Analytics;
using Moq.EntityFrameworkCore;

namespace Tests.Unit.Application.Features.Dashboard;

public sealed class GenerateReportCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IReportQueue> _reportQueueMock = new();
    private readonly GenerateReportCommandHandler _sut;

    private static readonly Guid RetailerId = Guid.NewGuid();
    private static readonly DateOnly From = new(2025, 1, 1);
    private static readonly DateOnly To = new(2025, 3, 31);

    public GenerateReportCommandHandlerTests()
    {
        _sut = new GenerateReportCommandHandler(
            _contextMock.Object,
            _currentUserServiceMock.Object,
            _reportQueueMock.Object);

        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns(RetailerId);

        _contextMock.Setup(x => x.Reports).ReturnsDbSet(new List<Report>());
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _reportQueueMock.Setup(x => x.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private GenerateReportCommand ValidCommand() => new(From, To);

    [Fact]
    public async Task Handle_RetailerIdIsNull_ThrowsUnauthorizedException()
    {
        _currentUserServiceMock.SetupGet(x => x.RetailerId).Returns((Guid?)null);

        var act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Retailer identity*");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessResult()
    {
        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsNonEmptyReportId()
    {
        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.Data!.ReportId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ValidRequest_AddsReportToContext()
    {
        Report? capturedReport = null;
        _contextMock.Setup(x => x.Reports.Add(It.IsAny<Report>()))
            .Callback<Report>(r => capturedReport = r);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _contextMock.Verify(x => x.Reports.Add(It.IsAny<Report>()), Times.Once);
        capturedReport.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ValidRequest_ReportHasCorrectRetailerId()
    {
        Report? capturedReport = null;
        _contextMock.Setup(x => x.Reports.Add(It.IsAny<Report>()))
            .Callback<Report>(r => capturedReport = r);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        capturedReport!.RetailerId.Should().Be(RetailerId);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReportHasCorrectDateRange()
    {
        Report? capturedReport = null;
        _contextMock.Setup(x => x.Reports.Add(It.IsAny<Report>()))
            .Callback<Report>(r => capturedReport = r);

        await _sut.Handle(new GenerateReportCommand(From, To), CancellationToken.None);

        capturedReport!.RangeFrom.Should().Be(From);
        capturedReport.RangeTo.Should().Be(To);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReportInitialStatusIsPending()
    {
        Report? capturedReport = null;
        _contextMock.Setup(x => x.Reports.Add(It.IsAny<Report>()))
            .Callback<Report>(r => capturedReport = r);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        capturedReport!.Status.Should().Be(ReportStatus.Pending);
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsSaveChangesBeforeEnqueue()
    {
        var callOrder = new List<string>();

        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"))
            .ReturnsAsync(1);

        _reportQueueMock.Setup(x => x.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("enqueue"))
            .Returns(Task.CompletedTask);

        await _sut.Handle(ValidCommand(), CancellationToken.None);

        callOrder.Should().ContainInOrder("save", "enqueue");
    }

    [Fact]
    public async Task Handle_ValidRequest_EnqueuesThePersistedReportId()
    {
        Guid? enqueuedId = null;
        Report? capturedReport = null;

        _contextMock.Setup(x => x.Reports.Add(It.IsAny<Report>()))
            .Callback<Report>(r => capturedReport = r);

        _reportQueueMock.Setup(x => x.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((id, _) => enqueuedId = id)
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        enqueuedId.Should().NotBeNull();
        enqueuedId.Should().Be(capturedReport!.Id);
        result.Data!.ReportId.Should().Be(capturedReport.Id);
    }

    [Fact]
    public async Task Handle_SaveChangesThrows_DoesNotCallEnqueue()
    {
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _reportQueueMock.Verify(x => x.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidRequest_SaveChangesCalledExactlyOnce()
    {
        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_EnqueueCalledExactlyOnce()
    {
        await _sut.Handle(ValidCommand(), CancellationToken.None);

        _reportQueueMock.Verify(x => x.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DifferentDateRanges_EachCreatesDistinctReport()
    {
        var captured = new List<Report>();
        _contextMock.Setup(x => x.Reports.Add(It.IsAny<Report>()))
            .Callback<Report>(r => captured.Add(r));

        await _sut.Handle(new GenerateReportCommand(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31)), CancellationToken.None);
        await _sut.Handle(new GenerateReportCommand(new DateOnly(2025, 2, 1), new DateOnly(2025, 2, 28)), CancellationToken.None);

        captured.Should().HaveCount(2);
        captured[0].Id.Should().NotBe(captured[1].Id);
        captured[0].RangeFrom.Should().NotBe(captured[1].RangeFrom);
    }
}