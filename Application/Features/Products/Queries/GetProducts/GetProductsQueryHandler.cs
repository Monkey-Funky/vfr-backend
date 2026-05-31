using Application.Features.Products.DTOs;
using Application.Interfaces.Services;
using Shared.Constants;

namespace Application.Features.Products.Queries.GetProducts;

/// <summary>
/// Returns paginated products for the authenticated retailer.
/// Cache-aside: TTL 5 minutes. Invalidated by Create/Update/Delete/Toggle product commands.
/// </summary>
public sealed class GetProductsQueryHandler
    : IRequestHandler<GetProductsQuery, PagedResult<ProductListDto>>
{
    private readonly IProductRepository _productRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetProductsQueryHandler(
        IProductRepository productRepo,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _productRepo = productRepo;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<ProductListDto>> Handle(
        GetProductsQuery query,
        CancellationToken cancellationToken)
    {
        // RetailerId is ALWAYS sourced from the JWT — never from the query body.
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        string cacheKey =
            $"products:{retailerId:N}:" +
            $"p{query.PageNumber}s{query.PageSize}" +
            $":cat{query.CategoryId?.ToString() ?? "null"}" +
            $":sub{query.SubCategoryId?.ToString() ?? "null"}" +
            $":status{query.Status ?? "null"}" +
            $":search{query.SearchTerm ?? "null"}";

        var cached = await _cacheService.GetAsync<PagedResult<ProductListDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var result = await _productRepo.GetProductsPagedAsync(
            query: query,
            retailerId: retailerId,
            ct: cancellationToken);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);

        return result;
    }
}
