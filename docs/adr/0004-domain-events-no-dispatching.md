# ADR-0004: Pluggable Event Dispatching (Currently NoOp)

## Status
Accepted - 2026-08-08 | **Amended (2026-08-24): pluggable seam introduced, NoOp chosen.**
The zero-dispatch decision below is superseded by the seam described at the bottom;
the reasoning (strong consistency, no bus) is reaffirmed and extended.

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

---

## Amendment (2026-08-24): Pluggable Event Dispatching — NoOp Today

The original decision left `ClearDomainEvents()` with zero callers and dispatch as a
non-existent concept. That created a dead seam: when async workflows eventually arrive,
every aggregate would need re-touching. This amendment installs the *abstraction*
without installing any delivery mechanism.

### What was added
- `ModularMonolith.DDD.Events.IEventDispatcher` — the seam. Application services may
  now `await` dispatch; the core has no idea what happens behind it.
- `NoOpEventDispatcher` — the current implementation: completes, delivers nothing,
  zero dependencies. Registered in the Host as singleton; swapping implementations is
  ONE DI line.
- `DispatchAndClearAsync(entity)` extension — publishes everything an aggregate
  raised, then clears its buffer (`ClearDomainEvents()` finally has a caller).
- First wired call sites: `CatalogAdminService.DeactivateProductAsync`
  (`ProductDeactivatedDomainEvent`, newly raised by the aggregate) and
  `PaymentAdminService.RefundAsync` (`PaymentRefundedDomainEvent`).

### Why we explicitly choose NoOp today
1. **Strong consistency stays default.** Nothing happens after commit that a caller
   did not already see. No dual-write windows, no retry semantics to reason about.
2. **In-process handlers would be premature coupling.** The only plausible consumers
   today would be cross-module reactions — and those MUST go through ACL ports
   (ADR-0002), not events.
3. **A real bus is a one-line DI swap away.** The seam makes waiting cheap.

### Future triggers — replace NoOp when:
| Trigger | Example | Replacement shape |
|---|---|---|
| Background side effects after commit | email/notification on refund | In-memory background dispatcher (fire-and-forget, same process) |
| Cross-module reactions that must not share the request transaction | analytics roll-ups, read-model warm-up | Outbox + transport (NEW ADR required — revisits ADR-0002) |
| Integration with external systems | PSP webhooks, CRM sync | Same outbox path |
| Event-sourced read models (ADR-0008 Stage 2+) | denormalized reporting store | Projection pump consuming the dispatcher |

### Rules that do NOT change
- Domain events remain **intra-module**. Cross-module communication is still ACL ports
  + Contracts (ADR-0002) — never events.
- Aggregates still own their events; services never invent events for state they did
  not load.
- No bus packages (Wolverine/MassTransit/RabbitMQ) — arch-tested.

## References
- ADR-0002: Transactional Inter-Module Communication
- ADR-0005: CrossModuleSaga (compensation, not events)
- ADR-0008: Read-Optimized CQS terminology
- v2 ADR-0005: Domain Event Dispatch Pipeline (superseded)