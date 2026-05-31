using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Shared.Constants;

namespace Application.Features.PaymentMethods.Commands.RemovePaymentMethod;

public sealed class RemovePaymentMethodCommandHandler
    : IRequestHandler<RemovePaymentMethodCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public RemovePaymentMethodCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<bool>> Handle(
        RemovePaymentMethodCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // IDOR guard: must belong to this retailer.
        PaymentMethod method = await _unitOfWork
            .Repository<PaymentMethod>()
            .FirstOrDefaultAsync(
                pm => pm.Id == command.PaymentMethodId
                   && pm.RetailerId == retailerId
                   && !pm.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentMethod), command.PaymentMethodId);

        // Guard: cannot remove the active default card if other cards exist.
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

        await _unitOfWork.Repository<PaymentMethod>().SoftDeleteAsync(method, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate the cached payment methods list.
        await _cacheService.RemoveAsync(CacheKeys.PaymentMethods(retailerId), cancellationToken);

        return Result<bool>.Success(true, "Payment method removed successfully.");
    }
}
