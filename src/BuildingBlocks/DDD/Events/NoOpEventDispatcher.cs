using ModularMonolith.DDD.Common;

namespace ModularMonolith.DDD.Events;

/// <summary>
/// The CURRENT reality (ADR-0004): events are recorded on aggregates and
/// intentionally NOT delivered anywhere. NoOp keeps the seam hot — application
/// services already await dispatch — while preserving strong consistency and
/// zero infrastructure. Swap by re-registering IEventDispatcher in the Host.
/// </summary>
public sealed class NoOpEventDispatcher : IEventDispatcher
{
    public static readonly NoOpEventDispatcher Instance = new();

    public Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
        => Task.CompletedTask;

    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
        => Task.CompletedTask;
}
