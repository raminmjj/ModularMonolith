# ADR-0002: Transactional Inter-Module Communication (No Wolverine, No Events)

## Status
**Amended (2026-08-24)** — see [ADR-0005](0005-cross-module-consistency-saga-toolkit.md).

> **Amendment note:** The original decision below claimed cross-module operations share
> "the same database transaction". That claim was inaccurate: each module owns a separate
> database and connection, and no `TransactionScope` is used anywhere in the codebase.
> The real semantics are a **synchronous saga with best-effort compensation**, now made
> explicit and tool-supported (see ADR-0005). The no-bus decision itself is reaffirmed. — 2026-08-07

## Context
v2 used WolverineFX with integration events and transactional outbox. While this works, it adds complexity:
- Wolverine runtime compilation
- Event publishing after commit
- Eventual consistency between modules
- Outbox pattern overhead

The team decided that inter-module communication should be **transactional** (same DB transaction), not event-based.

## Decision
**Remove WolverineFX entirely. Use direct method calls through hexagonal ports.**

Orders module calls Catalog's `IProductService.ReserveStockAsync()` directly (via `ICatalogGateway` ACL adapter). Both modules participate in the same database transaction — if either fails, both roll back.

## Consequences
- No message bus dependency
- Strong consistency (not eventual)
- Simpler mental model
- Can still extract to microservice later (swap adapter with HTTP client)
