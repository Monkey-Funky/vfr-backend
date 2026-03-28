namespace Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public string? CreatedBy { get; protected set; }
    public string? UpdatedBy { get; protected set; }
    public bool IsDeleted { get; protected set; }

    protected BaseEntity()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        IsDeleted = false;
    }

    /// <summary>
    /// Sets creation audit fields. Called by ApplicationDbContext on EntityState.Added.
    /// </summary>
    public void SetCreatedAudit(string? createdBy, DateTime createdAt)
    {
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Sets update audit fields. Called by ApplicationDbContext on EntityState.Modified.
    /// </summary>
    public void SetUpdatedAudit(string? updatedBy, DateTime updatedAt)
    {
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Marks the entity as soft-deleted. Called by Repository.SoftDeleteAsync.
    /// </summary>
    public void MarkAsDeleted()
    {
        IsDeleted = true;
    }
}