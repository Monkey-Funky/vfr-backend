using Application.Interfaces.Services;

namespace API.Services;
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User =>
        _httpContextAccessor.HttpContext?.User;

    /// <summary>
    /// RetailerId extracted from the JWT 'sub' claim.
    /// Only returns a value if the user is in the "Retailer" role.
    /// </summary>
    public Guid? RetailerId =>
        IsInRole("Retailer") && Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    /// <summary>
    /// CustomerId extracted from the JWT 'sub' claim.
    /// Only returns a value if the user is in the "Customer" role.
    /// </summary>
    public Guid? CustomerId =>
        IsInRole("Customer") && Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    public string? UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName =>
        User?.FindFirstValue(ClaimTypes.Name);

    public string? Email =>
        User?.FindFirstValue(ClaimTypes.Email);

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;

    public IEnumerable<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value)
        ?? Enumerable.Empty<string>();

    public bool IsInRole(string role) =>
        User?.IsInRole(role) ?? false;

    public string? GetRawBearerToken()
    {
        var authHeader = _httpContextAccessor.HttpContext?
            .Request.Headers.Authorization
            .FirstOrDefault();

        if (authHeader is null)
            return null;

        // Authorization header format: "Bearer {token}"
        const string bearerPrefix = "Bearer ";
        if (!authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authHeader[bearerPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}