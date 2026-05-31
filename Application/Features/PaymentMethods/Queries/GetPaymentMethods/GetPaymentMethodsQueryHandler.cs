using Application.Features.PaymentMethods.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Retailer;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.PaymentMethods.Queries.GetPaymentMethods;

/// <summary>
/// Returns the retailer's saved payment methods.
/// Cache-aside: TTL 10 minutes.
/// Invalidated by AddPaymentMethod, RemovePaymentMethod, SetDefaultPaymentMethod commands.
/// </summary>
public sealed class GetPaymentMethodsQueryHandler
    : IRequestHandler<GetPaymentMethodsQuery, IReadOnlyList<PaymentMethodDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEncryptionService _encryptionService;
    private readonly ICacheService _cacheService;

    public GetPaymentMethodsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEncryptionService encryptionService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _encryptionService = encryptionService;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyList<PaymentMethodDto>> Handle(
        GetPaymentMethodsQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey = CacheKeys.PaymentMethods(retailerId);

        var cached = await _cacheService.GetAsync<List<PaymentMethodDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        List<PaymentMethod> methods = await _context.PaymentMethods
            .AsNoTracking()
            .Where(pm => pm.RetailerId == retailerId && !pm.IsDeleted)
            .OrderByDescending(pm => pm.IsDefault)
            .ThenByDescending(pm => pm.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = methods
            .Select(pm => pm.ToDto(_encryptionService.Decrypt(pm.CardholderNameEncrypted)))
            .ToList();

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10), cancellationToken);

        return result.AsReadOnly();
    }
}