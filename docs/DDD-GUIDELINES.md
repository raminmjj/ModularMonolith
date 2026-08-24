# راهنمای Best Practice‌های DDD در ModularMonolith v3

این سند مکمل `ONBOARDING.md` است — به‌جای «چطور کار کن»، «چطور فکر کن» را توضیح می‌دهد.

## ۱. Aggregate Root را کوچک نگه دار
یک Aggregate = یک transaction. هر Aggregate بزرگتر = lock طولانی‌تر.

## ۲. Reference by ID
Aggregate A هرگز reference مستقیم به Aggregate B ندارد — فقط `Id`.

## ۳. Value Object را فراموش نکن
`Money`, `Email`, `Sku` — همه ValueObject هستند. Primitive Obsession ممنوع.

## ۴. Business Logic در Aggregate
قوانین بیزینس در متدهای Aggregate، نه در Service. Service فقط orchestration.

## ۵. Repository فقط برای Aggregate Root
هرگز Repository برای child entity نسازید.

## ۶. Ports در Application، Adapters در Adapter
`IProductRepository` در `Application/Ports/Outbound`، `ProductRepository` در `Adapter/Outbound/Repositories`.

## ۷. QueryApplication فقط Read است ⚠️

این یک قانون غیرقابل مذاکره است:

**مجاز:**
- Search, Filter, Sort, Projection, Pagination
- Report, Grid, Dashboard queries
- Direct EF Core projections (بدون aggregate loading)

**غیرمجاز:**
- `ChangePrice()`, `ReserveStock()`, `CreateOrder()` — هر تغییری در domain state
- Aggregate loading برای read (باید از Application استفاده شود)
- Write operations هر نوع

> اگر مرز QueryApplication شل شود، تبدیل می‌شود به «هر چیزی که جا نشد» — این یک آشغال‌دانی معماری است.

## ۸. Transactional ACL
ارتباط بین ماژول‌ها از طریق Port interface — direct call در همان transaction. بدون Event، بدون Wolverine.

## ۹. Mapperly برای mapping
Source-generated، compile-time safety، صفر reflection.

## ۱۰. Architecture Tests
هرگز Module boundary را نقض نکن — NetArchTest مانع این کار می‌شود.

## ۱۱. Module Boundary — غیرقابل مذاکره

هیچ ماژولی نباید به Application ماژول دیگر ارجاع دهد.

```text
❌ Orders.Application → Catalog.Application
❌ Catalog.Application → Identity.Application

✅ Orders.Application → Contracts (shared DTOs)
✅ Orders.Adapter.Outbound → Catalog.Application (ACL gateway)
```

اگر نیاز به داده‌های ماژول دیگر دارید:
1. DTO در `Contracts` تعریف کنید
2. Outbound Port در Application ماژول مصرف‌کننده تعریف کنید
3. Adapter در ماژول مصرف‌کننده، آن Port را به ماژول مقصد وصل کنید
