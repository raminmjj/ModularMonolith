# AGENTS.md — ModularMonolith v3

## Project Overview
- **.NET 10** Modular Monolith with **Hexagonal-First** architecture (Ports & Adapters)
- **No Wolverine, no message bus** — inter-module calls are **transactional direct method calls** via hexagonal ports
- **CQRS separation**: `Application/` (write hexagon) + `QueryApplication/` (flat read side)
- 6 modules: **Identity**, **Catalog**, **Orders**, **Customer** (provider), **Payment** (consumer), **Reporting** (read-only composition, ADR-0006)

## Structure (src/)

```
BuildingBlocks/
├── Framework/              ← Result, Error, CrossModuleSaga (saga toolkit, ADR-0005)
├── DDD/                    ← Entity, ValueObject, AggregateRoot
├── SharedKernel/           ← Money, Email (shared domain concepts)
├── Contracts/              ← Cross-module DTOs (NOT events)
└── Infrastructure.Persistence/ ← EF Core base (Repository, UnitOfWork)

Modules/{Identity,Catalog,Orders,Customer,Payment}/
├── Application/            ← Hexagon: Domain + Ports + Service
├── Adapter/
│   ├── Inbound/Rest/       ← Driving adapter (REST endpoints)
│   └── Outbound/           ← Driven adapters (SQL repos + ACL gateways)
├── QueryApplication/       ← Read side (direct EF Core projections)
└── Installer/              ← Composition Root (Add{Module}Module)

Modules/Reporting/          ← Composition context (NO own DbContext, ADR-0006):
   Application/ (ports + FailureReportService), Adapter/Outbound/ (read-side
   ACL → Customer/Payment QueryApplications), Adapter/Inbound/GraphQL/
   (HotChocolate admin endpoint), Installer/. Provably read-only.

Host/ModularMonolith.Host/  ← Entry point (thin, only wires modules)
```

## Key Architectural Rules (enforced by ArchitectureTests)

1. **Application → Adapter**: Application must NOT depend on any Adapter (Inbound or Outbound)
2. **Application → EF Core**: Application must NOT depend on `Microsoft.EntityFrameworkCore`
3. **Module isolation**: No module Application may reference another module's Application
   - **Exceptions**: `Orders.Adapter.Outbound` → `Catalog.Application`; `Payment.Adapter.Outbound` → `Customer.Application` (write-side ACL gateways)
   - **Exception #3 (reads)**: `Reporting.Adapter.Outbound` → `Customer.QueryApplication` + `Payment.QueryApplication` (ADR-0006)
4. **No Wolverine**: Any reference to `Wolverine` fails the build
5. **Inbound/Outbound ports** must be interfaces; **Services** must be `sealed`
5. **QueryApplication**: Read-only — no writes, no aggregate loading, no domain rules

## Developer Commands

```bash
# Restore, build, run (dev)
dotnet restore ModularMonolith.slnx
dotnet build ModularMonolith.slnx
export JWT__SIGNINGKEY=$(openssl rand -base64 48)   # Required in prod; auto-generated in dev
dotnet run --project src/Host/ModularMonolith.Host

# Apply EF migrations (explicit — the host NEVER auto-migrates on startup)
dotnet run --project src/Host/ModularMonolith.Host -- --migrate

# Create a new migration after changing a module's domain model
dotnet ef migrations add {Name} -c {Module}DbContext \
  -p src/Modules/{Module}/Adapter/Outbound -s src/Host/ModularMonolith.Host

# Run all tests
dotnet test ModularMonolith.slnx

# Run only architecture tests (fast, no DB)
dotnet test tests/ArchitectureTests/ModularMonolith.ArchitectureTests.csproj

# Run unit tests (domain + saga toolkit, no DB)
dotnet test tests/UnitTests/ModularMonolith.UnitTests.csproj

# Run integration tests (uses in-memory DB)
dotnet test tests/IntegrationTests/ModularMonolith.IntegrationTests.csproj

# Docker (requires .env — copy .env.example; compose runs migrations via init container)
docker compose up --build
```

## Environment Variables

| Variable | Required | Notes |
|----------|----------|-------|
| `JWT__SIGNINGKEY` | **Yes (prod)** | Min 32 chars. In dev, auto-generated if missing (with warning). Never put in `appsettings.json`. Compose reads `JWT_SIGNING_KEY` from `.env` and fails fast if unset. |
| `ConnectionStrings__Identity` | Yes | SQL Server. Missing connstring throws outside Development (no silent InMemory fallback). |
| `ConnectionStrings__Catalog` | Yes | SQL Server |
| `ConnectionStrings__Orders` | Yes | SQL Server |
| `ConnectionStrings__Customer` | Yes | SQL Server |
| `ConnectionStrings__Payment` | Yes | SQL Server |
| `Jwt__Issuer` / `Jwt__Audience` | No | Defaults in `docker-compose.yml` |
| `MSSQL_SA_PASSWORD` | Yes (compose) | SQL Server SA password via `.env` — never committed. |

## Database Migrations

- The host **never** migrates on startup (production safety: replicas, rollback, DBA-controlled releases).
- Dev/test uses InMemory (no migrations needed); SQL Server requires explicit migration.
- **Local/prod manual:** `dotnet run --project src/Host/ModularMonolith.Host -- --migrate`
- **Docker:** the `migrations` init container runs `--migrate`; `api` starts only after it exits successfully.
- Design-time factories (`*DbContextDesignTimeFactory`) let `dotnet ef` build relational models even though dev runs InMemory.

## API Endpoints (dev)

| Base | Description |
|------|-------------|
| `/health` | Health check (all 5 DbContexts) |
| `/scalar` | Scalar API docs UI (dev only) |
| `/openapi/v1.json` | OpenAPI document |
| `/api/v1/identity` | Register, Login, Refresh, /me, Admin user lookup |
| `/api/v1/catalog` | Products CRUD, price/stock, reservations |
| `/api/v1/orders` | Create order (with stock reservation), status changes |
| `/api/v1/customers` | Customer profiles, addresses, saved payment methods (tokens only) |
| `/api/v1/payments` | Payment initiation (saved-card / new-token), capture/fail |
| `/api/v1/admin/reports/graphql` | Admin GraphQL (HotChocolate): `customersWithFailedPayments(filter)` — Admin policy + `reports` rate-limit (10 req/min) |

### GraphQL example

```graphql
query FailedPayments($f: FailureReportFilterInput!) {
  customersWithFailedPayments(filter: $f) {
    totalCount page pageSize
    items {
      totalFailedAmount currency failureCount
      customer { id displayName status }
      failures { paymentId orderId amount currency failedAt reason }
    }
  }
}
# variables: { "f": { "from": "2026-08-01T00:00:00Z", "minAmount": 50, "customerStatus": "Active", "page": 1, "pageSize": 20 } }
```
## Testing Notes

- **ArchitectureTests**: 17 NetArchTest rules — run on every build/CI. Verify module boundaries, hexagonal compliance, no Wolverine, QueryApplication isolation/read-only.
- **UnitTests**: Domain logic (order state machine, reservation math/TTL) + CrossModuleSaga contract tests — pure, no DB.
- **IntegrationTests**: `EndToEndTests` using `WebApplicationFactory<Program>` with **in-memory EF Core** (no external DB needed). Assertions are hard — failures fail the test.
- Test order: architecture tests should pass before integration tests.

## Adding a New Module

1. Create `src/Modules/{NewModule}/` with 5 projects (Application, Adapter/Inbound/Rest, Adapter/Outbound, QueryApplication, Installer)
2. Follow existing module structure exactly — architecture tests will validate
3. Define DTOs in `BuildingBlocks/Contracts/` (shared, no behavior)
4. For cross-module calls: add Outbound Port in consumer's Application → implement Gateway in consumer's Adapter.Outbound → call provider's Inbound Port directly (transactional)
5. Register in Host: `builder.Services.AddNewModuleModule(builder.Configuration)`

## Common Pitfalls

- **Putting logic in QueryApplication** — it's read-only by design (arch-tested)
- **Referencing another module's Application** — use Contracts + ACL Gateway instead
- **Adding Wolverine or event dispatching** — architecture tests will fail
- **Committing JWT key or SA password to config** — use env vars / `.env` only
- **Expecting auto-migrations on startup** — they don't happen; run `--migrate` explicitly
- **Composing cross-module reads outside Reporting** — provider QueryApplications may only be consumed by `Reporting.Adapter.Outbound` (arch-tested)
- **Adding a DbContext to Reporting** — it owns no data by design; composition goes through its outbound ports
- **Summing report amounts across currencies** — the service throws rather than producing misleading totals
- **Assuming cross-module calls are atomic** — they are a synchronous saga with compensation (`CrossModuleSaga`, ADR-0005); every step needs an idempotent compensation, and compensation failures are logged/metered, never swallowed
- **Setting Guid keys on child entities without `ValueGeneratedNever()`** — see dotnet/efcore#27736; EF tracks new children as Modified and SaveChanges throws
- **Forgetting to run architecture tests** — they catch boundary violations early

## References

- `docs/ONBOARDING.md` — Eagle-eye view + run instructions
- `docs/DDD-GUIDELINES.md` — DDD best practices for this codebase
- `docs/adr/` — Architecture Decision Records (5 ADRs)
- `tests/ArchitectureTests/HexagonalBoundaryTests.cs` — executable rules