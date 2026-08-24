# ADR-0004: Domain Events — No Dispatching Infrastructure in v3

## Status
Accepted — 2026-08-08

## Context
v2 had a full Domain Event dispatch pipeline (`IDomainEventDispatcher`, `DomainEventDispatchRunner<TDbContext>`) that forwarded events to Wolverine's message bus after commit. This added complexity:
- Explicit `_dispatcher.DispatchAsync(ct)` calls in every handler
- Wolverine dependency for event publishing
- Infrastructure coupling in Application layer

In v3, we removed Wolverine and switched to transactional inter-module communication. The question arose: what to do with Domain Events that aggregates still raise internally?

## Decision
**Domain Events may exist inside Aggregates, but no dispatching infrastructure is introduced in v3.**

### What this means:
- Aggregates can still `Raise(new SomeDomainEvent(...))` — this is a pure in-memory operation
- Domain Events are collected in `Entity.DomainEvents` collection
- No `IDomainEventDispatcher` interface exists
- No post-commit dispatch pipeline exists
- No handler infrastructure for domain events exists

### Why keep them at all?
1. **Intent documentation** — `OrderPlacedDomainEvent` documents what happened
2. **Test assertions** — tests can assert `order.DomainEvents.Contains(...)`
3. **Audit trail** — events are available for future logging/audit
4. **Zero cost** — if not dispatched, they live in memory and are garbage-collected

### Why not add dispatch?
- v3 uses transactional direct calls, not event-driven communication
- Inter-module communication goes through ACL ports, not events
- Adding dispatch would re-introduce the complexity we removed

## Consequences

### Positive
- Simpler Application layer — no `IDomainEventDispatcher` to inject and call
- No Wolverine or message bus dependency
- Domain Events still serve as documentation and test assertions

### Negative
- Domain Events are "fire and forget" — no side effects
- If a future requirement needs event-driven side effects, infrastructure must be added

### Future Migration
If event dispatching becomes necessary:
1. Add `IDomainEventDispatcher` to `Application/Ports/Outbound`
2. Implement in `Adapter/Outbound` (could use in-process, MediatR, or Wolverine)
3. Call after `SaveChangesAsync` in Service methods
4. Keep domain events intra-module — use Contracts for cross-module communication

## References
- ADR-0002: Transactional Inter-Module Communication
- v2 ADR-0005: Domain Event Dispatch Pipeline (superseded by this ADR)
