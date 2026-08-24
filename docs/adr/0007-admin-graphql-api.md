# ADR-0007: Admin GraphQL API — Reporting as the Admin BFF

## Status
Accepted — 2026-08-24 · **Amended: module renamed `Reporting` → `Admin` (2026-08-24)** —
after absorbing mutations, "Reporting" implied read-only and misled. Namespaces
(`Modules.Admin.*`, `Contracts.Admin.*`), DI methods (`AddAdminModule`), and the arch
rules were renamed with it; ADR-0006 references to "Reporting" are historical.

## Context
The admin panel needs a single API covering catalog management, customer management,
order management, analytics, and payment operations. The Reporting module (ADR-0006)
already owned cross-module composition; this ADR extends it into a full admin BFF and
amends its read-only guarantee.

## Decisions

### D1 — Schema organization: modular classes, ONE schema (Option B)
`Queries/` and `Mutations/` are separate per-domain classes (`AdminCatalogQueries`,
`AdminOrderMutations`, …) registered as type extensions on the shared `admin`
HotChocolate executor. Modular source layout, one endpoint, one auth story.
Schema stitching rejected: it solves a multi-service problem we do not have.

### D2 — Mutations in GraphQL (Option X)
One protocol for the entire admin panel: same authorization, same rate-limit bucket,
same error shape (`extensions.code` from `Error.Code`). REST remains untouched and
continues to serve public/client traffic — the two coexist as sibling driving adapters
over identical hexagon ports.

### D3 — Authorization: single `Admin` policy, enforced TWICE
Endpoint-level `RequireAuthorization("Admin")` blocks non-admins before execution
(including introspection); field-level `[Authorize(Policy = "Admin")]` guards every
query/mutation independently. Per-role policies (D-Option B) remain a pure addition:
annotate resolvers individually.

## Boundary amendment — writes through the composition context
ADR-0006 declared Reporting provably read-only. With mutations, that guarantee is
replaced by a tighter-scoped one:

| Assembly | Write-side Application access |
|---|---|
| `Reporting.Application` | ❌ never |
| `Reporting.Adapter.Inbound.GraphQL` | ❌ never |
| `Reporting.Adapter.Outbound` | ✅ ONLY here — ACL gateways delegate to provider **inbound ports** |

This is the same sanctioned pattern as exceptions #1/#2 (Orders→Catalog,
Payment→Customer), generalized to all four providers. Enforced by
`Reporting_App_And_GraphQL_Must_Never_Reference_Write_Side_Applications`; even the
gateway adapter cannot touch provider ADAPTERS (DbContexts/repos).

New provider capabilities added where missing (small, domain-owned):
`IProductService.DeactivateProductAsync` (soft-delete via existing aggregate method),
`IPaymentService.RefundAsync` + `PaymentStatus.Refunded` + `MarkRefunded`
(Captured→Refunded only).

## Data flow & N+1 posture
Resolvers stay IO-free shells over five admin services. Every service composes via
ports: reads hit provider QueryApplications (flat DTOs, single filtered SQL each),
writes hit provider inbound ports through gateways.

SQL counts per admin request (constant, independent of page size):
- products / orders / payments list: **1**
- customer detail: **3** (customer + orders-by-identity-user + payments)
- order detail: **2** (order row + latest payment for the order)
- sales summary / top customers: **1–2** (+ name batch), 30s cached
Analytics guard against mixed-currency sums (throws) and cap scans at 5000 rows.

## Deferred (honest scope note)
**Categories** and **product images/media** have no backing concept in any module's
domain. Rather than fabricate data or bolt them onto Catalog hastily, they are
deferred: they require a real Catalog domain extension (Category aggregate +
media entity, EF configurations, migrations) tracked separately. The GraphQL schema
grows additively when that lands.

## Consequences
- ✅ Admin panel: one API surface, machine-enforced boundaries, reusable ports.
- ✅ Provider domains gained capabilities their own aggregates needed anyway
  (deactivation, refunds) — exposed equally to future REST endpoints.
- ⚠️ Reporting now drives writes: mis-scoped mutations would be reachable from the
  admin API — mitigated by the confined-gateway rule and dual authorization.
- ⚠️ Offset pagination chosen for admin tables (simple, jump-to-page); keyset
  pagination is the documented next step if datasets grow past ~10⁵ rows.
