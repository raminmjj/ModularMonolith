using AwesomeAssertions;
using ModularMonolith.DDD.Common;
using ModularMonolith.DDD.Events;
using ModularMonolith.Modules.Supplier.Application.Domain.Suppliers;
using ModularMonolith.Modules.Supplier.Application.Ports.Outbound;
using ModularMonolith.Modules.Supplier.Application.Domain.Events;
using NSubstitute;
using Xunit;

namespace ModularMonolith.UnitTests;

public class SupplierDomainTests
{
    public static Supplier VerifiedSupplier()
    {
        var s = Supplier.Create("Acme", $"admin{Guid.NewGuid():N}@acme.test", "+123456789", "Main St 1");
        s.Verify();
        return s;
    }

    [Fact]
    public void Create_Starts_Pending_And_Unverified()
    {
        var s = Supplier.Create("Acme", "a@acme.test", null, null);
        s.Status.Value.Should().Be("Pending");
        s.IsVerified.Should().BeFalse();
    }

    [Fact]
    public void Verify_Then_Suspend_Lifecycle_Works()
    {
        var s = Supplier.Create("Acme", "a@acme.test", null, null);
        s.Verify();
        s.Status.Value.Should().Be("Verified");
        s.IsVerified.Should().BeTrue();

        s.Suspend();
        s.Status.Value.Should().Be("Suspended");
        s.IsVerified.Should().BeFalse();
    }

    [Fact]
    public void Suspended_Supplier_Cannot_Be_Reverified()
    {
        var s = VerifiedSupplier();
        s.Suspend();
        var act = () => s.Verify();
        act.Should().Throw<DomainException>().Where(e => e.Code == "SUPPLIER_SUSPENDED");
    }

    [Fact]
    public void Unverified_Supplier_Cannot_Add_Agreements()
    {
        var s = Supplier.Create("Acme", "a@acme.test", null, null);
        var act = () => s.AddBrandAgreement(Guid.NewGuid(), 10m, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>().Where(e => e.Code == "SUPPLIER_NOT_VERIFIED");
    }

    [Fact]
    public void Duplicate_Active_Agreement_For_Same_Brand_Is_Rejected()
    {
        var s = VerifiedSupplier();
        var brandId = Guid.NewGuid();
        s.AddBrandAgreement(brandId, 10m, DateTimeOffset.UtcNow);

        var act = () => s.AddBrandAgreement(brandId, 15m, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>().Where(e => e.Code == "BRAND_ALREADY_SUPPLIED");
    }

    [Fact]
    public void Commission_Rate_Must_Be_Between_Zero_And_Hundred()
    {
        var s = VerifiedSupplier();
        var act = () => s.AddBrandAgreement(Guid.NewGuid(), 150m, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>().Where(e => e.Code == "COMMISSION_RATE_INVALID");
    }

    [Fact]
    public void RemoveBrandAgreement_Deactivates_And_Is_Case_Of_No_Active_Agreement_Fails()
    {
        var s = VerifiedSupplier();
        var brandId = Guid.NewGuid();
        s.AddBrandAgreement(brandId, 10m, DateTimeOffset.UtcNow);
        s.RemoveBrandAgreement(brandId);

        s.Agreements.Single(a => a.BrandId == brandId).IsActive.Should().BeFalse();

        var again = () => s.RemoveBrandAgreement(brandId);
        again.Should().Throw<DomainException>().Where(e => e.Code == "AGREEMENT_NOT_ACTIVE");
    }

    [Fact]
    public async Task AssignBrand_Publishes_Aggregate_Event_Via_Dispatcher_Sequence()
    {
        // Admin-path proof: ACL check passes → aggregate mutates → save → dispatch+clear.
        var supplier = VerifiedSupplier();
        var brandId = Guid.NewGuid();

        var repo = Substitute.For<ISupplierRepository>();
        repo.GetByIdAsync(supplier.Id, Arg.Any<CancellationToken>()).Returns(supplier);
        var uow = Substitute.For<IUnitOfWork>();
        var checker = Substitute.For<IBrandExistenceChecker>();
        checker.EnsureBrandExistsAsync(brandId, Arg.Any<CancellationToken>())
            .Returns(ModularMonolith.Framework.Results.Result.Success());
        var dispatcher = Substitute.For<IEventDispatcher>();

        var admin = new ModularMonolith.Modules.Supplier.Application.Service.SupplierAdminService(
            repo, uow, checker, dispatcher);

        var result = await admin.AssignBrandAsync(supplier.Id, brandId, 12.5m);

        result.IsSuccess.Should().BeTrue();
        await checker.Received(1).EnsureBrandExistsAsync(brandId, Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await dispatcher.Received(1).DispatchAsync(
            Arg.Is<IEnumerable<IDomainEvent>>(e =>
                e.OfType<BrandAgreementAssignedDomainEvent>().Any(x => x.BrandId == brandId && x.CommissionRate == 12.5m)));
        supplier.Agreements.Should().ContainSingle(a => a.BrandId == brandId && a.CommissionRate == 12.5m);
    }
}
