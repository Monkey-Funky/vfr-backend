using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;

namespace Application.Features.PaymentMethods.Commands.RemovePaymentMethod;

public sealed class RemovePaymentMethodCommandHandler
    : IRequestHandler<RemovePaymentMethodCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public RemovePaymentMethodCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(
        RemovePaymentMethodCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // ── Load the payment method (IDOR guard: must belong to retailer) ─────
        PaymentMethod method = await _unitOfWork
            .Repository<PaymentMethod>()
            .FirstOrDefaultAsync(
                pm => pm.Id == command.PaymentMethodId
                   && pm.RetailerId == retailerId
                   && !pm.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentMethod), command.PaymentMethodId);

        // ── Guard: Cannot remove the active default card ───────────────────────
        if (method.IsDefault)
        {
            bool otherCardsExist = await _unitOfWork
                .Repository<PaymentMethod>()
                .AnyAsync(
                    pm => pm.RetailerId == retailerId
                       && pm.Id != command.PaymentMethodId
                       && !pm.IsDeleted,
                    cancellationToken);

            if (otherCardsExist)
                throw new BusinessRuleException(
                    "CANNOT_DELETE_DEFAULT_PAYMENT_METHOD",
                    "This is your default payment card. Please designate another card " +
                    "as default before removing this one.");
        }

        // ── Soft-delete ───────────────────────────────────────────────────────
        await _unitOfWork.Repository<PaymentMethod>()
            .SoftDeleteAsync(method, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true, "Payment method removed successfully.");
    }
}