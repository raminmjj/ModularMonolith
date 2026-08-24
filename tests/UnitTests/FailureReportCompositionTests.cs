using Xunit;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using ModularMonolith.Contracts.Customer;
using ModularMonolith.Contracts.Admin;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Admin.Application.Ports.Outbound;
using ModularMonolith.Modules.Admin.Application.Service;
using NSubstitute;

namespace ModularMonolith.UnitTests;

/// <summary>
/// PERFORMANCE PROOF (ADR-0006): the composition performs EXACTLY ONE call per
/// provider read side — for any number of customers. This is the machine-checked
/// form of "N+1 impossible by construction": if anyone introduces per-customer IO,
/// these ReceivedCalls().Count assertions fail.
/// </summary>
public class FailureReportCompositionTests
{
    private static FailureReportFilter Filter(int page = 1, int pageSize = 100) =>
        new(null, null, null, CustomerStatus: null, Page: page, PageSize: pageSize);

    private static FailedPaymentDto Failure(Guid customerId, decimal amount = 10m, int i = 0) =>
        new(Guid.NewGuid(), customerId, Guid.NewGuid(), amount, "USD",
            DateTimeOffset.UtcNow.AddMinutes(-i), "declined");

    [Fact]
    public async Task Composition_Calls_Each_Provider_Exactly_Once_For_Any_Number_Of_Customers()
    {
        const int customerCount = 50; // simulate a large page

        var payments = Substitute.For<IPaymentReadDataProvider>();
        var customers = Substitute.For<ICustomerReadDataProvider>();
        var orders = Substitute.For<IOrderReadDataProvider>();

        var ids = Enumerable.Range(0, customerCount).Select(_ => Guid.NewGuid()).ToList();
        payments.GetFailedPaymentsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<FailedPaymentDto>>([.. ids.Select(id => Failure(id))]));
        customers.GetCustomersByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Result.Success<IReadOnlyList<CustomerSnapshot>>(
                [.. callInfo.ArgAt<IReadOnlyList<Guid>>(0).Select(id =>
                    new CustomerSnapshot(id, Guid.NewGuid(), $"C-{id:N}"[..8], "Active", "Standard"))]));
        orders.GetLastOrdersByCustomerAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<LastOrderDto>>([])); // customers without orders are legal

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new FailureReportService(payments, customers, orders, cache);

        var result = await sut.GetCustomersWithFailedPaymentsAsync(Filter());

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(customerCount);

        // THE PROOF: one call per provider — not one per customer.
        _ = payments.ReceivedCalls().Count().Should().Be(1);
        _ = customers.ReceivedCalls().Count().Should().Be(1);
        _ = orders.ReceivedCalls().Count().Should().Be(1);
    }

    [Fact]
    public async Task LastOrder_Is_Attached_To_Page_Items_And_Missing_Orders_Yield_Null()
    {
        var withOrder = Guid.NewGuid();
        var withoutOrder = Guid.NewGuid();
        var withOrderIdentity = Guid.NewGuid();

        var payments = Substitute.For<IPaymentReadDataProvider>();
        var customers = Substitute.For<ICustomerReadDataProvider>();
        var orders = Substitute.For<IOrderReadDataProvider>();

        payments.GetFailedPaymentsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<FailedPaymentDto>>(
                [Failure(withOrder), Failure(withoutOrder)]));
        customers.GetCustomersByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<CustomerSnapshot>>([
                new CustomerSnapshot(withOrder, withOrderIdentity, "With", "Active", "Standard"),
                new CustomerSnapshot(withoutOrder, Guid.NewGuid(), "Without", "Active", "Standard"),
            ]));
        orders.GetLastOrdersByCustomerAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<LastOrderDto>>(
                [new LastOrderDto(Guid.NewGuid(), withOrderIdentity, DateTimeOffset.UtcNow, 19.98m, "Pending", 1)]));

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new FailureReportService(payments, customers, orders, cache);

        var result = await sut.GetCustomersWithFailedPaymentsAsync(Filter());

        result.IsSuccess.Should().BeTrue();
        var entryWith = result.Value!.Items.Single(i => i.Customer.Id == withOrder);
        entryWith.Customer.LastOrder.Should().NotBeNull();
        entryWith.Customer.LastOrder!.Status.Should().Be("Pending");
        entryWith.Customer.LastOrder.ItemCount.Should().Be(1);

        var entryWithout = result.Value!.Items.Single(i => i.Customer.Id == withoutOrder);
        entryWithout.Customer.LastOrder.Should().BeNull(); // nullable → backward-compatible GraphQL field
    }

    [Fact]
    public async Task Cached_Second_Request_Performs_Zero_Provider_Calls()
    {
        var payments = Substitute.For<IPaymentReadDataProvider>();
        var customers = Substitute.For<ICustomerReadDataProvider>();
        var orders = Substitute.For<IOrderReadDataProvider>();

        var onlyCustomer = Guid.NewGuid();
        payments.GetFailedPaymentsAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<FailedPaymentDto>>([Failure(onlyCustomer)]));
        customers.GetCustomersByIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<CustomerSnapshot>>(
                [new CustomerSnapshot(onlyCustomer, Guid.NewGuid(), "Solo", "Active", "Standard")]));
        orders.GetLastOrdersByCustomerAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<LastOrderDto>>([]));

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new FailureReportService(payments, customers, orders, cache);
        var filter = Filter();

        (await sut.GetCustomersWithFailedPaymentsAsync(filter)).IsSuccess.Should().BeTrue();
        (await sut.GetCustomersWithFailedPaymentsAsync(filter)).IsSuccess.Should().BeTrue(); // cache hit

        // Still exactly one call each across BOTH requests.
        _ = payments.ReceivedCalls().Count().Should().Be(1);
        _ = customers.ReceivedCalls().Count().Should().Be(1);
        _ = orders.ReceivedCalls().Count().Should().Be(1);
    }
}

