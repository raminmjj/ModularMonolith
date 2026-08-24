using AwesomeAssertions;
using Xunit;
using ModularMonolith.DDD.Common;
using ModularMonolith.SharedKernel.ValueObjects;
using ModularMonolith.Modules.Catalog.Application.Domain.Products;
using ModularMonolith.Modules.Catalog.Application.Domain.ValueObjects;

namespace ModularMonolith.UnitTests;

/// <summary>
/// Reservation math + TTL reclaim logic — the invariants the saga compensation
/// and the reaper depend on. These had ZERO coverage before the review.
/// </summary>
public class ProductReservationTests
{
    private static Product ProductWithStock(int stock) =>
        Product.Create(Sku.Create($"T-{Guid.NewGuid():N}"[..12]), "Test", Money.Create(10m), stock);

    [Fact]
    public void Reserve_Reduces_AvailableStock()
    {
        var p = ProductWithStock(10);
        p.ReserveStock(4, Guid.NewGuid(), TimeSpan.FromMinutes(5));

        p.AvailableStock.Should().Be(6);
        p.ReservedStock.Should().Be(4);
    }

    [Fact]
    public void Reserve_More_Than_Available_Is_Rejected()
    {
        var p = ProductWithStock(3);
        var act = () => p.ReserveStock(5, Guid.NewGuid(), TimeSpan.FromMinutes(5));
        act.Should().Throw<DomainException>().Where(e => e.Code == "STOCK_INSUFFICIENT");
    }

    [Fact]
    public void Release_Returns_Reserved_Stock_And_Is_Idempotent()
    {
        var p = ProductWithStock(10);
        var id = Guid.NewGuid();
        p.ReserveStock(4, id, TimeSpan.FromMinutes(5));

        p.ReleaseReservation(id);
        p.ReleaseReservation(id); // second release is a no-op

        p.AvailableStock.Should().Be(10);
        p.ReservedStock.Should().Be(0);
    }

    [Fact]
    public void Commit_Deducts_From_Both_Reserved_And_Total_Stock()
    {
        var p = ProductWithStock(10);
        p.ReserveStock(4, Guid.NewGuid(), TimeSpan.FromMinutes(5));

        p.CommitReservation(p.Reservations.Single().Id);

        p.Stock.Should().Be(6);
        p.ReservedStock.Should().Be(0);
        p.AvailableStock.Should().Be(6);
    }

    [Fact]
    public void ReleaseExpiredReservations_Releases_Only_Ttl_Elapsed_Ones()
    {
        var p = ProductWithStock(10);
        var expiredId = Guid.NewGuid();
        var liveId = Guid.NewGuid();

        p.ReserveStock(3, expiredId, TimeSpan.Zero);      // TTL already elapsed at creation
        p.ReserveStock(2, liveId, TimeSpan.FromHours(1)); // still valid

        // Cutoff slightly in the future of both reservations' creation:
        // the zero-TTL reservation is expired, the one-hour reservation is not.
        var released = p.ReleaseExpiredReservations(DateTimeOffset.UtcNow.AddSeconds(5));

        released.Should().Equal(expiredId);
        p.ReservedStock.Should().Be(2);
        p.AvailableStock.Should().Be(8);
    }

    [Fact]
    public void ReleaseExpiredReservations_With_Nothing_Expired_Returns_Empty()
    {
        var p = ProductWithStock(5);
        p.ReserveStock(1, Guid.NewGuid(), TimeSpan.FromHours(1));

        p.ReleaseExpiredReservations(DateTimeOffset.UtcNow).Should().BeEmpty();
    }
}
