using FluentValidation;

namespace ModularMonolith.Modules.Catalog.Application.Validation;

/// <summary>
/// Validator for CreateProduct use case. Lives INSIDE the hexagon (Application)
/// and is invoked at the hexagon boundary (Service) BEFORE domain operations.
/// This is Hexagonal Best Practice: validate at the port, not in the adapter.
/// </summary>
public sealed class CreateProductValidator : AbstractValidator<(string Sku, string Name, decimal Price, string Currency, int InitialStock, string? Description)>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().WithMessage("SKU is required.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Name is required and must not exceed 200 characters.");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than zero.");
        RuleFor(x => x.Currency).Length(3).WithMessage("Currency must be a 3-letter ISO code.");
        RuleFor(x => x.InitialStock).GreaterThanOrEqualTo(0).WithMessage("Initial stock cannot be negative.");
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null).WithMessage("Description must not exceed 2000 characters.");
    }
}

/// <summary>
/// Validator for ChangePrice use case.
/// </summary>
public sealed class ChangePriceValidator : AbstractValidator<(Guid ProductId, decimal NewPrice, string Currency)>
{
    public ChangePriceValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required.");
        RuleFor(x => x.NewPrice).GreaterThan(0).WithMessage("Price must be greater than zero.");
        RuleFor(x => x.Currency).Length(3).WithMessage("Currency must be a 3-letter ISO code.");
    }
}

/// <summary>
/// Validator for ReserveStock use case.
/// </summary>
public sealed class ReserveStockValidator : AbstractValidator<(Guid ProductId, int Quantity)>
{
    public ReserveStockValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Product ID is required.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
    }
}
