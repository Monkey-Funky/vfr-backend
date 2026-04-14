using Domain.Enums.Customer;
using Domain.Exceptions;

namespace Domain.Entities.Customer;

/// <summary>
/// Core aggregate root — represents a customer user of the platform.
/// </summary>
public sealed class CustomerAccount : BaseEntity
{
    // =========================================================================
    // Core Identity Fields
    // =========================================================================

    public string FullName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? PasswordHash { get; private set; }

    public string? PhoneNumber { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public string? Gender { get; private set; }

    public string? AvatarUrl { get; private set; }

    public string? GoogleId { get; private set; }

    public bool IsEmailVerified { get; private set; }

    public string? RefreshTokenHash { get; private set; }

    public DateTime? RefreshTokenExpiresAt { get; private set; }

    public string Status { get; private set; } = CustomerStatus.Active;

    public int FailedLoginAttempts { get; private set; }

    public DateTime? LockoutUntil { get; private set; }
    public bool RememberMe { get; private set; }

    // =========================================================================
    // EF Core Constructor
    // =========================================================================

    private CustomerAccount() { }

    // =========================================================================
    // Factory Methods
    // =========================================================================

    public static CustomerAccount Create(
        string fullName,
        string email,
        string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName, nameof(fullName));
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash, nameof(passwordHash));

        return new CustomerAccount
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLower(),
            PasswordHash = passwordHash,
            Status = CustomerStatus.PendingEmailVerification,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static CustomerAccount CreateWithGoogle(
        string fullName,
        string email,
        string googleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName, nameof(fullName));
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(googleId, nameof(googleId));

        return new CustomerAccount
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLower(),
            GoogleId = googleId,
            IsEmailVerified = true,
            Status = CustomerStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    // =========================================================================
    // Domain Methods
    // =========================================================================

    public void CompleteProfile(string gender, DateOnly dateOfBirth, string? phoneNumber)
    {
        if (!CustomerStatus.IsValid(Status))
             throw new BusinessRuleException("INVALID_STATUS", "Action not allowed in current account status.");

        if (!new[] { "Male", "Female", "Other", "PreferNotToSay" }.Contains(gender))
            throw new BusinessRuleException("INVALID_GENDER", "Provided gender is invalid.");

        Gender = gender;
        DateOfBirth = dateOfBirth;
        PhoneNumber = phoneNumber?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateRefreshToken(string hash, DateTime expiresAt, bool rememberMe)
    {
        RefreshTokenHash = hash;
        RefreshTokenExpiresAt = expiresAt;
        RememberMe = rememberMe;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RevokeAllRefreshTokens()
    {
        RefreshTokenHash = null;
        RefreshTokenExpiresAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementFailedLogin()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 10)
        {
            LockoutUntil = DateTime.UtcNow.AddMinutes(15);
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResetFailedLogin()
    {
        FailedLoginAttempts = 0;
        LockoutUntil = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsLockedOut() =>
        LockoutUntil.HasValue && LockoutUntil.Value > DateTime.UtcNow;

    public void SetAvatarUrl(string? url)
    {
        AvatarUrl = url;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkEmailVerified()
    {
        IsEmailVerified = true;
        Status = CustomerStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResetPassword(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash, nameof(newPasswordHash));
        PasswordHash = newPasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetGoogleId(string googleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleId, nameof(googleId));
        GoogleId = googleId;
        UpdatedAt = DateTime.UtcNow;
    }
}
