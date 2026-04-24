// tests/Tests.Unit/Application/Behaviors/ValidationBehaviorTests.cs
using Application.Behaviors;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;
using Xunit;

namespace Tests.Unit.Application.Behaviors;

public sealed class ValidationBehaviorTests
{
    private sealed record TestRequest(string Value) : IRequest<bool>;

    // ── Builder helpers ───────────────────────────────────────────────────────

    private static IValidator<TestRequest> BuildFailingValidator(string field, string message)
    {
        var mock = new Mock<IValidator<TestRequest>>(MockBehavior.Strict);
        mock.Setup(v => v.Validate(It.IsAny<ValidationContext<TestRequest>>()))
            .Returns(new ValidationResult(new[] { new ValidationFailure(field, message) }));
        return mock.Object;
    }

    private static IValidator<TestRequest> BuildPassingValidator()
    {
        var mock = new Mock<IValidator<TestRequest>>(MockBehavior.Strict);
        mock.Setup(v => v.Validate(It.IsAny<ValidationContext<TestRequest>>()))
            .Returns(new ValidationResult());
        return mock.Object;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenValidatorReturnsFails_ThrowsValidationExceptionBeforeHandlerCalled()
    {
        // Arrange
        var validator = BuildFailingValidator("Value", "Value is required.");
        var behavior = new ValidationBehavior<TestRequest, bool>(new[] { validator });

        bool handlerInvoked = false;

        // FIX: RequestHandlerDelegate<TResponse> = Func<CancellationToken, Task<TResponse>>.
        //      Lambdas must accept a CancellationToken parameter.
        RequestHandlerDelegate<bool> next = _ =>
        {
            handlerInvoked = true;
            return Task.FromResult(true);
        };

        // Act
        Func<Task> act = () => behavior.Handle(new TestRequest(""), next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*Value is required.*");

        handlerInvoked.Should().BeFalse(
            "the inner handler must never execute when validation fails");
    }

    [Fact]
    public async Task Handle_WhenNoValidatorsRegistered_InvokesInnerHandlerAndReturnsResult()
    {
        // Arrange
        var behavior = new ValidationBehavior<TestRequest, bool>(
            Enumerable.Empty<IValidator<TestRequest>>());

        bool handlerInvoked = false;

        // FIX: 1-arg lambda
        RequestHandlerDelegate<bool> next = _ =>
        {
            handlerInvoked = true;
            return Task.FromResult(true);
        };

        // Act
        bool result = await behavior.Handle(new TestRequest("hello"), next, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        handlerInvoked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidatorPassesWithNoErrors_InvokesInnerHandlerAndReturnsResult()
    {
        // Arrange
        var validator = BuildPassingValidator();
        var behavior = new ValidationBehavior<TestRequest, bool>(new[] { validator });

        bool handlerInvoked = false;

        // FIX: 1-arg lambda
        RequestHandlerDelegate<bool> next = _ =>
        {
            handlerInvoked = true;
            return Task.FromResult(true);
        };

        // Act
        bool result = await behavior.Handle(new TestRequest("valid"), next, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        handlerInvoked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenMultipleValidatorsAllFail_AggregatesAllFailures()
    {
        // Arrange
        var v1 = BuildFailingValidator("Field1", "Error 1.");
        var v2 = BuildFailingValidator("Field2", "Error 2.");
        var behavior = new ValidationBehavior<TestRequest, bool>(new[] { v1, v2 });

        // FIX: inline 1-arg lambda
        Func<Task> act = () => behavior.Handle(
            new TestRequest(""),
            _ => Task.FromResult(true),
            CancellationToken.None);

        // Assert — both errors must be aggregated into one exception
        var exception = await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        exception.Which.Errors.Should().HaveCount(2);
    }
}