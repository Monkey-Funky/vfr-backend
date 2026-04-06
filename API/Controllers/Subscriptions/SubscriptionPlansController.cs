using Application.Features.Subscriptions.DTOs;
using Application.Features.Subscriptions.Queries.GetAllSubscriptionPlans;
using Application.Features.Subscriptions.Queries.GetSubscriptionPlanById;
using MediatR;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Subscriptions;

/// <summary>
/// Public subscription plan catalogue — no authentication required.
/// Provides plan listing (grouped by tier) and individual plan lookup
/// for the pricing page and plan comparison UI.
/// </summary>
[ApiController]
[AllowAnonymous]
[Produces("application/json")]
[Route("api/subscription-plans")]
[SwaggerTag("Subscription plan catalogue — publicly accessible pricing and feature information.")]
public sealed class SubscriptionPlansController : ControllerBase
{
    private ISender? _sender;

    /// <summary>Lazily resolved MediatR sender — see BaseApiController §2.2 for rationale.</summary>
    private ISender Sender =>
        _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    // ── GET /api/subscription-plans ───────────────────────────────────────────

    /// <summary>Returns all active subscription plans, grouped by tier.</summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get all subscription plans",
        Description = "Returns all active subscription plans grouped by tier " +
                      "(Basic, Standard, Enterprise, SaaS). " +
                      "Results are cached in Redis for 1 hour. " +
                      "Optionally filter by billing cycle: Monthly, Yearly, or SaaS.")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubscriptionPlanGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSubscriptionPlans(
        [FromQuery] string? billingCycle = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SubscriptionPlanGroupDto> result = await Sender.Send(
            new GetAllSubscriptionPlansQuery(billingCycle),
            cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<SubscriptionPlanGroupDto>>
            .SuccessResponse(result, "Subscription plans retrieved successfully."));
    }

    // ── GET /api/subscription-plans/{planId} ──────────────────────────────────

    /// <summary>Returns a single active subscription plan by ID.</summary>
    [HttpGet("{planId:guid}", Name = "GetSubscriptionPlanById")]
    [SwaggerOperation(
        Summary = "Get subscription plan by ID",
        Description = "Returns the full details of a single active subscription plan. " +
                      "Returns 404 if the plan does not exist or is inactive.")]
    [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscriptionPlanById(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        SubscriptionPlanDto dto = await Sender.Send(
            new GetSubscriptionPlanByIdQuery(planId),
            cancellationToken);

        return Ok(ApiResponse<SubscriptionPlanDto>.SuccessResponse(
            dto, "Subscription plan retrieved successfully."));
    }
}