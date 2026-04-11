
using Application.Features.Products.DTOs;
using Application.Features.Products.Mappings;
using Application.Features.Products.Queries.GetProducts;
using Application.Interfaces.Services;
using NpgsqlTypes;
using System.Linq;

namespace Infrastructure.Persistence.Repositories;


/// <summary>
/// Concrete implementation of IProductRepository.
///
/// FTS RULE:   Uses plainto_tsquery — NEVER to_tsquery.
///             plainto_tsquery is safe for arbitrary user input (no special operators).
///             to_tsquery would throw on inputs like "t-shirt &amp;&amp; blue" or "hello world".
///
/// QUERY RULE: All list/paginated queries use AsNoTracking().AsSplitQuery().
///             AsSplitQuery avoids the Cartesian explosion when Include() is used
///             alongside pagination — it executes separate SQL per collection navigation.
/// </summary>
public sealed class ProductRepository : Repository<Product>, IProductRepository
{
    private readonly ApplicationDbContext _db;

    public ProductRepository(ApplicationDbContext context) : base(context)
    {
        _db = context;
    }

    // ── Active product count (for plan limit check) ───────────────────────────

    public Task<int> GetActiveProductCountByRetailerAsync(
        Guid retailerId,
        CancellationToken ct)
        // Global query filter already excludes IsDeleted = true.
        => _db.Products
              .AsNoTracking()
              .CountAsync(p => p.RetailerId == retailerId, ct);

    // ── Paginated product list — ≤ 2 SQL statements ───────────────────────────

    public async Task<PagedResult<ProductListDto>> GetProductsPagedAsync(
        GetProductsQuery query,
        Guid retailerId,
        CancellationToken ct)
    {
        // ── Base query: retailer scope first (index-friendly predicate order) ──
        // Global query filter applies !IsDeleted automatically.
        var baseQuery = _db.Products
            .AsNoTracking()
            .Where(p => p.RetailerId == retailerId);

        // ── Filters ────────────────────────────────────────────────────────────
        if (query.CategoryId.HasValue)
            baseQuery = baseQuery.Where(p => p.CategoryId == query.CategoryId.Value);

        if (query.SubCategoryId.HasValue)
            baseQuery = baseQuery.Where(p => p.SubCategoryId == query.SubCategoryId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
            baseQuery = baseQuery.Where(p => p.Status == query.Status);

        // ── Full-Text Search via stored search_vector GIN column ───────────────
        // Uses shadow property EF.Property<NpgsqlTsVector>(p, "SearchVector").
        // Translates to: WHERE search_vector @@ plainto_tsquery('english', ?)
        // plainto_tsquery is safe for arbitrary user input (no syntax exceptions).
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim();
            var tsQuery = EF.Functions.PlainToTsQuery("english", searchTerm);
            baseQuery = baseQuery.Where(p =>
                EF.Property<NpgsqlTsVector>(p, "SearchVector").Matches(tsQuery));
        }

        // ── Statement 1: COUNT (lean — no includes, no joins) ─────────────────
        var totalCount = await baseQuery.CountAsync(ct);

        if (totalCount == 0)
        {
            return new PagedResult<ProductListDto>
            {
                Items = [],
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalCount = 0
            };
        }

        // ── Statement 2: Paginated projection with correlated subqueries ───────
        //
        // All joins and subqueries are expressed inside a single Select() so EF Core
        // translates the entire projection into one SQL SELECT.
        //
        // Global query filters on Categories, SubCategories, and ProductImages are
        // applied automatically by EF Core — no explicit !IsDeleted needed here.
        var items = await baseQuery
            .OrderByDescending(p => p.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                _db.Categories
                    .Where(c => c.Id == p.CategoryId)
                    .Select(c => c.Name)
                    .FirstOrDefault(),
                _db.SubCategories
                    .Where(s => s.Id == p.SubCategoryId)
                    .Select(s => s.Name)
                    .FirstOrDefault(),
                p.Barcode,
                p.Status,
                p.Price,
                p.Currency,
                // Thumbnail = first non-deleted image ordered by DisplayOrder
                _db.ProductImages
                    .Where(i => i.ProductId == p.Id)
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault(),
                p.CreatedAt
            ))
            .ToListAsync(ct);

        return new PagedResult<ProductListDto>
        {
            Items = items.AsReadOnly(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    // ── Barcode lookup (per retailer scope) ───────────────────────────────────

    public Task<Product?> GetByBarcodeAsync(
        Guid retailerId,
        string barcode,
        CancellationToken ct)
        // AnyAsync would be faster for existence checks — use GetByBarcodeAsync
        // only when the caller needs the entity (e.g. CreateProductCommandHandler).
        => _db.Products
              .AsNoTracking()
              .FirstOrDefaultAsync(
                  p => p.RetailerId == retailerId
                    && p.Barcode == barcode,
                  ct);

    // ── By-ID with images (for GetProductByIdQueryHandler) ───────────────────

    public Task<Product?> GetByIdWithImagesAsync(
        Guid productId,
        Guid retailerId,
        CancellationToken ct)
        => _db.Products
              .AsNoTracking()
              .AsSplitQuery()
              .Include(p => p.Images)
              .FirstOrDefaultAsync(
                  p => p.Id == productId
                    && p.RetailerId == retailerId,
                  ct);
}