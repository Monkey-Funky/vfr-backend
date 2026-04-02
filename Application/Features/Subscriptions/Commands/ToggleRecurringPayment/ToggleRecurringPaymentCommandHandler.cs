using Domain.Entities.Subscriptions;
using Domain.Enums;

namespace Application.Features.Subscriptions.Commands.ToggleRecurringPayment;

public sealed class ToggleRecurringPaymentCommandHandler
    : IRequestHandler<ToggleRecurringPaymentCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public ToggleRecurringPaymentCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<bool>> Handle(
        ToggleRecurringPaymentCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        Subscription subscription = await _unitOfWork
            .Repository<Subscription>()
            .FirstOrDefaultAsync(s => s.RetailerId == retailerId, cancellationToken)
            ?? throw new NotFoundException(
                "No subscription found for this retailer.");

        // Only allow toggling when subscription is in an active billing state.
        if (subscription.Status is SubscriptionStatus.None
                                  or SubscriptionStatus.Expired
                                  or SubscriptionStatus.Cancelled)
        {
            throw new BusinessRuleException(
                "RECURRING_TOGGLE_NOT_ALLOWED",
                $"Recurring payment settings cannot be changed for a subscription " +
                $"in '{subscription.Status}' status.");
        }

        // Toggle via domain method.
        subscription.ToggleRecurring();

        await _unitOfWork.Repository<Subscription>().UpdateAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache.
        await _cacheService.RemoveByPrefixAsync(
            $"subscriptions:{retailerId}:", cancellationToken);

        bool newValue = subscription.IsRecurringEnabled;
        string message = newValue
            ? "Automatic recurring billing has been enabled. Your subscription will renew automatically."
            : "Automatic recurring billing has been disabled. You will need to renew manually.";

        return Result<bool>.Success(newValue, message);
    }
}