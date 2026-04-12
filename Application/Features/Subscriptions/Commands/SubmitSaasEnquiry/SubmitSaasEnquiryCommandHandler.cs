using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;
using Domain.Events;

namespace Application.Features.Subscriptions.Commands.SubmitSaasEnquiry;

public sealed class SubmitSaasEnquiryCommandHandler
    : IRequestHandler<SubmitSaasEnquiryCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublisher _publisher;

    public SubmitSaasEnquiryCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPublisher publisher)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _publisher = publisher;
    }

    public async Task<Result<Guid>> Handle(
        SubmitSaasEnquiryCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // BUG-005 FIX: Block BOTH Pending AND InProgress enquiries.
        bool hasActiveEnquiry = await _unitOfWork
            .Repository<SaasEnquiry>()
            .AnyAsync(
                e => e.RetailerId == retailerId
                  && (e.Status == SaasEnquiryStatus.Pending
                   || e.Status == SaasEnquiryStatus.InProgress),
                cancellationToken);

        if (hasActiveEnquiry)
            throw new ConflictException(
                "A SaaS enquiry is already open for this account. " +
                "Our team will contact you shortly. You cannot submit another enquiry " +
                "while an existing one is Pending or In Progress.");

        SaasEnquiry enquiry = SaasEnquiry.Create(retailerId);

        await _unitOfWork.Repository<SaasEnquiry>().AddAsync(enquiry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new SaasEnquirySubmittedDomainEvent(
                EnquiryId: enquiry.Id,
                RetailerId: retailerId,
                OccurredAt: DateTime.UtcNow),
            cancellationToken);

        return Result<Guid>.Success(
            enquiry.Id,
            "Your SaaS enquiry has been submitted. Our team will contact you within 24 hours.");
    }
}