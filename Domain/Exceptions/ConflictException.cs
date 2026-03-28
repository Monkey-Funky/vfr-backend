namespace Domain.Exceptions;

/// <summary>
/// Thrown when a uniqueness constraint is violated — e.g. duplicate brand name,
/// duplicate email, duplicate category name within a retailer's scope.
/// Maps to HTTP 409 Conflict.
/// </summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string entityName, string field, object value)
        : base($"A {entityName} with {field} '{value}' already exists.") { }

    public ConflictException(string message)
        : base(message) { }
}