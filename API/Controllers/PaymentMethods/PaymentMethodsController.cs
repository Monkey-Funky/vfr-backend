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
/// Handles listing, adding, removing, and designating the default card.
/// </summary>
[Route("api/retailers/{retailerId:guid}/payment-methods")]
[SwaggerTag("Payment methods — manage saved cards for subscription billing.")]
public sealed class PaymentMethodsController : BaseApiController
{
    public PaymentMethodsController() { }

    // ── GET /api/retailers/{retailerId}/payment-methods ───────────────────────

    /// <summary>Lists all active payment methods for the authenticated retailer.</summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "List payment methods",
        Description = "Returns all active (non-deleted) saved cards for the authenticated retailer. " +
                      "The default card is listed first. Cardholder names are returned decrypted.")]
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

    /// <summary>Adds a new payment method to the authenticated retailer's account.</summary>
    [HttpPost]
    [SwaggerOperation(
        Summary = "Add payment method",
        Description = "Saves a new payment card for the retailer. " +
                      "The card must be pre-tokenized via Stripe Elements on the frontend — " +
                      "StripePaymentMethodId (pm_xxxx) is the resulting token. " +
                      "CardholderName is encrypted at rest using AES-256. " +
                      "Set SetAsDefault=true to designate this card as the recurring billing card.")]
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

    /// <summary>Returns a single payment method by ID.</summary>
    [HttpGet("{methodId:guid}", Name = "GetPaymentMethodById")]
    [SwaggerOperation(
        Summary = "Get payment method by ID",
        Description = "Returns the details of a single active payment method " +
                      "belonging to the authenticated retailer.")]
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

        // Delegate to the GetPaymentMethods handler and filter by ID.
        // A dedicated GetPaymentMethodByIdQuery may be added in a future iteration.
        IReadOnlyList<PaymentMethodDto> all = await Sender.Send(
            new GetPaymentMethodsQuery(),
            cancellationToken);

        PaymentMethodDto? dto = all.FirstOrDefault(pm => pm.Id == methodId);

        if (dto is null)
            throw new NotFoundException(nameof(PaymentMethodDto), methodId);

        return OkResponse(dto);
    }

    // ── DELETE /api/retailers/{retailerId}/payment-methods/{methodId} ─────────

    /// <summary>Removes a payment method. Cannot remove the active default card.</summary>
    [HttpDelete("{methodId:guid}")]
    [SwaggerOperation(
        Summary = "Remove payment method",
        Description = "Soft-deletes the specified payment method. " +
                      "Returns 422 if the card is the current default/recurring card and other " +
                      "cards exist — designate another card as default first. " +
                      "Returns 404 if the card does not belong to this retailer.")]
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

    /// <summary>Sets the specified payment method as the default recurring billing card.</summary>
    [HttpPut("{methodId:guid}/default")]
    [SwaggerOperation(
        Summary = "Set default payment method",
        Description = "Designates the specified card as the retailer's default/recurring billing card. " +
                      "All other cards for this retailer are simultaneously un-set as default. " +
                      "The update is atomic — both operations complete in a single transaction.")]
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