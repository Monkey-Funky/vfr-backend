using MediatR;

namespace Domain.Events;

/// <summary>
/// Marker interface for all domain events in the VFR platform.
/// Domain events are published via MediatR IPublisher after the
/// Unit of Work SaveChangesAsync completes successfully.
/// Implementing INotification allows MediatR to dispatch them
/// to registered INotificationHandler{T} instances.
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>The UTC timestamp when this event occurred.</summary>
    DateTime OccurredAt { get; }
}