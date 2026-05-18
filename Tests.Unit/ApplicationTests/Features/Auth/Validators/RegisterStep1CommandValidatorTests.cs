using Application.Features.Auth.Commands.RegisterStep1;
using FluentValidation.TestHelper;

namespace Tests.Unit.ApplicationTests.Features.Auth.Validators;

public sealed class RegisterStep1CommandValidatorTests
{
    private readonly RegisterStep1CommandValidator _sut = new();

    private static RegisterStep1Command ValidCommand() =>
        new("John Doe", "retailer@example.com", "StrongPass1!", "MyBrand");

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
    public void Invalid_FullNameExceeds100Chars_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { FullName = new string('A', 101) });
        result.ShouldHaveValidationErrorFor(x => x.FullName)
              .WithErrorMessage("Full name must not exceed 100 characters.");
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
        var result = _sut.TestValidate(ValidCommand() with { Email = "not-an-email" });
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("A valid email address is required.");
    }

    [Fact]
    public void Invalid_EmailExceeds200Chars_FailsWithMessage()
    {
        var longEmail = new string('a', 190) + "@test.com";
        var result = _sut.TestValidate(ValidCommand() with { Email = longEmail });
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email address must not exceed 200 characters.");
    }

    [Fact]
    public void Invalid_EmptyPassword_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Password = "" });
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password is required.");
    }

    [Fact]
    public void Invalid_PasswordTooShort_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Password = "Sh0rt!" });
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Password must be at least 8 characters.");
    }

    [Fact]
    public void Invalid_PasswordNoUppercase_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Password = "lowercase1!" });
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage(
                  "Password must contain at least one uppercase letter, one lowercase letter, " +
                  "one digit, and one special character.");
    }

    [Fact]
    public void Invalid_PasswordNoDigit_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Password = "NoDigits!Abc" });
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage(
                  "Password must contain at least one uppercase letter, one lowercase letter, " +
                  "one digit, and one special character.");
    }

    [Fact]
    public void Invalid_PasswordNoSpecialChar_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { Password = "NoSpecial123" });
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage(
                  "Password must contain at least one uppercase letter, one lowercase letter, " +
                  "one digit, and one special character.");
    }

    [Fact]
    public void Invalid_EmptyBrandName_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { BrandName = "" });
        result.ShouldHaveValidationErrorFor(x => x.BrandName)
              .WithErrorMessage("Brand name is required.");
    }

    [Fact]
    public void Invalid_BrandNameTooShort_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { BrandName = "X" });
        result.ShouldHaveValidationErrorFor(x => x.BrandName)
              .WithErrorMessage("Brand name must be at least 2 characters.");
    }

    [Fact]
    public void Invalid_BrandNameExceeds150Chars_FailsWithMessage()
    {
        var result = _sut.TestValidate(ValidCommand() with { BrandName = new string('B', 151) });
        result.ShouldHaveValidationErrorFor(x => x.BrandName)
              .WithErrorMessage("Brand name must not exceed 150 characters.");
    }
}