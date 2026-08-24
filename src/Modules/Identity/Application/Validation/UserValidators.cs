using FluentValidation;

namespace ModularMonolith.Modules.Identity.Application.Validation;

public sealed class RegisterValidator : AbstractValidator<(string Email, string Password, string DisplayName)>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid email is required.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(64).WithMessage("Password must be 8-64 characters.");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100).WithMessage("Display name is required and must not exceed 100 characters.");
    }
}

public sealed class LoginValidator : AbstractValidator<(string Email, string Password)>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid email is required.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
    }
}
