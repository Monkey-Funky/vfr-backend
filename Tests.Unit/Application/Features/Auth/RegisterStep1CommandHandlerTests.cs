// tests/Tests.Unit/Application/Features/Auth/RegisterStep1CommandHandlerTests.cs
using Application.Features.Auth.Commands.RegisterStep1;
using Application.Interfaces;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Auth;

public sealed class RegisterStep1CommandHandlerTests : TestBase
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<RegisterStep1CommandHandler>> _loggerMock;
    private readonly RegisterStep1CommandHandler _handler;

    public RegisterStep1CommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _retailerRepoMock = MockRepository.Create<IRepository<RetailerAccount>>();
        _tokenServiceMock = MockRepository.Create<ITokenService>();
        _loggerMock = new Mock<ILogger<RegisterStep1CommandHandler>>();

        _unitOfWorkMock
            .Setup(u => u.Repository<RetailerAccount>())
            .Returns(_retailerRepoMock.Object);

        _handler = new RegisterStep1CommandHandler(
            _unitOfWorkMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsConflictException()
    {
        // Arrange — email already exists
        _retailerRepoMock
            .Setup(r => r.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // email found

        var command = new RegisterStep1Command(
            "Test User", "existing@example.com", "P@ssw0rd1!", "MyBrand");

        // Act
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*email*");
    }

    [Fact]
    public async Task Handle_NewValidEmail_CreatesRetailerAccountAndReturnsStepToken()
    {
        // Arrange — neither email nor brand name exist yet
        _retailerRepoMock
            .Setup(r => r.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // both email and brand checks return false

        RetailerAccount? addedAccount = null;

        // FIX #1: IRepository<T>.AddAsync returns Task<T>, not Task.
        //         Use Returns<T, CancellationToken>(...) so the mock's return type matches.
        //         The Callback is merged into Returns to avoid the type-mismatch compile error.
        // FIX #2: Removed unused 'callCount' variable (compiler warning CS0219).
        _retailerRepoMock
            .Setup(r => r.AddAsync(It.IsAny<RetailerAccount>(), It.IsAny<CancellationToken>()))
            .Returns<RetailerAccount, CancellationToken>((account, _) =>
            {
                addedAccount = account;
                return Task.FromResult(account);
            })
            .Verifiable();

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1)
            .Verifiable();

        _tokenServiceMock
            .Setup(t => t.GenerateTempStepToken(It.IsAny<Guid>(), 1))
            .Returns("step-token-xyz")
            .Verifiable();

        var command = new RegisterStep1Command(
            "New User", "new@example.com", "P@ssw0rd1!", "NewBrand");

        // Act
        Shared.DTOs.Result<string> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("step-token-xyz");

        addedAccount.Should().NotBeNull();
        addedAccount!.Email.Should().Be("new@example.com");
        addedAccount.IsDeleted.Should().BeFalse();
        addedAccount.AccountStatus.Should().Be(RetailerAccount.Status.PendingEmailVerification,
            "the account must NOT be activated until Step 2 is completed");

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _tokenServiceMock.Verify(t => t.GenerateTempStepToken(It.IsAny<Guid>(), 1), Times.Once);
    }
}