namespace Application.Interfaces.Services;

/// <summary>
/// Abstraction over blob / object storage (AWS S3, Azure Blob, Cloudinary, etc.).
/// The implementation lives in Infrastructure.Services.
///
/// Callers (Application handlers) depend only on this interface and never
/// reference any cloud-provider SDK directly, keeping the Application layer clean.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads a readable stream to storage and returns the public URL of the file.
    /// </summary>
    /// <param name="stream">Readable content stream. Must not be disposed before the call returns.</param>
    /// <param name="fileName">Original file name including extension (e.g. "logo.png").</param>
    /// <param name="folder">Logical folder / key prefix inside the bucket (e.g. "brand-logos").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full public URL of the uploaded file (e.g. "https://cdn.example.com/brand-logos/logo.png").</returns>
    Task<string> UploadAsync(
        Stream stream,
        string fileName,
        string folder,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a previously uploaded file from storage.
    /// </summary>
    /// <param name="url">The full public URL returned by a previous <see cref="UploadAsync"/> call.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(string url, CancellationToken ct = default);
}