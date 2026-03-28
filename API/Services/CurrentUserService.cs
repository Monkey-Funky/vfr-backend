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
    /// The JWT is issued with sub = retailer's Guid during login.
    /// This is the ONLY authoritative source of the tenant identity.
    /// </summary>
    public Guid? RetailerId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
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
}