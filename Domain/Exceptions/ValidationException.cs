namespace Domain.Exceptions;

public sealed class ValidationException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }
}