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
    /// <param name="frontImageStream">Readable stream of the front-view photograph.</param>
    /// <param name="frontFileName">Original filename of the front image, including extension (e.g. "front.jpg").</param>
    /// <param name="frontContentType">MIME type of the front image (e.g. "image/jpeg").</param>
    /// <param name="sideImageStream">Readable stream of the side-view photograph.</param>
    /// <param name="sideFileName">Original filename of the side image, including extension (e.g. "side.jpg").</param>
    /// <param name="sideContentType">MIME type of the side image (e.g. "image/jpeg").</param>
    /// <param name="heightCm">The person's actual height in centimeters (required for scaling).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="BodyMeasurements"/> record populated by the AI model.</returns>
    Task<BodyMeasurements> ExtractAsync(
        Stream frontImageStream,
        string frontFileName,
        string frontContentType,
        Stream sideImageStream,
        string sideFileName,
        string sideContentType,
        decimal heightCm,
        CancellationToken ct = default);
}
