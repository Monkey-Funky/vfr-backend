using Application.Features.Customer.Address.DTOs;
using Application.Features.Customer.Address.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;
using Shared.Constants;

namespace Application.Features.Customer.Address.Queries.GetAddresses;

/// <summary>
/// Returns all addresses for the authenticated customer.
/// Cache-aside: TTL 15 minutes. Invalidated by Add/Update/Delete/SetDefault address commands.
/// </summary>
public sealed class GetCustomerAddressesQueryHandler
    : IRequestHandler<GetCustomerAddressesQuery, Result<IReadOnlyList<CustomerAddressDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetCustomerAddressesQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<IReadOnlyList<CustomerAddressDto>>> Handle(
        GetCustomerAddressesQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("User is not authenticated as a customer.");

        string cacheKey = CacheKeys.CustomerAddresses(customerId);

        var cached = await _cacheService.GetAsync<List<CustomerAddressDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return Result<IReadOnlyList<CustomerAddressDto>>.Success(cached, "Addresses retrieved successfully.");

        var addresses = await _unitOfWork.Repository<CustomerAddress>()
            .FindAsync(a => a.CustomerId == customerId && !a.IsDeleted, cancellationToken);

        var dtos = addresses.Select(a => a.ToCustomerAddressDto()).ToList();

        await _cacheService.SetAsync(cacheKey, dtos, TimeSpan.FromMinutes(15), cancellationToken);

        return Result<IReadOnlyList<CustomerAddressDto>>.Success(dtos, "Addresses retrieved successfully.");
    }
}
