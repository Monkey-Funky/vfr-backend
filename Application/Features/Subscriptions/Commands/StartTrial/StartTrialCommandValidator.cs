namespace Application.Features.Subscriptions.Commands.StartTrial;

/// <summary>
/// StartTrialCommand carries no user-supplied parameters; no stateless
/// validation rules are applicable. The handler performs all DB-level guards.
/// This class exists to satisfy the FluentValidation registration scan pattern.
/// </summary>
public sealed class StartTrialCommandValidator : AbstractValidator<StartTrialCommand>
{
    public StartTrialCommandValidator() { }
}