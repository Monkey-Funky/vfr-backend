
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using System.Runtime;

namespace Infrastructure.Services;

/// <summary>
/// AWS S3 implementation of <see cref="IFileStorageService"/>.
///
/// UPLOAD FLOW:
///   1. Read first 4 bytes from stream — validate JPEG or PNG magic bytes.
///   2. Generate GUID filename with the correct extension.
///   3. Reset stream position to start.
///   4. Execute PutObjectRequest inside the Polly "s3" resilience pipeline.
///   5. Return the constructed public URL (base URL + folder + filename).
///
/// DELETE FLOW:
///   1. Extract S3 object key from the public URL.
///   2. Execute DeleteObjectRequest inside the Polly "s3" resilience pipeline.
///
/// MAGIC BYTE REFERENCE:
///   JPEG: FF D8 FF       (first 3 bytes)
///   PNG : 89 50 4E 47   (all 4 bytes — "\x89PNG")
/// </summary>
public sealed class FileStorageService : IFileStorageService
{
    // ── Magic byte constants ──────────────────────────────────────────────────

    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];

    // ── Infrastructure ────────────────────────────────────────────────────────

    private readonly IAmazonS3 _s3Client;
    private readonly S3Settings _settings;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        IAmazonS3 s3Client,
        IOptions<S3Settings> settings,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<FileStorageService> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _resiliencePipeline = pipelineProvider.GetPipeline("s3");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // =========================================================================
    // IFileStorageService.UploadAsync
    // =========================================================================

    /// <inheritdoc />
    public async Task<string> UploadAsync(
        Stream stream,
        string fileName,
        string folder,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName, nameof(fileName));
        ArgumentException.ThrowIfNullOrWhiteSpace(folder, nameof(folder));

        // ── 1. Validate magic bytes ───────────────────────────────────────────
        var (isValid, extension, contentType) = await ValidateMagicBytesAsync(stream, ct);

        if (!isValid)
        {
            _logger.LogWarning(
                "FileStorageService.UploadAsync — magic byte validation failed. " +
                "OriginalFileName: {FileName}. Upload rejected.",
                fileName);

            throw new BusinessRuleException(
                "INVALID_FILE_TYPE",
                "Only JPEG and PNG images are allowed. " +
                "The uploaded file does not match a supported image format.");
        }

        // ── 2. Generate safe filename ─────────────────────────────────────────
        // The original filename is discarded. A collision-free GUID is used instead.
        var safeFileName = $"{Guid.NewGuid():N}{extension}";   // e.g. "a1b2c3d4...N.jpg"
        var s3Key = $"{folder.TrimEnd('/')}/{safeFileName}";

        // ── 3. Reset stream to beginning ──────────────────────────────────────
        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        // ── 4. Upload via Polly resilience pipeline ───────────────────────────
        await _resiliencePipeline.ExecuteAsync(async innerCt =>
        {
            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = s3Key,
                InputStream = stream,
                ContentType = contentType,
                // Uploaded files are intended to be public (brand logos, product images).
                // If the bucket uses ACLs, set CannedACL = S3CannedACL.PublicRead.
                // If the bucket uses a public-access policy, omit CannedACL.
            };

            await _s3Client.PutObjectAsync(request, innerCt);
        }, ct);

        // ── 5. Build and return public URL ────────────────────────────────────
        var publicUrl = $"{_settings.BaseUrl.TrimEnd('/')}/{s3Key}";

        _logger.LogInformation(
            "FileStorageService.UploadAsync — upload successful. " +
            "S3Key: {S3Key}, Folder: {Folder}, ContentType: {ContentType}",
            s3Key, folder, contentType);

        return publicUrl;
    }

    // =========================================================================
    // IFileStorageService.DeleteAsync
    // =========================================================================

    /// <inheritdoc />
    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("FileStorageService.DeleteAsync — called with null/empty URL. Skipping.");
            return;
        }

        // Extract S3 key from the public URL by removing the base URL prefix.
        // e.g. "https://cdn.example.com/brand-logos/abc123.jpg"
        //   → "brand-logos/abc123.jpg"
        var baseUrl = _settings.BaseUrl.TrimEnd('/') + "/";
        if (!url.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "FileStorageService.DeleteAsync — URL does not match configured BaseUrl. URL: {Url}",
                url);
            return;
        }

        var s3Key = url[baseUrl.Length..];

        await _resiliencePipeline.ExecuteAsync(async innerCt =>
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = s3Key,
            };

            await _s3Client.DeleteObjectAsync(request, innerCt);
        }, ct);

        _logger.LogInformation(
            "FileStorageService.DeleteAsync — object deleted. S3Key: {S3Key}", s3Key);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Reads the first 4 bytes of the stream to check the magic number signature.
    /// Returns (isValid, extension, contentType). Stream position is NOT reset here —
    /// the caller resets after validation.
    /// </summary>
    private static async Task<(bool IsValid, string Extension, string ContentType)>
        ValidateMagicBytesAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        var read = await stream.ReadAsync(header.AsMemory(0, 4), ct);

        if (read < 3)
            return (false, string.Empty, string.Empty);

        // JPEG: FF D8 FF (first 3 bytes)
        if (header[0] == JpegMagic[0] &&
            header[1] == JpegMagic[1] &&
            header[2] == JpegMagic[2])
        {
            return (true, ".jpg", "image/jpeg");
        }

        // PNG: 89 50 4E 47 (all 4 bytes)
        if (read >= 4 &&
            header[0] == PngMagic[0] &&
            header[1] == PngMagic[1] &&
            header[2] == PngMagic[2] &&
            header[3] == PngMagic[3])
        {
            return (true, ".png", "image/png");
        }

        return (false, string.Empty, string.Empty);
    }
}
