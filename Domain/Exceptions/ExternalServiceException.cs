namespace Domain.Exceptions;

/// <summary>
/// Thrown when a call to an external service fails after all Polly retry attempts
/// are exhausted — e.g. Stripe payment gateway, AWS S3, Google OAuth.
/// Maps to HTTP 502 Bad Gateway.
/// </summary>
public sealed class ExternalServiceException : DomainException
{
    public string ServiceName { get; }

    public ExternalServiceException(string serviceName, string message)
        : base(message)
    {
        ServiceName = serviceName;
    }

    public ExternalServiceException(string serviceName, string message, Exception innerException)
        : base(message, innerException)
    {
        ServiceName = serviceName;
    }
}