using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;

namespace Application.Features.PaymentMethods.Commands.SetDefaultPaymentMethod;

public sealed class SetDefaultPaymentMethodCommandHandler
    : IRequestHandler<SetDefaultPaymentMethodCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public SetDefaultPaymentMethodCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> Handle(
        SetDefaultPaymentMethodCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        IReadOnlyList<PaymentMethod> allMethods = await _unitOfWork
            .Repository<PaymentMethod>()
            .FindAsync(
                pm => pm.RetailerId == retailerId && !pm.IsDeleted,
                cancellationToken);

        if (allMethods.Count == 0)
            throw new NotFoundException(
                "No payment methods found for this retailer.");

        PaymentMethod? target = allMethods
            .FirstOrDefault(pm => pm.Id == command.PaymentMethodId);

        if (target is null)
            throw new NotFoundException(nameof(PaymentMethod), command.PaymentMethodId);

        if (target.IsDefault)
            return Result<bool>.Success(
                true, "This card is already your default payment method.");

        if (target.IsExpired)
            throw new BusinessRuleException(
                "PAYMENT_METHOD_EXPIRED_DEFAULT",
                $"Card ending in {target.CardNumberLast4} expired on {target.ExpiryDate} " +
                "and cannot be set as default. Please add a valid card first.");

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            foreach (PaymentMethod method in allMethods.Where(pm => pm.IsDefault))
            {
                method.UnsetDefault();
                await _unitOfWork.Repository<PaymentMethod>().UpdateAsync(method, ct);
            }

            target.SetAsDefault();
            await _unitOfWork.Repository<PaymentMethod>().UpdateAsync(target, ct);

            await _unitOfWork.SaveChangesAsync(ct);

        }, cancellationToken);

        return Result<bool>.Success(
            true,
            $"Card ending in {target.CardNumberLast4} is now your default payment method.");
    }
}