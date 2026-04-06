using Application.Features.Inventory.DTOs;


namespace Application.Features.Inventory.Queries.GetInventory;

/// <summary>
/// Returns a paginated list of inventory records for the authenticated retailer.
///
/// Filters:
///   • ProductName  — partial, case-insensitive match on the denormalised ProductName snapshot.
///
/// Sort:
///   • SortBySoldQuantityDesc = true  → descending sold quantity (best-sellers first).
///   • SortBySoldQuantityDesc = false → default creation order (newest first).
///
/// RetailerId is ALWAYS resolved from the JWT inside the handler — never from the request.
/// </summary>
public sealed record GetInventoryQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? ProductName = null,
    bool SortBySoldQuantityDesc = false
) : IRequest<PagedResult<InventoryDto>>;