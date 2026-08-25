using Microsoft.EntityFrameworkCore;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Brand.Application.Ports.Inbound;

namespace ModularMonolith.Modules.Supplier.Adapter.Outbound;

/// <summary>
/// ACL adapter (exception #5): implements Supplier's IBrandExistenceChecker by
/// delegating to Brand's inbound port. Direct method call — no events, no bus.
/// </summary>
internal sealed class BrandExistenceChecker(IBrandService brands) : Application.Ports.Outbound.IBrandExistenceChecker
{
    public async Task<Result> EnsureBrandExistsAsync(Guid brandId, CancellationToken ct = default)
        => await brands.EnsureBrandExistsAsync(brandId, ct);
}
