using Amazon.S3;
using Amazon.S3.Model;
using Application.Interfaces.Services;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;


namespace Infrastructure.Services.Dashboard;

/// <summary>
/// AWS S3 implementation of IS3StorageService.
/// Uses pre-signed URLs with 7-day expiry for report download links.
/// Wrapped in the existing Polly 's3' resilience pipeline registered in DI.
/// </summary>
public sealed class S3StorageService : IS3StorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3Settings _settings;

    public S3StorageService(IAmazonS3 s3Client, IOptions<S3Settings> settings)
    {
        _s3Client = s3Client;
        _settings = settings.Value;
    }

    public async Task<string> UploadReportAsync(
        string s3Key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        PutObjectRequest putRequest = new()
        {
            BucketName = _settings.BucketName,
            Key = s3Key,
            InputStream = content,
            ContentType = contentType
        };

        try
        {
            await _s3Client.PutObjectAsync(putRequest, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw new ExternalServiceException("AWS S3",
                $"Failed to upload report '{s3Key}': {ex.Message}", ex);
        }

        // Generate a pre-signed URL valid for 7 days.
        GetPreSignedUrlRequest urlRequest = new()
        {
            BucketName = _settings.BucketName,
            Key = s3Key,
            Expires = DateTime.UtcNow.AddDays(7)
        };

        return _s3Client.GetPreSignedURL(urlRequest);
    }
}