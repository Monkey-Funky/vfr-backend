using Application.Behaviors;
using FluentValidation;
using MediatR;
using DomainValidationException = Domain.Exceptions.ValidationException;

namespace Tests.Unit.ApplicationTests.Behaviors;

public sealed class ValidationBehaviorTests
{
    private sealed record TestCommand : IRequest<string>;

    private static RequestHandlerDelegate<string> NextDelegate(string result = "ok")
        => _ => Task.FromResult(result);

    [Fact]
    public async Task Handle_WithNoValidators_PassesThrough()
    {
        var sut = new ValidationBehavior<TestCommand, string>(
            Enumerable.Empty<IValidator<TestCommand>>());

        var result = await sut.Handle(new TestCommand(), NextDelegate("passed"), default);

        result.Should().Be("passed");
    }

    [Fact]
    public async Task Handle_WithValidCommand_PassesThrough()
    {
        var validator = new InlineValidator<TestCommand>();
        var sut = new ValidationBehavior<TestCommand, string>(new[] { validator });

        var result = await sut.Handle(new TestCommand(), NextDelegate("passed"), default);

        result.Should().Be("passed");
    }

    [Fact]
    public async Task Handle_WithInvalidCommand_ThrowsValidationException()
    {
        var validator = new InlineValidator<TestCommand>();
        validator.RuleFor(x => x).Custom((_, ctx) => ctx.AddFailure("Field", "Error"));
        var sut = new ValidationBehavior<TestCommand, string>(new[] { validator });

        var act = async () => await sut.Handle(new TestCommand(), NextDelegate(), default);

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task Handle_WithMultipleErrors_ReturnsAllErrors()
    {
        var validator = new InlineValidator<TestCommand>();
        validator.RuleFor(x => x).Custom((_, ctx) =>
        {
            ctx.AddFailure("FieldA", "Error A1");
            ctx.AddFailure("FieldA", "Error A2");
            ctx.AddFailure("FieldB", "Error B1");
        });
        var sut = new ValidationBehavior<TestCommand, string>(new[] { validator });

        var act = async () => await sut.Handle(new TestCommand(), NextDelegate(), default);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Should().HaveCount(2);
        ex.Which.Errors["FieldA"].Should().HaveCount(2);
        ex.Which.Errors["FieldB"].Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ValidationExceptionContainsFieldNames()
    {
        var validator = new InlineValidator<TestCommand>();
        validator.RuleFor(x => x).Custom((_, ctx) =>
        {
            ctx.AddFailure("Email", "Invalid email");
            ctx.AddFailure("Password", "Password too short");
        });
        var sut = new ValidationBehavior<TestCommand, string>(new[] { validator });

        var act = async () => await sut.Handle(new TestCommand(), NextDelegate(), default);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Keys.Should().Contain("Email");
        ex.Which.Errors.Keys.Should().Contain("Password");
    }

    [Fact]
    public async Task Handle_ValidationExceptionContainsMessages()
    {
        var validator = new InlineValidator<TestCommand>();
        validator.RuleFor(x => x).Custom((_, ctx) =>
            ctx.AddFailure("Email", "Email is required"));
        var sut = new ValidationBehavior<TestCommand, string>(new[] { validator });

        var act = async () => await sut.Handle(new TestCommand(), NextDelegate(), default);

        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors["Email"].Should().Contain("Email is required");
    }
}