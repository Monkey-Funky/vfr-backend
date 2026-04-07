

namespace Application.Interfaces.Services;

/// <summary>
/// Abstraction over Amazon S3 (or any object store) for uploading generated reports.
/// Implemented by S3StorageService in the Infrastructure layer.
/// </summary>
public interface IS3StorageService
{
    /// <summary>
    /// Uploads the stream to the given S3 key.
    /// Returns the pre-signed URL valid for 7 days.
    /// Throws ExternalServiceException on failure.
    /// </summary>
    Task<string> UploadReportAsync(
        string s3Key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);
}