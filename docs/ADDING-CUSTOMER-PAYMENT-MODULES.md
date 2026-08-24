# Adding the **Customer** & **Payment** Modules — Implementation Guide

> **Audience:** Developers extending ModularMonolith v3 (.NET 10, Hexagonal-First Modular Monolith).
>
> **Golden rules (non-negotiable):**
> - NO events, NO message bus, NO Wolverine, NO MediatR. Cross-module communication = **transactional direct method calls** through hexagonal ports.
> - An `Application/` hexagon **never** references another module's `Application/`. Only the consumer's `Adapter/Outbound` may reference the provider's `Application` (to call its inbound port).
> - Shared data crosses boundaries exclusively as **DTOs from `BuildingBlocks/Contracts/`** — never as domain entities.
> - **PCI-DSS stance:** raw card numbers never enter this monolith. Cards are tokenized by an upstream vault; we store and pass **tokens only**.

---

## 1. The Business Story & Scenario

### Why two bounded contexts?

Our e-commerce platform has two distinct subdomains that evolve at different speeds and for different reasons:

- **Customer** owns the *identity-of-the-buyer* language: profiles, shipping/billing addresses, account standing, and the customer's *wallet* of saved payment instruments (as opaque vault tokens). Its ubiquitous language is "who is this customer and what do they prefer?"
- **Payment** owns the *money-movement* language: authorization, capture, refunds, failure codes, transaction lifecycle. Its ubiquitous language is "did the money move?"

They share *nothing behaviorally*. A payment does not care how addresses work; a customer does not care about capture semantics. Merging them would couple compliance-sensitive money logic to CRM-style profile logic and force both to deploy together forever. Two contexts, one transaction boundary each, joined by a narrow synchronous contract — classic modular monolith.

### The use case

> A logged-in customer pays for order `#8842` using their saved Visa card.
>
> 1. The **Payment** module receives `InitiatePayment(customerId, orderId, amount, savedPaymentMethodId)`.
> 2. Before doing anything, it must ask Customer: *"Is this account in good standing?"* — a suspended/fraud-flagged account must be rejected before any PSP call.
> 3. It then asks Customer: *"Give me the saved payment method for this card"* — and receives only a **token**, never a PAN.
> 4. Payment creates a `PaymentTransaction` aggregate snapshotting the token, and proceeds with processing.
>
> Steps 2–3 happen inside the same HTTP request and the same logical transaction as step 4. If Customer says "suspended", nothing is persisted. No eventual consistency, no outbox, no events — a direct, transactional method call.

---

## 2. Architecture & Dependency Mapping

### Roles

| Module | Role | Owns |
|---|---|---|
| **Customer** | **Provider** | Profiles, addresses, saved payment methods (tokens). Exposes `ICustomerInboundPort`. |
| **Payment** | **Consumer** | Transactions, method snapshots, status lifecycle. Defines `ICustomerGatewayPort` (outbound port). |

### Shared contracts (`BuildingBlocks/Contracts/`)

Namespace: `ModularMonolith.Contracts.Customer` (one contracts project, folder-per-module — same pattern as `Contracts\Catalog`).

```csharp
public sealed record CustomerStatusDto(Guid CustomerId, bool IsSuspended, string AccountTier);

public sealed record SavedPaymentMethodDto(
    Guid PaymentMethodId,
    string TokenizedCardNumber,   // vault token, e.g. "tok_visa_8f3a…". NEVER a raw PAN.
    DateOnly ExpiryDate,
    string CardType);
```

Read-side projections (used only by the two QueryApplications):

```csharp
public sealed record CustomerSnapshot(Guid Id, Guid IdentityUserId, string Status, string AccountTier);
public sealed record PaymentSnapshot(Guid Id, Guid CustomerId, Guid OrderId, decimal Amount,
                                     string Currency, string Status, string MethodToken);
```

### The transactional call chain

```
Payment.Application.Service.PaymentService          (hexagon, orchestrates use case)
        │ depends on (port owned by consumer)
        ▼
Payment.Application.Ports.Outbound.ICustomerGatewayPort     ◄── interface, lives in CONSUMER
        ▲ implemented by
        │
Payment.Adapter.Outbound.Gateways.CustomerGateway           ◄── internal sealed ACL gateway
        │ direct method call (same process, same transaction)
        ▼
Customer.Application.Ports.Inbound.ICustomerInboundPort     ◄── interface, lives in PROVIDER
        ▲ implemented by
        │
Customer.Application.Service.CustomerService                ◄── sealed service (provider hexagon)
```

**Naming note:** prose sometimes says "*Customer.Adapter.Inbound*" — physically, the inbound port lives in `Customer/Application/Ports/Inbound/` so that consumers depend only on the provider's **Application** assembly, never its Adapter assemblies. This is exactly how the existing `Orders → Catalog` ACL works (`CatalogGateway` → `IProductService`). We add a second sanctioned exception:

| Allowed exception (the ONLY ones) | Reason |
|---|---|
| `Orders.Adapter.Outbound` → `Catalog.Application` | Stock reservation ACL |
| **`Payment.Adapter.Outbound` → `Customer.Application`** | **This guide: customer verification ACL** |

### Project reference map (what makes the tests green)

```
Customer.Application            ──► DDD, SharedKernel, Contracts, Framework        (NO module refs)
Customer.Adapter.Outbound       ──► Customer.Application, Infrastructure.Persistence
Customer.Adapter.Inbound.Rest   ──► Customer.Application, Framework, Contracts
Customer.QueryApplication       ──► Customer.Adapter.Outbound (DbContext), Contracts, Framework
Customer.Installer              ──► all four above

Payment.Application             ──► DDD, SharedKernel, Contracts, Framework        (NO module refs!)
Payment.Adapter.Outbound        ──► Payment.Application, Infrastructure.Persistence,
                                    Customer.Application   ← EXCEPTION #2
Payment.Adapter.Inbound.Rest    ──► Payment.Application, Framework, Contracts
Payment.QueryApplication        ──► Payment.Adapter.Outbound, Contracts, Framework
Payment.Installer               ──► all four above
```

---

## 3. Step-by-Step Implementation Guide

### Step 0 — Scaffold the ten projects

**WHAT:** 5 projects per module, mirroring `src/Modules/Orders/` exactly.

#### Full file tree (every file this guide creates or edits)

```
ModularMonolith.slnx                                    ✎ EDIT — register 10 new projects
│
src/
├── BuildingBlocks/
│   └── Contracts/                                      (one project, folder-per-module)
│       ├── Customer/
│       │   └── CustomerDtos.cs                         ★ CustomerStatusDto, SavedPaymentMethodDto, CustomerSnapshot
│       └── Payment/
│           └── PaymentDtos.cs                          ★ PaymentSnapshot
│
├── Modules/
│   ├── Customer/                                       ◄── PROVIDER
│   │   ├── Application/                                ← the hexagon (no EF, no adapters)
│   │   │   ├── ModularMonolith.Modules.Customer.Application.csproj
│   │   │   ├── DependencyInjection.cs                  AddCustomerApplication()
│   │   │   ├── Domain/
│   │   │   │   ├── Customers/
│   │   │   │   │   ├── Customer.cs                     AggregateRoot (profile, wallet)
│   │   │   │   │   ├── SavedPaymentMethod.cs           Entity (token only!)
│   │   │   │   │   └── Events/
│   │   │   │   │       └── CustomerEvents.cs           records only — NO dispatcher
│   │   │   │   └── ValueObjects/
│   │   │   │       ├── Address.cs                      VO
│   │   │   │       └── CustomerStatus.cs               smart enum VO
│   │   │   ├── Ports/
│   │   │   │   ├── Inbound/
│   │   │   │   │   └── ICustomerInboundPort.cs         ★ provider boundary (REST + Payment ACL)
│   │   │   │   └── Outbound/
│   │   │   │       ├── ICustomerRepository.cs
│   │   │   │       └── IUnitOfWork.cs
│   │   │   └── Service/
│   │   │       └── CustomerService.cs                  sealed inbound-port implementation
│   │   ├── Adapter/
│   │   │   ├── Inbound/
│   │   │   │   └── Rest/
│   │   │   │       ├── ModularMonolith.Modules.Customer.Adapter.Inbound.Rest.csproj
│   │   │   │       ├── Dtos/
│   │   │   │       │   └── CustomerRequestDtos.cs      requests carry tok_… tokens ONLY
│   │   │   │       └── Endpoints/
│   │   │   │           └── CustomersEndpoints.cs       /api/v1/customers + MapCustomerRestEndpoints()
│   │   │   └── Outbound/
│   │   │       ├── ModularMonolith.Modules.Customer.Adapter.Outbound.csproj
│   │   │       ├── DependencyInjection.cs              AddCustomerOutboundAdapters(config)
│   │   │       └── Repositories/
│   │   │           ├── CustomerRepository.cs           implements ICustomerRepository
│   │   │           └── SqlServer/
│   │   │               ├── CustomerDbContext.cs
│   │   │               └── Configurations/
│   │   │                   └── CustomerConfiguration.cs (+ owned types config)
│   │   ├── QueryApplication/                           read-only projections
│   │   │   ├── ModularMonolith.Modules.Customer.QueryApplication.csproj
│   │   │   ├── DependencyInjection.cs                  AddCustomerQueryApplication()
│   │   │   └── CustomerQueryService.cs                 DbContext → CustomerSnapshot
│   │   └── Installer/
│   │       ├── ModularMonolith.Modules.Customer.Installer.csproj
│   │       └── ModuleInstaller.cs                      AddCustomerModule(config)
│   │
│   └── Payment/                                        ◄── CONSUMER
│       ├── Application/                                ← knows NOTHING about Customer internals
│       │   ├── ModularMonolith.Modules.Payment.Application.csproj
│       │   ├── DependencyInjection.cs                  AddPaymentApplication()
│       │   ├── Domain/
│       │   │   ├── Payments/
│       │   │   │   ├── PaymentTransaction.cs           AggregateRoot (Pending→Captured/Failed)
│       │   │   │   └── Events/
│       │   │   │       └── PaymentEvents.cs            records only — NO dispatcher
│       │   │   └── ValueObjects/
│       │   │       ├── PaymentMethodSnapshot.cs        VO — vault token enforced (tok_ prefix)
│       │   │       └── PaymentStatus.cs                smart enum VO
│       │   ├── Ports/
│       │   │   ├── Inbound/
│       │   │   │   └── IPaymentService.cs
│       │   │   └── Outbound/
│       │   │       ├── ICustomerGatewayPort.cs         ★ consumer-owned outbound port
│       │   │       ├── IPaymentTransactionRepository.cs
│       │   │       └── IUnitOfWork.cs
│       │   └── Service/
│       │       └── PaymentService.cs                   verify status → fetch token → persist
│       ├── Adapter/
│       │   ├── Inbound/
│       │   │   └── Rest/
│       │   │       ├── ModularMonolith.Modules.Payment.Adapter.Inbound.Rest.csproj
│       │   │       ├── Dtos/
│       │   │       │   └── PaymentRequestDtos.cs       no PAN-shaped fields possible
│       │   │       └── Endpoints/
│       │   │           └── PaymentsEndpoints.cs        /api/v1/payments + MapPaymentsRestEndpoints()
│       │   └── Outbound/                               ← the ONLY place Customer.Application is referenced
│       │       ├── ModularMonolith.Modules.Payment.Adapter.Outbound.csproj   ★ ProjectReference → Customer.Application
│       │       ├── DependencyInjection.cs              AddPaymentOutboundAdapters(config) — registers gateway
│       │       ├── Gateways/
│       │       │   └── CustomerGateway.cs              ★ internal sealed : ICustomerGatewayPort → ICustomerInboundPort
│       │       └── Repositories/
│       │           ├── PaymentTransactionRepository.cs
│       │           └── SqlServer/
│       │               ├── PaymentDbContext.cs
│       │               └── Configurations/
│       │                   └── PaymentTransactionConfiguration.cs
│       ├── QueryApplication/
│       │   ├── ModularMonolith.Modules.Payment.QueryApplication.csproj
│       │   ├── DependencyInjection.cs                  AddPaymentQueryApplication()
│       │   └── PaymentQueryService.cs                  DbContext → PaymentSnapshot
│       └── Installer/
│           ├── ModularMonolith.Modules.Payment.Installer.csproj
│           └── ModuleInstaller.cs                      AddPaymentModule(config)
│
└── Host/
    └── ModularMonolith.Host/
        └── Program.cs                                  ✎ EDIT — wire modules, health checks, endpoint maps

tests/
└── ArchitectureTests/
    └── HexagonalBoundaryTests.cs                       ✎ EDIT — extend lists + 3 new rules (see §4)

docker-compose.yml                                     ✎ EDIT — ConnectionStrings__{Customer,Payment}
```

**Legend:** ★ = architecturally critical file · ✎ = existing file you edit (everything else is new).

**HOW:** Copy the csproj files from the Orders equivalents and adjust `RootNamespace` + project references per the map above. Critical details copied from existing modules:

```xml
<!-- Payment.Adapter.Outbound.csproj — the ONLY place Customer.Application may appear -->
<ProjectReference Include="..\..\..\Customer\Application\ModularMonolith.Modules.Customer.Application.csproj" />
```

Register all ten projects in `ModularMonolith.slnx` under `/src/Modules/Customer/` and `/src/Modules/Payment/` folders.

---

### Step 1 — Shared Contracts

**WHAT:** `CustomerStatusDto`, `SavedPaymentMethodDto` (+ the two read-side snapshots).
**WHERE:** `src/BuildingBlocks/Contracts/Customer/CustomerDtos.cs`
**RULES:** Records only, no behavior, no domain types, no EF attributes. These are the *only* Customer vocabulary Payment will ever see.

```csharp
namespace ModularMonolith.Contracts.Customer;

/// <summary>Cross-module view of account standing. Consumed by Payment via ACL.</summary>
public sealed record CustomerStatusDto(Guid CustomerId, bool IsSuspended, string AccountTier);

/// <summary>Saved instrument as an opaque VAULT TOKEN. Raw PANs never cross any boundary.</summary>
public sealed record SavedPaymentMethodDto(
    Guid PaymentMethodId,
    string TokenizedCardNumber,
    DateOnly ExpiryDate,
    string CardType);

/// <summary>Read-side projection (QueryApplication only).</summary>
public sealed record CustomerSnapshot(Guid Id, Guid IdentityUserId, string Status, string AccountTier);
```

And `src/BuildingBlocks/Contracts/Payment/PaymentDtos.cs`:

```csharp
namespace ModularMonolith.Contracts.Payment;

/// <summary>Read-side projection (QueryApplication only).</summary>
public sealed record PaymentSnapshot(
    Guid Id, Guid CustomerId, Guid OrderId,
    decimal Amount, string Currency, string Status, string MethodToken);
```

---

### Step 2 — Provider Module: **Customer**

#### 2a. Domain

**WHAT:** `Customer` aggregate root, `Address` value object, `SavedPaymentMethod` entity.
**WHERE:** `src/Modules/Customer/Application/Domain/Customers/`

```csharp
// Domain/Customers/Customer.cs
using ModularMonolith.DDD.Common;
using ModularMonolith.Modules.Customer.Application.Domain.ValueObjects;

namespace ModularMonolith.Modules.Customer.Application.Domain.Customers;

public sealed class Customer : AggregateRoot<Guid>
{
    public Guid IdentityUserId { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public CustomerStatus Status { get; private set; } = null!;
    public string AccountTier { get; private set; } = "Standard";

    private readonly List<Address> _addresses = [];
    public IReadOnlyCollection<Address> Addresses => _addresses.AsReadOnly();

    private readonly List<SavedPaymentMethod> _paymentMethods = [];
    public IReadOnlyCollection<SavedPaymentMethod> PaymentMethods => _paymentMethods.AsReadOnly();

    private Customer() { }

    public static Customer Register(Guid identityUserId, string displayName)
    {
        if (identityUserId == Guid.Empty)
            throw new DomainException("CUSTOMER_IDENTITY_REQUIRED", "Identity user id is required.");

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            IdentityUserId = identityUserId,
            DisplayName = displayName,
            Status = CustomerStatus.Active,
        };
        customer.Raise(new CustomerRegisteredDomainEvent(customer.Id));
        return customer;
    }

    public void AddAddress(Address address) => _addresses.Add(address);

    /// <summary>Stores a VAULT TOKEN — this method must never receive a raw PAN.</summary>
    public SavedPaymentMethod AddSavedPaymentMethod(string tokenizedCard, string cardType, DateOnly expiry)
        => SavedPaymentMethod.Create(Id, tokenizedCard, cardType, expiry, index: _paymentMethods.Count);

    public void Suspend()
    {
        if (Status == CustomerStatus.Suspended) return;
        Status = CustomerStatus.Suspended;
        Raise(new CustomerSuspendedDomainEvent(Id));
    }

    public void Reactivate() => Status = CustomerStatus.Active;
}

// Domain/ValueObjects/CustomerStatus.cs — smart enum, OrderStatus-style
public sealed class CustomerStatus : ValueObject
{
    public static readonly CustomerStatus Active = new("Active");
    public static readonly CustomerStatus Suspended = new("Suspended");

    public string Value { get; }
    private CustomerStatus(string value) => Value = value;
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}
```

```csharp
// Domain/ValueObjects/Address.cs
using ModularMonolith.DDD.Common;

namespace ModularMonolith.Modules.Customer.Application.Domain.ValueObjects;

public sealed class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }

    private Address(string street, string city, string postalCode, string country)
        => (Street, City, PostalCode, Country) = (street, city, postalCode, country);

    public static Address Create(string street, string city, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street)) throw new DomainException("ADDRESS_STREET_REQUIRED", "Street is required.");
        if (string.IsNullOrWhiteSpace(country) || country.Length != 2)
            throw new DomainException("ADDRESS_COUNTRY_INVALID", "Country must be an ISO-3166 alpha-2 code.");
        return new Address(street.Trim(), city.Trim(), postalCode.Trim(), country.ToUpperInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street; yield return City; yield return PostalCode; yield return Country;
    }
}
```

```csharp
// Domain/Customers/SavedPaymentMethod.cs — entity, NOT an aggregate (no independent lifecycle)
using ModularMonolith.DDD.Common;

namespace ModularMonolith.Modules.Customer.Application.Domain.Customers;

public sealed class SavedPaymentMethod : Entity<Guid>
{
    public Guid CustomerId { get; private set; }
    public string TokenizedCard { get; private set; } = null!; // vault token only
    public string CardType { get; private set; } = null!;
    public DateOnly ExpiryDate { get; private set; }
    public bool IsDefault { get; private set; }

    private SavedPaymentMethod() { }

    internal static SavedPaymentMethod Create(Guid customerId, string token, string cardType, DateOnly expiry, int index)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("tok_"))
            throw new DomainException("PAYMENT_METHOD_TOKEN_INVALID",
                "A vault token ('tok_…') is required. Raw card numbers are not accepted.");
        return new SavedPaymentMethod
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            TokenizedCard = token,
            CardType = cardType,
            ExpiryDate = expiry,
            IsDefault = index == 0,
        };
    }
}
```

> 🔒 **The domain itself rejects non-token values** (`tok_` prefix check). PCI hygiene is enforced at the deepest layer, not just at the edge.

#### 2b. Application ports

**WHERE:** `src/Modules/Customer/Application/Ports/`

```csharp
// Ports/Inbound/ICustomerInboundPort.cs  ★ THE provider boundary Payment will call
using ModularMonolith.Contracts.Customer;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Customer.Application.Domain.Customers;

namespace ModularMonolith.Modules.Customer.Application.Ports.Inbound;

/// <summary>
/// Inbound port — the hexagon's driving boundary. Used by BOTH the REST adapter
/// and (cross-module) Payment.Adapter.Outbound.CustomerGateway. Direct method
/// calls only — no events, no bus.
/// </summary>
public interface ICustomerInboundPort
{
    // Profile lifecycle (REST-facing)
    Task<Result<Guid>> RegisterAsync(Guid identityUserId, string displayName, CancellationToken ct = default);
    Task<Result> AddAddressAsync(Guid customerId, string street, string city, string postalCode, string country, CancellationToken ct = default);

    // Wallet (REST-facing) — tokens only
    Task<Result<Guid>> SavePaymentMethodAsync(Guid customerId, string tokenizedCard, string cardType, DateOnly expiry, CancellationToken ct = default);

    // Standing (ACL-facing — consumed by Payment)
    Task<Result<CustomerStatusDto>> GetStatusAsync(Guid customerId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SavedPaymentMethodDto>>> GetSavedPaymentMethodsAsync(Guid customerId, CancellationToken ct = default);
    Task<Result<SavedPaymentMethodDto>> GetSavedPaymentMethodAsync(Guid customerId, Guid paymentMethodId, CancellationToken ct = default);
}
```

```csharp
// Ports/Outbound/ICustomerRepository.cs
namespace ModularMonolith.Modules.Customer.Application.Ports.Outbound;

public interface ICustomerRepository
{
    Task<Domain.Customers.Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsForIdentityUserAsync(Guid identityUserId, CancellationToken ct = default);
    Task AddAsync(Domain.Customers.Customer aggregate, CancellationToken ct = default);
}

// Ports/Outbound/IUnitOfWork.cs — module-owned UoW port (same as Catalog/Orders pattern)
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<Framework.Results.Result> ExecuteInTransactionAsync(
        Func<Task<Framework.Results.Result>> action, CancellationToken ct = default);
}
```

#### 2c. Service (sealed)

**WHERE:** `src/Modules/Customer/Application/Service/CustomerService.cs`

```csharp
using ModularMonolith.Contracts.Customer;
using ModularMonolith.DDD.Common;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Customer.Application.Domain.Customers;
using ModularMonolith.Modules.Customer.Application.Domain.ValueObjects;
using ModularMonolith.Modules.Customer.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Customer.Application.Service;

public sealed class CustomerService : Ports.Inbound.ICustomerInboundPort
{
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _unitOfWork;

    public CustomerService(ICustomerRepository customers, IUnitOfWork unitOfWork)
        => (_customers, _unitOfWork) = (customers, unitOfWork);

    public async Task<Result<CustomerStatusDto>> GetStatusAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(customerId, ct);
        return customer is null
            ? Result.Failure<CustomerStatusDto>(new Error("CUSTOMER_NOT_FOUND", $"Customer '{customerId}' was not found."))
            : Result.Success(new CustomerStatusDto(customer.Id, customer.Status == CustomerStatus.Suspended, customer.AccountTier));
    }

    public async Task<Result<SavedPaymentMethodDto>> GetSavedPaymentMethodAsync(Guid customerId, Guid paymentMethodId, CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(customerId, ct);
        var method = customer?.PaymentMethods.FirstOrDefault(m => m.Id == paymentMethodId);
        return method is null
            ? Result.Failure<SavedPaymentMethodDto>(new Error("PAYMENT_METHOD_NOT_FOUND", "Saved payment method was not found."))
            : Result.Success(new SavedPaymentMethodDto(method.Id, method.TokenizedCard, method.ExpiryDate, method.CardType));
    }

    public async Task<Result<IReadOnlyList<SavedPaymentMethodDto>>> GetSavedPaymentMethodsAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(customerId, ct);
        if (customer is null)
            return Result.Failure<IReadOnlyList<SavedPaymentMethodDto>>(new Error("CUSTOMER_NOT_FOUND", "Customer was not found."));
        IReadOnlyList<SavedPaymentMethodDto> dtos = [.. customer.PaymentMethods
            .Select(m => new SavedPaymentMethodDto(m.Id, m.TokenizedCard, m.ExpiryDate, m.CardType))];
        return Result.Success(dtos);
    }

    public async Task<Result<Guid>> SavePaymentMethodAsync(Guid customerId, string tokenizedCard, string cardType, DateOnly expiry, CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(customerId, ct);
        if (customer is null) return Result.Failure<Guid>(new Error("CUSTOMER_NOT_FOUND", "Customer was not found."));

        var method = customer.AddSavedPaymentMethod(tokenizedCard, cardType, expiry); // domain validates tok_ prefix
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(method.Id);
    }

    // RegisterAsync / AddAddressAsync follow the identical load → mutate → save pattern. Omitted for brevity.
    public Task<Result<Guid>> RegisterAsync(Guid identityUserId, string displayName, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Result> AddAddressAsync(Guid customerId, string street, string city, string postalCode, string country, CancellationToken ct = default) => throw new NotImplementedException();
}
```

#### 2d. Outbound adapter (EF Core)

**WHERE:** `src/Modules/Customer/Adapter/Outbound/`

```csharp
// Repositories/CustomerRepository.cs
internal sealed class CustomerRepository(CustomerDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Customers.Include(c => c.PaymentMethods).FirstOrDefaultAsync(c => c.Id == id, ct);
    public Task<bool> ExistsForIdentityUserAsync(Guid identityUserId, CancellationToken ct = default) =>
        db.Customers.AnyAsync(c => c.IdentityUserId == identityUserId, ct);
    public async Task AddAsync(Customer aggregate, CancellationToken ct = default) =>
        await db.Customers.AddAsync(aggregate, ct);
}
```

```csharp
// Repositories/SqlServer/CustomerDbContext.cs
public sealed class CustomerDbContext(DbContextOptions<CustomerDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    protected override void OnModelCreating(ModelBuilder b) => b.ApplyConfigurationsFromAssembly(GetType().Assembly);
}
```

Plus `Configurations/CustomerConfiguration.cs` (owning side for `_paymentMethods`/`_addresses`, unique index on `IdentityUserId`, token column max-length ~200) and a `DependencyInjection.cs` registering `AddModuleDbContext<CustomerDbContext>(config, "Customer", "Customer", "__EFMigrationsHistory_Customer")`, `DbContext → CustomerDbContext` mapping, `ICustomerRepository`, `IUnitOfWork → EfCoreUnitOfWork<CustomerDbContext>` — copy verbatim from `Orders.Adapter.Outbound/DependencyInjection.cs`.

#### 2e. Inbound adapter (REST)

**WHERE:** `src/Modules/Customer/Adapter/Inbound/Rest/Endpoints/CustomersEndpoints.cs`
Pattern: copy `OrdersEndpoints.cs` — implement `IEndpoint`, map group `/api/v1/customers`, resolve `ICustomerInboundPort`, translate `Result` → `Results.Ok/Created/BadRequest(ProblemDetails)`. Endpoints: `POST /` (register), `POST /{id}/addresses`, `POST /{id}/payment-methods` (**accepts `tokenizedCard` from the client-side vault SDK — reject anything without `tok_` prefix**), `GET /{id}` (via QueryApplication), `POST /{id}/suspend` (admin policy).

---

### Step 3 — Consumer Module: **Payment**

#### 3a. Domain

**WHERE:** `src/Modules/Payment/Application/Domain/Payments/`

```csharp
// Domain/ValueObjects/PaymentMethodSnapshot.cs — immutable record of WHAT was charged
using ModularMonolith.DDD.Common;

namespace ModularMonolith.Modules.Payment.Application.Domain.ValueObjects;

public sealed class PaymentMethodSnapshot : ValueObject
{
    public string Token { get; }        // vault token — NEVER a PAN
    public string CardType { get; }
    public DateOnly ExpiryDate { get; }

    private PaymentMethodSnapshot(string token, string cardType, DateOnly expiryDate)
        => (Token, CardType, ExpiryDate) = (token, cardType, expiryDate);

    public static PaymentMethodSnapshot FromSaved(SavedPaymentMethodDto dto) =>
        new(NormalizeToken(dto.TokenizedCardNumber), dto.CardType, dto.ExpiryDate);

    public static PaymentMethodSnapshot FromNewCardToken(string token, string cardType, DateOnly expiry) =>
        new(NormalizeToken(token), cardType, expiry);

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("tok_"))
            throw new DomainException("PAYMENT_TOKEN_INVALID",
                "Only vault tokens are accepted. Raw card data must never reach the Payment module.");
        return token;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Token; yield return CardType; yield return ExpiryDate;
    }
}
```

```csharp
// Domain/Payments/PaymentTransaction.cs
using ModularMonolith.DDD.Common;
using ModularMonolith.SharedKernel.ValueObjects;
using ModularMonolith.Modules.Payment.Application.Domain.Events;
using ModularMonolith.Modules.Payment.Application.Domain.ValueObjects;

namespace ModularMonolith.Modules.Payment.Application.Domain.Payments;

public sealed class PaymentTransaction : AggregateRoot<Guid>
{
    public Guid CustomerId { get; private set; }
    public Guid OrderId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public PaymentStatus Status { get; private set; } = null!;
    public PaymentMethodSnapshot Method { get; private set; } = null!; // token snapshot
    public DateTimeOffset CreatedAt { get; private set; }

    private PaymentTransaction() { }

    /// <summary>Factory requires proof of prior verification — caller passes verified inputs.</summary>
    public static PaymentTransaction Initiate(
        Guid customerId, Guid orderId, Money amount, PaymentMethodSnapshot method)
    {
        if (customerId == Guid.Empty || orderId == Guid.Empty)
            throw new DomainException("PARTY_IDS_REQUIRED", "Customer and order ids are required.");
        if (amount.Amount <= 0)
            throw new DomainException("AMOUNT_POSITIVE_REQUIRED", "Amount must be positive.");

        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            OrderId = orderId,
            Amount = amount,
            Method = method,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        tx.Raise(new PaymentInitiatedDomainEvent(tx.Id, customerId, amount.Amount, amount.Currency));
        return tx;
    }

    public void MarkCaptured() { EnsurePending(); Status = PaymentStatus.Captured; Raise(new PaymentCapturedDomainEvent(Id)); }
    public void MarkFailed(string reason)
    {
        EnsurePending();
        Status = PaymentStatus.Failed;
        Raise(new PaymentFailedDomainEvent(Id, reason));
    }
    private void EnsurePending()
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("PAYMENT_NOT_PENDING", $"Cannot transition from '{Status.Value}'.");
    }
}

public sealed class PaymentStatus : ValueObject
{
    public static readonly PaymentStatus Pending = new("Pending");
    public static readonly PaymentStatus Captured = new("Captured");
    public static readonly PaymentStatus Failed = new("Failed");
    public string Value { get; }
    private PaymentStatus(string value) => Value = value;
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}
```

#### 3b. Application ports — including the crucial gateway port

**WHERE:** `src/Modules/Payment/Application/Ports/`

```csharp
// Ports/Outbound/ICustomerGatewayPort.cs  ★ THE consumer-owned outbound port
using ModularMonolith.Contracts.Customer;
using ModularMonolith.Framework.Results;

namespace ModularMonolith.Modules.Payment.Application.Ports.Outbound;

/// <summary>
/// Transactional ACL port — Payment asks Customer synchronously within the same
/// transaction. NO events, NO message bus. Implemented by CustomerGateway in
/// Adapter/Outbound/Gateways, which delegates DIRECTLY to Customer's
/// ICustomerInboundPort. Uses ONLY Contracts DTOs — zero knowledge of
/// Customer's internals.
/// </summary>
public interface ICustomerGatewayPort
{
    Task<Result<CustomerStatusDto>> GetCustomerStatusAsync(Guid customerId, CancellationToken ct = default);
    Task<Result<SavedPaymentMethodDto>> GetSavedPaymentMethodAsync(Guid customerId, Guid paymentMethodId, CancellationToken ct = default);
}
```

```csharp
// Ports/Outbound/IPaymentTransactionRepository.cs
public interface IPaymentTransactionRepository
{
    Task<Domain.Payments.PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Domain.Payments.PaymentTransaction aggregate, CancellationToken ct = default);
}

// Ports/Outbound/IUnitOfWork.cs — same module-owned UoW port as above

// Ports/Inbound/IPaymentService.cs
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Payment.Application.Domain.Payments;

namespace ModularMonolith.Modules.Payment.Application.Ports.Inbound;

public interface IPaymentService
{
    Task<Result<Guid>> InitiateWithSavedCardAsync(Guid customerId, Guid orderId, decimal amount, string currency, Guid savedPaymentMethodId, CancellationToken ct = default);
    Task<Result<Guid>> InitiateWithNewCardTokenAsync(Guid customerId, Guid orderId, decimal amount, string currency, string cardToken, string cardType, DateOnly expiry, CancellationToken ct = default);
    Task<Result> CaptureAsync(Guid paymentId, CancellationToken ct = default);
    Task<Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default);
}
```

#### 3c. Service — orchestration showing the ACL flow

**WHERE:** `src/Modules/Payment/Application/Service/PaymentService.cs`

```csharp
using ModularMonolith.DDD.Common;
using ModularMonolith.Framework.Results;
using ModularMonolith.SharedKernel.ValueObjects;
using ModularMonolith.Modules.Payment.Application.Domain.Payments;
using ModularMonolith.Modules.Payment.Application.Domain.ValueObjects;
using ModularMonolith.Modules.Payment.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Payment.Application.Service;

public sealed class PaymentService : Ports.Inbound.IPaymentService
{
    private readonly IPaymentTransactionRepository _transactions;
    private readonly ICustomerGatewayPort _customers; // consumer-owned port — NOT Customer's type!
    private readonly IUnitOfWork _unitOfWork;

    public PaymentService(IPaymentTransactionRepository transactions, ICustomerGatewayPort customers, IUnitOfWork unitOfWork)
        => (_transactions, _customers, _unitOfWork) = (transactions, customers, unitOfWork);

    public async Task<Result<Guid>> InitiateWithSavedCardAsync(
        Guid customerId, Guid orderId, decimal amount, string currency, Guid savedPaymentMethodId, CancellationToken ct = default)
    {
        // Phase 1 — verify standing via transactional ACL (direct call into Customer hexagon)
        var status = await _customers.GetCustomerStatusAsync(customerId, ct);
        if (!status.IsSuccess) return Result.Failure<Guid>(status.Error);
        if (status.Value!.IsSuspended)
            return Result.Failure<Guid>(new Error("CUSTOMER_SUSPENDED", "Account is suspended; payment refused."));

        // Phase 2 — fetch the saved card TOKEN via the same ACL (token only, never a PAN)
        var method = await _customers.GetSavedPaymentMethodAsync(customerId, savedPaymentMethodId, ct);
        if (!method.IsSuccess) return Result.Failure<Guid>(method.Error);
        if (method.Value!.ExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result.Failure<Guid>(new Error("CARD_EXPIRED", "The selected card has expired."));

        // Phase 3 — persist within the SAME transaction; suspension check and write commit atomically
        var tx = PaymentTransaction.Initiate(
            customerId, orderId, Money.Create(amount, currency),
            PaymentMethodSnapshot.FromSaved(method.Value));

        await _transactions.AddAsync(tx, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(tx.Id);
    }

    public Task<Result<Guid>> InitiateWithNewCardTokenAsync(...) // same flow, PaymentMethodSnapshot.FromNewCardToken(...)
    public async Task<Result> CaptureAsync(Guid paymentId, CancellationToken ct = default)
    {
        var tx = await _transactions.GetByIdAsync(paymentId, ct);
        if (tx is null) return Result.Failure(new Error("PAYMENT_NOT_FOUND", "Payment was not found."));
        tx.MarkCaptured();                       // domain enforces Pending → Captured
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
    public async Task<Result> FailAsync(Guid paymentId, string reason, CancellationToken ct = default) { /* symmetric */ }
}
```

#### 3d. Outbound adapter — **the `CustomerGateway`** ⭐

**WHAT:** The single sanctioned bridge between the two modules.
**WHERE:** `src/Modules/Payment/Adapter/Outbound/Gateways/CustomerGateway.cs`

```csharp
using ModularMonolith.Contracts.Customer;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Customer.Application.Ports.Inbound;   // ← EXCEPTION #2 (csproj ref)
using ModularMonolith.Modules.Payment.Application.Ports.Outbound;

namespace ModularMonolith.Modules.Payment.Adapter.Outbound.Gateways;

/// <summary>
/// Transactional ACL adapter — implements Payment's OWN ICustomerGatewayPort by
/// delegating DIRECTLY to Customer's ICustomerInboundPort. Same process, same
/// transaction: if Customer rejects (unknown/suspended) nothing persists here either.
/// NO events, NO message bus, NO Wolverine.
/// </summary>
internal sealed class CustomerGateway(ICustomerInboundPort customerPort) : ICustomerGatewayPort
{
    public Task<Result<CustomerStatusDto>> GetCustomerStatusAsync(Guid customerId, CancellationToken ct = default)
        => customerPort.GetStatusAsync(customerId, ct);                    // straight delegation

    public Task<Result<SavedPaymentMethodDto>> GetSavedPaymentMethodAsync(Guid customerId, Guid paymentMethodId, CancellationToken ct = default)
        => customerPort.GetSavedPaymentMethodAsync(customerId, paymentMethodId, ct);
}
```

Then `Repositories/PaymentTransactionRepository.cs`, `Repositories/SqlServer/PaymentDbContext.cs` + configurations, and `DependencyInjection.cs` copying the Orders outbound DI, adding the key line:

```csharp
services.AddScoped<Application.Ports.Outbound.ICustomerGatewayPort, Gateways.CustomerGateway>();
```

#### 3e. Inbound adapter (REST)

**WHERE:** `src/Modules/Payment/Adapter/Inbound/Rest/PaymentsEndpoints.cs` — group `/api/v1/payments`; endpoints `POST /saved-card`, `POST /new-card-token`, `POST /{id}/capture`, `POST /{id}/fail`. Request DTOs carry `Guid SavedPaymentMethodId` or `string CardToken` — **there is no request field anywhere that could accept a PAN** (add a validation rule rejecting body fields named like card numbers).

---

### Step 4 — QueryApplications (read side, strictly read-only)

**RULES:** No writes, no `Find`/aggregate loading with behavior, no domain rules — just `DbContext.Set<T>().Where(...).Select(...)` projections into Contract records. Inject the abstract `DbContext` exactly like `OrderQueryService` does.

**WHERE:** `src/Modules/Customer/QueryApplication/CustomerQueryService.cs` and `src/Modules/Payment/QueryApplication/PaymentQueryService.cs`

```csharp
// src/Modules/Customer/QueryApplication/CustomerQueryService.cs
using Microsoft.EntityFrameworkCore;
using ModularMonolith.Contracts.Customer;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Customer.Application.Domain.Customers;

namespace ModularMonolith.Modules.Customer.QueryApplication;

public interface ICustomerQueryService
{
    Task<Result<CustomerSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerSnapshot>> ListAsync(int page, int pageSize, CancellationToken ct = default);
}

internal sealed class CustomerQueryService(DbContext db) : ICustomerQueryService
{
    public async Task<Result<CustomerSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var result = await db.Set<Customer>()
            .Where(c => c.Id == id)
            .Select(c => new CustomerSnapshot(c.Id, c.IdentityUserId, c.Status.Value, c.AccountTier))
            .FirstOrDefaultAsync(ct);
        return result is null
            ? Result.Failure<CustomerSnapshot>(new Error("CUSTOMER_NOT_FOUND", "Customer was not found."))
            : Result.Success(result);
    }

    public async Task<IReadOnlyList<CustomerSnapshot>> ListAsync(int page, int pageSize, CancellationToken ct = default) =>
        await db.Set<Customer>().OrderBy(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new CustomerSnapshot(c.Id, c.IdentityUserId, c.Status.Value, c.AccountTier))
            .ToListAsync(ct);
}
```

```csharp
// src/Modules/Payment/QueryApplication/PaymentQueryService.cs (essentials)
internal sealed class PaymentQueryService(DbContext db) : IPaymentQueryService
{
    public async Task<Result<PaymentSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var result = await db.Set<PaymentTransaction>()
            .Where(p => p.Id == id)
            .Select(p => new PaymentSnapshot(p.Id, p.CustomerId, p.OrderId,
                p.Amount.Amount, p.Amount.Currency, p.Status.Value, p.Method.Token))
            .FirstOrDefaultAsync(ct);
        return result is null
            ? Result.Failure<PaymentSnapshot>(new Error("PAYMENT_NOT_FOUND", "Payment was not found."))
            : Result.Success(result);
    }
    public async Task<IReadOnlyList<PaymentSnapshot>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default) =>
        await db.Set<PaymentTransaction>().Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(/* same projection */).ToListAsync(ct);
}
```

Each gets a `DependencyInjection.cs` with `Add{Customer|Payment}QueryApplication()` registering the interface → sealed implementation. Both csprojs reference their own `Adapter.Outbound` (for the `DbContext`) plus `Contracts` and `Framework` — same as Orders' QueryApplication.

---

### Step 5 — Composition Root

**WHAT:** One installer per module; Host stays thin.
**WHERE:** `src/Modules/{Customer,Payment}/Installer/ModuleInstaller.cs` and `src/Host/ModularMonolith.Host/Program.cs`

```csharp
// src/Modules/Customer/Installer/ModuleInstaller.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Customer.Adapter.Inbound.Rest.Endpoints;
using ModularMonolith.Modules.Customer.Adapter.Outbound;
using ModularMonolith.Modules.Customer.Application;
using ModularMonolith.Modules.Customer.QueryApplication;

namespace ModularMonolith.Modules.Customer;

public static class ModuleInstaller
{
    public static IServiceCollection AddCustomerModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddCustomerApplication();          // ICustomerInboundPort → CustomerService (sealed)
        services.AddCustomerOutboundAdapters(config); // DbContext, repos, IUnitOfWork
        services.AddCustomerRestAdapter();            // IEndpoint scan (Scrutor)
        services.AddCustomerQueryApplication();
        return services;
    }
}
```

```csharp
// src/Modules/Payment/Installer/ModuleInstaller.cs — identical shape
public static class ModuleInstaller
{
    public static IServiceCollection AddPaymentModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddPaymentApplication();           // IPaymentService → PaymentService (sealed)
        services.AddPaymentOutboundAdapters(config); // DbContext, repos, IUnitOfWork, ICustomerGatewayPort → CustomerGateway
        services.AddPaymentRestAdapter();
        services.AddPaymentQueryApplication();
        return services;
    }
}
```

**Host wiring** — three edits in `Program.cs`:

```csharp
// 1. Registration order matters: Customer (provider) BEFORE Payment (consumer),
//    so ICustomerInboundPort is resolvable when CustomerGateway resolves lazily at first use.
builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddCatalogModule(builder.Configuration)
    .AddOrdersModule(builder.Configuration)
    .AddCustomerModule(builder.Configuration)   // NEW
    .AddPaymentModule(builder.Configuration);   // NEW

// 2. Health checks
builder.Services.AddHealthChecks()
    /* …existing… */
    .AddDbContextCheck<CustomerDbContext>("Customer DB")
    .AddDbContextCheck<PaymentDbContext>("Payment DB");

// 3. Endpoint mapping
app.MapCustomerRestEndpoints();   // /api/v1/customers
app.MapPaymentsRestEndpoints();   // /api/v1/payments
```

**Config:** add `ConnectionStrings__Customer` and `ConnectionStrings__Payment` to `appsettings.Development.json` / `docker-compose.yml` (SQL Server, separate schemas/databases — one DbContext per module, always).

---

## 4. Architecture Test Checklist

Update `tests/ArchitectureTests/HexagonalBoundaryTests.cs` — extend every assembly list with `Customer.Application` / `Payment.Application` and **add these new rules**:

### New rules to add

```csharp
[Fact]
public void Payment_Application_Must_Not_Depend_On_Customer_Application()
{
    // v3 rule #2: the consumer hexagon knows ONLY Contracts + its own port.
    var paymentApp = typeof(ModularMonolith.Modules.Payment.Application.DependencyInjection).Assembly;
    var result = Types.InAssembly(paymentApp)
        .ShouldNot().HaveDependencyOn("ModularMonolith.Modules.Customer.Application")
        .GetResult();
    result.IsSuccessful.Should().BeTrue(
        "Payment.Application must NOT depend on Customer.Application — use ICustomerGatewayPort + Contracts DTOs.");
}

[Fact]
public void Only_Payment_Adapter_Outbound_May_Reference_Customer_Application()
{
    // The gateway is the ONLY sanctioned bridge (exception #2, like Orders→Catalog).
    foreach (var asm in new[]
    {
        typeof(ModularMonolith.Modules.Payment.Application.DependencyInjection).Assembly,
        typeof(ModularMonolith.Modules.Payment.Adapter.Inbound.Rest.Endpoints.IEndpoint).Assembly,
        typeof(ModularMonolith.Modules.Payment.QueryApplication.DependencyInjection).Assembly,
    })
    {
        var result = Types.InAssembly(asm)
            .ShouldNot().HaveDependencyOn("ModularMonolith.Modules.Customer")
            .GetResult();
        result.IsSuccessful.Should().BeTrue($"{asm.GetName().Name} must not touch Customer — only Payment.Adapter.Outbound may.");
    }
}

[Fact]
public void Customer_Module_Must_Not_Depend_On_Payment_Or_Other_Modules()
{
    var customerApp = typeof(ModularMonolith.Modules.Customer.Application.DependencyInjection).Assembly;
    var result = Types.InAssembly(customerApp)
        .ShouldNot().HaveDependencyOnAny(
            "ModularMonolith.Modules.Payment",
            "ModularMonolith.Modules.Orders",
            "ModularMonolith.Modules.Catalog",
            "ModularMonolith.Modules.Identity")
        .GetResult();
    result.IsSuccessful.Should().BeTrue("Provider must never know its consumers.");
}
```

Also update the *existing* generic tests to enumerate the new assemblies:

- `Application_Should_Not_Depend_On_Adapters` → add both new Application assemblies; forbidden prefixes now include `ModularMonolith.Modules.Customer.Adapter` / `…Payment.Adapter`.
- `Application_Should_Not_Depend_On_EntityFrameworkCore`, `Domain_Should_Not_Depend_On_Frameworks` (incl. `"Wolverine"`), `No_Module_Should_Use_Wolverine`, `No_Module_Application_Can_Reference_Another_Module_Application` → add `(Payment.App, "…Customer"), (Payment.App, "…Orders"), (Payment.App, "…Catalog"), (Payment.App, "…Identity"), (Customer.App, …all…)`.
- `Inbound_Ports_Must_Be_Interfaces` / `Outbound_Ports_Must_Be_Interfaces` / `Services_Must_Be_Sealed` → run against `Customer.Application` and `Payment.Application` too.

### Manual review checklist (NetArchTest can't see these)

- [ ] `Payment.Application` contains **zero** types from `ModularMonolith.Modules.*.Application` other than its own — grep the csproj: only `Customer.Adapter.Outbound.csproj` lists `Customer.Application`.
- [ ] No `Wolverine`/`MediatR`/`MassTransit`/event-dispatch packages in any of the 10 new csprojs.
- [ ] No raw card numbers: search new code for `PAN|CardNumber|card_number` — only `Token`, `TokenizedCard`, `Last4` allowed. Domain factories enforce `tok_` prefix.
- [ ] `QueryApplication` classes inject `DbContext` and contain only `Select` projections — no `Add/Update/Remove`, no `SaveChanges`.
- [ ] Gateway (`CustomerGateway`) is `internal sealed` and pure delegation — no caching, no retry loops, no business decisions.
- [ ] Both modules registered in Host **after** their dependencies; health checks + endpoint maps updated.
- [ ] `dotnet build ModularMonolith.slnx && dotnet test tests/ArchitectureTests/ModularMonolith.ArchitectureTests.csproj` green before touching integration tests.
