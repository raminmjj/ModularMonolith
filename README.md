# ModularMonolith v3 — Hexagonal-First Modular Monolith برای .NET 10

## تکنولوژی‌ها

| حوزه | تکنولوژی |
|------|----------|
| Architecture | **Hexagonal-First** (Ports & Adapters) — نه Layer-First |
| Messaging | **هیچ** — مستقیم متد کال transactional بین ماژول‌ها |
| CQRS | QueryApplication جدا — بدون Hexagonal overhead |
| ORM | EF Core 10 (هر ماژول DbContext مستقل) |
| Mapping | Riok.Mapperly (source generator) |
| Validation | FluentValidation |
| Auth | JWT Bearer + Refresh Token + Account Lockout |
| API Docs | Scalar (جایگزین Swagger) |
| Logging | Serilog |
| Tracing | OpenTelemetry |
| Architecture Tests | NetArchTest (۱۰ تست) |

## ساختار

```
src/
├── BuildingBlocks/
│   ├── Framework/              ← Result, Error, IClock
│   ├── DDD/                    ← Entity, ValueObject, AggregateRoot
│   ├── SharedKernel/           ← Money, Email (shared domain concepts)
│   ├── Contracts/              ← Cross-module DTOs (NOT events)
│   └── Infrastructure.Persistence/ ← EF Core base (Repository, UnitOfWork)
├── Modules/
│   ├── Catalog/
│   │   ├── Application/        ← Hexagon: Domain + Ports + Service
│   │   ├── Adapter/
│   │   │   ├── Inbound/Rest/   ← Driving adapter (REST API)
│   │   │   └── Outbound/       ← Driven adapter (SQL DB)
│   │   ├── QueryApplication/   ← Read side (no hexagon)
│   │   └── Installer/          ← Composition Root
│   ├── Identity/               ← همان الگو
│   └── Orders/                 ← همان الگو + ACL Gateway
└── Host/                       ← Entry Point (no Wolverine)
```

## نحوه استفاده

```bash
# نصب template
dotnet new install ./ModularMonolith.Template.3.0.0.nupkg

# ایجاد پروژه
dotnet new modular-monolith-v3 -n MyApp
cd MyApp

# JWT key (env var — هرگز در appsettings)
export JWT__SIGNINGKEY=$(openssl rand -base64 48)

# اجرا
dotnet run --project src/Host/MyApp.Host
```

### آدرس‌ها

| آدرس | توضیح |
|------|--------|
| `/health` | Health check |
| `/openapi/v1.json` | OpenAPI document |
| `/scalar` | Scalar API docs UI |

## API Endpoints

### Identity (`/api/v1/identity`)
- `POST /register` — ثبت‌نام
- `POST /login` — ورود + access/refresh token
- `POST /refresh` — تمدید token
- `GET /users/{id}` — دریافت کاربر (Admin)
- `GET /me` — پروفایل فعلی (Authenticated)

### Catalog (`/api/v1/catalog`)
- `POST /products` — ساخت محصول (Admin)
- `POST /products/{id}/price` — تغییر قیمت (Admin)
- `POST /products/{id}/stock` — تعدیل موجودی (Admin)
- `POST /products/{id}/reserve` — رزرو موجودی
- `POST /products/{id}/reserve/{rid}/commit` — تأیید رزرو
- `POST /products/{id}/reserve/{rid}/release` — آزادسازی رزرو

### Orders (`/api/v1/orders`)
- `POST /` — ثبت سفارش (با stock reservation)
- `GET /{id}` — دریافت سفارش
- `POST /{id}/status` — تغییر وضعیت

## تفاوت v3 نسبت به v2

| v2 | v3 |
|----|-----|
| Layer-First (Domain/Application/Infrastructure/Presentation) | **Hexagonal-First** (Application/Adapter/QueryApplication) |
| WolverineFX | **هیچ** — مستقیم متد کال |
| Integration Events | **Transactional ACL** (direct call, same transaction) |
| Domain Event Dispatcher | حذف — events در memory |
| CQRS در همان Application | **QueryApplication جدا** |
| Layer names | **Hexagonal names** (Adapter/Inbound, Adapter/Outbound, Ports) |

## مستندات

- [`docs/ONBOARDING.md`](docs/ONBOARDING.md) — Eagle Eye View
- [`docs/DDD-GUIDELINES.md`](docs/DDD-GUIDELINES.md) — Best Practice‌های DDD
- [`docs/adr/`](docs/adr/) — Architecture Decision Records

## لایسنس

MIT
