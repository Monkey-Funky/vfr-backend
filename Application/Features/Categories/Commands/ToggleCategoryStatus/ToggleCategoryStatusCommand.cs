
using Application.Features.Categories.DTOs;

namespace Application.Features.Categories.Commands.ToggleCategoryStatus;

/// <summary>
/// Toggles a Category's status between Active and Inactive.
/// Returns the newly applied status so the caller does not need to re-fetch.
/// </summary>
public sealed record ToggleCategoryStatusCommand(Guid CategoryId)
    : IRequest<Result<CategoryStatusDto>>;