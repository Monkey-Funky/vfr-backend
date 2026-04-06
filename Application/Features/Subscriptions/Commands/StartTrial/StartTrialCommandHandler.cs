using Application.Features.Subscriptions.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Subscriptions.Commands.StartTrial;

public sealed class StartTrialCommandHandler
    : IRequestHandler<StartTrialCommand, Result<SubscriptionSummaryDto>>
{
    // The trial plan is the Basic Monthly plan (lowest tier available for trial).
    // In production, this could be a configurable setting or a dedicated "Trial" plan.
    private static readonly Guid TrialPlanId =
        new("11111111-1111-1111-1111-111111111001"); // Basic Monthly (from seed data)

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public StartTrialCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<SubscriptionSummaryDto>> Handle(
        StartTrialCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // ── Guard: Retailer must have no existing subscription ────────────────
        bool hasExistingSubscription = await _unitOfWork
            .Repository<Subscription>()
            .AnyAsync(s => s.RetailerId == retailerId, cancellationToken);

        if (hasExistingSubscription)
            throw new BusinessRuleException(
                "TRIAL_ALREADY_EXISTS",
                "A subscription or trial already exists for this account. " +
                "To start a new trial, please contact support.");

        // ── Guard: Verify trial plan exists ───────────────────────────────────
        SubscriptionPlan? plan = await _unitOfWork
            .Repository<SubscriptionPlan>()
            .FirstOrDefaultAsync(p => p.Id == TrialPlanId && p.IsActive, cancellationToken)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), TrialPlanId);

        // ── Create subscription in Trial status ───────────────────────────────
        DateTime now = DateTime.UtcNow;
        DateTime trialEndsAt = now.AddDays(14);

        Subscription subscription = Subscription.Create(
            retailerId: retailerId,
            planId: plan.Id,
            initialStatus: SubscriptionStatus.Trial,
            startDate: now,
            trialEndsAt: trialEndsAt);

        await _unitOfWork.Repository<Subscription>().AddAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── Invalidate subscription plans cache (per-retailer subscription cache) ──
        await _cacheService.RemoveByPrefixAsync(
            $"subscriptions:{retailerId}:", cancellationToken);

        SubscriptionSummaryDto dto = new(
            SubscriptionId: subscription.Id,
            PlanId: plan.Id,
            PlanName: plan.Name,
            Tier: plan.Tier,
            Status: subscription.Status,
            StartDate: subscription.StartDate,
            EndDate: subscription.EndDate,
            TrialEndsAt: subscription.TrialEndsAt,
            IsRecurringEnabled: subscription.IsRecurringEnabled,
            IsActive: subscription.IsActive,
            IsInTrial: subscription.IsInTrial);

        return Result<SubscriptionSummaryDto>.Success(
            dto,
            $"Your 14-day free trial has started and will end on {trialEndsAt:MMMM dd, yyyy}.");
    }
}