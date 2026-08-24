using AwesomeAssertions;
using ModularMonolith.DDD.Common;
using ModularMonolith.DDD.Events;
using ModularMonolith.SharedKernel.ValueObjects;
using ModularMonolith.Modules.Catalog.Application.Domain.Events;
using ModularMonolith.Modules.Catalog.Application.Domain.Products;
using ModularMonolith.Modules.Catalog.Application.Domain.ValueObjects;
using ModularMonolith.Modules.Catalog.Application.Ports.Outbound;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Catalog.Application.Service;
using NSubstitute;
using Xunit;

namespace ModularMonolith.UnitTests;

/// <summary>
/// Contract tests for the pluggable event dispatching seam (ADR-0004):
/// NoOp delivers nothing, and the admin service publishes what the aggregate raised.
/// </summary>
public class EventDispatchingTests
{
    [Fact]
    public async Task NoOp_Dispatcher_Completes_Without_Side_Effects()
    {
        var dispatcher = NoOpEventDispatcher.Instance;

        await dispatcher.Invoking(d => d.DispatchAsync(new ProductDeactivatedDomainEvent(Guid.NewGuid())))
            .Should().NotThrowAsync();
        await dispatcher.Invoking(d => d.DispatchAsync([new ProductDeactivatedDomainEvent(Guid.NewGuid())]))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task DispatchAndClearAsync_Publishes_Aggregate_Events_And_Clears_Buffer()
    {
        var product = Product.Create(Sku.Create($"T-{Guid.NewGuid():N}"[..12]), "P", Money.Create(5m), 10);
        product.ClearDomainEvents(); // isolate: only the deactivation event under test
        product.Deactivate(); // raises ProductDeactivatedDomainEvent
        product.DomainEvents.Should().ContainSingle(e => e is ProductDeactivatedDomainEvent);

        var dispatcher = Substitute.For<IEventDispatcher>();
        await dispatcher.DispatchAndClearAsync(product);

        await dispatcher.Received(1).DispatchAsync(
            Arg.Is<IEnumerable<IDomainEvent>>(e => e.OfType<ProductDeactivatedDomainEvent>().Any()));
        product.DomainEvents.Should().BeEmpty("buffer must be cleared after publication");
    }

    [Fact]
    public async Task DeactivateProductAsync_Publishes_ProductDeactivated_Via_Dispatcher()
    {
        // Arrange: existing active product.
        var productId = Guid.NewGuid();
        var repo = Substitute.For<IProductRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var dispatcher = Substitute.For<IEventDispatcher>();

        var product = Product.Create(Sku.Create($"T-{Guid.NewGuid():N}"[..12]), "P", Money.Create(5m), 10);
        typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(product, productId); // deterministic id
        repo.GetByIdAsync(productId, Arg.Any<CancellationToken>()).Returns(product);

        // Real NoOp would work too; substitute lets us assert the call happened.
        var sut = new CatalogAdminService(
            Substitute.For<ModularMonolith.Modules.Catalog.Application.Ports.Inbound.IProductService>(),
            repo,
            uow,
            dispatcher);

        // Act.
        var result = await sut.DeactivateProductAsync(productId);

        // Assert: operation succeeded AND the aggregate's event went through the seam.
        result.IsSuccess.Should().BeTrue();
        product.IsActive.Should().BeFalse();
        await dispatcher.Received(1).DispatchAsync(
            Arg.Is<IEnumerable<IDomainEvent>>(e => e.OfType<ProductDeactivatedDomainEvent>().Any()));
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
