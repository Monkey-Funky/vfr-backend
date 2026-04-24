using Application.Features.Customer.FitFeedback.DTOs;

namespace Application.Features.Customer.FitFeedback.Queries.GetFitFeedbackByOrder;

public sealed record GetFitFeedbackByOrderQuery(
    Guid OrderId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<FitFeedbackDto>>;
