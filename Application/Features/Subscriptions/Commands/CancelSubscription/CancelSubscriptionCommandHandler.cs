using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Subscriptions.Commands.CancelSubscription;

public sealed class CancelSubscriptionCommandHandler
    : IRequestHandler<CancelSubscriptionCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public CancelSubscriptionCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<bool>> Handle(
        CancelSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // Load subscription — RetailerId from JWT (never from route/body)
        Subscription subscription = await _unitOfWork
            .Repository<Subscription>()
            .FirstOrDefaultAsync(s => s.RetailerId == retailerId, cancellationToken)
            ?? throw new NotFoundException(
                "No subscription found for this retailer.");

        // Domain method throws BusinessRuleException("SUBSCRIPTION_ALREADY_CANCELLED")
        // if the subscription is already cancelled — prevents double cancellation.
        subscription.Cancel();

        await _unitOfWork.Repository<Subscription>().UpdateAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate subscription cache for this retailer
        await _cacheService.RemoveByPrefixAsync(
            $"subscriptions:{retailerId}:", cancellationToken);

        return Result<bool>.Success(
            true,
            "Your subscription has been cancelled successfully. " +
            "You will retain access until the end of your current billing period.");
    }
}