using AwesomeAssertions;
using ModularMonolith.DDD.Common;
using ModularMonolith.Modules.Brand.Application.Domain.Brands;
using Xunit;

namespace ModularMonolith.UnitTests;

public class BrandDomainTests
{
    [Fact]
    public void Create_Starts_PendingReview()
    {
        var b = Brand.Create("Acme", null, "desc", null, "de");
        b.Status.Value.Should().Be("PendingReview");
        b.Slug.Value.Should().Be("acme"); // slug derived from name
    }

    [Fact]
    public void Approve_Then_Reject_Is_Blocked_By_State_Machine()
    {
        var b = Brand.Create("Acme", "acme", null, null, "de");
        b.Approve();
        b.Status.Value.Should().Be("Approved");

        var act = () => b.Reject("no longer wanted");
        act.Should().Throw<DomainException>().Where(e => e.Code == "BRAND_NOT_PENDING");
    }

    [Fact]
    public void Reject_Requires_A_Reason()
    {
        var b = Brand.Create("Acme", "acme", null, null, "de");
        var act = () => b.Reject(" ");
        act.Should().Throw<DomainException>().Where(e => e.Code == "REJECTION_REASON_REQUIRED");

        b.Reject("trademark conflict");
        b.Status.Value.Should().Be("Rejected");
        b.RejectionReason.Should().Be("trademark conflict");
    }

    [Fact]
    public void Slug_Validates_Characters()
    {
        var act = () => Modules.Brand.Application.Domain.ValueObjects.Slug.Create("bad slug!!");
        act.Should().Throw<DomainException>().Where(e => e.Code == "SLUG_INVALID");
    }

    [Fact]
    public void Country_Must_Be_Iso_Alpha2()
    {
        var act = () => Brand.Create("Acme", "acme", null, null, "Germany");
        act.Should().Throw<DomainException>().Where(e => e.Code == "BRAND_COUNTRY_INVALID");
    }
}
