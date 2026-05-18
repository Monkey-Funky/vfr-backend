using Infrastructure.Services.System;

namespace Tests.Unit.InfrastructureTests.Services;

public sealed class DateTimeServiceTests
{
    [Fact]
    public void UtcNow_ReturnsRecentTime()
    {
        // Arrange
        var sut = new DateTimeService();

        // Act
        var result = sut.UtcNow;

        // Assert
        result.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        result.Kind.Should().Be(DateTimeKind.Utc);
    }
}
