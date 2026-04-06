using Application.Features.Orders.DTOs;

namespace Application.Features.Orders.Queries.ExportOrdersCsv;

/// <summary>
/// Streams all orders for the retailer as a CSV byte array.
/// RetailerId is resolved from the JWT inside the handler.
///
/// CANCELLATION NOTE:
///   The controller MUST pass HttpContext.RequestAborted as the CancellationToken.
///   This ensures the DB DataReader is disposed when the client disconnects mid-stream.
/// </summary>
public sealed record ExportOrdersCsvQuery : IRequest<byte[]>;