# ADR-0003: QueryApplication Separation (CQRS)

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
- Clear CQRS separation without infrastructure complexity
