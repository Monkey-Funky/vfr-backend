// tests/Tests.Unit/Auth/Helpers/RetailerAccountFactory.cs
using Domain.Entities.Retailer;

namespace Tests.Unit.Auth.Helpers;

/// <summary>
/// Builds RetailerAccount instances through the real domain factory methods.
///
/// WHY WORK FACTOR 4:
///   BCrypt.HashPassword at the production work factor (12) takes ~300 ms per call.
///   Factor 4 takes ~5 ms and produces a hash that BCrypt.Verify accepts identically.
///   Tests exercise handler control flow, not hash strength, so factor 4 is correct.
/// </summary>
public static class RetailerAccountFactory
{
    private const int TestWorkFactor = 4;

    /// <summary>Creates a fully Active account (Status = Active, IsEmailVerified = true).</summary>
    public static RetailerAccount CreateActive(
        string email = "retailer@example.com",
        string rawPassword = "Password1!",
        string fullName = "Test Retailer",
        string brandName = "TestBrand")
    {
        string hash = BCrypt.Net.BCrypt.HashPassword(rawPassword, workFactor: TestWorkFactor);
        RetailerAccount account = RetailerAccount.Create(fullName, email, hash, brandName);
        account.CompleteRegistration(businessType: "Fashion", has3DModels: false, brandLogoUrl: null);
        return account;
    }

    /// <summary>Creates a PendingEmailVerification account (Step 1 done, Step 2 not done).</summary>
    public static RetailerAccount CreatePending(
        string email = "pending@example.com",
        string rawPassword = "Password1!",
        string fullName = "Pending Retailer",
        string brandName = "PendingBrand")
    {
        string hash = BCrypt.Net.BCrypt.HashPassword(rawPassword, workFactor: TestWorkFactor);
        return RetailerAccount.Create(fullName, email, hash, brandName);
        // Status stays PendingEmailVerification intentionally
    }

    /// <summary>Creates an Active account locked by 10 consecutive failed login attempts.</summary>
    public static RetailerAccount CreateLockedOut(
        string email = "locked@example.com",
        string rawPassword = "Password1!")
    {
        RetailerAccount account = CreateActive(email, rawPassword);
        for (int i = 0; i < 10; i++)
            account.IncrementFailedLoginCount();
        return account;
    }

    /// <summary>
    /// Creates an Active account that already holds a hashed refresh token,
    /// as it would look after a successful login.
    /// Returns both the entity and the raw (unhashed) token for test assertions.
    /// </summary>
    public static (RetailerAccount Account, string RawRefreshToken) CreateWithRefreshToken(
        string email = "retailer@example.com",
        string rawPassword = "Password1!")
    {
        RetailerAccount account = CreateActive(email, rawPassword);
        string rawToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        string hashedToken = BCrypt.Net.BCrypt.HashPassword(rawToken, workFactor: TestWorkFactor);
        account.UpdateRefreshToken(
            hash: hashedToken,
            expiresAt: DateTime.UtcNow.AddDays(7),
            rememberMe: false);
        return (account, rawToken);
    }
}