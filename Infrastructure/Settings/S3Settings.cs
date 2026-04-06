namespace Infrastructure.Settings;

/// <summary>
/// Strongly-typed binding for the "S3" configuration section.
///
/// BucketName and Region are safe in appsettings.json.
/// AWS credentials (AccessKey, SecretKey) MUST NOT appear in appsettings.json;
/// use IAM roles (production) or user-secrets / environment variables (dev).
/// </summary>
public sealed class S3Settings
{
    public string BucketName { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;

    /// <summary>
    /// Base public URL for constructing file URLs.
    /// For CloudFront-fronted buckets: "https://cdn.example.com"
    /// For direct S3 access: "https://{bucket}.s3.{region}.amazonaws.com"
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;
}