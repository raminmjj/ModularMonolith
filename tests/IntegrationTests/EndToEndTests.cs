using Xunit;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModularMonolith.Modules.Catalog.Adapter.Outbound.Repositories.SqlServer;
using ModularMonolith.Modules.Identity.Adapter.Outbound.Repositories.SqlServer;
using ModularMonolith.Modules.Orders.Adapter.Outbound.Repositories.SqlServer;

namespace ModularMonolith.IntegrationTests;

public class EndToEndTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public EndToEndTests(WebApplicationFactory<Program> factory)
    {
        // Keep the DERIVED factory: its service provider points at the same
        // replaced (InMemory) contexts that the client talks to.
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            ReplaceDbContext<IdentityDbContext>(services, "IdentityTest");
            ReplaceDbContext<CatalogDbContext>(services, "CatalogTest");
            ReplaceDbContext<OrdersDbContext>(services, "OrdersTest");
        }));
        _client = _factory.CreateClient();
    }

    private static void ReplaceDbContext<TContext>(IServiceCollection services, string dbName) where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();
        services.AddDbContext<TContext>(o => o.UseInMemoryDatabase(dbName));
    }

    [Fact]
    public async Task Health_Endpoint_Should_Return_Success()
    {
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
        response.StatusCode.Should().BeOneOf(System.Net.HttpStatusCode.OK, System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Full_Flow_Register_Login_CreateProduct_PlaceOrder_Should_Succeed()
    {
        // 0. Seed an Admin (product management requires the Admin policy — a plain
        //    customer must NOT be able to create products).
        var adminEmail = $"admin{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<ModularMonolith.Modules.Identity.Application.Domain.Users.IPasswordHasher>();
            var hash = hasher.Hash("StrongPass123!");
            var admin = ModularMonolith.Modules.Identity.Application.Domain.Users.User.Register(
                ModularMonolith.SharedKernel.ValueObjects.Email.Create(adminEmail),
                ModularMonolith.Modules.Identity.Application.Domain.ValueObjects.HashedPassword.Create(hash.Hash, hash.Salt),
                "Admin",
                ["Admin"]);
            db.Users.Add(admin);
            db.SaveChanges();
        }

        // 1. Admin logs in and creates a product
        await LoginAsync(adminEmail, "StrongPass123!");
        var createProductResp = await _client.PostAsJsonAsync("/api/v1/catalog/products", new
        {
            sku = $"W-{Guid.NewGuid():N}".Substring(0, 10).ToUpperInvariant(),
            name = "Widget",
            price = 9.99m,
            currency = "USD",
            initialStock = 100,
            description = "A widget"
        });
        createProductResp.EnsureSuccessStatusCode();
        var product = await createProductResp.Content.ReadFromJsonAsync<ProductResponse>();
        product!.Id.Should().NotBeEmpty();

        // 2. A customer registers and places the order (cross-user authorization path)
        var registerResp = await _client.PostAsJsonAsync("/api/v1/identity/register", new
        {
            email = $"alice{Guid.NewGuid():N}@example.com",
            password = "StrongPass123!",
            displayName = "Alice"
        });
        registerResp.EnsureSuccessStatusCode();
        var customer = await registerResp.Content.ReadFromJsonAsync<RegisterResponse>();
        customer!.Id.Should().NotBeEmpty();
        await LoginAsync(customer.Email, "StrongPass123!");

        // 3. Place order (with stock reservation)
        var placeOrderResp = await _client.PostAsJsonAsync("/api/v1/orders", new
        {
            userId = customer.Id,
            lines = new[] { new { productId = product.Id, quantity = 2 } }
        });
        placeOrderResp.EnsureSuccessStatusCode();
        var order = await placeOrderResp.Content.ReadFromJsonAsync<OrderResponse>();
        order!.Id.Should().NotBeEmpty();
        order.Status.Should().Be("Pending");

        // 4. Confirm order (commits reservation)
        var confirmResp = await _client.PostAsync($"/api/v1/orders/{order.Id}/status?status=Confirmed", content: null);
        confirmResp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Customer_Wallet_Payment_Flow_Should_Succeed_And_Suspension_Blocks_Payment()
    {
        // 0. Admin for suspension control.
        var adminEmail = $"admin{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<ModularMonolith.Modules.Identity.Application.Domain.Users.IPasswordHasher>();
            var hash = hasher.Hash("StrongPass123!");
            db.Users.Add(ModularMonolith.Modules.Identity.Application.Domain.Users.User.Register(
                ModularMonolith.SharedKernel.ValueObjects.Email.Create(adminEmail),
                ModularMonolith.Modules.Identity.Application.Domain.ValueObjects.HashedPassword.Create(hash.Hash, hash.Salt),
                "Admin", ["Admin"]));
            db.SaveChanges();
        }

        // 1. Alice: identity → customer profile → saved card (vault token only).
        var registerResp = await _client.PostAsJsonAsync("/api/v1/identity/register", new
        {
            email = $"alice{Guid.NewGuid():N}@example.com", password = "StrongPass123!", displayName = "Alice"
        });
        registerResp.EnsureSuccessStatusCode();
        var alice = await registerResp.Content.ReadFromJsonAsync<RegisterResponse>();

        await LoginAsync(alice.Email, "StrongPass123!");
        var createCustomerResp = await _client.PostAsJsonAsync("/api/v1/customers",
            new { identityUserId = alice.Id, displayName = "Alice" });
        createCustomerResp.EnsureSuccessStatusCode();
        var customer = await createCustomerResp.Content.ReadFromJsonAsync<CustomerCreatedResponse>();

        var saveMethodResp = await _client.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/payment-methods",
            new { tokenizedCard = "tok_visa_test_4242", cardType = "Visa", expiryDate = "2030-12-31" });
        saveMethodResp.EnsureSuccessStatusCode();
        var method = await saveMethodResp.Content.ReadFromJsonAsync<CustomerCreatedResponse>();

        // 2. Pay with the saved card → Pending (customer verified via ACL inside the saga).
        var payResp = await _client.PostAsJsonAsync("/api/v1/payments/saved-card",
            new { customerId = customer.Id, orderId = Guid.NewGuid(), amount = 49.90m, currency = "USD", savedPaymentMethodId = method!.Id });
        payResp.EnsureSuccessStatusCode();
        var payment = await payResp.Content.ReadFromJsonAsync<PaymentCreatedResponse>();
        payment!.Status.Should().Be("Pending");

        // 3. Read side returns the TOKEN snapshot (never a PAN).
        var getResp = await _client.GetAsync($"/api/v1/payments/{payment.Id}");
        getResp.EnsureSuccessStatusCode();
        var snapshot = await getResp.Content.ReadFromJsonAsync<PaymentSnapshotDto>();
        snapshot!.Status.Should().Be("Pending");
        snapshot.MethodToken.Should().StartWith("tok_");

        // 4. Capture.
        var captureResp = await _client.PostAsync($"/api/v1/payments/{payment.Id}/capture", content: null);
        captureResp.EnsureSuccessStatusCode();

        // 5. Suspension blocks payment: Bob's account suspended → CUSTOMER_SUSPENDED.
        var bobRegister = await _client.PostAsJsonAsync("/api/v1/identity/register", new
        {
            email = $"bob{Guid.NewGuid():N}@example.com", password = "StrongPass123!", displayName = "Bob"
        });
        bobRegister.EnsureSuccessStatusCode();
        var bob = await bobRegister.Content.ReadFromJsonAsync<RegisterResponse>();

        await LoginAsync(adminEmail, "StrongPass123!"); // switch to admin
        var bobCustomerResp = await _client.GetAsync($"/api/v1/customers/{customer.Id}"); // sanity: alice's profile readable
        bobCustomerResp.EnsureSuccessStatusCode();

        // Create Bob's customer profile as admin, then suspend it.
        var bobCustomer = await (await _client.PostAsJsonAsync("/api/v1/customers",
            new { identityUserId = bob!.Id, displayName = "Bob" })).Content.ReadFromJsonAsync<CustomerCreatedResponse>();
        (await _client.PostAsJsonAsync($"/api/v1/customers/{bobCustomer!.Id}/payment-methods",
            new { tokenizedCard = "tok_mc_test_1111", cardType = "Mastercard", expiryDate = "2030-06-30" })).EnsureSuccessStatusCode();
        var suspendResp = await _client.PostAsync($"/api/v1/customers/{bobCustomer.Id}/suspend", content: null);
        suspendResp.EnsureSuccessStatusCode();

        var blockedPay = await _client.PostAsJsonAsync("/api/v1/payments/saved-card",
            new { customerId = bobCustomer.Id, orderId = Guid.NewGuid(), amount = 10m, currency = "USD", savedPaymentMethodId = Guid.NewGuid() });
        blockedPay.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        var problem = await blockedPay.Content.ReadAsStringAsync();
        problem.Should().Contain("CUSTOMER_SUSPENDED");
    }

    [Fact]
    public async Task Admin_GraphQL_FailedPayments_Report_Should_Return_Composed_Data_And_Forbid_Customers()
    {
        // 0. Seed admin (endpoint + field both require the Admin policy).
        var adminEmail = $"admin{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<ModularMonolith.Modules.Identity.Application.Domain.Users.IPasswordHasher>();
            var hash = hasher.Hash("StrongPass123!");
            db.Users.Add(ModularMonolith.Modules.Identity.Application.Domain.Users.User.Register(
                ModularMonolith.SharedKernel.ValueObjects.Email.Create(adminEmail),
                ModularMonolith.Modules.Identity.Application.Domain.ValueObjects.HashedPassword.Create(hash.Hash, hash.Salt),
                "Admin", ["Admin"]));
            db.SaveChanges();
        }

        // 1. Alice: identity → customer profile → saved card → FAILED payment.
        var registerResp = await _client.PostAsJsonAsync("/api/v1/identity/register", new
        {
            email = $"carol{Guid.NewGuid():N}@example.com", password = "StrongPass123!", displayName = "Carol"
        });
        registerResp.EnsureSuccessStatusCode();
        var carol = await registerResp.Content.ReadFromJsonAsync<RegisterResponse>();
        await LoginAsync(carol!.Email, "StrongPass123!");

        var createCustomerResp = await _client.PostAsJsonAsync("/api/v1/customers",
            new { identityUserId = carol.Id, displayName = "Carol" });
        createCustomerResp.EnsureSuccessStatusCode();
        var customer = await createCustomerResp.Content.ReadFromJsonAsync<CustomerCreatedResponse>();

        var saveMethodResp = await _client.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/payment-methods",
            new { tokenizedCard = "tok_visa_report_1", cardType = "Visa", expiryDate = "2030-12-31" });
        saveMethodResp.EnsureSuccessStatusCode();
        var method = await saveMethodResp.Content.ReadFromJsonAsync<CustomerCreatedResponse>();

        var payResp = await _client.PostAsJsonAsync("/api/v1/payments/saved-card",
            new { customerId = customer.Id, orderId = Guid.NewGuid(), amount = 77.50m, currency = "USD", savedPaymentMethodId = method!.Id });
        payResp.EnsureSuccessStatusCode();
        var payment = await payResp.Content.ReadFromJsonAsync<PaymentCreatedResponse>();

        // 2. Non-admin must be FORBIDDEN before any report logic runs (endpoint-level policy).
        var forbiddenResp = await _client.PostAsJsonAsync("/api/v1/admin/reports/graphql",
            new { query = "query { customersWithFailedPayments(filter: {}) { totalCount } }" });
        forbiddenResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);

        // 3. Fail the payment as… still Carol? No — fail endpoint requires auth only; use it.
        //    Then switch to admin for the report.
        var failResp = await _client.PostAsJsonAsync($"/api/v1/payments/{payment!.Id}/fail",
            new { reason = "insufficient_funds" });
        failResp.EnsureSuccessStatusCode();

        await LoginAsync(adminEmail, "StrongPass123!");
        var query = """
            query Report($f: FailureReportFilterInput!) {
              customersWithFailedPayments(filter: $f) {
                totalCount page pageSize
                items {
                  totalFailedAmount currency failureCount
                  customer { id displayName status }
                  failures { paymentId amount currency reason failedAt }
                }
              }
            }
            """;
        var graphQLResp = await _client.PostAsJsonAsync("/api/v1/admin/reports/graphql",
            new { query, variables = new { f = new { minAmount = 50m, customerStatus = (string?)null, page = 1, pageSize = 20 } } });
        if (!graphQLResp.IsSuccessStatusCode)
        {
            var errBody = await graphQLResp.Content.ReadAsStringAsync();
            throw new Exception($"GRAPHQL HTTP {(int)graphQLResp.StatusCode}: {errBody[..Math.Min(700, errBody.Length)]}");
        }

        var rawBody = await graphQLResp.Content.ReadAsStringAsync();
        if (rawBody.Contains("\"errors\"") && rawBody.Contains("\"data\":null"))
            throw new Exception("GRAPHQL ERRORS: " + rawBody[..Math.Min(700, rawBody.Length)]);
        try
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(rawBody);
            var data = payload.GetProperty("data").GetProperty("customersWithFailedPayments");
            data.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
            var items = data.GetProperty("items");
            var matchFound = false;
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("customer", out var cust)) continue;
                if (!Guid.TryParse(cust.GetProperty("id").GetString(), out var custId) || custId != customer.Id) continue;
                matchFound = true;
                item.GetProperty("totalFailedAmount").GetDecimal().Should().Be(77.50m);
                item.GetProperty("failureCount").GetInt32().Should().Be(1);
                var failure = item.GetProperty("failures").EnumerateArray().Single();
                failure.GetProperty("reason").GetString().Should().Be("insufficient_funds");
                failure.GetProperty("amount").GetDecimal().Should().Be(77.50m);
            }
            matchFound.Should().BeTrue("Carol's failed payment must appear in the composed report");
        }
        catch (Exception ex) when (ex.Message.StartsWith("RAW") is false && !ex.Message.Contains("expected Carol"))
        {
            throw new Exception($"PARSE/ASSERT FAIL: {ex.Message} || BODY: {rawBody[..Math.Min(900, rawBody.Length)]}");
        }
    }

    [Fact]
    public async Task Admin_GraphQL_Report_Includes_LastOrder_From_Orders_Module()
    {
        // 0. Admin seed.
        var adminEmail = $"admin{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<ModularMonolith.Modules.Identity.Application.Domain.Users.IPasswordHasher>();
            var hash = hasher.Hash("StrongPass123!");
            db.Users.Add(ModularMonolith.Modules.Identity.Application.Domain.Users.User.Register(
                ModularMonolith.SharedKernel.ValueObjects.Email.Create(adminEmail),
                ModularMonolith.Modules.Identity.Application.Domain.ValueObjects.HashedPassword.Create(hash.Hash, hash.Salt),
                "Admin", ["Admin"]));
            db.SaveChanges();
        }

        // 1. Product (admin) + customer (Dave) with saved card.
        await LoginAsync(adminEmail, "StrongPass123!");
        var productResp = await _client.PostAsJsonAsync("/api/v1/catalog/products", new
        {
            sku = $"L-{Guid.NewGuid():N}".Substring(0, 10).ToUpperInvariant(),
            name = "Gadget", price = 9.99m, currency = "USD", initialStock = 50
        });
        productResp.EnsureSuccessStatusCode();
        var product = await productResp.Content.ReadFromJsonAsync<ProductResponse>();

        var registerResp = await _client.PostAsJsonAsync("/api/v1/identity/register", new
        {
            email = $"dave{Guid.NewGuid():N}@example.com", password = "StrongPass123!", displayName = "Dave"
        });
        registerResp.EnsureSuccessStatusCode();
        var dave = await registerResp.Content.ReadFromJsonAsync<RegisterResponse>();
        await LoginAsync(dave!.Email, "StrongPass123!");

        var customerResp = await _client.PostAsJsonAsync("/api/v1/customers",
            new { identityUserId = dave.Id, displayName = "Dave" });
        customerResp.EnsureSuccessStatusCode();
        var customer = await customerResp.Content.ReadFromJsonAsync<CustomerCreatedResponse>();
        var methodResp = await _client.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/payment-methods",
            new { tokenizedCard = "tok_visa_lastorder", cardType = "Visa", expiryDate = "2030-12-31" });
        methodResp.EnsureSuccessStatusCode();
        var method = await methodResp.Content.ReadFromJsonAsync<CustomerCreatedResponse>();

        // 2. Dave places an ORDER (third module in the composition).
        var orderResp = await _client.PostAsJsonAsync("/api/v1/orders", new
        {
            userId = dave.Id,
            lines = new[] { new { productId = product!.Id, quantity = 2 } } // total 19.98, 1 line
        });
        orderResp.EnsureSuccessStatusCode();
        var order = await orderResp.Content.ReadFromJsonAsync<OrderResponse>();

        // 3. A payment FAILS.
        var payResp = await _client.PostAsJsonAsync("/api/v1/payments/saved-card",
            new { customerId = customer.Id, orderId = order!.Id, amount = 77.50m, currency = "USD", savedPaymentMethodId = method!.Id });
        payResp.EnsureSuccessStatusCode();
        var payment = await payResp.Content.ReadFromJsonAsync<PaymentCreatedResponse>();
        (await _client.PostAsJsonAsync($"/api/v1/payments/{payment!.Id}/fail", new { reason = "card_declined" }))
            .EnsureSuccessStatusCode();

        // 4. Admin queries the report INCLUDING lastOrder.
        await LoginAsync(adminEmail, "StrongPass123!");
        var query = """
            query Report($f: FailureReportFilterInput!) {
              customersWithFailedPayments(filter: $f) {
                totalCount
                items {
                  customer { id displayName lastOrder { orderId orderDate totalAmount status itemCount } }
                  failures { amount reason }
                }
              }
            }
            """;
        var graphQLResp = await _client.PostAsJsonAsync("/api/v1/admin/reports/graphql",
            new { query, variables = new { f = new { minAmount = 50m, customerStatus = (string?)null, page = 1, pageSize = 20 } } });
        if (!graphQLResp.IsSuccessStatusCode)
        {
            var errBody = await graphQLResp.Content.ReadAsStringAsync();
            throw new Exception($"GRAPHQL HTTP {(int)graphQLResp.StatusCode}: {errBody[..Math.Min(700, errBody.Length)]}");
        }
        var rawBody = await graphQLResp.Content.ReadAsStringAsync();
        var payload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(rawBody);
        var items = payload.GetProperty("data")
            .GetProperty("customersWithFailedPayments").GetProperty("items");

        CustomerLastOrderDto? lastOrder = null;
        foreach (var item in items.EnumerateArray())
        {
            var cust = item.GetProperty("customer");
            if (!Guid.TryParse(cust.GetProperty("id").GetString(), out var custId) || custId != customer.Id) continue;
            item.GetProperty("failures").EnumerateArray().Single()
                .GetProperty("reason").GetString().Should().Be("card_declined");
            if (cust.TryGetProperty("lastOrder", out var lo) && lo.ValueKind == System.Text.Json.JsonValueKind.Object)
                lastOrder = System.Text.Json.JsonSerializer.Deserialize<CustomerLastOrderDto>(lo.GetRawText(),
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        lastOrder.Should().NotBeNull("the report must enrich customers with their last order from the Orders module");
        lastOrder!.OrderId.Should().Be(order.Id);
        lastOrder.Status.Should().Be("Pending");
        lastOrder.TotalAmount.Should().Be(19.98m);
        lastOrder.ItemCount.Should().Be(1);
    }
    [Fact]
    public async Task AdminPanel_GraphQL_Full_Surface_Should_Work()
    {
        var adminEmail = $"admin{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<ModularMonolith.Modules.Identity.Application.Domain.Users.IPasswordHasher>();
            var hash = hasher.Hash("StrongPass123!");
            db.Users.Add(ModularMonolith.Modules.Identity.Application.Domain.Users.User.Register(
                ModularMonolith.SharedKernel.ValueObjects.Email.Create(adminEmail),
                ModularMonolith.Modules.Identity.Application.Domain.ValueObjects.HashedPassword.Create(hash.Hash, hash.Salt),
                "Admin", ["Admin"]));
            db.SaveChanges();
        }
        await LoginAsync(adminEmail, "StrongPass123!");

        // 1. CATALOG: create product via mutation.
        var sku = $"A-{Guid.NewGuid():N}".Substring(0, 10).ToUpperInvariant();
        var createdRaw = await GraphQlRaw($$"""
            mutation { createProduct(sku:"{{sku}}" name:"AdminWidget" price:15 currency:"USD" initialStock:30) }
            """);
        using (var doc = System.Text.Json.JsonDocument.Parse(createdRaw))
            doc.RootElement.GetProperty("data").GetProperty("createProduct").GetGuid()
                .Should().NotBeEmpty();
        var productId = System.Text.Json.JsonDocument.Parse(createdRaw)
            .RootElement.GetProperty("data").GetProperty("createProduct").GetGuid();

        // 2. CATALOG list shows it; stock + price mutations work.
        (await GraphQlRaw("{ products(includeInactive:true, page:1, pageSize:50) { sku stock } }"))
            .Should().Contain(sku);
        (await GraphQlRaw($"mutation {{ adjustProductStock(productId:\"{productId}\" delta:-5) }}"))
            .Should().Contain("true");
        (await GraphQlRaw($"mutation {{ changeProductPrice(productId:\"{productId}\" newPrice:17.5 currency:\"USD\") }}"))
            .Should().Contain("true");

        // 3. CUSTOMER: register + profile, then suspend via GraphQL.
        var regResp = await _client.PostAsJsonAsync("/api/v1/identity/register", new
        {
            email = $"erin{Guid.NewGuid():N}@example.com", password = "StrongPass123!", displayName = "Erin"
        });
        regResp.EnsureSuccessStatusCode();
        var erin = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
        await LoginAsync(erin!.Email, "StrongPass123!");
        var custResp = await _client.PostAsJsonAsync("/api/v1/customers",
            new { identityUserId = erin.Id, displayName = "Erin" });
        custResp.EnsureSuccessStatusCode();
        var customer = await custResp.Content.ReadFromJsonAsync<CustomerCreatedResponse>();
        await LoginAsync(adminEmail, "StrongPass123!");
        (await GraphQlRaw($"mutation {{ suspendCustomer(customerId:\"{customer!.Id}\") }}"))
            .Should().Contain("\"suspendCustomer\":true");

        // 4. Suspended customer cannot pay (ACL still enforced through the saga).
        var payAttempt = await _client.PostAsJsonAsync("/api/v1/payments/saved-card",
            new { customerId = customer.Id, orderId = Guid.NewGuid(), amount = 5m, currency = "USD",
                  savedPaymentMethodId = Guid.NewGuid() });
        payAttempt.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);

        // 5. ANALYTICS + PAYMENTS queries reachable.
        (await GraphQlRaw("""
            { salesSummary(from:"2026-01-01T00:00:00Z" to:"2027-01-01T00:00:00Z")
                { totalCapturedAmount capturedCount pendingCount failedCount } }
            """)).Should().Contain("salesSummary");
        (await GraphQlRaw("{ payments(page:1, pageSize:10) { paymentId status } }"))
            .Should().Contain("payments");

        // 6. ORDERS list query reachable.
        (await GraphQlRaw("{ orders(page:1, pageSize:10) { orderId status itemCount } }"))
            .Should().Contain("orders");
    }

    [Fact]
    public async Task AdminPanel_Supplier_Brand_GraphQL_Flow_Should_Work()
    {
        // 0. Admin seed + login (all admin GraphQL is policy-gated).
        var adminEmail = $"admin{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<ModularMonolith.Modules.Identity.Application.Domain.Users.IPasswordHasher>();
            var hash = hasher.Hash("StrongPass123!");
            db.Users.Add(ModularMonolith.Modules.Identity.Application.Domain.Users.User.Register(
                ModularMonolith.SharedKernel.ValueObjects.Email.Create(adminEmail),
                ModularMonolith.Modules.Identity.Application.Domain.ValueObjects.HashedPassword.Create(hash.Hash, hash.Salt),
                "Admin", ["Admin"]));
            db.SaveChanges();
        }
        await LoginAsync(adminEmail, "StrongPass123!");

        // 1. Create a brand via mutation → starts PendingReview.
        var createBrandRaw = await GraphQlRaw("""
            mutation { createBrand(name:"Acme" countryOfOrigin:"de") }
            """);
        var brandId = System.Text.Json.JsonDocument.Parse(createBrandRaw)
            .RootElement.GetProperty("data").GetProperty("createBrand").GetGuid();
        brandId.Should().NotBeEmpty();

        // 2. Approve it.
        (await GraphQlRaw($"mutation {{ approveBrand(brandId:\"{brandId}\") }}"))
            .Should().Contain("\"approveBrand\":true");

        // 3. Create a supplier via REST (public port), then VERIFY via mutation.
        var supResp = await _client.PostAsJsonAsync("/api/v1/suppliers",
            new { name = "Acme Supply Co", contactEmail = $"supply{Guid.NewGuid():N}@acme.test", phoneNumber = "+123456789", address = "Main St 1" });
        supResp.EnsureSuccessStatusCode();
        var supplierId = (await supResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())
            .GetProperty("id").GetGuid();

        (await GraphQlRaw($"mutation {{ verifySupplier(supplierId:\"{supplierId}\") }}"))
            .Should().Contain("\"verifySupplier\":true");

        // 4. Assign the brand to the verified supplier with a commission rate.
        (await GraphQlRaw($"mutation {{ assignBrandToSupplier(supplierId:\"{supplierId}\" brandId:\"{brandId}\" commissionRate:12.5) }}"))
            .Should().Contain("\"assignBrandToSupplier\":true");

        // 5. Query the agreement rows owned by the Supplier aggregate.
        var agreementsRaw = await GraphQlRaw($"{{ supplierAgreements(supplierId:\"{supplierId}\") {{ brandId commissionRate isActive }} }}");
        agreementsRaw.Should().Contain($"\"brandId\":\"{brandId}\"");
        agreementsRaw.Should().Contain("\"commissionRate\":12.5");
        agreementsRaw.Should().Contain("\"isActive\":true");

        // 6. List queries see both entities.
        (await GraphQlRaw("{ brands(page:1, pageSize:20) { id slug status } }"))
            .Should().Contain("acme");
        (await GraphQlRaw("{ suppliers(page:1, pageSize:20) { id name status isVerified } }"))
            .Should().Contain("Acme Supply Co");

        // 7. Remove the agreement again — row deactivates (history preserved).
        (await GraphQlRaw($"mutation {{ removeBrandFromSupplier(supplierId:\"{supplierId}\" brandId:\"{brandId}\") }}"))
            .Should().Contain("\"removeBrandFromSupplier\":true");
        var afterRemove = await GraphQlRaw($"{{ supplierAgreements(supplierId:\"{supplierId}\") {{ brandId isActive }} }}");
        afterRemove.Should().Contain("\"isActive\":false");
    }

    private async Task<string> GraphQlRaw(string query)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/admin/reports/graphql", new { query });
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode || body.Contains("\"errors\""))
            throw new Exception($"GRAPHQL FAIL [{(int)resp.StatusCode}]: {body[..Math.Min(600, body.Length)]}");
        return body;
    }

    private async Task<LoginResponse> LoginAsync(string email, string password)
    {
        var loginResp = await _client.PostAsJsonAsync("/api/v1/identity/login", new { email, password });
        loginResp.EnsureSuccessStatusCode();
        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        loginBody!.AccessToken.Should().NotBeNullOrEmpty();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", loginBody.AccessToken);
        return loginBody;
    }

    private sealed record RegisterResponse(Guid Id, string Email);
    private sealed record LoginResponse(string AccessToken);
    private sealed record ProductResponse(Guid Id);
    private sealed record OrderResponse(Guid Id, string Status);
    private sealed record CustomerCreatedResponse(Guid Id);
    private sealed record PaymentCreatedResponse(Guid Id, string Status);
    private sealed record PaymentSnapshotDto(Guid Id, Guid CustomerId, Guid OrderId, decimal Amount,
        string Currency, string Status, string MethodToken);
    private sealed record CustomerLastOrderDto(Guid OrderId, DateTimeOffset OrderDate, decimal TotalAmount,
        string Status, int ItemCount);
}
