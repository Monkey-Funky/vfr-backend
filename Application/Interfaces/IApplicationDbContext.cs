namespace Application.Interfaces;

/// <summary>
/// Abstraction over EF Core DbContext. Application layer only knows this interface —
/// it never references the concrete ApplicationDbContext or any EF Core types.
/// DbSet properties are added here as entities are created in subsequent prompts.
/// </summary>
public interface IApplicationDbContext
{
    // Example (added in P-011): DbSet<RetailerAccount> RetailerAccounts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}