// src/Infrastructure/Services/TokenService.cs
using System.Text;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Auth;

/// <summary>
/// Implements <see cref="ITokenService"/> using RS256 JWT for access tokens,
/// HS256 JWT for step tokens, and cryptographically random opaque strings
/// for refresh tokens.
///
/// SECURITY RULES:
///   • Access tokens : RS256, signed with the singleton RSA private key.
///   • Step tokens   : HS256, signed with JwtSettings:StepTokenSecret (separate secret).
///                     Carry a "token_type":"step" claim to prevent semantic misuse.
///   • Refresh tokens: 64 random bytes — NOT a JWT, not parseable by clients.
///   • Raw refresh tokens are NEVER logged. BCrypt hash is what gets persisted.
/// </summary>
public sealed class TokenService : ITokenService
{
    private readonly RSA _signingRsa;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<TokenService> _logger;

    // Lazy-initialised; created once per TokenService lifetime.
    private RsaSecurityKey? _rsaSecurityKey;
    private SigningCredentials? _signingCredentials;

    public TokenService(
    RSA signingRsa,
    IOptions<JwtSettings> jwtSettings,
    ILogger<TokenService> logger)
    {
        _signingRsa = signingRsa ?? throw new ArgumentNullException(nameof(signingRsa));
        _jwtSettings = jwtSettings?.Value ?? throw new ArgumentNullException(nameof(jwtSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_jwtSettings.StepTokenSecret))
            throw new InvalidOperationException(
                "JwtSettings:StepTokenSecret is not configured. " +
                "Set it via user-secrets (dev) or Azure Key Vault (prod).");

        // FIX F-11: Enforce NIST minimum key length for HMAC-SHA256.
        if (_jwtSettings.StepTokenSecret.Length < 32)
            throw new InvalidOperationException(
                "JwtSettings:StepTokenSecret must be at least 32 characters. " +
                "Generate one with: openssl rand -base64 48");
    }

    // ── Lazy RS256 key / credentials ──────────────────────────────────────────

    private RsaSecurityKey RsaKey
        => _rsaSecurityKey ??= new RsaSecurityKey(_signingRsa);

    private SigningCredentials Rs256Credentials
        => _signingCredentials ??= new SigningCredentials(RsaKey, SecurityAlgorithms.RsaSha256);

    // =========================================================================
    // ITokenService.GenerateRetailerAccessToken (Retailer)
    // =========================================================================

    /// <inheritdoc />
    public string GenerateAccessToken(RetailerAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return BuildToken(
            account.Id, 
            account.Email, 
            "Retailer", 
            new Claim("brand_name", account.BrandName));
    }

    // =========================================================================
    // ITokenService.GenerateCustomerAccessToken (Customer)
    // =========================================================================

    /// <inheritdoc />
    public string GenerateCustomerAccessToken(CustomerAccount customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return BuildToken(
            customer.Id, 
            customer.Email, 
            "Customer", 
            new Claim("full_name", customer.FullName));
    }

    // =========================================================================
    // ITokenService.GenerateRefreshToken
    // =========================================================================

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        // 64 bytes = 512 bits of entropy. URL-safe Base64 encoding.
        // The caller MUST BCrypt-hash this before writing it to the database.
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    // =========================================================================
    // ITokenService.GenerateTempStepToken
    // =========================================================================

    /// <inheritdoc />
    public string GenerateTempStepToken(Guid tempId, int step)
    {
        if (tempId == Guid.Empty) throw new ArgumentException("tempId must not be empty.", nameof(tempId));
        if (step < 1) throw new ArgumentOutOfRangeException(nameof(step));

        var key = BuildStepTokenKey();
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            // FIX F-02: Explicit token_type claim provides a semantic guard so that
            // a step token cannot be mistaken for or substituted by any other token type,
            // even if key separation were somehow bypassed. Validated in RegisterStep2CommandHandler.
            new Claim("token_type",      "step"),
            new Claim("temp_account_id", tempId.ToString()),
            new Claim("step",            step.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),   // 15-minute window for Step 2
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // =========================================================================
    // ITokenService.ValidateTempStepToken
    // =========================================================================

    /// <inheritdoc />
    public ClaimsPrincipal? ValidateTempStepToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var handler = new JwtSecurityTokenHandler();
        var key = BuildStepTokenKey();

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,          // Step token must not be expired
            ValidateIssuerSigningKey = true,
            ValidIssuer = _jwtSettings.Issuer,
            ValidAudience = _jwtSettings.Audience,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.Zero, // No grace period — per §1.4 of SecurityArchitecture
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        };

        try
        {
            return handler.ValidateToken(token, parameters, out _);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ValidateTempStepToken — token validation failed.");
            return null;
        }
    }

    // =========================================================================
    // ITokenService.GetClaimsFromExpiredToken
    // =========================================================================

    /// <inheritdoc />
    public ClaimsPrincipal? GetClaimsFromExpiredToken(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return null;

        var handler = new JwtSecurityTokenHandler();

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,         // Deliberately skip — token IS expired
            ValidateIssuerSigningKey = true,
            ValidIssuer = _jwtSettings.Issuer,
            ValidAudience = _jwtSettings.Audience,
            IssuerSigningKey = RsaKey,        // Reuse cached key — no RSA leak
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        };

        try
        {
            return handler.ValidateToken(accessToken, parameters, out _);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "GetClaimsFromExpiredToken — token validation failed (bad signature or structure).");
            return null;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private SymmetricSecurityKey BuildStepTokenKey()
        => new(Encoding.UTF8.GetBytes(_jwtSettings.StepTokenSecret!));

    private string BuildToken(Guid id, string email, string role, Claim specificClaim)
    {
        var now = DateTime.UtcNow;
        var expiry = now.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim("role", role),
            specificClaim
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: now,
            expires: expiry,
            signingCredentials: Rs256Credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}