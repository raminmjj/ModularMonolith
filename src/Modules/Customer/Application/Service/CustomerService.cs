using ModularMonolith.Contracts.Customer;
using ModularMonolith.DDD.Common;
using ModularMonolith.Framework.Results;
using ModularMonolith.Modules.Customer.Application.Domain.Customers;
using ModularMonolith.Modules.Customer.Application.Domain.ValueObjects;
using ModularMonolith.Modules.Customer.Application.Ports.Outbound;
using CustomerAggregate = ModularMonolith.Modules.Customer.Application.Domain.Customers.Customer;

namespace ModularMonolith.Modules.Customer.Application.Service;

/// <summary>
/// Inbound port implementation. Orchestrates: load → call domain method → save.
/// All business rules live in the Customer aggregate; this class never mutates state.
/// </summary>
public sealed class CustomerService : Ports.Inbound.ICustomerInboundPort
{
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _unitOfWork;

    public CustomerService(ICustomerRepository customers, IUnitOfWork unitOfWork)
        => (_customers, _unitOfWork) = (customers, unitOfWork);

    public async Task<Result<Guid>> RegisterAsync(Guid identityUserId, string displayName, CancellationToken ct = default)
    {
        if (await _customers.ExistsForIdentityUserAsync(identityUserId, ct))
            return Result.Failure<Guid>(new Error("CUSTOMER_EXISTS", "A customer profile already exists for this identity user."));

        var customer = CustomerAggregate.Register(identityUserId, displayName);
        await _customers.AddAsync(customer, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(customer.Id);
    }

    public async Task<Result> AddAddressAsync(Guid customerId, string street, string city, string postalCode, string country, CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(customerId, ct);
        if (customer is null) return Result.Failure(new Error("CUSTOMER_NOT_FOUND", "Customer was not found."));

        Address address;
        try { address = Address.Create(street, city, postalCode, country); }
        catch (DomainException dex) { return Result.Failure(new Error(dex.Code, dex.Message)); }

        customer.AddAddress(address);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SuspendAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(customerId, ct);
        if (customer is null) return Result.Failure(new Error("CUSTOMER_NOT_FOUND", "Customer was not found."));
        customer.Suspend();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ReactivateAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(customerId, ct);
        if (customer is null) return Result.Failure(new Error("CUSTOMER_NOT_FOUND", "Customer was not found."));
        customer.Reactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<Guid>> SavePaymentMethodAsync(Guid customerId, string tokenizedCard, string cardType, DateOnly expiry, CancellationToken ct = default)
    {
        var customer = await _customers.GetByIdAsync(customerId, ct);
        if (customer is null) return Result.Failure<Guid>(new Error("CUSTOMER_NOT_FOUND", "Customer was not found."));

        SavedPaymentMethod method;
        try { method = customer.AddSavedPaymentMethod(tokenizedCard, cardType, expiry); }
        catch (DomainException dex) { return Result.Failure<Guid>(new Error(dex.Code, dex.Message)); }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(method.Id);
    }

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
        if (customer is null)
            return Result.Failure<SavedPaymentMethodDto>(new Error("CUSTOMER_NOT_FOUND", "Customer was not found."));

        var method = customer.PaymentMethods.FirstOrDefault(x => x.Id == paymentMethodId);
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
}
