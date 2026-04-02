using FluentAssertions;
using NetArchTest.Rules;

namespace Tests.Architecture;

/// <summary>
/// These tests enforce Clean Architecture layer dependency rules.
/// They run in CI and block any merge that introduces a layer violation.
/// Failing test = layer boundary crossed = do not merge.
/// </summary>
public sealed class ArchitectureTests
{
    private const string DomainNamespace = "Domain";
    private const string ApplicationNamespace = "Application";
    private const string InfrastructureNamespace = "Infrastructure";
    private const string ApiNamespace = "API";

    // ── Domain must not depend on anything else ──────────────────────────────

    [Fact]
    public void Domain_Should_Not_HaveDependencyOn_Application()
    {
        var result = Types.InNamespace(DomainNamespace)
            .Should()
            .NotHaveDependencyOn(ApplicationNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain layer must have zero outward dependencies.");
    }

    [Fact]
    public void Domain_Should_Not_HaveDependencyOn_Infrastructure()
    {
        var result = Types.InNamespace(DomainNamespace)
            .Should()
            .NotHaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain layer must have zero outward dependencies.");
    }

    [Fact]
    public void Domain_Should_Not_HaveDependencyOn_API()
    {
        var result = Types.InNamespace(DomainNamespace)
            .Should()
            .NotHaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain layer must have zero outward dependencies.");
    }

    // ── Application must not depend on Infrastructure or API ─────────────────

    [Fact]
    public void Application_Should_Not_HaveDependencyOn_Infrastructure()
    {
        var result = Types.InNamespace(ApplicationNamespace)
            .Should()
            .NotHaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application layer must not depend on Infrastructure.");
    }

    [Fact]
    public void Application_Should_Not_HaveDependencyOn_API()
    {
        var result = Types.InNamespace(ApplicationNamespace)
            .Should()
            .NotHaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application layer must not depend on API.");
    }

    // ── API controllers must not directly depend on Infrastructure ────────────

    [Fact]
    public void Controllers_Should_Not_HaveDirectDependencyOn_Infrastructure()
    {
        var result = Types.InNamespace($"{ApiNamespace}.Controllers")
            .Should()
            .NotHaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Controllers must only depend on Application layer (via MediatR). " +
            "Infrastructure is registered in Program.cs DI only.");
    }

    // ── Domain entities must inherit BaseEntity ───────────────────────────────

    [Fact]
    public void DomainEntities_Should_InheritFrom_BaseEntity()
    {
        var result = Types.InNamespace($"{DomainNamespace}.Entities")
            .Should()
            .Inherit(typeof(Domain.Common.BaseEntity))
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All domain entities must inherit from BaseEntity.");
    }

    // ── Handlers must be in Application layer ────────────────────────────────

    [Fact]
    public void CommandHandlers_Should_ResideIn_ApplicationLayer()
    {
        var result = Types.InCurrentDomain()
            .That()
            .HaveNameEndingWith("CommandHandler")
            .Should()
            .ResideInNamespace(ApplicationNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All command handlers must reside in the Application layer.");
    }

    [Fact]
    public void QueryHandlers_Should_ResideIn_ApplicationLayer()
    {
        var result = Types.InCurrentDomain()
            .That()
            .HaveNameEndingWith("QueryHandler")
            .Should()
            .ResideInNamespace(ApplicationNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All query handlers must reside in the Application layer.");
    }

    // ── Validators must be in Application layer ───────────────────────────────

    [Fact]
    public void Validators_Should_ResideIn_ApplicationLayer()
    {
        var result = Types.InCurrentDomain()
            .That()
            .HaveNameEndingWith("Validator")
            .And()
            .AreNotAbstract()
            .Should()
            .ResideInNamespace(ApplicationNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All FluentValidation validators must reside in the Application layer.");
    }
}