using Domain.Enums.Customer;

namespace Tests.Unit.DomainTests.Entities;

public sealed class CustomerAccountTests
{
    [Fact]
    public void Create_ValidParameters_SetsPropertiesCorrectly()
    {
        var account = CustomerAccount.Create("John Doe", "john@example.com", "hashed_password_123");

        account.Id.Should().NotBeEmpty();
        account.FullName.Should().Be("John Doe");
        account.Email.Should().Be("john@example.com");
        account.PasswordHash.Should().Be("hashed_password_123");
        account.IsEmailVerified.Should().BeFalse();
        account.FailedLoginAttempts.Should().Be(0);
        account.GoogleId.Should().BeNull();
        account.LockoutUntil.Should().BeNull();
        account.AvatarUrl.Should().BeNull();
        account.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_EmailIsLowercasedAndTrimmed()
    {
        var account = CustomerAccount.Create("John Doe", "  JOHN@EXAMPLE.COM  ", "hashed_password");

        account.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void Create_StatusIsPendingEmailVerification()
    {
        var account = CustomerAccount.Create("John Doe", "john@example.com", "hashed_password");

        account.Status.Should().Be(CustomerStatus.PendingEmailVerification);
    }

    [Fact]
    public void CreateWithGoogle_SetsGoogleIdAndNullPassword()
    {
        var account = CustomerAccount.CreateWithGoogle("Jane Smith", "jane@gmail.com", "google_sub_abc123");

        account.GoogleId.Should().Be("google_sub_abc123");
        account.PasswordHash.Should().BeNull();
        account.IsEmailVerified.Should().BeTrue();
        account.Status.Should().Be(CustomerStatus.Active);
        account.Email.Should().Be("jane@gmail.com");
        account.FullName.Should().Be("Jane Smith");
    }

    [Fact]
    public void MarkEmailVerified_SetsStatusToActive()
    {
        var account = CustomerAccount.Create("John Doe", "john@example.com", "hashed_password");
        account.Status.Should().Be(CustomerStatus.PendingEmailVerification);
        account.IsEmailVerified.Should().BeFalse();

        account.MarkEmailVerified();

        account.IsEmailVerified.Should().BeTrue();
        account.Status.Should().Be(CustomerStatus.Active);
    }

    [Fact]
    public void IncrementFailedLogin_IncrementsCounter()
    {
        var account = CustomerAccount.Create("John Doe", "john@example.com", "hashed_password");

        account.IncrementFailedLogin();
        account.FailedLoginAttempts.Should().Be(1);
        account.IsLockedOut().Should().BeFalse();

        account.IncrementFailedLogin();
        account.FailedLoginAttempts.Should().Be(2);
        account.IsLockedOut().Should().BeFalse();

        account.IncrementFailedLogin();
        account.FailedLoginAttempts.Should().Be(3);
        account.IsLockedOut().Should().BeFalse();
    }

    [Fact]
    public void IncrementFailedLogin_AtTen_LocksAccount()
    {
        var account = CustomerAccount.Create("John Doe", "john@example.com", "hashed_password");

        for (var i = 0; i < 9; i++)
        {
            account.IncrementFailedLogin();
            account.IsLockedOut().Should().BeFalse($"account should not be locked after {i + 1} attempt(s)");
        }

        account.IncrementFailedLogin();

        account.FailedLoginAttempts.Should().Be(10);
        account.IsLockedOut().Should().BeTrue();
        account.LockoutUntil.Should().NotBeNull();
        account.LockoutUntil!.Value.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void ResetFailedLogins_SetsCounterToZero()
    {
        var account = CustomerAccount.Create("John Doe", "john@example.com", "hashed_password");
        for (var i = 0; i < 10; i++) account.IncrementFailedLogin();
        account.IsLockedOut().Should().BeTrue();

        account.ResetFailedLogin();

        account.FailedLoginAttempts.Should().Be(0);
        account.LockoutUntil.Should().BeNull();
        account.IsLockedOut().Should().BeFalse();
    }

    [Fact]
    public void IsLockedOut_ReturnsTrueWhenCounterIsAtMax()
    {
        var account = CustomerAccount.Create("John Doe", "john@example.com", "hashed_password");
        account.IsLockedOut().Should().BeFalse();

        for (var i = 0; i < 10; i++) account.IncrementFailedLogin();

        account.IsLockedOut().Should().BeTrue();
    }

    [Fact]
    public void SetAvatar_UpdatesAvatarId()
    {
        var account = CustomerAccount.CreateWithGoogle("Jane Smith", "jane@gmail.com", "google_id");
        const string avatarUrl = "https://cdn.example.com/avatars/jane.png";

        account.SetAvatarUrl(avatarUrl);

        account.AvatarUrl.Should().Be(avatarUrl);
    }

    [Fact]
    public void RemoveAvatar_ClearsAvatarId()
    {
        var account = CustomerAccount.CreateWithGoogle("Jane Smith", "jane@gmail.com", "google_id");
        account.SetAvatarUrl("https://cdn.example.com/avatars/jane.png");
        account.AvatarUrl.Should().NotBeNull();

        account.SetAvatarUrl(null);

        account.AvatarUrl.Should().BeNull();
    }
}