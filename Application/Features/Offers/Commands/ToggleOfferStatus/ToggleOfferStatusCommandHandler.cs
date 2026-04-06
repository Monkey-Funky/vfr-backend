using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Offers.Commands.ToggleOfferStatus;

public sealed class ToggleOfferStatusCommandHandler
    : IRequestHandler<ToggleOfferStatusCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public ToggleOfferStatusCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<bool>> Handle(
        ToggleOfferStatusCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // IDOR guard: retailerId in predicate
        Offer offer = await _unitOfWork
            .Repository<Offer>()
            .FirstOrDefaultAsync(
                o => o.Id == command.OfferId && o.RetailerId == retailerId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Offer), command.OfferId);

        // Domain method enforces the Expired guard and cycles Active ↔ Inactive
        offer.ToggleStatus();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cacheService.RemoveByPrefixAsync($"offers:{retailerId}:", cancellationToken);

        return Result<bool>.Success(true);
    }
}
