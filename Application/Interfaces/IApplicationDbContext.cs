namespace Application.Interfaces;

public interface IApplicationDbContext
{
    // DbSets will be added here when entities are created
    // Example: DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}