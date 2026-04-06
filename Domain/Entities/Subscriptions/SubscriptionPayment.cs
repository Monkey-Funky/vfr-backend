using Domain.Common;
using Domain.Enums.Subscription;

namespace Domain.Entities.Subscriptions;

/// <summary>
/// Immutable payment record for a subscription billing event.
/// Created on SelectPlan and UpgradePlan; updated by Stripe webhook handlers.
/// 
/// Fields match B.14 of 02-DatabaseSchema.md.
/// Extends BaseEntity for ID generation; EF config ignores unused audit fields.
/// </summary>
public sealed class SubscriptionPayment : BaseEntity
{
    // ── Identifiers ───────────────────────────────────────────────────────────

    public Guid RetailerId { get; private set; }
    public Guid SubscriptionPlanId { get; private set; }
    public Guid? PaymentMethodId { get; private set; }

    // ── Payment Details ───────────────────────────────────────────────────────

    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = default!;
    public SubscriptionPaymentStatus Status { get; private set; }
    public bool IsRecurring { get; private set; }

    // ── Subscription Period ───────────────────────────────────────────────────

    public DateTime PeriodStartDate { get; private set; }
    public DateTime PeriodEndDate { get; private set; }

    // ── Stripe Integration ────────────────────────────────────────────────────

    /// <summary>Set when Stripe confirms payment. Used for webhook correlation.</summary>
    public string? StripePaymentIntentId { get; private set; }

    /// <summary>Timestamp when payment was confirmed by Stripe.</summary>
    public DateTime? PaidAt { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    public SubscriptionPlan Plan { get; private set; } = default!;

    // ── Private Constructor (EF Core) ─────────────────────────────────────────

    private SubscriptionPayment() { }

    // ── Factory Method ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new payment record in Pending status.
    /// The record is transitioned to Completed or Failed after Stripe responds.
    /// </summary>
    public static SubscriptionPayment Create(
        Guid retailerId,
        Guid subscriptionPlanId,
        Guid? paymentMethodId,
        decimal amount,
        string currency,
        DateTime periodStartDate,
        DateTime periodEndDate,
        bool isRecurring = false)
    {
        return new SubscriptionPayment
        {
            RetailerId = retailerId,
            SubscriptionPlanId = subscriptionPlanId,
            PaymentMethodId = paymentMethodId,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            Status = SubscriptionPaymentStatus.Pending,
            IsRecurring = isRecurring,
            PeriodStartDate = periodStartDate,
            PeriodEndDate = periodEndDate
        };
    }

    // ── Mutation Methods ──────────────────────────────────────────────────────

    /// <summary>
    /// Marks the payment as successfully completed by Stripe.
    /// </summary>
    public void MarkCompleted(string stripePaymentIntentId)
    {
        Status = SubscriptionPaymentStatus.Completed;
        StripePaymentIntentId = stripePaymentIntentId;
        PaidAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the payment as failed. ErrorMessage is logged by the handler — never stored.
    /// </summary>
    public void MarkFailed()
    {
        Status = SubscriptionPaymentStatus.Failed;
    }

    /// <summary>
    /// Marks the payment as Processing (dispatched to Stripe, awaiting confirmation).
    /// </summary>
    public void MarkProcessing()
    {
        Status = SubscriptionPaymentStatus.Processing;
    }

    /// <summary>
    /// Marks the payment as Refunded.
    /// </summary>
    public void MarkRefunded()
    {
        Status = SubscriptionPaymentStatus.Refunded;
    }
}