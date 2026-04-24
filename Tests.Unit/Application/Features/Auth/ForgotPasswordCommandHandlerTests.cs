// tests/Tests.Unit/Application/Features/Auth/ForgotPasswordCommandHandlerTests.cs
using Application.Features.Auth.Commands.ForgotPassword;
using Application.Interfaces;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.DTOs;
using Tests.Unit.Common;
using Xunit;

namespace Tests.Unit.Application.Features.Auth;

public sealed class ForgotPasswordCommandHandlerTests : TestBase
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<RetailerAccount>> _retailerRepoMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly Mock<IEmailService> _emailMock;
    private readonly Mock<ILogger<ForgotPasswordCommandHandler>> _loggerMock;
    private readonly ForgotPasswordCommandHandler _handler;

    public ForgotPasswordCommandHandlerTests()
    {
        _unitOfWorkMock = MockRepository.Create<IUnitOfWork>();
        _retailerRepoMock = MockRepository.Create<IRepository<RetailerAccount>>();
        _cacheMock = MockRepository.Create<ICacheService>();
        _emailMock = MockRepository.Create<IEmailService>();
        _loggerMock = new Mock<ILogger<ForgotPasswordCommandHandler>>();

        _unitOfWorkMock.Setup(u => u.Repository<RetailerAccount>()).Returns(_retailerRepoMock.Object);

        _handler = new ForgotPasswordCommandHandler(
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            _emailMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_UnknownEmail_DoesNotThrowAndReturnsSilentSuccess()
    {
        // Arrange — account not found (security: never reveal email existence)
        _retailerRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RetailerAccount?)null);

        var command = new ForgotPasswordCommand("unknown@example.com");

        // Act & Assert — must NOT throw
        Result<bool> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            "the handler must always return success to prevent email enumeration");

        // IEmailService must NOT be called when email is unknown
        _emailMock.Verify(
            e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(),
                                  It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_KnownActiveEmail_CachesHashedOtpAndSendsEmail()
    {
        // Arrange — Active account found
        var account = RetailerAccount.Create(
            "Jane Retailer", "jane@example.com",
            BCrypt.Net.BCrypt.HashPassword("P@ssw0rd1!", workFactor: 4),
            "JaneBrand");
        typeof(RetailerAccount)
            .GetProperty("AccountStatus")!
            .SetValue(account, RetailerAccount.Status.Active);

        _retailerRepoMock
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RetailerAccount, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        string? cachedHashedOtp = null;
        _cacheMock
            .Setup(c => c.SetAsync(
                It.Is<string>(k => k.StartsWith("pwd_reset:")),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object, TimeSpan, CancellationToken>(
                (_, val, _, _) => cachedHashedOtp = val?.ToString())
            .Returns(Task.CompletedTask)
            .Verifiable();

        _emailMock
            .Setup(e => e.SendEmailAsync(
                "jane@example.com",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var command = new ForgotPasswordCommand("jane@example.com");

        // Act
        Result<bool> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        cachedHashedOtp.Should().NotBeNullOrWhiteSpace(
            "a BCrypt-hashed OTP must be stored in Redis with a 15-minute TTL");

        _cacheMock.Verify(
            c => c.SetAsync(
                It.Is<string>(k => k.StartsWith("pwd_reset:")),
                It.IsAny<string>(),
                TimeSpan.FromMinutes(15),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _emailMock.Verify(
            e => e.SendEmailAsync(
                "jane@example.com",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}