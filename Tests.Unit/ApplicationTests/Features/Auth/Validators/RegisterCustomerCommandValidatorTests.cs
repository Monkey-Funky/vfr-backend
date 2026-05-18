using Application.Features.Customer.Auth.Commands.Register;
using FluentValidation.TestHelper;

namespace Tests.Unit.ApplicationTests.Features.Auth.Validators;

public sealed class RegisterCustomerCommandValidatorTests
{
    private readonly RegisterCustomerCommandValidator _sut = new();

    private static RegisterCustomerCommand ValidCommand() =>
        new("Jane Doe", "customer@example.com", "StrongPass1!");

    [Fact]
    public void Valid_AllFieldsValid_PassesValidation()
    {
        var result = _sut.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Invalid_EmptyFullName_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { FullName = "" });
        result.ShouldHaveValidationErrorFor(x => x.FullName)
              .WithErrorMessage("Full name is required.");
    }

    [Fact]
    public void Invalid_EmptyEmail_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Email = "" });
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email address is required.");
    }

    [Fact]
    public void Invalid_WeakPassword_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Password = "weakpassword" });
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage(
                  "Password must contain at least one uppercase letter, one lowercase letter, " +
                  "one digit, and one special character.");
    }
}