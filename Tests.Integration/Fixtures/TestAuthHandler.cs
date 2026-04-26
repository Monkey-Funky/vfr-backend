using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tests.Integration.Fixtures;

/// <summary>
/// Custom authentication handler that bypasses real JWT validation for integration tests.
///
/// Tests control the identity via request headers:
///   X-Test-UserId  → ClaimTypes.NameIdentifier (sub)
///   X-Test-Email   → ClaimTypes.Email
///   X-Test-Role    → ClaimTypes.Role
///   X-Test-Name    → ClaimTypes.Name
///
/// If no headers are provided, a default Retailer identity is used.
/// To simulate an unauthenticated request, set X-Test-Anonymous = "true".
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";
    public const string DefaultRetailerId = "00000000-0000-0000-0000-000000000001";
    public const string DefaultCustomerId = "00000000-0000-0000-0000-000000000002";
    public const string DefaultEmail = "test@retailer.com";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Allow tests to simulate unauthenticated requests
        if (Request.Headers.TryGetValue("X-Test-Anonymous", out var anon)
            && string.Equals(anon, "true", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers.TryGetValue("X-Test-UserId", out var uid)
            ? uid.ToString()
            : DefaultRetailerId;

        var email = Request.Headers.TryGetValue("X-Test-Email", out var em)
            ? em.ToString()
            : DefaultEmail;

        var role = Request.Headers.TryGetValue("X-Test-Role", out var r)
            ? r.ToString()
            : "Retailer";

        var name = Request.Headers.TryGetValue("X-Test-Name", out var n)
            ? n.ToString()
            : "TestUser";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, name),
        };

        // Add role-specific claims
        if (role == "Retailer")
            claims.Add(new Claim("brand_name", "TestBrand"));
        else if (role == "Customer")
            claims.Add(new Claim("full_name", name));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
