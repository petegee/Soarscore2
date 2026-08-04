// ParameterResolver — docs/plans/scoring-service-plan.md WI-2.
//
// Resolves NumberOrParam/FlagOrParam to concrete decimal/bool values, and
// produces ResolvedTask snapshots from TaskDefinitions. Pipeline stages
// consume ResolvedTask — they never need to resolve parameter refs themselves.

using System.Collections.Immutable;
using Soarscore.Domain.CompetitionClasses;

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
    /// Literal passes through; Ref looks up the binding.
    /// Throws UnresolvedParameterException if a Ref has no binding.
    /// </summary>
    public static decimal Resolve(
        NumberOrParam value,
        IReadOnlyDictionary<string, MeasuredValue> bindings)
    {
        return value switch
        {
            NumberOrParam.Literal l => l.Value,
            NumberOrParam.Ref r => ResolveBinding(r.ParameterName, bindings),
            _ => throw new ArgumentException($"Unknown NumberOrParam subtype: {value.GetType().Name}")
        };
    }

    /// <summary>
    /// Resolve with a fallback when the slot itself is null.
    /// </summary>
    public static decimal ResolveOr(
        NumberOrParam? value,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        decimal @default)
    {
        return value is not null ? Resolve(value, bindings) : @default;
    }

    // ----------------------------------------------------- FlagOrParam

    /// <summary>
    /// Resolve a FlagOrParam to a concrete bool.
    /// </summary>
    public static bool ResolveFlag(
        FlagOrParam value,
        IReadOnlyDictionary<string, MeasuredValue> bindings)
    {
        return value switch
        {
            FlagOrParam.Literal l => l.Value,
            FlagOrParam.Ref r => ResolveFlagBinding(r.ParameterName, bindings),
            _ => throw new ArgumentException($"Unknown FlagOrParam subtype: {value.GetType().Name}")
        };
    }

    /// <summary>
    /// Resolve with a fallback when the slot itself is null.
    /// </summary>
    public static bool ResolveFlagOr(
        FlagOrParam? value,
        IReadOnlyDictionary<string, MeasuredValue> bindings,
        bool @default)
    {
        return value is not null ? ResolveFlag(value, bindings) : @default;
    }

    // ---------------------------------------------------- ResolvedTask

    /// <summary>
    /// Produce a ResolvedTask from a TaskDefinition by resolving every
    /// NumberOrParam/FlagOrParam slot against the provided bindings.
    /// This is the main entry point — pipeline stages consume ResolvedTask.
    /// </summary>
    public static ResolvedTask ResolveTask(
        TaskDefinition task,
        IReadOnlyDictionary<string, MeasuredValue> bindings)
    {
        return new ResolvedTask(
            Code: task.Code,
            Name: task.Name,
            Metrics: task.Metrics,
            Flights: task.Flights,
            Timing: ResolveTiming(task.Timing, bindings),
            Group: task.Group is not null ? ResolveGroup(task.Group, bindings) : null,
            Normalise: task.Normalise,
            ValidWhen: task.ValidWhen,
            FlightValidWhen: task.FlightValidWhen,
            RawScore: task.RawScore,
            Reflight: task.Reflight,
            Score: ResolveScoreTerms(task.Score, bindings),
            ScoreNormalised: ResolveScoreTerms(task.ScoreNormalised, bindings)
        );
    }

    // --------------------------------------------------------- private

    private static decimal ResolveBinding(string parameterName, IReadOnlyDictionary<string, MeasuredValue> bindings)
    {
        if (!bindings.TryGetValue(parameterName, out var value))
            throw new UnresolvedParameterException(parameterName);

        if (value.Kind != MeasuredKind.Number)
            throw new UnresolvedParameterException(parameterName,
                $"Parameter '{parameterName}' is bound as {value.Kind}, but Number was expected.");

        if (value.Number is null)
            throw new UnresolvedParameterException(parameterName,
                $"Parameter '{parameterName}' is bound but has no Number value.");

        return value.Number.Value;
    }

    private static bool ResolveFlagBinding(string parameterName, IReadOnlyDictionary<string, MeasuredValue> bindings)
    {
        if (!bindings.TryGetValue(parameterName, out var value))
            throw new UnresolvedParameterException(parameterName);

        if (value.Kind != MeasuredKind.Flag)
            throw new UnresolvedParameterException(parameterName,
                $"Parameter '{parameterName}' is bound as {value.Kind}, but Flag was expected.");

        if (value.Flag is null)
            throw new UnresolvedParameterException(parameterName,
                $"Parameter '{parameterName}' is bound but has no Flag value.");

        return value.Flag.Value;
    }

    private static ResolvedTiming ResolveTiming(
        TaskTiming timing,
        IReadOnlyDictionary<string, MeasuredValue> bindings)
    {
        return new ResolvedTiming(
            Kind: timing.Kind,
            WorkingTime: timing.WorkingTime is not null
                ? Resolve(timing.WorkingTime, bindings)
                : null,
            PreparationTime: timing.PreparationTime is not null
                ? Resolve(timing.PreparationTime, bindings)
                : null,
            MaxLaunches: timing.MaxLaunches is not null
                ? (int)Resolve(timing.MaxLaunches, bindings)
                : null
        );
    }

    private static ResolvedGroupConstraint ResolveGroup(
        GroupConstraint group,
        IReadOnlyDictionary<string, MeasuredValue> bindings)
    {
        return new ResolvedGroupConstraint(
            MinPerGroup: Resolve(group.MinPerGroup, bindings),
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
        IReadOnlyDictionary<string, MeasuredValue> bindings)
    {
        if (terms.IsDefaultOrEmpty)
            return [];

        var builder = ImmutableArray.CreateBuilder<ScoreTerm>(terms.Length);
        foreach (var term in terms)
            builder.Add(ResolveScoreTerm(term, bindings));
        return builder.MoveToImmutable();
    }

    private static ScoreTerm ResolveScoreTerm(
        ScoreTerm term,
        IReadOnlyDictionary<string, MeasuredValue> bindings)
    {
        return term switch
        {
            RateTerm t => t with
            {
                Cap = ResolveNumberOrParamNullable(t.Cap, bindings)
            },

            LookupTerm t => t,  // no NumberOrParam slots

            PiecewiseTerm t => t with
            {
                Origin = ResolveNumberOrParamNullable(t.Origin, bindings),
                Bands = t.Bands.Select(b => b with
                {
                    From = ResolveNumberOrParamNullable(b.From, bindings),
                    To = ResolveNumberOrParamNullable(b.To, bindings)
                }).ToImmutableArray()
            },

            ConstantTerm t => t,  // no NumberOrParam slots

            ConditionalTerm t => t with
            {
                Then = ResolveScoreTerm(t.Then, bindings),
                Else = t.Else is not null ? ResolveScoreTerm(t.Else, bindings) : null
            },

            _ => throw new ArgumentException($"Unknown ScoreTerm subtype: {term.GetType().Name}")
        };
    }

    /// <summary>
    /// If the value is a Ref, resolve it to a Literal. If Literal or null, pass through.
    /// </summary>
    private static NumberOrParam? ResolveNumberOrParamNullable(
        NumberOrParam? value,
        IReadOnlyDictionary<string, MeasuredValue> bindings)
    {
        if (value is null)
            return null;

        if (value is NumberOrParam.Literal)
            return value;

        if (value is NumberOrParam.Ref r)
            return (NumberOrParam)Resolve(r, bindings);  // implicit conversion decimal → Literal

        return value;
    }
}
