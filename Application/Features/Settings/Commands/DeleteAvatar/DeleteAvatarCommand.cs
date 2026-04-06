

namespace Application.Features.Settings.Commands.DeleteAvatar;

/// <summary>
/// Deletes the current avatar from S3 and clears the <c>AvatarUrl</c> field.
/// No-op if the account has no avatar (returns 200 without error).
/// </summary>
public sealed record DeleteAvatarCommand : IRequest<Result<bool>>;