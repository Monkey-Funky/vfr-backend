using System.Security.Claims;
using System.Security.Cryptography;
using Infrastructure.Services.Auth;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Domain.Entities.Retailer;
using Domain.Entities.Customer;
using System.IdentityModel.Tokens.Jwt;

namespace Tests.Unit.Infrastructure.Services;

public sealed class TokenServiceTests
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly Mock<ILogger<TokenService>> _loggerMock = new();
    private readonly IOptions<JwtSettings> _jwtOptions;
    private readonly TokenService _sut;

    public TokenServiceTests()
    {
        var settings = new JwtSettings
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7,
            StepTokenSecret = "this-is-a-very-long-secret-for-testing-purposes-32-chars"
        };
        _jwtOptions = Options.Create(settings);
        _sut = new TokenService(_rsa, _jwtOptions, _loggerMock.Object);
    }

    [Fact]
    public void GenerateAccessToken_Retailer_ReturnsValidJwtWithClaims()
    {
        // Arrange
        var retailer = RetailerAccount.Create(
            "Retailer Name",
            "test@brand.com",
            "hashed_password",
            "Brand Name");

        // Act
        var token = _sut.GenerateAccessToken(retailer);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be("TestIssuer");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == retailer.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == global::Domain.Constants.Roles.Retailer);
        jwt.Claims.Should().Contain(c => c.Type == "brand_name" && c.Value == "Brand Name");
    }

    [Fact]
    public void GenerateCustomerAccessToken_Customer_ReturnsValidJwtWithClaims()
    {
        // Arrange
        var customer = CustomerAccount.Create("John Doe", "customer@email.com", "hashed_password");

        // Act
        var token = _sut.GenerateCustomerAccessToken(customer);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == global::Domain.Constants.Roles.Customer);
        jwt.Claims.Should().Contain(c => c.Type == "full_name" && c.Value == "John Doe");
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsOpaqueString()
    {
        // Act
        var token1 = _sut.GenerateRefreshToken();
        var token2 = _sut.GenerateRefreshToken();

        // Assert
        token1.Should().NotBeNullOrWhiteSpace();
        token1.Should().NotBe(token2);
        token1.Length.Should().BeGreaterThan(64); // Base64 encoded 64 bytes
    }

    [Fact]
    public void GenerateAndValidateStepToken_ValidToken_ReturnsPrincipal()
    {
        // Arrange
        var tempId = Guid.NewGuid();
        var step = 1;

        // Act
        var token = _sut.GenerateTempStepToken(tempId, step);
        var principal = _sut.ValidateTempStepToken(token);

        // Assert
        principal.Should().NotBeNull();
        principal!.FindFirst("temp_account_id")!.Value.Should().Be(tempId.ToString());
        principal.FindFirst("step")!.Value.Should().Be(step.ToString());
        principal.FindFirst("token_type")!.Value.Should().Be("step");
    }

    [Fact]
    public void ValidateTempStepToken_InvalidSignature_ReturnsNull()
    {
        // Arrange
        var token = _sut.GenerateTempStepToken(Guid.NewGuid(), 1);
        var tamperedToken = token + "tampered";

        // Act
        var result = _sut.ValidateTempStepToken(tamperedToken);

        // Assert
        result.Should().BeNull();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("token validation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
