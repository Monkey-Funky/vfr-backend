using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Subscriptions.Commands.UpgradePlan;

public sealed class UpgradePlanCommandHandler
    : IRequestHandler<UpgradePlanCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPaymentGatewayService _paymentGateway;
    private readonly ICacheService _cacheService;

    public UpgradePlanCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPaymentGatewayService paymentGateway,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _paymentGateway = paymentGateway;
        _cacheService = cacheService;
    }

    public async Task<Result<bool>> Handle(
        UpgradePlanCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // ── Load current subscription ─────────────────────────────────────────
        Subscription subscription = await _unitOfWork
            .Repository<Subscription>()
            .FirstOrDefaultAsync(s => s.RetailerId == retailerId, cancellationToken)
            ?? throw new NotFoundException(
                "No subscription found. Please select a plan first.");

        if (subscription.Status is not (SubscriptionStatus.Active
                                       or SubscriptionStatus.PendingDowngrade))
        {
            throw new BusinessRuleException(
                "UPGRADE_NOT_ALLOWED",
                $"Upgrades are only available for Active subscriptions. " +
                $"Current status: {subscription.Status}.");
        }

        // ── Load current and new plans ────────────────────────────────────────
        SubscriptionPlan currentPlan = await _unitOfWork
            .Repository<SubscriptionPlan>()
            .FirstOrDefaultAsync(p => p.Id == subscription.PlanId, cancellationToken)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), subscription.PlanId);

        SubscriptionPlan newPlan = await _unitOfWork
            .Repository<SubscriptionPlan>()
            .FirstOrDefaultAsync(
                p => p.Id == command.NewPlanId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), command.NewPlanId);

        if (newPlan.PriceAmount <= currentPlan.PriceAmount)
            throw new BusinessRuleException(
                "UPGRADE_TO_LOWER_PLAN",
                "The requested plan has a lower or equal price. " +
                "Use DowngradePlan to switch to a lower-tier plan.");

        // ── Load and validate payment method ──────────────────────────────────
        PaymentMethod paymentMethod = await _unitOfWork
            .Repository<PaymentMethod>()
            .FirstOrDefaultAsync(
                pm => pm.Id == command.PaymentMethodId
                   && pm.RetailerId == retailerId
                   && !pm.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentMethod), command.PaymentMethodId);

        if (string.IsNullOrWhiteSpace(paymentMethod.StripePaymentMethodId))
            throw new BusinessRuleException(
                "PAYMENT_METHOD_NOT_TOKENIZED",
                "The selected payment method does not have a valid Stripe token.");

        // BUG-007 FIX: Reject expired payment method before touching Stripe.
        if (paymentMethod.IsExpired)
            throw new BusinessRuleException(
                "PAYMENT_METHOD_EXPIRED",
                $"Card ending in {paymentMethod.CardNumberLast4} expired on {paymentMethod.ExpiryDate}. " +
                "Please add a valid payment method and try again.");

        // ── BUG-006 FIX: Calculate prorated charge with actual dates and decimal arithmetic ──
        DateTime now = DateTime.UtcNow;

        decimal proratedAmount = CalculateProratedCharge(
            oldPlanPrice: currentPlan.PriceAmount,
            newPlanPrice: newPlan.PriceAmount,
            periodStartDate: subscription.StartDate,
            periodEndDate: subscription.EndDate ?? now.AddMonths(1));

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // BUG-001 FIX: Load tracked subscription inside transaction
                Subscription? trackedSubscription =
                    await _unitOfWork.GetTrackedByIdAsync<Subscription>(subscription.Id, ct);

                if (trackedSubscription is null)
                    throw new NotFoundException(nameof(Subscription), subscription.Id);

                // ── Step 1: Create prorated payment record ────────────────────
                SubscriptionPayment payment = SubscriptionPayment.Create(
                    retailerId: retailerId,
                    subscriptionPlanId: newPlan.Id,
                    paymentMethodId: command.PaymentMethodId,
                    amount: proratedAmount,
                    currency: newPlan.Currency,
                    periodStartDate: now,
                    periodEndDate: trackedSubscription.EndDate ?? now.AddMonths(1),
                    isRecurring: false);

                await _unitOfWork.Repository<SubscriptionPayment>().AddAsync(payment, ct);
                payment.MarkProcessing();
                await _unitOfWork.SaveChangesAsync(ct);

                // ── Step 2: Charge Stripe ─────────────────────────────────────
                PaymentResult result = await _paymentGateway.ChargeAsync(
                    paymentMethod.StripePaymentMethodId!,
                    proratedAmount,
                    newPlan.Currency.ToLowerInvariant(),
                    ct);

                if (!result.Success)
                {
                    payment.MarkFailed();
                    await _unitOfWork.SaveChangesAsync(ct);
                    throw new ExternalServiceException(
                        "Stripe",
                        $"Prorated payment failed: {result.ErrorMessage}.");
                }

                // BUG-002 FIX: Save Completed payment BEFORE subscription update.
                payment.MarkCompleted(result.StripePaymentIntentId!);
                await _unitOfWork.SaveChangesAsync(ct);

                // ── Step 3: Apply the upgrade immediately ─────────────────────
                trackedSubscription.UpgradePlan(newPlan.Id);
                await _unitOfWork.Repository<Subscription>().UpdateAsync(trackedSubscription, ct);
                await _unitOfWork.SaveChangesAsync(ct);

            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // BUG-001 FIX: Concurrent upgrade detected — another request modified this subscription.
            throw new ConflictException(
                "A concurrent subscription operation was detected. " +
                "Please check your subscription status and retry if needed.");
        }

        // ── Invalidate caches ─────────────────────────────────────────────────
        await _cacheService.RemoveByPrefixAsync(
            $"subscriptions:{retailerId}:", cancellationToken);

        return Result<bool>.Success(true, $"Successfully upgraded to the '{newPlan.Name}' plan.");
    }

    
    private static decimal CalculateProratedCharge(
        decimal oldPlanPrice,
        decimal newPlanPrice,
        DateTime periodStartDate,
        DateTime periodEndDate)
    {
        DateTime now = DateTime.UtcNow;

        // Use actual days in this specific billing period (handles leap years, month lengths)
        decimal totalDays = (decimal)(periodEndDate - periodStartDate).TotalDays;
        decimal daysRemaining = (decimal)Math.Max((periodEndDate - now).TotalDays, 0.0);

        if (totalDays <= 0m)
            return 0m;

        // Pure decimal arithmetic — no double or int division
        decimal dailyDelta = (newPlanPrice - oldPlanPrice) / totalDays;
        decimal prorated = decimal.Round(
            dailyDelta * daysRemaining,
            2,
            MidpointRounding.AwayFromZero);

        return Math.Max(prorated, 0m);
    }
}