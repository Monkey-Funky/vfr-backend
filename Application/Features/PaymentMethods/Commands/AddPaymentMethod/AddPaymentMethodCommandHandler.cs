using Application.Interfaces;
using Domain.Entities.Retailer;

namespace Application.Features.PaymentMethods.Commands.AddPaymentMethod;

public sealed class AddPaymentMethodCommandHandler
    : IRequestHandler<AddPaymentMethodCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEncryptionService _encryptionService;

    public AddPaymentMethodCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IEncryptionService encryptionService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _encryptionService = encryptionService;
    }

    public async Task<Result<Guid>> Handle(
        AddPaymentMethodCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // ── Encrypt PII before persisting ─────────────────────────────────────
        string encryptedName = _encryptionService.Encrypt(command.CardholderName);

        // BUG-007 FIX: PaymentMethod.Create() now validates that the card's
        // expiry month/year is not in the past. A BusinessRuleException is thrown
        // if the card is expired — preventing expired cards from ever being stored.
        PaymentMethod method = PaymentMethod.Create(
            retailerId: retailerId,
            providerType: command.ProviderType,
            cardholderNameEncrypted: encryptedName,
            cardNumberLast4: command.CardNumberLast4,
            expiryDate: command.ExpiryDate,
            stripePaymentMethodId: command.StripePaymentMethodId,
            isSaved: command.IsSaved);

        await _unitOfWork.Repository<PaymentMethod>().AddAsync(method, cancellationToken);

        // ── Optionally set as the default card ────────────────────────────────
        if (command.SetAsDefault)
        {
            // BUG-008 FIX: No longer using fragile "as List<T>" cast.
            // Use IReadOnlyList<T> directly — no unsafe runtime casts.
            IReadOnlyList<PaymentMethod> existingMethods = await _unitOfWork
                .Repository<PaymentMethod>()
                .FindAsync(
                    pm => pm.RetailerId == retailerId && pm.IsDefault && !pm.IsDeleted,
                    cancellationToken);

            foreach (PaymentMethod existing in existingMethods)
            {
                existing.UnsetDefault();
                await _unitOfWork.Repository<PaymentMethod>().UpdateAsync(existing, cancellationToken);
            }

            // SetAsDefault() also validates expiry (BUG-007) — will throw if somehow expired.
            method.SetAsDefault();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(
            method.Id,
            $"{command.ProviderType} card ending in {command.CardNumberLast4} has been added successfully.");
    }
}