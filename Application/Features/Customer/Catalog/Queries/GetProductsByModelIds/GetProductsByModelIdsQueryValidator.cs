namespace Application.Features.Customer.Catalog.Queries.GetProductsByModelIds;

public sealed class GetProductsByModelIdsQueryValidator
    : AbstractValidator<GetProductsByModelIdsQuery>
{
    public GetProductsByModelIdsQueryValidator()
    {
        RuleFor(q => q.ModelIds)
            .NotNull()
            .WithMessage("ModelIds list must not be null.")
            .Must(ids => ids is { Count: > 0 })
            .WithMessage("At least one model ID must be provided.")
            .Must(ids => ids.Count <= 50)
            .WithMessage("A maximum of 50 model IDs may be resolved per request.")
            .Must(ids => ids.All(id => !string.IsNullOrWhiteSpace(id)))
            .WithMessage("Model IDs must not be null or empty strings.");
    }
}