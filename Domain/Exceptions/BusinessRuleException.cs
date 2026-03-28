namespace Domain.Exceptions;

/// <summary>
/// Thrown when a business rule is violated — e.g. subscription plan limit exceeded,
/// invalid status transition, or operation not permitted for the current plan tier.
/// Maps to HTTP 422 Unprocessable Entity.
/// </summary>
public sealed class BusinessRuleException : DomainException
{
    public string Code { get; }

    public BusinessRuleException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}