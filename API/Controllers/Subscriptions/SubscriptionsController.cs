using Application.Features.Subscriptions.Commands.DowngradePlan;
using Application.Features.Subscriptions.Commands.SelectPlan;
using Application.Features.Subscriptions.Commands.StartTrial;
using Application.Features.Subscriptions.Commands.SubmitSaasEnquiry;
using Application.Features.Subscriptions.Commands.ToggleRecurringPayment;
using Application.Features.Subscriptions.Commands.UpgradePlan;
using Application.Features.Subscriptions.DTOs;
using Application.Features.Subscriptions.Queries.GetCurrentSubscription;
using Application.Features.Subscriptions.Queries.GetCurrentSubscriptionDetails;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Subscriptions;

/// <summary>Request body for SelectPlan.</summary>
public sealed record SelectPlanRequest(
    Guid PlanId,
    Guid PaymentMethodId,
    string BillingCycle);

/// <summary>Request body for UpgradePlan.</summary>
public sealed record UpgradePlanRequest(
    Guid NewPlanId,
    Guid PaymentMethodId);

/// <summary>Request body for DowngradePlan.</summary>
public sealed record DowngradePlanRequest(
    Guid NewPlanId);

/// <summary>
/// Subscription lifecycle management for the authenticated retailer.
/// Handles trial activation, plan selection, upgrades, downgrades,
/// and recurring billing settings.
/// </summary>
[Route("api/retailers/{retailerId:guid}")]
[SwaggerTag("Subscription lifecycle — trial, plan selection, upgrade, downgrade, and billing settings.")]
public sealed class SubscriptionsController : BaseApiController
{
    public SubscriptionsController() { }

    // ── POST /api/retailers/{retailerId}/subscriptions/trial ──────────────────

    /// <summary>Starts a 14-day free trial for a retailer with no existing subscription.</summary>
    [HttpPost("subscriptions/trial")]
    [SwaggerOperation(
        Summary = "Start free trial",
        Description = "Activates a 14-day free trial on the Basic Monthly plan. " +
                      "Fails if the retailer already has an existing subscription or trial. " +
                      "No payment method required.")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionSummaryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> StartTrial(
        Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<SubscriptionSummaryDto> result = await Sender.Send(
            new StartTrialCommand(),
            cancellationToken);

        return CreatedResponse(
            "GetCurrentSubscription",
            new { retailerId },
            result.Data!);
    }

    // ── POST /api/retailers/{retailerId}/subscriptions/select ─────────────────

    /// <summary>Selects and activates a subscription plan with immediate payment.</summary>
    [HttpPost("subscriptions/select")]
    [SwaggerOperation(
        Summary = "Select subscription plan",
        Description = "Charges the specified payment method and activates the selected plan immediately. " +
                      "Creates a SubscriptionPayment record and a Subscription in Active status. " +
                      "Transitions an existing Trial subscription to Active if present.")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> SelectPlan(
        Guid retailerId,
        [FromBody] SelectPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<Guid> result = await Sender.Send(
            new SelectPlanCommand(
                request.PlanId,
                request.PaymentMethodId,
                request.BillingCycle),
            cancellationToken);

        return CreatedResponse(
            "GetCurrentSubscription",
            new { retailerId },
            result.Data);
    }

    // ── POST /api/retailers/{retailerId}/subscriptions/upgrade ────────────────

    /// <summary>Immediately upgrades the subscription to a higher-tier plan with prorated charge.</summary>
    [HttpPost("subscriptions/upgrade")]
    [SwaggerOperation(
        Summary = "Upgrade subscription plan",
        Description = "Upgrades the active subscription to a higher-tier plan with immediate effect. " +
                      "A prorated charge is calculated for the remaining days in the current billing cycle. " +
                      "Any pending downgrade is cancelled by the upgrade.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> UpgradePlan(
        Guid retailerId,
        [FromBody] UpgradePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<bool> result = await Sender.Send(
            new UpgradePlanCommand(
                request.NewPlanId,
                request.PaymentMethodId),
            cancellationToken);

        return OkResponse(result.Data, result.Message);
    }

    // ── POST /api/retailers/{retailerId}/subscriptions/downgrade ──────────────

    /// <summary>Schedules a plan downgrade to take effect at the next renewal date.</summary>
    [HttpPost("subscriptions/downgrade")]
    [SwaggerOperation(
        Summary = "Schedule plan downgrade",
        Description = "Schedules a downgrade to a lower-tier plan at the current subscription's renewal date. " +
                      "The current plan remains Active until the end date. No charge applies immediately.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DowngradePlan(
        Guid retailerId,
        [FromBody] DowngradePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<bool> result = await Sender.Send(
            new DowngradePlanCommand(request.NewPlanId),
            cancellationToken);

        return OkResponse(result.Data, result.Message);
    }

    // ── GET /api/retailers/{retailerId}/subscription/current ──────────────────

    /// <summary>Returns the retailer's current subscription with UI button state flags.</summary>
    [HttpGet("subscription/current", Name = "GetCurrentSubscription")]
    [SwaggerOperation(
        Summary = "Get current subscription",
        Description = "Returns the retailer's active subscription with plan details and " +
                      "button state flags (CanUpgrade, CanDowngrade, CanCancel, CanStartTrial) " +
                      "for use by the frontend pricing page.")]
    [ProducesResponseType(typeof(ApiResponse<CurrentSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentSubscription(
        Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        CurrentSubscriptionDto dto = await Sender.Send(
            new GetCurrentSubscriptionQuery(),
            cancellationToken);

        return OkResponse(dto);
    }

    // ── GET /api/retailers/{retailerId}/subscription/current/details ──────────

    /// <summary>Returns the retailer's current subscription with the full plan feature list.</summary>
    [HttpGet("subscription/current/details")]
    [SwaggerOperation(
        Summary = "Get current subscription details",
        Description = "Returns the retailer's active subscription with the complete plan feature list " +
                      "(commission rate, limits, SaaS flags, pending downgrade info). " +
                      "Used by the account billing detail page.")]
    [ProducesResponseType(typeof(ApiResponse<CurrentSubscriptionDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentSubscriptionDetails(
        Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        CurrentSubscriptionDetailsDto dto = await Sender.Send(
            new GetCurrentSubscriptionDetailsQuery(),
            cancellationToken);

        return OkResponse(dto);
    }

    // ── PATCH /api/retailers/{retailerId}/subscription/recurring ──────────────

    /// <summary>Toggles automatic recurring billing on or off for the current subscription.</summary>
    [HttpPatch("subscription/recurring")]
    [SwaggerOperation(
        Summary = "Toggle recurring billing",
        Description = "Enables or disables automatic recurring payment for the active subscription. " +
                      "When disabled, the RecurringPaymentJob skips this retailer at renewal time. " +
                      "Not available for Expired, Cancelled, or None-status subscriptions.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ToggleRecurringPayment(
        Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<bool> result = await Sender.Send(
            new ToggleRecurringPaymentCommand(),
            cancellationToken);

        return OkResponse(result.Data, result.Message);
    }

    // ── POST /api/retailers/{retailerId}/subscriptions/saas-enquiry ───────────

    /// <summary>Submits a SaaS/White-Label interest enquiry for the authenticated retailer.</summary>
    [HttpPost("subscriptions/saas-enquiry")]
    [SwaggerOperation(
        Summary = "Submit SaaS enquiry",
        Description = "Submits a SaaS/White-Label enquiry on behalf of the authenticated retailer. " +
                      "Triggers an admin notification. " +
                      "Only one pending enquiry per retailer is allowed at a time.")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitSaasEnquiry(
        Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<Guid> result = await Sender.Send(
            new SubmitSaasEnquiryCommand(),
            cancellationToken);

        return CreatedResponse(
            "GetCurrentSubscription",
            new { retailerId },
            result.Data);
    }
}