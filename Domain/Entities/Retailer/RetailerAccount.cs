// src/Domain/Entities/Retailer/RetailerAccount.cs
namespace Domain.Entities.Retailer;

/// <summary>
/// Core aggregate root — represents a retailer who owns a Virtual Fitting Room storefront.
///
/// DESIGN RULES:
///   • All property setters are private. State changes only happen through
///     factory methods (Create, CreateWithGoogle) or explicit domain methods.
///   • The private parameterless constructor exists only for EF Core. Do not use it.
///   • Never expose PasswordHash, RefreshTokenHash, or AccessFailedCount in DTOs.
///     Use RetailerMappings.ToProfileDto() for safe public projection.
/// </summary>
public sealed class RetailerAccount : BaseEntity
{
    // =========================================================================
    // Status Constants
    // =========================================================================

    /// <summary>
    /// All valid lifecycle status values for a RetailerAccount.
    /// These match the CHECK constraint on the DB column
    /// (status varchar(50) CHECK IN (...)).
    /// </summary>
    public static class Status
    {
        /// <summary>
        /// Account created in Step 1 of registration.
        /// Email not yet verified; Step 2 not yet completed.
        /// Login is blocked until the account reaches Active.
        /// </summary>
        public const string PendingEmailVerification = "PendingEmailVerification";

        /// <summary>Fully registered and operational retailer account.</summary>
        public const string Active = "Active";

        /// <summary>
        /// Suspended by an administrator.
        /// Login is blocked; existing tokens are still technically valid until they expire
        /// (the handler must re-check status on every login attempt).
        /// </summary>
        public const string Suspended = "Suspended";

        /// <summary>
        /// Retailer requested account deletion.
        /// Enters a 30-day grace period before hard deletion.
        /// </summary>
        public const string PendingDeletion = "PendingDeletion";

        /// <summary>
        /// Account has been soft-deleted (is_deleted = true).
        /// Data is retained for legal/audit compliance.
        /// </summary>
        public const string Deleted = "Deleted";
    }

    // =========================================================================
    // Core Identity Fields
    // =========================================================================

    /// <summary>Full legal or display name of the retailer owner (max 100 chars).</summary>
    public string FullName { get; private set; } = string.Empty;

    /// <summary>
    /// Primary login email. Stored as-is (original casing).
    /// All DB queries use case-insensitive comparison (EF .ToLower() or Npgsql ILIKE).
    /// Partial unique index: email WHERE is_deleted = false.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// BCrypt hash of the account password, work factor 12.
    /// NEVER expose this field in any DTO or API response.
    /// Empty string for accounts created exclusively via Google OAuth.
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    // =========================================================================
    // Brand / Business Fields
    // =========================================================================

    /// <summary>
    /// Globally unique brand name across the platform (max 150 chars).
    /// Partial unique index: brand_name WHERE is_deleted = false.
    /// </summary>
    public string BrandName { get; private set; } = string.Empty;

    /// <summary>
    /// Business category selected during Step 2 of registration (max 50 chars).
    /// e.g. "Fashion", "Electronics", "Furniture", "Unknown" (for Google OAuth accounts).
    /// </summary>
    public string BusinessType { get; private set; } = string.Empty;

    /// <summary>Whether the retailer already has 3D product models for VFR.</summary>
    public bool Has3DModels { get; private set; }

    /// <summary>
    /// Full public URL of the brand logo image in blob storage.
    /// Null until Step 2 completion or until the retailer uploads a logo via profile update.
    /// </summary>
    public string? BrandLogoUrl { get; private set; }

    // =========================================================================
    // OAuth
    // =========================================================================

    /// <summary>
    /// Google subject identifier (sub claim from Google ID token).
    /// Null for password-based accounts that have never linked Google.
    /// </summary>
    public string? GoogleId { get; private set; }

    // =========================================================================
    // Email Verification
    // =========================================================================

    /// <summary>
    /// True after the retailer clicks the verification link sent during Step 2.
    /// Also true for accounts created via Google OAuth (Google pre-verifies emails).
    /// Login is blocked when this is false.
    /// </summary>
    public bool IsEmailVerified { get; private set; }

    // =========================================================================
    // Refresh Token (Rotation)
    // =========================================================================

    /// <summary>
    /// BCrypt hash of the current valid refresh token.
    /// The raw token is returned to the client but NEVER persisted.
    /// Null when no active session exists (after logout or password change).
    /// </summary>
    public string? RefreshTokenHash { get; private set; }

    /// <summary>
    /// UTC expiry of the currently stored refresh token.
    /// Null when RefreshTokenHash is null.
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; private set; }

    /// <summary>
    /// FIX F-07: Tracks whether the session that issued the current refresh token
    /// was started with RememberMe = true (30-day TTL) or false (7-day TTL).
    ///
    /// Persisted so that every token rotation preserves the original TTL decision.
    /// Without this flag, every rotation silently degrades a 30-day session to 7 days.
    ///
    /// Reset to false when tokens are revoked (logout / password reset).
    /// </summary>
    public bool IsRememberMeSession { get; private set; }

    // =========================================================================
    // Subscription
    // =========================================================================

    /// <summary>
    /// FK to the retailer's current subscription record.
    /// Null for newly registered retailers who haven't subscribed yet.
    /// </summary>
    public Guid? SubscriptionId { get; private set; }

    // =========================================================================
    // Account Status
    // =========================================================================

    /// <summary>
    /// Current lifecycle status.
    /// Use the <see cref="Status"/> nested class for valid constant values.
    /// </summary>
    public string AccountStatus { get; private set; } = Status.PendingEmailVerification;

    // =========================================================================
    // Account Lockout
    // =========================================================================

    /// <summary>
    /// Number of consecutive failed login attempts since the last successful login
    /// or the last lockout reset.
    /// Resets to 0 after a successful login (<see cref="ResetFailedLoginCount"/>).
    /// Account is locked when this reaches 10 (<see cref="IncrementFailedLoginCount"/>).
    /// </summary>
    public int AccessFailedCount { get; private set; }

    /// <summary>
    /// UTC timestamp until which this account is locked out.
    /// Null = account is NOT locked out.
    /// Set to UtcNow + 15 minutes when AccessFailedCount reaches 10.
    /// </summary>
    public DateTime? LockoutEndAt { get; private set; }

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private RetailerAccount() { }

    // =========================================================================
    // Factory Methods
    // =========================================================================

    /// <summary>
    /// Creates a <b>partial</b> RetailerAccount from Step 1 registration data.
    /// The returned entity has <c>Status = PendingEmailVerification</c> and
    /// <c>IsEmailVerified = false</c>.
    ///
    /// MUST be followed by <see cref="CompleteRegistration"/> (Step 2) to activate.
    /// </summary>
    /// <param name="fullName">Retailer's full name. Max 100 chars.</param>
    /// <param name="email">Unique email address. Max 200 chars. Stored as-is (original casing).</param>
    /// <param name="passwordHash">
    ///   BCrypt hash (work factor 12) of the raw password.
    ///   The caller is responsible for hashing before calling this method.
    /// </param>
    /// <param name="brandName">Globally unique brand name. Max 150 chars.</param>
    /// <returns>A new, unsaved RetailerAccount instance ready to be added to the DbContext.</returns>
    public static RetailerAccount Create(
        string fullName,
        string email,
        string passwordHash,
        string brandName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName, nameof(fullName));
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash, nameof(passwordHash));
        ArgumentException.ThrowIfNullOrWhiteSpace(brandName, nameof(brandName));

        return new RetailerAccount
        {
            FullName = fullName.Trim(),
            Email = email.Trim(),
            PasswordHash = passwordHash,
            BrandName = brandName.Trim(),
            AccountStatus = Status.PendingEmailVerification,
            IsEmailVerified = false,
        };
    }

    /// <summary>
    /// Creates a fully active RetailerAccount from a Google OAuth sign-in.
    /// The account is immediately <c>Active</c> because Google has already verified
    /// the email before issuing the ID token.
    ///
    /// The retailer should update BusinessType and BrandName via profile management later.
    /// </summary>
    /// <param name="fullName">Display name from the Google ID token profile claim.</param>
    /// <param name="email">Verified email from the Google ID token.</param>
    /// <param name="googleId">Google sub claim — unique identifier per Google account.</param>
    /// <param name="brandName">
    ///   Initial brand name (derived automatically, e.g. from email prefix + short GUID).
    ///   Unique collision must be handled by the caller before invoking this method.
    /// </param>
    public static RetailerAccount CreateWithGoogle(
        string fullName,
        string email,
        string googleId,
        string brandName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName, nameof(fullName));
        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(googleId, nameof(googleId));
        ArgumentException.ThrowIfNullOrWhiteSpace(brandName, nameof(brandName));

        return new RetailerAccount
        {
            FullName = fullName.Trim(),
            Email = email.Trim(),
            PasswordHash = string.Empty,     // No password for OAuth-only accounts
            BrandName = brandName.Trim(),
            GoogleId = googleId,
            IsEmailVerified = true,             // Google pre-verifies all emails
            AccountStatus = Status.Active,
            BusinessType = "Unknown",        // Retailer updates this in profile settings
        };
    }

    // =========================================================================
    // Domain Methods
    // =========================================================================

    /// <summary>
    /// Completes Step 2 of registration by adding business details and activating the account.
    /// Sets <c>AccountStatus = Active</c> and <c>IsEmailVerified = true</c>.
    ///
    /// FIX F-05: IsEmailVerified is now set here. Completing Step 2 is the implicit
    /// email-ownership proof for the email-registration flow (the retailer must have
    /// received the Step 1 step token in their inbox to have reached Step 2).
    /// </summary>
    public void CompleteRegistration(
        string businessType,
        bool has3DModels,
        string? brandLogoUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(businessType, nameof(businessType));

        BusinessType = businessType.Trim();
        Has3DModels = has3DModels;
        BrandLogoUrl = brandLogoUrl;
        AccountStatus = Status.Active;
        IsEmailVerified = true;   
    }

    /// <summary>
    /// Stores a new hashed refresh token and its expiry. Called after every
    /// successful login or token refresh (token rotation pattern).
    ///
    /// FIX F-07: The <paramref name="rememberMe"/> flag is now persisted on the entity
    /// so that subsequent token rotations in <c>RefreshTokenCommandHandler</c> can
    /// preserve the original TTL (30 days vs 7 days) without the client re-supplying the flag.
    ///
    /// IMPORTANT: Pass the BCrypt hash, NOT the raw token. The raw token goes to the client.
    /// </summary>
    /// <param name="hash">BCrypt hash of the raw refresh token.</param>
    /// <param name="expiresAt">UTC expiry timestamp for this refresh token.</param>
    /// <param name="rememberMe">
    ///   True if the session was started with RememberMe = true (30-day TTL).
    ///   Defaults to false for token rotations that don't change the session type.
    /// </param>
    public void UpdateRefreshToken(string hash, DateTime expiresAt, bool rememberMe = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash, nameof(hash));

        RefreshTokenHash = hash;
        RefreshTokenExpiresAt = expiresAt;
        IsRememberMeSession = rememberMe;
    }

    /// <summary>
    /// Immediately invalidates all active sessions by nulling the refresh token fields.
    /// Called on logout and on password change/reset.
    /// </summary>
    public void RevokeAllRefreshTokens()
    {
        RefreshTokenHash = null;
        RefreshTokenExpiresAt = null;
        IsRememberMeSession = false;
    }

    /// <summary>
    /// Marks the account's email as verified.
    /// Note: This does NOT set AccountStatus = Active.
    ///       Activation happens via <see cref="CompleteRegistration"/> in Step 2.
    /// </summary>
    public void MarkEmailVerified()
    {
        IsEmailVerified = true;
    }

    /// <summary>
    /// Transitions the account into the PendingDeletion lifecycle state.
    /// The account enters a 30-day grace period before hard deletion runs.
    /// </summary>
    public void MarkPendingDeletion()
    {
        AccountStatus = Status.PendingDeletion;
    }

    /// <summary>
    /// Increments the consecutive failed login counter.
    /// When the count reaches 10, the account is locked for 15 minutes.
    ///
    /// Always call this on every failed authentication attempt so that
    /// the DB update correctly records the new count.
    /// </summary>
    public void IncrementFailedLoginCount()
    {
        AccessFailedCount++;

        if (AccessFailedCount >= 10)
        {
            // Lock the account for 15 minutes (matches Identity DefaultLockoutTimeSpan)
            LockoutEndAt = DateTime.UtcNow.AddMinutes(15);
        }
    }

    /// <summary>
    /// Resets the failed login counter and clears any active lockout.
    /// Always call this after a successful authentication.
    /// </summary>
    public void ResetFailedLoginCount()
    {
        AccessFailedCount = 0;
        LockoutEndAt = null;
    }

    /// <summary>
    /// Returns <c>true</c> if the account is currently within a lockout window.
    /// </summary>
    public bool IsLockedOut()
        => LockoutEndAt.HasValue && LockoutEndAt.Value > DateTime.UtcNow;

    /// <summary>
    /// Associates a Google account with this retailer.
    /// Called when an existing password-based account signs in with Google for the first time.
    /// </summary>
    public void SetGoogleId(string googleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleId, nameof(googleId));
        GoogleId = googleId;
    }

    /// <summary>
    /// Replaces the current password hash with a new one.
    /// Only called by the ResetPassword flow after OTP verification.
    /// Direct access to the password hash is intentionally locked behind this method.
    /// </summary>
    /// <param name="newPasswordHash">BCrypt hash (work factor 12) of the new password.</param>
    public void ResetPassword(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash, nameof(newPasswordHash));
        PasswordHash = newPasswordHash;
    }
}