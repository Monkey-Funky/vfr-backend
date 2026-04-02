namespace Infrastructure.Settings;

/// <summary>
/// Stripe API configuration.
/// Bound from "StripeSettings" configuration section.
/// SecretKey must NEVER appear in appsettings.json — use user-secrets (dev)
/// or Azure Key Vault (prod).
/// </summary>
public sealed record StripeSettings
{
    public const string SectionName = "StripeSettings";

    /// <summary>Stripe Secret Key (sk_live_xxx or sk_test_xxx).</summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Stripe Webhook Signing Secret (for future webhook handler in P-049).</summary>
    public string WebhookSecret { get; init; } = string.Empty;
}