using Application.Features.Customer.VirtualTryOn.DTOs;
using Domain.Enums.Customer;

namespace Application.Interfaces.Services.Customer;

public interface IVirtualTryOnService
{
    Task<TryOnResultDto> ProcessTryOnAsync(
        Guid customerId, 
        Guid productId, 
        TryOnSessionType sessionType, 
        Domain.Entities.Customer.Avatar? avatar, 
        CancellationToken ct);
}
