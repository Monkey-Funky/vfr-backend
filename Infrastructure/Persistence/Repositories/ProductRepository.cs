
using Application.Features.Products.Queries.GetProducts;
using Application.Mappings;
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
        => _db.Products
              .AsNoTracking()
              .CountAsync(p => p.RetailerId == retailerId && !p.IsDeleted, ct);

    // ── Paginated product list with FTS ───────────────────────────────────────

    public async Task<PagedResult<ProductListDto>> GetProductsPagedAsync(
        GetProductsQuery query,
        Guid retailerId,
        CancellationToken ct)
    {
        // Start with the retailer-scoped base query
        // AsNoTracking() + AsSplitQuery() are applied before materialisation.
        var baseQuery = _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Where(p => p.RetailerId == retailerId && !p.IsDeleted);

        // ── Filters ────────────────────────────────────────────────────────────
        if (query.CategoryId.HasValue)
            baseQuery = baseQuery.Where(p => p.CategoryId == query.CategoryId.Value);

        if (query.SubCategoryId.HasValue)
            baseQuery = baseQuery.Where(p => p.SubCategoryId == query.SubCategoryId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
            baseQuery = baseQuery.Where(p => p.Status == query.Status);

        // ── Full-Text Search ───────────────────────────────────────────────────
        // EF Core + Npgsql: use EF.Functions.ToTsQuery / PlainToTsQuery for FTS.
        // plainto_tsquery: converts free-form user text to a tsquery safely.
        // Never use to_tsquery directly — it throws on special characters.
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim();

            // baseQuery ← correct name (NOT "queryable")
            baseQuery = baseQuery.Where(p =>
                EF.Functions.ToTsVector(
                    "english",
                    p.Name
                    + " " + (p.Description ?? "")
                    + " " + (p.Barcode ?? ""))
                .Matches(EF.Functions.PlainToTsQuery("english", searchTerm)));
        }

        // ── Total count ────────────────────────────────────────────────────────
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

        // ── Paginate and project ───────────────────────────────────────────────
        var products = await baseQuery
            .OrderByDescending(p => p.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(p => p.Images)
            .ToListAsync(ct);

        // Collect all CategoryIds and SubCategoryIds in one batch query
        var categoryIds = products.Where(p => p.CategoryId.HasValue)
                                     .Select(p => p.CategoryId!.Value)
                                     .Distinct()
                                     .ToList();
        var subCategoryIds = products.Where(p => p.SubCategoryId.HasValue)
                                     .Select(p => p.SubCategoryId!.Value)
                                     .Distinct()
                                     .ToList();

        var categoryNames = categoryIds.Count > 0
            ? await _db.Categories
                  .AsNoTracking()
                  .Where(c => categoryIds.Contains(c.Id) && !c.IsDeleted)
                  .ToDictionaryAsync(c => c.Id, c => c.Name, ct)
            : new Dictionary<Guid, string>();

        var subCategoryNames = subCategoryIds.Count > 0
            ? await _db.SubCategories
                  .AsNoTracking()
                  .Where(s => subCategoryIds.Contains(s.Id) && !s.IsDeleted)
                  .ToDictionaryAsync(s => s.Id, s => s.Name, ct)
            : new Dictionary<Guid, string>();

        var items = products.Select(p => p.ToListDto(
            categoryName: p.CategoryId.HasValue
                                 ? categoryNames.GetValueOrDefault(p.CategoryId.Value)
                                 : null,
            subCategoryName: p.SubCategoryId.HasValue
                                 ? subCategoryNames.GetValueOrDefault(p.SubCategoryId.Value)
                                 : null))
            .ToList()
            .AsReadOnly();

        return new PagedResult<ProductListDto>
        {
            Items = items,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    // ── Barcode lookup ────────────────────────────────────────────────────────

    public Task<Product?> GetByBarcodeAsync(
        Guid retailerId,
        string barcode,
        CancellationToken ct)
        => _db.Products
              .AsNoTracking()
              .FirstOrDefaultAsync(
                  p => p.RetailerId == retailerId
                    && p.Barcode == barcode
                    && !p.IsDeleted,
                  ct);

    // ── By-ID with images ────────────────────────────────────────────────────

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
                    && p.RetailerId == retailerId
                    && !p.IsDeleted,
                  ct);
}
