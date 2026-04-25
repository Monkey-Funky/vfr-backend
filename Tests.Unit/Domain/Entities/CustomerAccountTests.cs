using Domain.Enums.Customer;

namespace Tests.Unit.Domain.Entities;

/// <summary>
/// Unit tests for <see cref="CustomerAccount"/> aggregate root.
/// Validates registration flows, profile updates, and lockout logic.
/// </summary>
public sealed class CustomerAccountTests
{
    // ── Factory Methods ──────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidInput_InitializesPendingAccount()
    {
        // Act
        var customer = CustomerAccount.Create("John Doe", "john@example.com", "hashed_pwd");

        // Assert
        customer.FullName.Should().Be("John Doe");
        customer.Email.Should().Be("john@example.com");
        customer.PasswordHash.Should().Be("hashed_pwd");
        customer.Status.Should().Be(CustomerStatus.PendingEmailVerification);
        customer.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public void CreateWithGoogle_ValidInput_InitializesActiveAccount()
    {
        // Act
        var customer = CustomerAccount.CreateWithGoogle("Jane Smith", "jane@gmail.com", "google_123");

        // Assert
        customer.FullName.Should().Be("Jane Smith");
        customer.Email.Should().Be("jane@gmail.com");
        customer.GoogleId.Should().Be("google_123");
        customer.Status.Should().Be(CustomerStatus.Active);
        customer.IsEmailVerified.Should().BeTrue();
    }

    // ── Profile Updates ──────────────────────────────────────────────────────

    [Fact]
    public void UpdateProfile_ValidData_UpdatesProperties()
    {
        // Arrange
        var customer = CustomerAccount.CreateWithGoogle("Jane Smith", "jane@gmail.com", "google_123");
        var dob = new DateOnly(1990, 1, 1);

        // Act
        customer.UpdateProfile("Jane Doe", "12345678", dob, "Female");

        // Assert
        customer.FullName.Should().Be("Jane Doe");
        customer.PhoneNumber.Should().Be("12345678");
        customer.DateOfBirth.Should().Be(dob);
        customer.Gender.Should().Be("Female");
    }

    [Fact]
    public void UpdateProfile_InvalidGender_ThrowsBusinessRuleException()
    {
        // Arrange
        var customer = CustomerAccount.CreateWithGoogle("Jane Smith", "jane@gmail.com", "google_123");

        // Act
        var act = () => customer.UpdateProfile("Jane Doe", null, null, "Attack Helicopter");

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("INVALID_GENDER");
    }

    // ── Lockout Logic ────────────────────────────────────────────────────────

    [Fact]
    public void IncrementFailedLogin_LocksOutAfter10Attempts()
    {
        // Arrange
        var customer = CustomerAccount.Create("John", "john@ex.com", "pwd");

        // Act & Assert
        for (int i = 1; i <= 9; i++)
        {
            customer.IncrementFailedLogin();
            customer.IsLockedOut().Should().BeFalse();
            customer.FailedLoginAttempts.Should().Be(i);
        }

        customer.IncrementFailedLogin();
        customer.IsLockedOut().Should().BeTrue();
        customer.FailedLoginAttempts.Should().Be(10);
        customer.LockoutUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void ResetFailedLogin_ClearsLockoutAndCount()
    {
        // Arrange
        var customer = CustomerAccount.Create("John", "john@ex.com", "pwd");
        for (int i = 0; i < 10; i++) customer.IncrementFailedLogin();
        customer.IsLockedOut().Should().BeTrue();

        // Act
        customer.ResetFailedLogin();

        // Assert
        customer.IsLockedOut().Should().BeFalse();
        customer.FailedLoginAttempts.Should().Be(0);
        customer.LockoutUntil.Should().BeNull();
    }
}
