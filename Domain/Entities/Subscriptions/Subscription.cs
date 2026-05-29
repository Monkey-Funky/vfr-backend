using Domain.Enums.Subscription;
using Domain.Exceptions;

namespace Domain.Entities.Subscriptions;

/// <summary>
/// Represents a retailer's subscription to the VFR platform.
/// Enforces the subscription status state machine in domain methods.
///
/// Valid State Transitions:
///   None             → Trial            : StartTrial()
///   None             → Active           : Activate()  (direct plan selection)
///   Trial            → Active           : Activate()
///   Active           → PendingDowngrade : SetPendingDowngrade()
///   Active           → Cancelled        : Cancel()
///   Active           → Expired          : Expire()
///   PendingDowngrade → Active           : Activate()  (on renewal; applies pending plan)
///   PendingDowngrade → Cancelled        : Cancel()
///   PendingDowngrade → Expired          : Expire()
///   Any (excl. Cancelled) → Cancelled  : Cancel()
///
/// Invalid transitions throw BusinessRuleException(code: "INVALID_SUBSCRIPTION_TRANSITION").
///
/// DB Schema Note:
///   The following properties require columns beyond B.3 of 02-DatabaseSchema.md.
///   Add them in the EF migration for this feature:
///     - pending_downgrade_plan_id  uuid      NULL
///     - pending_downgrade_eff_at   timestamptz NULL
///     - is_recurring_enabled       boolean   NOT NULL DEFAULT true
/// </summary>
public sealed class Subscription : BaseEntity
{
    // ── Identifiers ───────────────────────────────────────────────────────────

    public Guid RetailerId { get; private set; }
    public Guid PlanId { get; private set; }

    // ── State ─────────────────────────────────────────────────────────────────

    public SubscriptionStatus Status { get; private set; }

    // ── Dates ─────────────────────────────────────────────────────────────────

    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public DateTime? TrialEndsAt { get; private set; }

    // ── Downgrade Scheduling ──────────────────────────────────────────────────

    /// <summary>
    /// The plan to switch to at the next renewal, set when a downgrade is requested.
    /// Null unless Status == PendingDowngrade.
    /// </summary>
    public Guid? PendingDowngradePlanId { get; private set; }

    /// <summary>
    /// The UTC date when the pending downgrade will take effect (= current EndDate).
    /// Null unless Status == PendingDowngrade.
    /// </summary>
    public DateTime? PendingDowngradeEffectiveAt { get; private set; }

    // ── Billing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// When true, the RecurringPaymentJob will automatically charge the retailer
    /// at the end of each billing cycle.
    /// </summary>
    public bool IsRecurringEnabled { get; private set; }

    // ── Computed Domain Properties ────────────────────────────────────────────

    /// <summary>
    /// True when the subscription is currently usable by the retailer.
    /// Per spec: returns true when Status == Active OR (Status == Trial AND TrialEndsAt > UtcNow).
    /// </summary>
    public bool IsActive =>
        Status == SubscriptionStatus.Active ||
        (Status == SubscriptionStatus.Trial && TrialEndsAt > DateTime.UtcNow);

    /// <summary>
    /// True when the subscription is in its Trial period and the trial has not ended.
    /// Kept as a separate convenience property for UI button state computations.
    /// </summary>
    public bool IsInTrial =>
        Status == SubscriptionStatus.Trial && TrialEndsAt > DateTime.UtcNow;

    // ── Navigation Properties ─────────────────────────────────────────────────

    public SubscriptionPlan Plan { get; private set; } = default!;
    public SubscriptionPlan? PendingDowngradePlan { get; private set; }

    // ── Private Constructor (EF Core) ─────────────────────────────────────────

    private Subscription() { }

    // ── Factory Method ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new Subscription for a retailer. Called by StartTrial and SelectPlan handlers.
    /// </summary>
    public static Subscription Create(
        Guid retailerId,
        Guid planId,
        SubscriptionStatus initialStatus,
        DateTime startDate,
        DateTime? endDate = null,
        DateTime? trialEndsAt = null)
    {
        if (retailerId == Guid.Empty)
            throw new BusinessRuleException("SUBSCRIPTION_RETAILER_REQUIRED", "RetailerId is required.");

        if (planId == Guid.Empty)
            throw new BusinessRuleException("SUBSCRIPTION_PLAN_REQUIRED", "PlanId is required.");

        return new Subscription
        {
            RetailerId = retailerId,
            PlanId = planId,
            Status = initialStatus,
            StartDate = startDate,
            EndDate = endDate,
            TrialEndsAt = trialEndsAt,
            IsRecurringEnabled = true
        };
    }

    // ── Domain Methods — State Machine ────────────────────────────────────────

    /// <summary>
    /// Transitions the subscription to Active state, setting the new billing end date.
    /// Valid from: None, Trial, Active (renewal), PendingDowngrade (renewal, applies pending plan).
    /// </summary>
    public void Activate(DateTime endDate)
    {
        const string ErrorCode = "INVALID_SUBSCRIPTION_TRANSITION";

        if (Status == SubscriptionStatus.Expired)
            throw new BusinessRuleException(ErrorCode,
                $"Cannot activate a subscription that is in '{Status}' status.");

        if (Status == SubscriptionStatus.Cancelled)
            throw new BusinessRuleException(ErrorCode,
                "Cannot activate a cancelled subscription. A new subscription must be created.");

        // If transitioning from PendingDowngrade, apply the scheduled plan change.
        if (Status == SubscriptionStatus.PendingDowngrade && PendingDowngradePlanId.HasValue)
        {
            PlanId = PendingDowngradePlanId.Value;
            PendingDowngradePlanId = null;
            PendingDowngradeEffectiveAt = null;
        }

        Status = SubscriptionStatus.Active;
        EndDate = endDate;
        TrialEndsAt = null;
        SetUpdatedAudit(null, DateTime.UtcNow);
    }

    /// <summary>
    /// Transitions the subscription to Expired state.
    /// Valid from: Active, PendingDowngrade.
    /// </summary>
    public void Expire()
    {
        const string ErrorCode = "INVALID_SUBSCRIPTION_TRANSITION";

        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.PendingDowngrade))
            throw new BusinessRuleException(ErrorCode,
                $"Cannot expire a subscription in '{Status}' status. " +
                "Only Active or PendingDowngrade subscriptions can expire.");

        Status = SubscriptionStatus.Expired;
        SetUpdatedAudit(null, DateTime.UtcNow);
    }

    /// <summary>
    /// Cancels the subscription from any non-terminal state.
    /// Valid from: Any state except Cancelled.
    /// R-002: Sets EndDate to now (immediate cancellation).
    ///        Throws SUBSCRIPTION_ALREADY_CANCELLED if already cancelled.
    /// </summary>
    public void Cancel()
    {
        if (Status == SubscriptionStatus.Cancelled)
            throw new BusinessRuleException(
                "SUBSCRIPTION_ALREADY_CANCELLED",
                "Subscription is already cancelled.");

        // Clear any pending downgrade if applicable
        if (Status == SubscriptionStatus.PendingDowngrade)
        {
            PendingDowngradePlanId = null;
            PendingDowngradeEffectiveAt = null;
        }

        Status = SubscriptionStatus.Cancelled;
        EndDate = DateTime.UtcNow;
        SetUpdatedAudit(null, DateTime.UtcNow);
    }

    /// <summary>
    /// Schedules a downgrade to take effect at the next renewal.
    /// Valid from: Active only.
    /// The current subscription remains Active until EndDate.
    /// </summary>
    public void SetPendingDowngrade(Guid newPlanId, DateTime effectiveAt)
    {
        const string ErrorCode = "INVALID_SUBSCRIPTION_TRANSITION";

        if (Status != SubscriptionStatus.Active)
            throw new BusinessRuleException(ErrorCode,
                $"Cannot schedule a downgrade from '{Status}' status. " +
                "Only Active subscriptions can be downgraded.");

        if (newPlanId == PlanId)
            throw new BusinessRuleException("DOWNGRADE_SAME_PLAN",
                "The requested plan is the same as the current plan. No downgrade needed.");

        Status = SubscriptionStatus.PendingDowngrade;
        PendingDowngradePlanId = newPlanId;
        PendingDowngradeEffectiveAt = effectiveAt;
        SetUpdatedAudit(null, DateTime.UtcNow);
    }

    /// <summary>
    /// Toggles automatic recurring payment on or off.
    /// </summary>
    public void ToggleRecurring()
    {
        IsRecurringEnabled = !IsRecurringEnabled;
        SetUpdatedAudit(null, DateTime.UtcNow);
    }

    /// <summary>
    /// Immediately upgrades the plan (no status change — only PlanId changes).
    /// Valid from: Active or PendingDowngrade.
    /// Any pending downgrade is cancelled by the upgrade.
    /// </summary>
    public void UpgradePlan(Guid newPlanId)
    {
        const string ErrorCode = "INVALID_SUBSCRIPTION_TRANSITION";

        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.PendingDowngrade))
            throw new BusinessRuleException(ErrorCode,
                $"Cannot upgrade a subscription in '{Status}' status.");

        if (newPlanId == PlanId)
            throw new BusinessRuleException("UPGRADE_SAME_PLAN",
                "The requested plan is the same as the current plan. No upgrade needed.");

        PlanId = newPlanId;
        Status = SubscriptionStatus.Active; // clear PendingDowngrade if set
        PendingDowngradePlanId = null;
        PendingDowngradeEffectiveAt = null;
        SetUpdatedAudit(null, DateTime.UtcNow);
    }
}