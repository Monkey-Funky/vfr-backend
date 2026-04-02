// tests/Tests.Unit/Auth/RegisterStep1CommandHandlerTests.cs
using Application.Features.Auth.Commands.RegisterStep1;
using Application.Interfaces;
using Domain.Entities.Retailer;
using Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.DTOs;
using System.Linq.Expressions;
using Tests.Unit.Auth.Helpers;
using Xunit;

namespace Tests.Unit.Auth;

public sealed class RegisterStep1CommandHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly ILogger<RegisterStep1CommandHandler> _logger =
        Substitute.For<ILogger<RegisterStep1CommandHandler>>();
    private readonly IRepository<RetailerAccount> _accountRepo =
        Substitute.For<IRepository<RetailerAccount>>();

    private readonly RegisterStep1CommandHandler _sut;

    public RegisterStep1CommandHandlerTests()
    {
        _unitOfWork.SetupRepositories(_accountRepo);
        _sut = new RegisterStep1CommandHandler(_unitOfWork, _tokenService, _logger);
    }

    private static RegisterStep1Command ValidCommand() => new(
        FullName: "Jane Smith",
        Email: "jane@example.com",
        Password: "Password1!",
        BrandName: "JanesBoutique");

    // =========================================================================
    // HAPPY PATH
    // =========================================================================

    [Fact]
    public async Task Handle_ValidInput_ReturnsTempStepToken()
    {
        // Arrange — no conflicts on either AnyAsync call
        _accountRepo.AnyReturns<RetailerAccount>(false);

        // FIX: No AddAsync.Returns() setup needed — handler discards the return value.
        // NSubstitute's Task.FromResult(default(T)) default is correct.

        const string ExpectedToken = "eyJstep.header.sig";
        _tokenService.GenerateTempStepToken(Arg.Any<Guid>(), step: 1)
            .Returns(ExpectedToken);

        // Act
        Result<string> result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(ExpectedToken);
        result.Message.Should().Contain("Step 1");
    }

    [Fact]
    public async Task Handle_ValidInput_PersistsAccountWithPendingStatus()
    {
        // Arrange
        _accountRepo.AnyReturns<RetailerAccount>(false);
        _tokenService.GenerateTempStepToken(Arg.Any<Guid>(), Arg.Any<int>())
            .Returns("token");

        // Act
        await _sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert — AddAsync must be called with a PendingEmailVerification account
        await _accountRepo.Received(1).AddAsync(
            Arg.Is<RetailerAccount>(a =>
                a.AccountStatus == RetailerAccount.Status.PendingEmailVerification
                && !a.IsEmailVerified
                && a.Email == "jane@example.com"),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidInput_StoresPasswordAsBcryptHash_NeverRaw()
    {
        // Arrange
        _accountRepo.AnyReturns<RetailerAccount>(false);

        RetailerAccount? capturedAccount = null;

        // Use Arg.Do to capture the argument passed to AddAsync WITHOUT calling .Returns().
        // Arg.Do intercepts the argument and executes the delegate; no Returns needed.
        await _accountRepo.AddAsync(
            Arg.Do<RetailerAccount>(a => capturedAccount = a),
            Arg.Any<CancellationToken>());

        _tokenService.GenerateTempStepToken(Arg.Any<Guid>(), Arg.Any<int>())
            .Returns("token");

        const string RawPassword = "Password1!";

        // Act
        await _sut.Handle(ValidCommand() with { Password = RawPassword }, CancellationToken.None);

        // FIX: Assert not-null BEFORE accessing properties — eliminates CS8602.
        capturedAccount.Should().NotBeNull(
            because: "AddAsync must have been called with the new account");

        // After Should().NotBeNull() the compiler still requires !, which is safe here.
        capturedAccount!.PasswordHash.Should().NotBe(RawPassword,
            because: "password must be hashed before storage");

        capturedAccount.PasswordHash.Should().StartWith("$2",
            because: "BCrypt hashes always begin with $2a$, $2b$, or $2y$");

        BCrypt.Net.BCrypt.Verify(RawPassword, capturedAccount.PasswordHash)
            .Should().BeTrue("the stored hash must verify against the original raw password");
    }

    // =========================================================================
    // FAILURE — Duplicate Email
    // =========================================================================

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsConflictException()
    {
        // Arrange — any AnyAsync call returns true (simulates email already taken)
        _accountRepo.AnyReturns<RetailerAccount>(true);

        // Act
        Func<Task> act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*email*");
    }

    [Fact]
    public async Task Handle_DuplicateEmail_DoesNotPersistAccount()
    {
        // Arrange
        _accountRepo.AnyReturns<RetailerAccount>(true);

        // Act
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(ValidCommand(), CancellationToken.None));

        // Assert — no insert, no save
        await _accountRepo.DidNotReceive().AddAsync(
            Arg.Any<RetailerAccount>(), Arg.Any<CancellationToken>());

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // FAILURE — Duplicate Brand Name
    // =========================================================================

    [Fact]
    public async Task Handle_DuplicateBrandName_ThrowsConflictException()
    {
        // Arrange — email check returns false (email OK), brand check returns true (brand taken)
        // NSubstitute sequences return values in order for successive calls to the same method.
        _accountRepo.AnyAsync(
                Arg.Any<Expression<Func<RetailerAccount, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(false, true);  // 1st call: email → OK; 2nd call: brand → taken

        // Act
        Func<Task> act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*brand*");
    }

    [Fact]
    public async Task Handle_DuplicateBrandName_DoesNotPersistAccount()
    {
        // Arrange
        _accountRepo.AnyAsync(
                Arg.Any<Expression<Func<RetailerAccount, bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(false, true);

        // Act
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(ValidCommand(), CancellationToken.None));

        // Assert
        await _accountRepo.DidNotReceive().AddAsync(
            Arg.Any<RetailerAccount>(), Arg.Any<CancellationToken>());

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}