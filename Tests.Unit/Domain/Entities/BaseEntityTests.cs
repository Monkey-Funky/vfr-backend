namespace Tests.Unit.Domain.Entities;

/// <summary>
/// Unit tests for <see cref="BaseEntity"/>.
/// Uses a concrete stub to test protected members and common behavior.
/// </summary>
public sealed class BaseEntityTests
{
    private sealed class BaseEntityStub : BaseEntity { }

    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        // Act
        var entity = new BaseEntityStub();

        // Assert
        entity.Id.Should().NotBe(Guid.Empty);
        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        entity.IsDeleted.Should().BeFalse();
        entity.UpdatedAt.Should().BeNull();
        entity.CreatedBy.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void MarkAsDeleted_SetsIsDeletedToTrue()
    {
        // Arrange
        var entity = new BaseEntityStub();

        // Act
        entity.MarkAsDeleted();

        // Assert
        entity.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void SetCreatedAudit_SetsProperties()
    {
        // Arrange
        var entity = new BaseEntityStub();
        var now = DateTime.UtcNow;
        var user = "test-user";

        // Act
        entity.SetCreatedAudit(user, now);

        // Assert
        entity.CreatedBy.Should().Be(user);
        entity.CreatedAt.Should().Be(now);
    }

    [Fact]
    public void SetUpdatedAudit_SetsProperties()
    {
        // Arrange
        var entity = new BaseEntityStub();
        var now = DateTime.UtcNow;
        var user = "test-user";

        // Act
        entity.SetUpdatedAudit(user, now);

        // Assert
        entity.UpdatedBy.Should().Be(user);
        entity.UpdatedAt.Should().Be(now);
    }
}
