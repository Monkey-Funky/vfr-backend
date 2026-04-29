namespace Application.Features.Customer.Catalog.DTOs;

public sealed record CategoryInfoDto(
    Guid Id,
    string Name,
    string? Description
);
