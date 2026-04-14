using API.Controllers.BaseControllers;
using Application.Features.PaymentMethods.Commands.AddPaymentMethod;
using Application.Features.PaymentMethods.Commands.RemovePaymentMethod;
using Application.Features.PaymentMethods.Commands.SetDefaultPaymentMethod;
using Application.Features.PaymentMethods.DTOs;
using Application.Features.PaymentMethods.Queries.GetPaymentMethods;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.PaymentMethods;

/// <summary>Request body for adding a new payment method.</summary>
public sealed record AddPaymentMethodRequest(
    string ProviderType,
    string CardholderName,
    string CardNumberLast4,
    string ExpiryDate,
    string? StripePaymentMethodId,
    bool IsSaved = true,
    bool SetAsDefault = false);

/// <summary>
/// Payment method management for the authenticated retailer.
/// </summary>
[Route("api/retailers/{retailerId:guid}/payment-methods")]
[SwaggerTag("Payment methods — manage saved cards for subscription billing.")]
public sealed class PaymentMethodsController : RetailerBaseApiController
{
    public PaymentMethodsController() { }

    // ── GET /api/retailers/{retailerId}/payment-methods ───────────────────────

    [HttpGet]
    [SwaggerOperation(
        Summary = "List payment methods",
        Description = "Returns all active (non-deleted) saved cards for the authenticated retailer. " +
                      "The default card is listed first.")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PaymentMethodDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaymentMethods(
        Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        IReadOnlyList<PaymentMethodDto> result = await Sender.Send(
            new GetPaymentMethodsQuery(),
            cancellationToken);

        return OkResponse(result);
    }

    // ── POST /api/retailers/{retailerId}/payment-methods ──────────────────────

    [HttpPost]
    [SwaggerOperation(
        Summary = "Add payment method",
        Description = "Saves a new payment card for the retailer. " +
                      "The card must be pre-tokenized via Stripe Elements on the frontend.")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddPaymentMethod(
        Guid retailerId,
        [FromBody] AddPaymentMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<Guid> result = await Sender.Send(
            new AddPaymentMethodCommand(
                request.ProviderType,
                request.CardholderName,
                request.CardNumberLast4,
                request.ExpiryDate,
                request.StripePaymentMethodId,
                request.IsSaved,
                request.SetAsDefault),
            cancellationToken);

        return CreatedResponse(
            "GetPaymentMethodById",
            new { retailerId, methodId = result.Data },
            result.Data);
    }

    // ── GET /api/retailers/{retailerId}/payment-methods/{methodId} ────────────

    [HttpGet("{methodId:guid}", Name = "GetPaymentMethodById")]
    [SwaggerOperation(
        Summary = "Get payment method by ID",
        Description = "Returns the details of a single active payment method belonging to the authenticated retailer.")]
    [ProducesResponseType(typeof(ApiResponse<PaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentMethodById(
        Guid retailerId,
        Guid methodId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        IReadOnlyList<PaymentMethodDto> all = await Sender.Send(
            new GetPaymentMethodsQuery(),
            cancellationToken);

        PaymentMethodDto? dto = all.FirstOrDefault(pm => pm.Id == methodId);

        if (dto is null)
            throw new NotFoundException(nameof(PaymentMethodDto), methodId);

        return OkResponse(dto);
    }

    // ── DELETE /api/retailers/{retailerId}/payment-methods/{methodId} ─────────

    [HttpDelete("{methodId:guid}")]
    [SwaggerOperation(
        Summary = "Remove payment method",
        Description = "Soft-deletes the specified payment method. " +
                      "Returns 422 if the card is the current default/recurring card and other cards exist.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RemovePaymentMethod(
        Guid retailerId,
        Guid methodId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        await Sender.Send(
            new RemovePaymentMethodCommand(methodId),
            cancellationToken);

        return NoContentResponse();
    }

    // ── PUT /api/retailers/{retailerId}/payment-methods/{methodId}/default ────

    [HttpPut("{methodId:guid}/default")]
    [SwaggerOperation(
        Summary = "Set default payment method",
        Description = "Designates the specified card as the retailer's default/recurring billing card. " +
                      "All other cards are simultaneously un-set as default. Atomic operation.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultPaymentMethod(
        Guid retailerId,
        Guid methodId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<bool> result = await Sender.Send(
            new SetDefaultPaymentMethodCommand(methodId),
            cancellationToken);

        return OkResponse(result.Data, result.Message);
    }
}