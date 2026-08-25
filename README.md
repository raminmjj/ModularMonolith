# ModularMonolith v3 — Hexagonal-First Modular Monolith for .NET 10

A production-shaped reference implementation of a **Hexagonal-First modular monolith**:
eight bounded-context modules, machine-enforced boundaries, synchronous cross-module
ACL gateways, a read-optimized query side per module, and a full admin GraphQL panel —
**no message bus, no Wolverine/MassTransit/MediatR**.

## Technologies

| Area | Technology |
|------|------------|
| Architecture | **Hexagonal-First** (Ports & Adapters), enforced by NetArchTest |
| Inter-module calls | **Transactional direct method calls** via ACL gateway ports (ADR-0002/0005) |
| Query side | **Read-Optimized CQS ("CQS+")** — flat `QueryApplication` projections per module (ADR-0008) |
| Domain events | **Pluggable `IEventDispatcher` — currently NoOp** (ADR-0004); swap without touching the core |
| ORM | EF Core 10 — independent DbContext per module |
| Validation | FluentValidation at hexagon boundaries |
| Auth | JWT Bearer + refresh tokens + account lockout |
| API surface | REST (+ OpenAPI/Scalar) and **Admin GraphQL** (HotChocolate 16) |
| Observability | Serilog · OpenTelemetry tracing & metrics (incl. saga counters) |
| Tests | xUnit — 17 architecture rules, 50 unit, 7 integration |

## Modules (`src/Modules/`)

| Module | Responsibility |
|--------|----------------|
| **Identity** | Users, credentials, JWT issue/refresh, lockout |
| **Catalog** | Products, stock, reservations (TTL reaper included) |
| **Orders** | Order lifecycle; reserves stock via Catalog ACL (saga with compensation) |
| **Customer** | Profiles, addresses, saved payment methods (**vault tokens only**) |
| **Payment** | Payment lifecycle: initiate → capture / fail / refund |
| **Supplier** | Supplier verification + **owns** the BrandSupplyAgreement M2M (ADR-0009) |
| **Brand** | Brand identity + approval lifecycle (PendingReview → Approved/Rejected) |
| **Admin** | Thin composition layer: admin GraphQL panel over provider read sides + write gateways (ADR-0006/0007) |

Every domain module follows the same five-project layout:

```
Modules/{Module}/
├── Application/        ← Hexagon: Domain + Ports(Inbound/Outbound) + Services
├── Adapter/
│   ├── Inbound/        ← Driving adapters (REST, GraphQL for Admin)
│   └── Outbound/       ← Driven adapters: EF Core repos, ACL gateways
├── QueryApplication/   ← Read side: flat EF projections into Contract DTOs
└── Installer/          ← Add{Module}Module() composition root
```

## Key architectural rules (machine-enforced — 17 tests)

- Application never depends on Adapters or EF Core; ports are interfaces; services are `sealed`
- No module Application references another module's Application — cross-module access only via Contracts DTOs through consumer-owned ports
- Sanctioned ACL bridges: `Orders.Adapter.Outbound → Catalog.Application`, `Payment.Adapter.Outbound → Customer.Application`, `Supplier.Adapter.Outbound → Brand.Application`, `Admin.Adapter.Outbound → provider Applications/QueryApplications`
- Single-module admin operations live in **domain-owned** admin services (`Catalog.ICatalogAdminService`, …); Admin composes and stays aggregate-free
- No Wolverine / MediatR / external message bus — anywhere

See [`tests/ArchitectureTests/HexagonalBoundaryTests.cs`](tests/ArchitectureTests/HexagonalBoundaryTests.cs) for the executable rules.

## Getting started

```bash
dotnet restore ModularMonolith.slnx
dotnet build ModularMonolith.slnx

export JWT__SIGNINGKEY=$(openssl rand -base64 48)   # required in prod; auto-generated in dev
dotnet run --project src/Host/ModularMonolith.Host

# Apply EF migrations (the host NEVER auto-migrates on startup)
dotnet run --project src/Host/ModularMonolith.Host -- --migrate

# Tests
dotnet test ModularMonolith.slnx                                            # everything
dotnet test tests/ArchitectureTests/ModularMonolith.ArchitectureTests.csproj # boundaries (fast)
dotnet test tests/UnitTests/ModularMonolith.UnitTests.csproj                 # domain + saga toolkit
dotnet test tests/IntegrationTests/ModularMonolith.IntegrationTests.csproj   # E2E via WebApplicationFactory

# Docker (requires .env — copy .env.example; migrations run via init container)
docker compose up --build
```

### Endpoints

| Base | Description |
|------|-------------|
| `/health` | Health checks (all module DbContexts) |
| `/scalar` | Scalar API docs UI (dev only) |
| `/openapi/v1.json` | OpenAPI document |
| `/api/v1/identity` | Register, Login, Refresh, `/me`, Admin user lookup |
| `/api/v1/catalog` | Products CRUD, price/stock, reservations |
| `/api/v1/orders` | Create order (with stock reservation), status changes |
| `/api/v1/customers` | Customer profiles, addresses, saved payment methods (tokens only) |
| `/api/v1/payments` | Payment initiation (saved-card / new-token), capture/fail |
| `/api/v1/suppliers` | Suppliers: create, get (admin ops via GraphQL) |
| `/api/v1/brands` | Brands: create, get (admin ops via GraphQL) |
| `/api/v1/admin/reports/graphql` | **Admin GraphQL** — full panel (see below); Admin policy ×2 + rate limit + max-depth 10 |

### Admin GraphQL example

```graphql
query FailedPayments($f: FailureReportFilterInput!) {
  customersWithFailedPayments(filter: $f) {
    totalCount page pageSize
    items {
      totalFailedAmount currency failureCount
      customer {
        id displayName status
        lastOrder { orderId orderDate totalAmount status itemCount }  # Orders enrichment
      }
      failures { paymentId orderId amount currency failedAt reason }
    }
  }
}
# variables: { "f": { "from": "2026-08-01T00:00:00Z", "minAmount": 50, "page": 1, "pageSize": 20 } }
```

Mutations cover catalog CRUD, customer suspension, order status changes,
payment capture/refund/fail, supplier verification, brand approval/rejection,
and brand↔supplier agreement assignment (commission rates).

## Environment variables

| Variable | Required | Notes |
|----------|----------|-------|
| `JWT__SIGNINGKEY` | Yes (prod) | Min 32 chars; dev auto-generates with warning. Compose reads `JWT_SIGNING_KEY` from `.env` and fails fast if unset |
| `ConnectionStrings__{Identity,Catalog,Orders,Customer,Payment,Supplier,Brand}` | Yes | SQL Server. Missing connstring throws outside Development (no silent InMemory fallback) |
| `MSSQL_SA_PASSWORD` | Yes (compose) | Via `.env` — never committed |

## Documentation

- [`AGENTS.md`](AGENTS.md) — authoritative conventions, structure, pitfalls
- [`docs/ONBOARDING.md`](docs/ONBOARDING.md) — eagle-eye view
- [`docs/DDD-GUIDELINES.md`](docs/DDD-GUIDELINES.md) — DDD practices for this codebase
- [`docs/adr/`](docs/adr/) — Architecture Decision Records:
  - 0001 Hexagonal-First · 0002 Transactional inter-module communication ·
    0003 QueryApplication separation · 0004 Pluggable event dispatching (NoOp) ·
    0005 CrossModuleSaga toolkit · 0006 Reporting/Admin composition + GraphQL ·
    0007 Admin GraphQL API · 0008 Read-Optimized CQS terminology ·
    0009 Supplier–Brand M2M pattern

## v2 → v3 differences

| v2 | v3 |
|----|-----|
| Layer-First (Domain/Application/Infrastructure/Presentation) | **Hexagonal-First** (`Application` / `Adapter/Inbound·Outbound` / `QueryApplication`) |
| WolverineFX message bus | **No bus** — transactional ACL direct calls + explicit compensation saga toolkit |
| Integration events | Contracts DTOs through consumer-owned ports |
| Hard-wired domain event dispatcher | **Pluggable `IEventDispatcher`, currently NoOp** (ADR-0004) |
| "CQRS" label | **Read-Optimized CQS (CQS+)** — same DB, synchronous projections (ADR-0008) |
| CQRS inside one Application | Separate `QueryApplication` per module |

## License

MIT
