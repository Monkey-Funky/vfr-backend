using Application.Features.PaymentMethods.DTOs;
using Application.Features.PaymentMethods.Queries.GetPaymentMethods;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Tests.Unit.ApplicationTests.Features.PaymentMethods;

public sealed class GetPaymentMethodsQueryHandler
    : IRequestHandler<GetPaymentMethodsQuery, IReadOnlyList<PaymentMethodDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEncryptionService _encryptionService;

    public GetPaymentMethodsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IEncryptionService encryptionService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _encryptionService = encryptionService;
    }

    public async Task<IReadOnlyList<PaymentMethodDto>> Handle(
        GetPaymentMethodsQuery request,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        var methods = await _context.PaymentMethods
            .AsNoTracking()
            .Where(pm => pm.RetailerId == retailerId && !pm.IsDeleted)
            .OrderByDescending(pm => pm.IsDefault)
            .ThenByDescending(pm => pm.CreatedAt)
            .ToListAsync(cancellationToken);

        return methods
            .Select(pm => pm.ToDto(_encryptionService.Decrypt(pm.CardholderNameEncrypted)))
            .ToList()
            .AsReadOnly();
    }
}
