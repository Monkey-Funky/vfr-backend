using Application.Features.Customer.Auth.Commands.Login;
using FluentValidation.TestHelper;

namespace Tests.Unit.ApplicationTests.Features.Auth.Validators;

public sealed class LoginCustomerCommandValidatorTests
{
    private readonly LoginCustomerCommandValidator _sut = new();

    private static LoginCustomerCommand ValidCommand() =>
        new("customer@example.com", "ValidPassword1!");

    [Fact]
    public void Valid_EmailAndPassword_PassesValidation()
    {
        var result = _sut.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Invalid_EmptyEmail_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Email = "" });
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email address is required.");
    }

    [Fact]
    public void Invalid_BadEmailFormat_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Email = "invalid-email" });
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("A valid email address is required.");
    }

    [Fact]
    public void Invalid_EmptyPassword_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Password = "" });
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password is required.");
    }
}