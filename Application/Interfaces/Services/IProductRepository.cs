using Application.Features.Products.DTOs;
using Application.Features.Products.Queries.GetProducts;
using Application.Interfaces.Persistence;

namespace Application.Interfaces.Services;


/// <summary>
/// Product-specific repository contract.
/// Extends <see cref="IRepository{T}"/> with product-domain queries.
///
/// RULE: All list/page queries use AsNoTracking().AsSplitQuery().
/// RULE: FTS uses plainto_tsquery (never to_tsquery) — safe for user input.
/// </summary>
public interface IProductRepository : IRepository<Product>
{
    /// <summary>
    /// Returns the count of non-deleted products for the given retailer.
    /// Must be called inside a transaction for TOCTOU safety.
    /// </summary>
    Task<int> GetActiveProductCountByRetailerAsync(
        Guid retailerId,
        CancellationToken ct);

    /// <summary>
    /// Returns a paginated product list with FTS and filter support.
    /// retailerId is ALWAYS injected by the handler — never from the query object.
    /// </summary>
    Task<PagedResult<ProductListDto>> GetProductsPagedAsync(
        GetProductsQuery query,
        Guid retailerId,
        CancellationToken ct);

    /// <summary>Lookup by barcode within the retailer's scope.</summary>
    Task<Product?> GetByBarcodeAsync(
        Guid retailerId,
        string barcode,
        CancellationToken ct);

    /// <summary>Fetch a product with its non-deleted images (AsNoTracking).</summary>
    Task<Product?> GetByIdWithImagesAsync(
        Guid productId,
        Guid retailerId,
        CancellationToken ct);
}