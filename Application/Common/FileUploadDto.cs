
namespace Application.Common;

/// <summary>
/// Framework-free abstraction for an uploaded file.
/// Decouples the Application layer from Microsoft.AspNetCore.Http.IFormFile.
///
/// LIFECYCLE: The caller (API controller action) is responsible for opening
/// the stream via IFormFile.OpenReadStream() before constructing this DTO.
/// The handler disposes the stream after use via "await using".
///
/// WHY NOT IFormFile DIRECTLY:
///   The Application layer has no reference to Microsoft.AspNetCore.App.
///   Accepting IFormFile in a command would force an ASP.NET Core dependency
///   into a layer that must remain framework-agnostic.
/// </summary>
public sealed record FileUploadDto(
    /// <summary>Readable stream of the file content. Caller opens; handler disposes.</summary>
    Stream Content,

    /// <summary>Original filename from the client (used only for extension extraction in FileStorageService).</summary>
    string FileName,

    /// <summary>MIME type reported by the client (e.g. "image/jpeg"). Magic byte validation in FileStorageService is the authoritative check.</summary>
    string ContentType,

    /// <summary>File size in bytes. Used for size validation in FluentValidation rules.</summary>
    long Length
);
