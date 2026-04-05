namespace Application.Features.Products.Queries.GetProducts;


public sealed class GetProductsQueryHandler
    : IRequestHandler<GetProductsQuery, PagedResult<ProductListDto>>
{
    private readonly IProductRepository _productRepo;
    private readonly ICurrentUserService _currentUserService;

    public GetProductsQueryHandler(
        IProductRepository productRepo,
        ICurrentUserService currentUserService)
    {
        _productRepo = productRepo;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<ProductListDto>> Handle(
        GetProductsQuery query,
        CancellationToken cancellationToken)
    {
        // RetailerId is ALWAYS sourced from the JWT — never from the query body.
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        return await _productRepo.GetProductsPagedAsync(
            query: query,
            retailerId: retailerId,
            ct: cancellationToken);
    }
}