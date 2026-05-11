using Application.Interfaces.Services;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Polly;
using Polly.Registry;


namespace Infrastructure.Services.Storage;


/// <summary>
/// Cloudinary implementation of <see cref="IFileStorageService"/>.
///
/// UPLOAD FLOW:
///   1. Read first 4 bytes — validate JPEG or PNG magic bytes.
///   2. Generate GUID-based public_id with folder prefix.
///   3. Reset stream position.
///   4. Execute Upload inside the Polly "s3" resilience pipeline.
///   5. Return the secure URL from Cloudinary response.
///
/// DELETE FLOW:
///   1. Extract public_id from the Cloudinary URL.
///   2. Execute Destroy inside the Polly "s3" resilience pipeline.
/// </summary>
public sealed class CloudinaryFileStorageService : IFileStorageService
{
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];

    private readonly Cloudinary _cloudinary;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<CloudinaryFileStorageService> _logger;

    public CloudinaryFileStorageService(
        Cloudinary cloudinary,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<CloudinaryFileStorageService> logger)
    {
        _cloudinary = cloudinary ?? throw new ArgumentNullException(nameof(cloudinary));
        _resiliencePipeline = pipelineProvider.GetPipeline("s3");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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

        // 1. Validate magic bytes
        var (isValid, extension, _) = await ValidateMagicBytesAsync(stream, ct);

        if (!isValid)
        {
            _logger.LogWarning(
                "CloudinaryFileStorageService.UploadAsync — magic byte validation failed. " +
                "OriginalFileName: {FileName}. Upload rejected.", fileName);

            throw new Domain.Exceptions.BusinessRuleException(
                "INVALID_FILE_TYPE",
                "Only JPEG and PNG images are allowed. " +
                "The uploaded file does not match a supported image format.");
        }

        // 2. Generate safe public_id
        var publicId = $"{folder.TrimEnd('/')}/{Guid.NewGuid():N}";

        // 3. Reset stream
        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        // 4. Upload via Polly resilience pipeline
        ImageUploadResult? result = null;

        await _resiliencePipeline.ExecuteAsync(async innerCt =>
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                PublicId = publicId,
                Overwrite = false,
            };

            result = await _cloudinary.UploadAsync(uploadParams, innerCt);

            if (result.Error != null)
            {
                throw new Domain.Exceptions.ExternalServiceException(
                    "Cloudinary",
                    $"Upload failed: {result.Error.Message}");
            }
        }, ct);

        var publicUrl = result!.SecureUrl.ToString();

        _logger.LogInformation(
            "CloudinaryFileStorageService.UploadAsync — upload successful. " +
            "PublicId: {PublicId}, Folder: {Folder}",
            publicId, folder);

        return publicUrl;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning(
                "CloudinaryFileStorageService.DeleteAsync — called with null/empty URL. Skipping.");
            return;
        }

        // Extract public_id from Cloudinary URL
        // URL format: https://res.cloudinary.com/{cloud}/image/upload/v{version}/{public_id}.{ext}
        var publicId = ExtractPublicId(url);
        if (publicId is null)
        {
            _logger.LogWarning(
                "CloudinaryFileStorageService.DeleteAsync — could not extract public_id. URL: {Url}",
                url);
            return;
        }

        await _resiliencePipeline.ExecuteAsync(async innerCt =>
        {
            var destroyParams = new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Image,
            };

            var result = await _cloudinary.DestroyAsync(destroyParams);

            if (result.Result != "ok" && result.Result != "not found")
            {
                _logger.LogWarning(
                    "CloudinaryFileStorageService.DeleteAsync — unexpected result: {Result}. PublicId: {PublicId}",
                    result.Result, publicId);
            }
        }, ct);

        _logger.LogInformation(
            "CloudinaryFileStorageService.DeleteAsync — object deleted. PublicId: {PublicId}",
            publicId);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the Cloudinary public_id from a secure URL.
    /// Example: https://res.cloudinary.com/ddjzbouvr/image/upload/v1234/brand-logos/abc123.jpg
    ///       → brand-logos/abc123
    /// </summary>
    private static string? ExtractPublicId(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath; // /ddjzbouvr/image/upload/v1234/brand-logos/abc123.jpg

            // Find "/upload/" or "/upload/v{digits}/"
            const string uploadSegment = "/upload/";
            var uploadIndex = path.IndexOf(uploadSegment, StringComparison.OrdinalIgnoreCase);
            if (uploadIndex < 0) return null;

            var afterUpload = path[(uploadIndex + uploadSegment.Length)..];

            // Skip version segment if present (v1234567890/)
            if (afterUpload.StartsWith('v') && afterUpload.Contains('/'))
            {
                var versionEnd = afterUpload.IndexOf('/');
                var versionPart = afterUpload[1..versionEnd];
                if (long.TryParse(versionPart, out _))
                    afterUpload = afterUpload[(versionEnd + 1)..];
            }

            // Remove file extension
            var lastDot = afterUpload.LastIndexOf('.');
            if (lastDot > 0)
                afterUpload = afterUpload[..lastDot];

            return afterUpload;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<(bool IsValid, string Extension, string ContentType)>
        ValidateMagicBytesAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4];
        var read = await stream.ReadAsync(header.AsMemory(0, 4), ct);

        if (read < 3) return (false, string.Empty, string.Empty);

        if (header[0] == JpegMagic[0] && header[1] == JpegMagic[1] && header[2] == JpegMagic[2])
            return (true, ".jpg", "image/jpeg");

        if (read >= 4 &&
            header[0] == PngMagic[0] && header[1] == PngMagic[1] &&
            header[2] == PngMagic[2] && header[3] == PngMagic[3])
            return (true, ".png", "image/png");

        return (false, string.Empty, string.Empty);
    }
}