using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Shared.Constants;

namespace Application.Features.PaymentMethods.Commands.AddPaymentMethod;

public sealed class AddPaymentMethodCommandHandler
    : IRequestHandler<AddPaymentMethodCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEncryptionService _encryptionService;
    private readonly ICacheService _cacheService;

    public AddPaymentMethodCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IEncryptionService encryptionService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _encryptionService = encryptionService;
        _cacheService = cacheService;
    }

    public async Task<Result<Guid>> Handle(
        AddPaymentMethodCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string encryptedName = _encryptionService.Encrypt(command.CardholderName);

        PaymentMethod method = PaymentMethod.Create(
            retailerId: retailerId,
            providerType: command.ProviderType,
            cardholderNameEncrypted: encryptedName,
            cardNumberLast4: command.CardNumberLast4,
            expiryDate: command.ExpiryDate,
            stripePaymentMethodId: command.StripePaymentMethodId,
            isSaved: command.IsSaved);

        await _unitOfWork.Repository<PaymentMethod>().AddAsync(method, cancellationToken);

        if (command.SetAsDefault)
        {
            // Atomically unset existing defaults before setting the new one.
            IReadOnlyList<PaymentMethod> existingDefaults = await _unitOfWork
                .Repository<PaymentMethod>()
                .FindAsync(
                    pm => pm.RetailerId == retailerId && pm.IsDefault && !pm.IsDeleted,
                    cancellationToken);

            foreach (var existing in existingDefaults)
            {
                existing.UnsetDefault();
                await _unitOfWork.Repository<PaymentMethod>().UpdateAsync(existing, cancellationToken);
            }

            method.SetAsDefault();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate the cached payment methods list so the new card appears immediately.
        await _cacheService.RemoveAsync(CacheKeys.PaymentMethods(retailerId), cancellationToken);

        return Result<Guid>.Success(
            method.Id,
            $"{command.ProviderType} card ending in {command.CardNumberLast4} has been added successfully.");
    }
}
