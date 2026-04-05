namespace API.Controllers.Products.Requests;

/// <summary>
/// Request model for PUT and PATCH /api/retailers/{retailerId}/products/{productId}.
///
/// PARTIAL UPDATE SEMANTICS:
///   Each updatable field has a companion "ShouldUpdate{Field}" boolean.
///   The client sets ShouldUpdate{Field} = true only for fields it wants to change.
///   This allows explicit clearing (e.g. removing a description by sending
///   ShouldUpdateDescription = true, NewDescription = null) without confusing
///   "omitted" with "intentionally cleared".
///
///   PUT callers are expected to set all ShouldUpdate* flags to true.
///   PATCH callers set only the flags for fields they wish to mutate.
///
/// IDOR:
///   ProductId comes from the route — never from this body.
/// </summary>
public sealed record UpdateProductRequest(
    string? NewName,
    string? NewDescription,
    bool ShouldUpdateDescription,
    decimal? NewPrice,
    bool ShouldUpdatePrice,
    string? NewBarcode,
    bool ShouldUpdateBarcode,
    Guid? NewCategoryId,
    bool ShouldUpdateCategory,
    Guid? NewSubCategoryId,
    string? NewStatus
);