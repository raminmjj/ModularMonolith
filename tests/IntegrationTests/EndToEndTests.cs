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
}
