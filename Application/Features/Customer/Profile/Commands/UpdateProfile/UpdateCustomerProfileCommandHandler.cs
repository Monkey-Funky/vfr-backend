using Application.Features.Customer.Auth.DTOs;
using Application.Features.Customer.Auth.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Customer;

namespace Application.Features.Customer.Profile.Commands.UpdateProfile;

public sealed class UpdateCustomerProfileCommandHandler
    : IRequestHandler<UpdateCustomerProfileCommand, Result<CustomerProfileDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<UpdateCustomerProfileCommandHandler> _logger;

    public UpdateCustomerProfileCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        ILogger<UpdateCustomerProfileCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<CustomerProfileDto>> Handle(
        UpdateCustomerProfileCommand command,
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

        customer.UpdateProfile(
            command.FullName,
            command.PhoneNumber,
            command.DateOfBirth,
            command.Gender);

        await _unitOfWork.Repository<CustomerAccount>().UpdateAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Customer profile updated successfully. CustomerId: {CustomerId}", customerId);

        // Invalidate the cached profile so the next read reflects the updated data.
        await _cacheService.RemoveAsync($"cust_profile:{customerId:N}", cancellationToken);

        var profileDto = customer.ToProfileDto();
        return Result<CustomerProfileDto>.Success(profileDto, "Profile updated successfully.");
    }
}
