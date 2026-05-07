using Domain.Entities.Customer;

namespace Application.Interfaces.Services.Customer;

/// <summary>
/// Abstraction over an external AI model that extracts body measurements
/// from a full-body photograph.
///
/// The implementation lives in Infrastructure.Services.
/// The Application layer depends only on this interface, keeping it
/// framework-agnostic and testable in isolation.
///
/// CONTRACT:
///   • The image stream is consumed (read) by the implementation but NOT persisted.
///   • The caller is responsible for disposing the stream after this method returns.
///   • The AI model requires the person's actual height to scale pixel-based estimates.
/// </summary>
public interface IBodyMeasurementExtractionService
{
    /// <summary>
    /// Sends the provided image stream and height to the AI model and returns
    /// the extracted body measurements.
    /// </summary>
    /// <param name="imageStream">Readable stream of the full-body photograph.</param>
    /// <param name="fileName">Original filename including extension (e.g. "photo.jpg"). Used to set the filename in the multipart/form-data request.</param>
    /// <param name="contentType">MIME type of the image (e.g. "image/jpeg"). Required by the AI API's multipart boundary.</param>
    /// <param name="heightCm">The person's actual height in centimeters (required for scaling).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="BodyMeasurements"/> record populated by the AI model.</returns>
    Task<BodyMeasurements> ExtractAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        decimal heightCm,
        CancellationToken ct = default);
}
