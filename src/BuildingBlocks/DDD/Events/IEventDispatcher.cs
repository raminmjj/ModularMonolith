using ModularMonolith.DDD.Common;

namespace ModularMonolith.DDD.Events;

/// <summary>
/// Abstraction over HOW domain events leave the application core.
/// The core (aggregates + services) depends only on this interface — the actual
/// delivery mechanism is pluggable infrastructure (currently: NoOp, see ADR-0004).
/// Swapping to InMemory/background/bus implementations later requires ZERO changes
/// to domain or application code.
/// </summary>
public interface IEventDispatcher
{
    /// <summary>Dispatches a single domain event.</summary>
    Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : IDomainEvent;

    /// <summary>Dispatches a batch of events (e.g., all events raised by one aggregate).</summary>
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default);
}

/// <summary>
/// Convenience: publish everything an aggregate raised, then clear its buffer.
/// Gives ClearDomainEvents() its first real caller — events can no longer silently
/// accumulate on tracked aggregates.
/// </summary>
public static class EventDispatcherExtensions
{
    public static async Task DispatchAndClearAsync(
        this IEventDispatcher dispatcher, Entity<Guid> entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(entity);

        var events = entity.DomainEvents.ToArray();
        if (events.Length == 0) return;

        await dispatcher.DispatchAsync(events, ct);
        entity.ClearDomainEvents();
    }
}
