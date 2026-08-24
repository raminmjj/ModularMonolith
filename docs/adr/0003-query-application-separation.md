# ADR-0003: QueryApplication Separation (Read-Optimized CQS)

> **Terminology note (2026-08-24):** originally titled with "(CQRS)". Renamed per
> [ADR-0008](0008-read-optimized-cqs-terminology.md) — this architecture is CQS with
> physical read/write separation, not full CQRS (no separate stores, no event-driven
> propagation). The separation decision itself stands unchanged.

## Status
Accepted — 2026-08-07

## Context
Hexagonal architecture imposes overhead on read operations: aggregate loading, domain rules, port indirection. For read-heavy operations (product listing, order history), this overhead is unnecessary.

## Decision
**Separate QueryApplication from the hexagon.** Each module has:
- `Application/` — hexagon (write side: Domain + Ports + Service)
- `QueryApplication/` — flat read side (direct EF Core projections, no aggregate loading)

## Consequences
- Read operations are fast (no aggregate overhead)
- Write operations are protected by hexagonal boundaries
- Clear read/write separation without infrastructure complexity (terminology: ADR-0008)
