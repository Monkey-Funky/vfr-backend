using Application.Features.Customer.Auth.DTOs;
using Application.Features.Customer.Auth.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;


namespace Application.Features.Customer.Profile.Queries.GetProfile;

public sealed class GetCustomerProfileQueryHandler 
    : IRequestHandler<GetCustomerProfileQuery, Result<CustomerProfileDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetCustomerProfileQueryHandler> _logger;

    public GetCustomerProfileQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<GetCustomerProfileQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<CustomerProfileDto>> Handle(
        GetCustomerProfileQuery request, 
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("User is not authenticated as a customer.");

        var customer = await _unitOfWork.Repository<CustomerAccount>()
            .GetByIdAsync(customerId, cancellationToken);

        if (customer is null || customer.IsDeleted)
        {
            _logger.LogWarning("Customer profile not found for Id: {CustomerId}", customerId);
            throw new NotFoundException(nameof(CustomerAccount), customerId);
        }

        var profileDto = customer.ToProfileDto();
        return Result<CustomerProfileDto>.Success(profileDto, "Customer profile retrieved successfully.");
    }
}
