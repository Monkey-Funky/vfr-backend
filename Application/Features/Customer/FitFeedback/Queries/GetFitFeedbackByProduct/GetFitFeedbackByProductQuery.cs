using Application.Features.Customer.FitFeedback.DTOs;

namespace Application.Features.Customer.FitFeedback.Queries.GetFitFeedbackByProduct;

public sealed record GetFitFeedbackByProductQuery(
    Guid ProductId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<FitFeedbackDto>>;
