using System.Collections.Immutable;
using AwesomeAssertions;
using Soarscore.Domain.PublishedClassDefinition;
using Soarscore.Domain.Scoring;
using Xunit;

namespace Soarscore.Domain.Tests;

/// <summary>
/// Tests for <see cref="ParameterResolver"/> — kanban/completed/bind-parameter-steel-thread-plan.md
/// WI-2 (finding 2). ParameterResolver had no test file before this thread; these
/// cover the three-step resolution order (binding, then declared default, then throw)
/// for both the Number path (Resolve) and the Flag path (ResolveFlag).
/// </summary>
public class ParameterResolverTests
{
    private static readonly Dictionary<string, MeasuredValue> NoBindings = new();

    // ---------------------------------------------------------- Number path

    [Fact]
    public void Resolve_binding_wins_over_declared_default()
    {
        var bindings = new Dictionary<string, MeasuredValue> { ["minPerGroup"] = MeasuredValue.Of(6m) };
        var declared = ImmutableArray.Create(new Parameter { Name = "minPerGroup", DefaultValue = MeasuredValue.Of(4m) });

        var result = ParameterResolver.Resolve(NumberOrParam.Param("minPerGroup"), bindings, declared);

        result.Should().Be(6m);
    }

    [Fact]
    public void Resolve_falls_back_to_declared_default_when_unbound()
    {
        var declared = ImmutableArray.Create(new Parameter { Name = "minPerGroup", DefaultValue = MeasuredValue.Of(4m) });

        var result = ParameterResolver.Resolve(NumberOrParam.Param("minPerGroup"), NoBindings, declared);

        result.Should().Be(4m);
    }

    [Fact]
    public void Resolve_unbound_with_no_default_throws()
    {
        var declared = ImmutableArray<Parameter>.Empty;

        FluentActions.Invoking(() =>
                ParameterResolver.Resolve(NumberOrParam.Param("minPerGroup"), NoBindings, declared))
            .Should().Throw<UnresolvedParameterException>();
    }

    [Fact]
    public void Resolve_wrong_kind_binding_throws()
    {
        var bindings = new Dictionary<string, MeasuredValue> { ["minPerGroup"] = MeasuredValue.Of(true) };
        var declared = ImmutableArray<Parameter>.Empty;

        FluentActions.Invoking(() =>
                ParameterResolver.Resolve(NumberOrParam.Param("minPerGroup"), bindings, declared))
            .Should().Throw<UnresolvedParameterException>();
    }

    [Fact]
    public void Resolve_wrong_kind_default_throws()
    {
        var declared = ImmutableArray.Create(new Parameter { Name = "minPerGroup", DefaultValue = MeasuredValue.Of(true) });

        FluentActions.Invoking(() =>
                ParameterResolver.Resolve(NumberOrParam.Param("minPerGroup"), NoBindings, declared))
            .Should().Throw<UnresolvedParameterException>();
    }

    // ------------------------------------------------------------ Flag path

    [Fact]
    public void ResolveFlag_binding_wins_over_declared_default()
    {
        var bindings = new Dictionary<string, MeasuredValue> { ["carryPenalties"] = MeasuredValue.Of(true) };
        var declared = ImmutableArray.Create(new Parameter
        {
            Name = "carryPenalties", Kind = MeasuredKind.Flag, DefaultValue = MeasuredValue.Of(false)
        });

        var result = ParameterResolver.ResolveFlag(FlagOrParam.Param("carryPenalties"), bindings, declared);

        result.Should().BeTrue();
    }

    [Fact]
    public void ResolveFlag_falls_back_to_declared_default_when_unbound()
    {
        var declared = ImmutableArray.Create(new Parameter
        {
            Name = "carryPenalties", Kind = MeasuredKind.Flag, DefaultValue = MeasuredValue.Of(false)
        });

        var result = ParameterResolver.ResolveFlag(FlagOrParam.Param("carryPenalties"), NoBindings, declared);

        result.Should().BeFalse();
    }

    [Fact]
    public void ResolveFlag_unbound_with_no_default_throws()
    {
        var declared = ImmutableArray<Parameter>.Empty;

        FluentActions.Invoking(() =>
                ParameterResolver.ResolveFlag(FlagOrParam.Param("carryPenalties"), NoBindings, declared))
            .Should().Throw<UnresolvedParameterException>();
    }

    [Fact]
    public void ResolveFlag_wrong_kind_binding_throws()
    {
        var bindings = new Dictionary<string, MeasuredValue> { ["carryPenalties"] = MeasuredValue.Of(5m) };
        var declared = ImmutableArray<Parameter>.Empty;

        FluentActions.Invoking(() =>
                ParameterResolver.ResolveFlag(FlagOrParam.Param("carryPenalties"), bindings, declared))
            .Should().Throw<UnresolvedParameterException>();
    }

    [Fact]
    public void ResolveFlag_wrong_kind_default_throws()
    {
        var declared = ImmutableArray.Create(new Parameter
        {
            Name = "carryPenalties", Kind = MeasuredKind.Flag, DefaultValue = MeasuredValue.Of(5m)
        });

        FluentActions.Invoking(() =>
                ParameterResolver.ResolveFlag(FlagOrParam.Param("carryPenalties"), NoBindings, declared))
            .Should().Throw<UnresolvedParameterException>();
    }
}
