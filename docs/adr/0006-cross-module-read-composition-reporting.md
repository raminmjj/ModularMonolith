# ADR-0006: Cross-Module Read Composition — Reporting Module with GraphQL

## Status
Accepted — 2026-08-24

## Context
The first cross-module *composition* requirement arrived: an admin panel report of
customers with failed payments (customer info + per-customer failure totals), with
filtering, paging, and admin-only access. The architecture review had flagged that
"cross-module reads have no home." This ADR gives them one.

Three options were evaluated.

## Options Considered

### Option A — Dedicated Reporting module, LEAN variant *(CHOSEN)*
A new bounded context whose entire purpose is composition over provider **read sides**:

```
Modules/Reporting/
├── Application/            ← ports (IPaymentReadDataProvider, ICustomerReadDataProvider)
│                              + IFailureReportService (grouping, filtering, paging, cache)
├── Adapter/
│   ├── Inbound/GraphQL/    ← HotChocolate resolvers (driving adapter)
│   └── Outbound/           ← read-side ACL adapters → Customer/Payment QueryApplications
└── Installer/              ← AddReportingModule
```

Deliberately **4 projects, not 5**: Reporting owns NO data — no DbContext, no own
QueryApplication. Its "read side" IS the composition performed through its outbound
ports. Adding a fake QueryApplication would be ceremony without meaning.

Key structural property: the composition hexagon defines its OWN ports; the outbound
adapters delegate to `ICustomerQueryService` / `IPaymentQueryService` (provider read
models crossing the boundary exclusively as Contracts DTOs). This is the read-side
mirror of the write-side ACL gateway pattern (ADR-0002/0005).

- ✅ Machine-enforced read-only: arch test bans ALL Reporting assemblies from referencing ANY write-side Application.
- ✅ Only `Reporting.Adapter.Outbound` may see provider QueryApplications (exception #3).
- ✅ GraphQL adapter is blind to everything except `IFailureReportService`.
- ✅ Composition logic is unit-testable without HTTP or GraphQL.
- ❌ Four new projects for one report (mitigated: each is tiny; the pattern scales to future admin reports).

### Option B — Host-level GraphQL BFF *(REJECTED)*
- ❌ Breaks the documented "thin host" principle — Host would gain business logic
  (filtering semantics, currency rules, caching policy) that module boundaries cannot see or test.
- ❌ No seam for future reports: every admin need would accrete in Program.cs.

### Option C — Extend Payment module *(REJECTED)*
- ❌ Domain leakage: Payment would render customer display data it does not own.
- ❌ Wrong owner: tomorrow's admin report might be "products with most refunds" —
  putting reporting inside one domain forces every future report into whichever
  module got here first.
- ✅ Reuse of existing gateway infra is real but trivial to replicate via ports (done in A).

## Decision
1. Adopt Option A (lean). Sanctioned exception #3:
   `Reporting.Adapter.Outbound → {Customer,Payment}.QueryApplication` — enforced by
   `Only_Reporting_Adapter_Outbound_May_Reference_Read_Sides`.
2. Provider modules expose NEW flat query methods on their existing QueryApplications
   (`GetFailedPaymentsAsync`, `GetByIdsAsync`) — single-statement SQL each.
3. All grouping/filtering/paging/caching lives in Reporting.Application.
4. GraphQL = HotChocolate 16 (`HotChocolate.AspNetCore`, `HotChocolate.AspNetCore.Authorization`),
   endpoint `/api/v1/admin/reports/graphql`, named rate-limit policy `reports`.

## Consequences
- ✅ Cross-module reads finally have a home with the same enforcement rigor as writes.
- ✅ N+1 impossible by construction: exactly TWO provider calls per request
  (1 filtered SQL on Payment, 1 batched IN-query on Customer), regardless of group count.
  Nested GraphQL types resolve from the already-composed page — no DataLoader needed;
  add DataLoaders only if per-entity sub-fields later gain their own IO.
- ✅ 30s IMemoryCache on composed pages; hard cap 5000 rows/request guards unbounded scans.
- ⚠️ Currency guard: if a range contains mixed currencies the service throws rather than
  summing misleadingly — callers must filter by currency first (future: FX normalization).
- ⚠️ Paging happens post-composition (in-memory) — acceptable for date-bounded admin
  ranges; revisit with keyset pagination if groups grow past ~10⁴.
