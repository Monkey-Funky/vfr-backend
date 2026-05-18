using Application.Behaviors;
using FluentValidation;
using MediatR;
using DomainValidationException = Domain.Exceptions.ValidationException;

namespace Tests.Unit.ApplicationTests.Behaviors;

public sealed class ValidationBehaviorTests
{
    public sealed record TestRequest : IRequest<string>;

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        // Arrange
        var sut = new ValidationBehavior<TestRequest, string>(Enumerable.Empty<IValidator<TestRequest>>());

        // Act
        var result = await sut.Handle(new TestRequest(), (_) => Task.FromResult("Success"), default);

        // Assert
        result.Should().Be("Success");
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNext()
    {
        // Arrange
        var validator = new TestRequestValidator(valid: true);
        var sut = new ValidationBehavior<TestRequest, string>(new[] { validator });

        // Act
        var result = await sut.Handle(new TestRequest(), (_) => Task.FromResult("Success"), default);

        // Assert
        result.Should().Be("Success");
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var validator = new TestRequestValidator(valid: false);
        var sut = new ValidationBehavior<TestRequest, string>(new[] { validator });

        // Act
        var act = () => sut.Handle(new TestRequest(), (_) => Task.FromResult("Success"), default);

        // Assert
        var ex = await act.Should().ThrowAsync<DomainValidationException>();
        ex.Which.Errors.Should().ContainKey("Prop1");
    }

    private sealed class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator(bool valid)
        {
            if (!valid)
            {
                RuleFor(x => x).Custom((x, context) => context.AddFailure("Prop1", "Error 1"));
            }
        }
    }
}
