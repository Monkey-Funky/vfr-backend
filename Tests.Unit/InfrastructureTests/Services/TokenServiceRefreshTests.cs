using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Infrastructure.Services.Auth;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Tests.Unit.InfrastructureTests.Services;

public sealed class TokenServiceRefreshTests
{
    private const string ValidStepTokenSecret = "this-is-a-very-long-secret-for-testing-purposes-32-chars";
    private const string TestIssuer = "TestIssuer";
    private const string TestAudience = "TestAudience";

    private readonly RsaSecurityKey _rsaSecurityKey;
    private readonly Mock<ILogger<TokenService>> _loggerMock;
    private readonly IOptions<JwtSettings> _jwtOptions;
    private readonly TokenService _sut;

    public TokenServiceRefreshTests()
    {
        var rsa = RSA.Create(2048);
        _rsaSecurityKey = new RsaSecurityKey(rsa) { KeyId = "test-signing-key" };

        _loggerMock = new Mock<ILogger<TokenService>>();

        var settings = new JwtSettings
        {
            Issuer = TestIssuer,
            Audience = TestAudience,
            AccessTokenExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7,
            StepTokenSecret = ValidStepTokenSecret
        };

        _jwtOptions = Options.Create(settings);
        _sut = new TokenService(_rsaSecurityKey, _jwtOptions, _loggerMock.Object);
    }

    [Fact]
    public void GenerateRetailerAccessToken_ContainsCorrectClaims()
    {
        var retailer = RetailerAccount.Create("John Smith", "retailer@brand.com", "hashed_pw", "MyBrand");

        var token = _sut.GenerateAccessToken(retailer);

        token.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be(TestIssuer);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == retailer.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "retailer@brand.com");
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == Domain.Constants.Roles.Retailer);
        jwt.Claims.Should().Contain(c => c.Type == "brand_name" && c.Value == "MyBrand");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Iat);
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void GenerateCustomerAccessToken_ContainsCorrectClaims()
    {
        var customer = CustomerAccount.Create("Jane Doe", "customer@email.com", "hashed_pw");

        var token = _sut.GenerateCustomerAccessToken(customer);

        token.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be(TestIssuer);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == customer.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == customer.Email);
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == Domain.Constants.Roles.Customer);
        jwt.Claims.Should().Contain(c => c.Type == "full_name" && c.Value == "Jane Doe");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueTokenEachTime()
    {
        var tokens = Enumerable.Range(0, 20).Select(_ => _sut.GenerateRefreshToken()).ToList();

        tokens.Should().OnlyHaveUniqueItems();
        tokens.Should().AllSatisfy(t => t.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void GenerateRefreshToken_IsUrlSafeBase64()
    {
        var token = _sut.GenerateRefreshToken();

        Action act = () => Convert.FromBase64String(token);

        act.Should().NotThrow();

        var decodedBytes = Convert.FromBase64String(token);
        decodedBytes.Length.Should().Be(64);

        token.Should().MatchRegex(@"^[A-Za-z0-9+/=]+$");
    }

    [Fact]
    public void ValidateStepToken_ValidToken_ReturnsEmail()
    {
        var tempId = Guid.NewGuid();
        const int step = 1;

        var token = _sut.GenerateTempStepToken(tempId, step);
        var principal = _sut.ValidateTempStepToken(token);

        principal.Should().NotBeNull();
        principal!.FindFirst("temp_account_id")!.Value.Should().Be(tempId.ToString());
        principal.FindFirst("step")!.Value.Should().Be(step.ToString());
        principal.FindFirst("token_type")!.Value.Should().Be("step");
    }

    [Fact]
    public void ValidateStepToken_ExpiredToken_ThrowsSecurityException()
    {
        var expiredSettings = new JwtSettings
        {
            Issuer = TestIssuer,
            Audience = TestAudience,
            AccessTokenExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7,
            StepTokenSecret = ValidStepTokenSecret
        };

        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(ValidStepTokenSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiredJwt = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: new[]
            {
                new Claim("token_type", "step"),
                new Claim("temp_account_id", Guid.NewGuid().ToString()),
                new Claim("step", "1"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            },
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires: DateTime.UtcNow.AddMinutes(-1),
            signingCredentials: creds);

        var expiredToken = new JwtSecurityTokenHandler().WriteToken(expiredJwt);

        var result = _sut.ValidateTempStepToken(expiredToken);

        result.Should().BeNull();
    }

    [Fact]
    public void ValidateStepToken_TamperedToken_ThrowsSecurityException()
    {
        var token = _sut.GenerateTempStepToken(Guid.NewGuid(), 1);
        var parts = token.Split('.');
        var tamperedPayload = parts[1] + "TAMPERED";
        var tamperedToken = $"{parts[0]}.{tamperedPayload}.{parts[2]}";

        var result = _sut.ValidateTempStepToken(tamperedToken);

        result.Should().BeNull();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("token validation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}