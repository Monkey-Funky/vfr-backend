using Domain.Common;
using FluentAssertions;

namespace Tests.Unit.Domain;

public sealed class BaseEntityTests
{
    private sealed class ConcreteEntity : BaseEntity
    {
        public ConcreteEntity() { }
    }

    [Fact]
    public void Constructor_WhenEntityCreated_IdIsNonEmptyGuid()
    {
        // Arrange & Act
        var entity = new ConcreteEntity();

        // Assert
        entity.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_WhenEntityCreated_CreatedAtIsWithinOneSecondOfUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var entity = new ConcreteEntity();

        // Assert
        var after = DateTime.UtcNow;
        entity.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Constructor_WhenEntityCreated_IsDeletedIsFalse()
    {
        // Arrange & Act
        var entity = new ConcreteEntity();

        // Assert
        entity.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WhenTwoEntitiesCreated_EachHasUniqueId()
    {
        // Arrange & Act
        var first = new ConcreteEntity();
        var second = new ConcreteEntity();

        // Assert
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void MarkAsDeleted_WhenCalled_SetsIsDeletedToTrue()
    {
        // Arrange
        var entity = new ConcreteEntity();

        // Act
        entity.MarkAsDeleted();

        // Assert
        entity.IsDeleted.Should().BeTrue();
    }
}