# راهنمای شروع به کار (Onboarding) — Version 3

## دید کلی (Eagle Eye)

این پروژه یک **Modular Monolith با معماری Hexagonal-First** است. برخلاف معماری‌های Layer-First سنتی، در اینجا **Hexagon مرکز طراحی است**، نه لایه‌ها.

### سه اصل طراحی v3

۱. **Hexagonal-First**: هر ماژول از داخل به بیرون به‌صورت Ports & Adapters طراحی شده — نه به‌صورت لایه‌های افقی.
۲. **Transactional Inter-Module**: ماژول‌ها به‌صورت همزمان و transactional با هم صحبت می‌کنند — بدون Event، بدون Wolverine، بدون eventual consistency.
۳. **Read-optimized CQS (CQS+) Separation**: سمت خواندن (QueryApplication) از Hexagon جدا است تا overhead آن روی read-heavy operations اعمال نشود.

### نقشه معماری

```
┌─────────────────────────────────────────────┐
│                   Host                       │
│  (Composition Root — no business logic)     │
└──────────┬──────────────────────────────────┘
           │
    ┌──────┴──────┬──────────────┬────────────┐
    │             │              │            │
    ▼             ▼              ▼            ▼
┌────────┐  ┌────────┐    ┌────────┐    ┌────────┐
│Identity│  │Catalog │    │Orders  │    │Catalog │
│Module  │  │Module  │    │Module  │───→│Module  │
└────────┘  └────────┘    └────────┘    └────────┘
    │             │              │            │
    │             │              │     Transactional
    │             │              │      ACL call
    │             │              │            │
    ▼             ▼              ▼            ▼
┌─────────────────────────────────────────────┐
│              SQL Database                    │
│  (identity schema | catalog schema | orders) │
└─────────────────────────────────────────────┘
```

### ساختار هر ماژول

```
{Module}/
├── Application/              ← Hexagon (قلب)
│   ├── Domain/               ← Aggregates, ValueObjects, DomainEvents
│   ├── Ports/
│   │   ├── Inbound/          ← Use Case interfaces (IProductService, etc.)
│   │   └── Outbound/         ← Repository/Gateway interfaces
│   └── Service/              ← Inbound port implementation
├── Adapter/
│   ├── Inbound/Rest/         ← Driving adapter (REST API → calls Inbound ports)
│   └── Outbound/             ← Driven adapters (implements Outbound ports)
│       ├── Repositories/     ← EF Core DbContext + Repository
│       └── Gateways/         ← Cross-module ACL adapters
├── QueryApplication/         ← Read side (Read-optimized CQS (CQS+) — flat, no hexagon)
└── Installer/                ← Composition Root (Add{Module}Module)
```

### جریان یک درخواست

```
HTTP POST /api/v1/orders
    ↓
OrdersEndpoints (Adapter/Inbound/Rest)
    ↓
IOrderService.PlaceOrderAsync (Inbound Port)
    ↓
OrderService (Service — orchestration)
    ↓
ICatalogGateway.ReserveStockAsync (Outbound Port — ACL)
    ↓
CatalogGateway (Adapter/Outbound/Gateway)
    ↓
IProductService.ReserveStockAsync (Catalog Inbound Port — direct call!)
    ↓
ProductService → Product.ReserveStock() → SaveChanges
```

**نکته**: همه این‌ها در **یک transaction** اتفاق می‌افتند — بدون message bus، بدون eventual consistency.

---

## اجرای پروژه

```bash
dotnet restore ModularMonolith.slnx
dotnet build ModularMonolith.slnx
export JWT__SIGNINGKEY=$(openssl rand -base64 48)
dotnet run --project src/Host/ModularMonolith.Host
```

---

## قوانین طلایی

### ۱. Application به Adapter وابسته نیست
Hexagon (Application) هیچ‌گاه به Adapter ارجاع نمی‌دهد. Adapterها به Application وابسته‌اند، نه برعکس.

### ۲. بدون Wolverine، بدون Event
ارتباط بین ماژول‌ها از طریق **direct method call** از طریق Port interfaceها انجام می‌شود — در همان transaction.

### ۳. QueryApplication جدا
عملیات خواندن از Hexagon خارج است — مستقیم projection، بدون aggregate loading، بدون domain rules.

### ۴. هر Adapter یک مسئولیت
`Adapter/Inbound/Rest` فقط HTTP → Inbound Port. `Adapter/Outbound/Repositories` فقط Outbound Port → EF Core.

### ۵. Module Installer
Host فقط `Add{Module}Module()` را صدا می‌زند — هیچ‌چیز از داخل ماژول نمی‌داند.

---

## سؤالات متداول

**س: چرا Wolverine حذف شد؟**
ج: چون ارتباط بین ماژول‌ها transactional است، نه event-based. Wolverine برای event publishing و outbox بود که دیگر نیاز نیست.

**س: چرا QueryApplication جدا است؟**
ج: چون Hexagonal overhead (ports, aggregate loading) برای read-heavy operations مناسب نیست. Read-optimized CQS (CQS+) separation این مشکل را حل می‌کند.

**س: چگونه یک ماژول را به microservice تبدیل کنم؟**
ج: فقط `Adapter/Outbound/Gateways/CatalogGateway` را با HTTP client جایگزین کنید — Application دست‌نخورده می‌ماند.

خوش آمدید! 🚀

