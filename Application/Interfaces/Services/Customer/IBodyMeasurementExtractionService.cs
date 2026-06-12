using Domain.Entities.Customer;

namespace Application.Interfaces.Services.Customer;

/// <summary>
/// Abstraction over an external AI model that extracts body measurements
/// from two full-body photographs (front view + side view).
///
/// The implementation lives in Infrastructure.Services.
/// The Application layer depends only on this interface, keeping it
/// framework-agnostic and testable in isolation.
///
/// CONTRACT:
///   • Both streams are consumed (read) by the implementation but NOT persisted.
///   • The caller is responsible for disposing both streams after this method returns.
///   • The AI model requires the person's actual height to scale pixel-based estimates.
///   • front_image → the customer facing directly toward the camera (arms slightly away from body).
///   • side_image  → the customer facing 90° to the side (arms slightly away from body).
/// </summary>
public interface IBodyMeasurementExtractionService
{
    /// <summary>
    /// Sends both image streams and the customer's height to the AI model
    /// and returns the extracted body measurements.
    /// </summary>
    /// <param name="imageStream">Readable stream of the full-body photograph.</param>
    /// <param name="fileName">Original filename of the image, including extension (e.g. "image.jpg").</param>
    /// <param name="contentType">MIME type of the image (e.g. "image/jpeg").</param>
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
