// ParameterResolver — kanban/completed/scoring-service-plan.md WI-2.
//
// Resolves NumberOrParam/FlagOrParam to concrete decimal/bool values, and
// produces ResolvedTask snapshots from TaskDefinitions. Pipeline stages
// consume ResolvedTask — they never need to resolve parameter refs themselves.

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.Domain.Scoring;

/// <summary>
/// Thrown when a ParameterRef has no corresponding binding in the bindings dictionary.
/// </summary>
public sealed class UnresolvedParameterException : Exception
{
    public string ParameterName { get; }

    public UnresolvedParameterException(string parameterName)
        : base($"Parameter '{parameterName}' has no binding in the provided bindings dictionary.")
    {
        ParameterName = parameterName;
    }

    public UnresolvedParameterException(string parameterName, string? message)
        : base(message ?? $"Parameter '{parameterName}' has no binding.")
    {
        ParameterName = parameterName;
    }
}

/// <summary>
/// Resolves NumberOrParam/FlagOrParam to concrete values and produces
/// ResolvedTask snapshots.
/// </summary>
public static class ParameterResolver
{
    // ---------------------------------------------------- NumberOrParam

    /// <summary>
    /// Resolve a NumberOrParam to a concrete decimal.
    /// Literal passes through; Ref resolves against the bindings, falling
    /// back to the declared Parameter's DefaultValue when unbound.
    /// Throws UnresolvedParameterException if a Ref is unbound and undefaulted.
    /// </summary>
    public static decimal Resolve(
        NumberOrParam value,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters)
    {
        return value switch
        {
            NumberOrParam.Literal l => l.Value,
            NumberOrParam.Ref r => ResolveBinding(r.ParameterName, bindings, declaredParameters),
            _ => throw new ArgumentException($"Unknown NumberOrParam subtype: {value.GetType().Name}")
        };
    }

    /// <summary>
    /// Resolve with a fallback when the slot itself is null.
    /// </summary>
    public static decimal ResolveOr(
        NumberOrParam? value,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters,
        decimal @default)
    {
        return value is not null ? Resolve(value, bindings, declaredParameters) : @default;
    }

    // ----------------------------------------------------- FlagOrParam

    /// <summary>
    /// Resolve a FlagOrParam to a concrete bool.
    /// </summary>
    public static bool ResolveFlag(
        FlagOrParam value,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters)
    {
        return value switch
        {
            FlagOrParam.Literal l => l.Value,
            FlagOrParam.Ref r => ResolveFlagBinding(r.ParameterName, bindings, declaredParameters),
            _ => throw new ArgumentException($"Unknown FlagOrParam subtype: {value.GetType().Name}")
        };
    }

    /// <summary>
    /// Resolve with a fallback when the slot itself is null.
    /// </summary>
    public static bool ResolveFlagOr(
        FlagOrParam? value,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters,
        bool @default)
    {
        return value is not null ? ResolveFlag(value, bindings, declaredParameters) : @default;
    }

    // ---------------------------------------------------- ResolvedTask

    /// <summary>
    /// Produce a ResolvedTask from a TaskDefinition by resolving every
    /// NumberOrParam/FlagOrParam slot against the provided bindings.
    /// This is the main entry point — pipeline stages consume ResolvedTask.
    /// </summary>
    public static ResolvedTask ResolveTask(
        TaskDefinition task,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters)
    {
        return new ResolvedTask(
            Code: task.Code,
            Name: task.Name,
            Metrics: task.Metrics,
            Flights: task.Flights,
            Timing: ResolveTiming(task.Timing, bindings, declaredParameters),
            Group: task.Group is not null ? ResolveGroup(task.Group, bindings, declaredParameters) : null,
            Normalise: task.Normalise,
            ValidWhen: task.ValidWhen,
            FlightValidWhen: task.FlightValidWhen,
            RawScore: task.RawScore,
            Reflight: task.Reflight,
            Score: ResolveScoreTerms(task.Score, bindings, declaredParameters),
            ScoreNormalised: ResolveScoreTerms(task.ScoreNormalised, bindings, declaredParameters)
        );
    }

    // ------------------------------------------------- reference walking

    /// <summary>
    /// True if <paramref name="task"/> names <paramref name="parameterName"/> in
    /// any of the ParameterRef slots the class-diagram notation permits
    /// (ParameterReference.cs) that live inside a TaskDefinition — Timing,
    /// Group, a task-level Reflight override, and both ScoreTerm lists.
    /// kanban/completed/per-round-parameter-bindings-plan.md: this is what makes
    /// "does round N's task actually consume this parameter" checkable.
    /// </summary>
    public static bool TaskReferencesParameter(TaskDefinition task, string parameterName)
    {
        if (References(task.Timing.WorkingTime, parameterName)) return true;
        if (References(task.Timing.PreparationTime, parameterName)) return true;
        if (References(task.Timing.MaxLaunches, parameterName)) return true;
        if (task.Group is not null && References(task.Group.MinPerGroup, parameterName)) return true;
        if (task.Reflight?.MinNewGroupSize is not null && References(task.Reflight.MinNewGroupSize, parameterName)) return true;
        if (task.Score.Any(t => ScoreTermReferences(t, parameterName))) return true;
        if (task.ScoreNormalised.Any(t => ScoreTermReferences(t, parameterName))) return true;

        return false;
    }

    private static bool References(NumberOrParam? value, string parameterName) =>
        value is NumberOrParam.Ref r && r.ParameterName == parameterName;

    private static bool ScoreTermReferences(ScoreTerm term, string parameterName) =>
        term switch
        {
            RateTerm t => References(t.Cap, parameterName),

            PiecewiseTerm t => References(t.Origin, parameterName)
                || t.Bands.Any(b => References(b.From, parameterName) || References(b.To, parameterName)),

            ConditionalTerm t => ScoreTermReferences(t.Then, parameterName)
                || (t.Else is not null && ScoreTermReferences(t.Else, parameterName)),

            LookupTerm or ConstantTerm => false,

            _ => throw new ArgumentException($"Unknown ScoreTerm subtype: {term.GetType().Name}")
        };

    // --------------------------------------------------------- private

    private static decimal ResolveBinding(
        string parameterName,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters)
    {
        var value = ResolveBindingOrDefault(parameterName, bindings, declaredParameters);

        if (value.Kind != MeasuredKind.Number)
            throw new UnresolvedParameterException(parameterName,
                $"Parameter '{parameterName}' is bound as {value.Kind}, but Number was expected.");

        if (value.Number is null)
            throw new UnresolvedParameterException(parameterName,
                $"Parameter '{parameterName}' is bound but has no Number value.");

        return value.Number.Value;
    }

    private static bool ResolveFlagBinding(
        string parameterName,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters)
    {
        var value = ResolveBindingOrDefault(parameterName, bindings, declaredParameters);

        if (value.Kind != MeasuredKind.Flag)
            throw new UnresolvedParameterException(parameterName,
                $"Parameter '{parameterName}' is bound as {value.Kind}, but Flag was expected.");

        if (value.Flag is null)
            throw new UnresolvedParameterException(parameterName,
                $"Parameter '{parameterName}' is bound but has no Flag value.");

        return value.Flag.Value;
    }

    /// <summary>
    /// Three-step resolution order: a binding (last-write-wins, already
    /// flattened by the caller) wins; failing that, the declared Parameter's
    /// DefaultValue; failing that, throw.
    /// </summary>
    private static MeasuredValue ResolveBindingOrDefault(
        string parameterName,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters)
    {
        if (bindings.TryGetValue(parameterName, out var bound))
            return bound;

        var declared = declaredParameters.IsDefault
            ? null
            : declaredParameters.FirstOrDefault(p => p.Name == parameterName);

        if (declared?.DefaultValue is { } defaultValue)
            return defaultValue;

        throw new UnresolvedParameterException(parameterName);
    }

    private static ResolvedTiming ResolveTiming(
        TaskTiming timing,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters)
    {
        return new ResolvedTiming(
            Kind: timing.Kind,
            WorkingTime: timing.WorkingTime is not null
                ? Resolve(timing.WorkingTime, bindings, declaredParameters)
                : null,
            PreparationTime: timing.PreparationTime is not null
                ? Resolve(timing.PreparationTime, bindings, declaredParameters)
                : null,
            MaxLaunches: timing.MaxLaunches is not null
                ? (int)Resolve(timing.MaxLaunches, bindings, declaredParameters)
                : null
        );
    }

    private static ResolvedGroupConstraint ResolveGroup(
        GroupConstraint group,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters)
    {
        return new ResolvedGroupConstraint(
            MinPerGroup: Resolve(group.MinPerGroup, bindings, declaredParameters),
            MinValidResults: group.MinValidResults
        );
    }

    /// <summary>
    /// Walk the ScoreTerm tree and replace every NumberOrParam.Ref with a
    /// NumberOrParam.Literal. After this pass, pipeline stages can safely
    /// read the concrete values without needing the bindings dictionary.
    /// </summary>
    private static ImmutableArray<ScoreTerm> ResolveScoreTerms(
        ImmutableArray<ScoreTerm> terms,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters)
    {
        if (terms.IsDefaultOrEmpty)
            return [];

        var builder = ImmutableArray.CreateBuilder<ScoreTerm>(terms.Length);
        foreach (var term in terms)
            builder.Add(ResolveScoreTerm(term, bindings, declaredParameters));
        return builder.MoveToImmutable();
    }

    private static ScoreTerm ResolveScoreTerm(
        ScoreTerm term,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters)
    {
        return term switch
        {
            RateTerm t => t with
            {
                Cap = ResolveNumberOrParamNullable(t.Cap, bindings, declaredParameters)
            },

            LookupTerm t => t,  // no NumberOrParam slots

            PiecewiseTerm t => t with
            {
                Origin = ResolveNumberOrParamNullable(t.Origin, bindings, declaredParameters),
                Bands = t.Bands.Select(b => b with
                {
                    From = ResolveNumberOrParamNullable(b.From, bindings, declaredParameters),
                    To = ResolveNumberOrParamNullable(b.To, bindings, declaredParameters)
                }).ToImmutableArray()
            },

            ConstantTerm t => t,  // no NumberOrParam slots

            ConditionalTerm t => t with
            {
                Then = ResolveScoreTerm(t.Then, bindings, declaredParameters),
                Else = t.Else is not null ? ResolveScoreTerm(t.Else, bindings, declaredParameters) : null
            },

            _ => throw new ArgumentException($"Unknown ScoreTerm subtype: {term.GetType().Name}")
        };
    }

    /// <summary>
    /// If the value is a Ref, resolve it to a Literal. If Literal or null, pass through.
    /// </summary>
    private static NumberOrParam? ResolveNumberOrParamNullable(
        NumberOrParam? value,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        ImmutableArray<Parameter> declaredParameters)
    {
        if (value is null)
            return null;

        if (value is NumberOrParam.Literal)
            return value;

        if (value is NumberOrParam.Ref r)
            return (NumberOrParam)Resolve(r, bindings, declaredParameters);  // implicit conversion decimal → Literal

        return value;
    }
}
