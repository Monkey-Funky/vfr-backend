using Application.Features.Customer.FitFeedback.DTOs;
using Application.Features.Customer.FitFeedback.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.FitFeedback.Queries.GetFitFeedbackByProduct;

public sealed class GetFitFeedbackByProductQueryHandler : IRequestHandler<GetFitFeedbackByProductQuery, PagedResult<FitFeedbackDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetFitFeedbackByProductQueryHandler(
        IApplicationDbContext context, 
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<FitFeedbackDto>> Handle(GetFitFeedbackByProductQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("Customer identity missing.");

        var query = _context.FitFeedback
            .AsNoTracking()
            .Where(f => f.ProductId == request.ProductId && f.CustomerId == customerId)
            .OrderByDescending(f => f.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var feedbacks = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = feedbacks.Select(f => f.ToDto()).ToList();

        return new PagedResult<FitFeedbackDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
