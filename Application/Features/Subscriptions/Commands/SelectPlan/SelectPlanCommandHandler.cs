using Domain.Entities.Retailer;
using Domain.Entities.Subscriptions;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Subscriptions.Commands.SelectPlan;

public sealed class SelectPlanCommandHandler
    : IRequestHandler<SelectPlanCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPaymentGatewayService _paymentGateway;
    private readonly ICacheService _cacheService;

    public SelectPlanCommandHandler(
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

    public async Task<Result<Guid>> Handle(
        SelectPlanCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // ── Load and validate the subscription plan ───────────────────────────
        SubscriptionPlan plan = await _unitOfWork
            .Repository<SubscriptionPlan>()
            .FirstOrDefaultAsync(p => p.Id == command.PlanId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), command.PlanId);

        if (plan.BillingCycle != command.BillingCycle)
            throw new BusinessRuleException(
                "BILLING_CYCLE_MISMATCH",
                $"The selected plan uses '{plan.BillingCycle}' billing, " +
                $"but '{command.BillingCycle}' was requested.");

        // ── Load and validate the payment method (IDOR guard) ─────────────────
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
                "The selected payment method does not have a valid Stripe token. " +
                "Please re-add your payment method.");

        // BUG-007 FIX: Reject expired payment method before touching Stripe.
        if (paymentMethod.IsExpired)
            throw new BusinessRuleException(
                "PAYMENT_METHOD_EXPIRED",
                $"Card ending in {paymentMethod.CardNumberLast4} expired on {paymentMethod.ExpiryDate}. " +
                "Please add a valid payment method and try again.");

        // ── Check for an existing subscription (pre-check only — not the lock) ─
        // This is a quick UX check. The authoritative check happens inside the transaction
        // with a tracked load to prevent the BUG-001 race condition.
        Subscription? preCheckSubscription = await _unitOfWork
            .Repository<Subscription>()
            .FirstOrDefaultAsync(s => s.RetailerId == retailerId, cancellationToken);

        if (preCheckSubscription is not null
            && preCheckSubscription.Status == SubscriptionStatus.Active
            && preCheckSubscription.PlanId == plan.Id)
        {
            throw new BusinessRuleException(
                "PLAN_ALREADY_ACTIVE",
                "You are already subscribed to this plan.");
        }

        // ── Determine billing period dates ────────────────────────────────────
        DateTime now = DateTime.UtcNow;
        DateTime periodEnd = plan.BillingCycle switch
        {
            "Yearly" => now.AddYears(1),
            "Monthly" => now.AddMonths(1),
            _ => now.AddYears(1) // SaaS defaults to yearly
        };

        Guid subscriptionId = Guid.Empty;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // ── BUG-001 FIX (Layer 2): Load tracked entity for optimistic concurrency ──
                // By loading the entity WITH tracking and using xmin concurrency token,
                // any concurrent modification by another request will cause
                // DbUpdateConcurrencyException on SaveChangesAsync.
                Subscription? existingSubscription = preCheckSubscription is null
                    ? null
                    : await _unitOfWork.GetTrackedByIdAsync<Subscription>(
                        preCheckSubscription.Id, ct);

                // Re-verify state inside transaction — the state could have changed since
                // the pre-check due to a concurrent request.
                if (existingSubscription is not null
                    && existingSubscription.Status == SubscriptionStatus.Active
                    && existingSubscription.PlanId == plan.Id)
                {
                    throw new BusinessRuleException(
                        "PLAN_ALREADY_ACTIVE",
                        "You are already subscribed to this plan.");
                }

                // ── Step 1: Create payment record (Pending) ───────────────────
                SubscriptionPayment payment = SubscriptionPayment.Create(
                    retailerId: retailerId,
                    subscriptionPlanId: plan.Id,
                    paymentMethodId: command.PaymentMethodId,
                    amount: plan.PriceAmount,
                    currency: plan.Currency,
                    periodStartDate: now,
                    periodEndDate: periodEnd,
                    isRecurring: false);

                await _unitOfWork.Repository<SubscriptionPayment>().AddAsync(payment, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                // ── Step 2: Mark payment as Processing, save ──────────────────
                payment.MarkProcessing();
                await _unitOfWork.SaveChangesAsync(ct);

                // ── Step 3: Charge Stripe ─────────────────────────────────────
                PaymentResult result = await _paymentGateway.ChargeAsync(
                    paymentMethod.StripePaymentMethodId!,
                    plan.PriceAmount,
                    plan.Currency.ToLowerInvariant(),
                    ct);

                if (!result.Success)
                {
                    payment.MarkFailed();
                    await _unitOfWork.SaveChangesAsync(ct);
                    throw new ExternalServiceException(
                        "Stripe",
                        $"Payment failed: {result.ErrorMessage}. Please try a different payment method.");
                }

                // ── BUG-002 FIX: Save Completed + StripePaymentIntentId FIRST ──
                // This ensures the payment is durably recorded even if the subscription
                // step below fails. The reconciliation job can detect this state.
                payment.MarkCompleted(result.StripePaymentIntentId!);
                await _unitOfWork.SaveChangesAsync(ct);

                // ── Step 4: Create or activate the subscription ───────────────
                if (existingSubscription is null)
                {
                    // New subscription (no prior record for this retailer)
                    Subscription subscription = Subscription.Create(
                        retailerId: retailerId,
                        planId: plan.Id,
                        initialStatus: SubscriptionStatus.Active,
                        startDate: now,
                        endDate: periodEnd);

                    await _unitOfWork.Repository<Subscription>().AddAsync(subscription, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                    subscriptionId = subscription.Id;
                }
                else
                {
                    // Transition from Trial → Active (or re-activating from another state)
                    existingSubscription.Activate(periodEnd);

                    if (existingSubscription.PlanId != plan.Id)
                        existingSubscription.UpgradePlan(plan.Id);

                    await _unitOfWork.Repository<Subscription>().UpdateAsync(existingSubscription, ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                    subscriptionId = existingSubscription.Id;
                }

            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // BUG-001 FIX (Layer 2): Concurrent modification detected via xmin token.
            // The other request already changed the subscription state.
            throw new ConflictException(
                "A concurrent subscription operation was detected. " +
                "Please check your subscription status and retry if needed.");
        }
        catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
        {
            // BUG-001 FIX (Layer 1): DB unique constraint on retailer_id fired.
            // Another request already created a subscription for this retailer.
            throw new ConflictException(
                "A subscription was already created for this account by a concurrent request. " +
                "Please check your current subscription status.");
        }

        // ── Invalidate relevant caches ────────────────────────────────────────
        await _cacheService.RemoveByPrefixAsync(
            $"subscriptions:{retailerId}:", cancellationToken);

        return Result<Guid>.Success(subscriptionId, "Plan selected and activated successfully.");
    }

    /// <summary>
    /// Detects PostgreSQL unique constraint violations.
    /// Error code "23505" = unique_violation in PostgreSQL.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505") == true
        || ex.InnerException?.Message.Contains("unique constraint") == true
        || ex.InnerException?.Message.Contains("unique_violation") == true;
}