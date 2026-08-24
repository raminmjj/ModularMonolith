# ADR-0005: Cross-Module Consistency — Saga Toolkit, Not Wolverine

## Status
Accepted — 2026-08-24

## Context
ADR-0002 originally claimed cross-module operations share one database transaction.
That claim was false: each module owns a separate database and connection, and no
`TransactionScope` exists in the codebase. The real semantics are a **synchronous
saga with compensation**, previously hand-rolled and inconsistent:

- `OrderService.CompensateReservationsAsync` swallowed exceptions (`catch { /* Best effort */ }`)
  — failed compensations were invisible.
- No tooling existed for the next cross-module flows (Orders→Payment, Payment→Customer).
- Reservation TTL expiry had **no reclaimer**: expired reservations leaked `ReservedStock`
  until an order cancel happened to release them.

Before building anything, three options were evaluated.

## Options Considered

### Option A — Custom saga toolkit in BuildingBlocks *(CHOSEN)*
A minimal `CrossModuleSaga` executor: ordered steps, each with an optional typed
compensation; on failure, completed steps are compensated in reverse order,
best-effort but **logged and counted as metrics**. Plus a Catalog
`ReservationExpirationService` (`BackgroundService`) that reclaims expired stock.

- ✅ Zero new dependencies; ~120 lines total.
- ✅ Pure delegates → unit-testable without mocking any framework.
- ✅ Matches the v3 constraint set exactly (direct calls, no events).
- ✅ Compensation failures become observable instead of swallowed.
- ❌ We own it (small surface, low risk).
- ❌ Synchronous only — no durability across process crashes mid-saga
  (mitigated below).

### Option B — Wolverine *(REJECTED)*
- ❌ Directly contradicts the v3 founding decision (ADR-0002/0004) made after v2's
  Wolverine pain (runtime compilation, outbox overhead, eventual consistency).
- ❌ Violates the enforced architecture rules: `No_Module_Should_Use_Wolverine`
  fails the build on any Wolverine reference.
- ❌ Introduces a message bus + handler model for what is fundamentally one process,
  one request path — massive mechanism-to-problem mismatch.

### Option C — Transactional Outbox + BackgroundService *(REJECTED as primary)*
An outbox solves *reliable asynchronous delivery of messages*. This system has no
message delivery — inter-module calls are synchronous method calls. An outbox here
would be machinery without a consumer: rows written to a table nobody reads.
The only durable element we actually need — TTL-based reservation reclaim — is
delivered by a plain `BackgroundService` inside Option A.

## Decision
1. Adopt **Option A**: `ModularMonolith.Framework.Sagas.CrossModuleSaga` +
   `SagasMetrics`, used by all current and future cross-module write operations.
2. Add a Catalog-hosted `ReservationExpirationService` (Option C's durable element)
   that releases expired reservations every minute — closing the stock-leak hole.
3. **Reaffirm ADR-0004**: Wolverine remains banned. This ADR does *not* reconsider
   that rejection — it removes the last practical argument for revisiting it by
   giving sagas first-class support.

## Consequences
- ✅ Cross-module failure semantics are explicit, uniform, logged, and measured.
- ✅ Expired reservations are reclaimed automatically; `AvailableStock` self-heals.
- ✅ New modules follow a documented pattern instead of inventing try/catch chains.
- ⚠️ Mid-saga process crash can still strand partial state (e.g., reservation held).
  Mitigations: every reservation carries a TTL and is now reclaimed automatically;
  compensation failures are logged/metered for ops follow-up. If these guarantees
  ever prove insufficient, the migration path is per-step idempotency keys + a
  persisted saga log — NOT a message bus.
- ⚠️ Compensations must remain idempotent (release is naturally idempotent:
  `ReleaseReservation` no-ops when the reservation is absent).
