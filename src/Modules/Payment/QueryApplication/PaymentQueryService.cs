using Microsoft.EntityFrameworkCore;
using ModularMonolith.Contracts.Payment;
using ModularMonolith.Contracts.Reporting;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Payment.Application.Domain.Payments;

using ModularMonolith.Modules.Payment.Adapter.Outbound.Repositories.SqlServer;
namespace ModularMonolith.Modules.Payment.QueryApplication;

public interface IPaymentQueryService
{
    Task<Result<PaymentSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentSnapshot>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>
    /// Flat failed-payment rows for cross-module report composition (single SQL).
    /// No grouping/paging here — the Reporting module owns composition.
    /// </summary>
    Task<IReadOnlyList<FailedPaymentDto>> GetFailedPaymentsAsync(
        DateTimeOffset? from, DateTimeOffset? to, decimal? minAmount, CancellationToken ct = default);

    /// <summary>Flat payment rows for the admin payment list (single filtered SQL).</summary>
    Task<IReadOnlyList<PaymentAdminRowDto>> ListPaymentsAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Latest payment for an order (admin order-detail composition).</summary>
    Task<Result<PaymentSnapshot>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>
/// Strictly read-only: direct EF Core projection into a Contract record.
/// No aggregate behavior, no SaveChanges (arch-tested: no outbound port references).
/// </summary>
internal sealed class PaymentQueryService(PaymentDbContext db) : IPaymentQueryService
{
    public async Task<Result<PaymentSnapshot>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var result = await db.Set<PaymentTransaction>()
            .Where(p => p.Id == id)
            .Select(p => new PaymentSnapshot(
                p.Id, p.CustomerId, p.OrderId, p.Amount.Amount, p.Amount.Currency,
                p.Status.Value, p.Method.Token))
            .FirstOrDefaultAsync(ct);

        return result is null
            ? Result.Failure<PaymentSnapshot>(new Error("PAYMENT_NOT_FOUND", "Payment was not found."))
            : Result.Success(result);
    }

    public async Task<IReadOnlyList<PaymentSnapshot>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default)
        => await db.Set<PaymentTransaction>()
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentSnapshot(
                p.Id, p.CustomerId, p.OrderId, p.Amount.Amount, p.Amount.Currency,
                p.Status.Value, p.Method.Token))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FailedPaymentDto>> GetFailedPaymentsAsync(
        DateTimeOffset? from, DateTimeOffset? to, decimal? minAmount, CancellationToken ct = default)
    {
        var query = db.Set<PaymentTransaction>().Where(p => p.Status.Value == "Failed");

        if (from is not null) query = query.Where(p => p.FailedAt >= from);
        if (to is not null) query = query.Where(p => p.FailedAt <= to);
        if (minAmount is not null) query = query.Where(p => p.Amount.Amount >= minAmount);

        return await query
            .OrderByDescending(p => p.FailedAt)
            .Select(p => new FailedPaymentDto(
                p.Id, p.CustomerId, p.OrderId, p.Amount.Amount, p.Amount.Currency,
                p.FailedAt!.Value, p.FailureReason))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PaymentAdminRowDto>> ListPaymentsAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Set<PaymentTransaction>().AsQueryable();

        if (from is not null) query = query.Where(p => p.CreatedAt >= from);
        if (to is not null) query = query.Where(p => p.CreatedAt <= to);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status.Value == status);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new PaymentAdminRowDto(
                p.Id, p.CustomerId, p.OrderId, p.Amount.Amount, p.Amount.Currency,
                p.Status.Value, p.CreatedAt, p.Method.Token))
            .ToListAsync(ct);
    }

    public async Task<Result<PaymentSnapshot>> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var result = await db.Set<PaymentTransaction>()
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentSnapshot(
                p.Id, p.CustomerId, p.OrderId, p.Amount.Amount, p.Amount.Currency,
                p.Status.Value, p.Method.Token))
            .FirstOrDefaultAsync(ct);

        return result is null
            ? Result.Failure<PaymentSnapshot>(new Error("PAYMENT_NOT_FOUND", "No payment exists for this order."))
            : Result.Success(result);
    }
}
