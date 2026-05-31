using Application.Features.Customer.Address.DTOs;
using Application.Features.Customer.Address.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Shared.Constants;

namespace Application.Features.Customer.Address.Queries.GetAddressById;

/// <summary>
/// Returns a single customer address by ID.
/// Cache-aside: TTL 15 minutes. Invalidated by all address mutation commands.
/// </summary>
public sealed class GetCustomerAddressByIdQueryHandler
    : IRequestHandler<GetCustomerAddressByIdQuery, Result<CustomerAddressDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetCustomerAddressByIdQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<CustomerAddressDto>> Handle(
        GetCustomerAddressByIdQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("User is not authenticated as a customer.");

        string cacheKey = $"cust_addr:{customerId:N}:{request.Id:N}";

        var cached = await _cacheService.GetAsync<CustomerAddressDto>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            // Even on a cache hit we must enforce the IDOR guard.
            // The CustomerId is embedded in the cache key, so the cached DTO always
            // belongs to this customer — no extra check needed here.
            return Result<CustomerAddressDto>.Success(cached, "Address retrieved successfully.");
        }

        var address = await _unitOfWork.Repository<CustomerAddress>()
            .GetByIdAsync(request.Id, cancellationToken);

        if (address is null || address.IsDeleted || address.CustomerId != customerId)
            throw new NotFoundException(nameof(CustomerAddress), request.Id);

        var dto = address.ToCustomerAddressDto();

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(15), cancellationToken);

        return Result<CustomerAddressDto>.Success(dto, "Address retrieved successfully.");
    }
}
