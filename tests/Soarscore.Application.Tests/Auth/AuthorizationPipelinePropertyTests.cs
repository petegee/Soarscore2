// authentication-and-authorisation.md WI-10 — the two named property
// invariants, over the real AuthorizationPipeline and the real
// CommandPolicyTable:
//
//   **Enforcement totality.** For every message type in CommandPolicyTable
//   and every generated principal state (anonymous, authenticated-unlinked,
//   competitor, organiser, allow-listed, system), AuthorizeAsync is total —
//   it never throws and never returns an outcome that is neither Allow (no
//   code, no message) nor Deny (both present) — and an anonymous principal is
//   denied for every message type. The anonymous arm is the
//   auditability-critical half: no unauthenticated caller can ever reach a
//   handler while the pipeline is registered.
//
//   **Capture-policy temporal blindness (NFR-4).** The capture decision for a
//   (principal, competition) pair is invariant under any insertion, removal
//   or reordering of score-capture events in the competition's other streams:
//   the policy reads configuration and the principal, never what has been
//   captured. Operationalised as: with the configured policy and the target
//   entry's existence pinned, arbitrary captured-data states (other entry
//   rows in the index, any field values on the target row) leave the outcome
//   identical. No capture command gains a precondition on round state.
//
// Message instances are built by reflection (Dummy.For) so a newly mapped
// command is swept into the property automatically — the same totality spirit
// as the architecture test that pins the table against the routes. A message
// type the builder cannot construct fails loudly rather than being skipped.

using AwesomeAssertions;
using CsCheck;
using Soarscore.Application.Auth;
using Soarscore.Application.Commands.Entries;
using Soarscore.Application.Queries.Competitions;
using Soarscore.Application.Queries.Entries;
using Soarscore.Application.Tests.Shared.Competitions;
using Soarscore.Application.Tests.Shared.Entries;
using Soarscore.Domain;
using Soarscore.Domain.Competitions;
using Soarscore.Domain.Entries;
using Soarscore.Domain.People;
using Soarscore.Domain.PublishedClassDefinition;
using Xunit;
using FakeServiceProvider = Soarscore.Application.Tests.Shared.Competitions.FakeServiceProvider;

namespace Soarscore.Application.Tests.Auth;

public class AuthorizationPipelinePropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly CompetitionId Competition = CompetitionId.New();
    private static readonly PersonId ListOwner = PersonId.New();

    // The generated principal states, in the story's order. Allow-listed seeds
    // the competition's policy with the principal's own id, so the state
    // exercises the allow path, not just the label.
    private const int Anonymous = 0;
    private const int Unlinked = 1;
    private const int Competitor = 2;
    private const int Organiser = 3;
    private const int AllowListed = 4;
    private const int SystemActor = 5;

    private static readonly string[] StateNames =
    [
        "anonymous", "authenticated-unlinked", "competitor", "organiser", "allow-listed", "system",
    ];

    [Fact]
    public void The_pipeline_is_total_and_denies_anonymous_across_the_whole_table()
    {
        var messages = CommandPolicyTable.Table.Keys
            .Select(type => (Type: type, Message: Dummy.For(type)))
            .ToList();

        Gen.Int[Anonymous, SystemActor].Sample(state =>
        {
            var user = PrincipalFor(state);
            var competitions = new FakeCompetitionsQuery();
            var entries = new FakeEntryQuery();
            SeedPortsFor(state, messages, competitions, entries);
            var pipeline = PipelineFor(user, competitions, entries);

            foreach (var (type, message) in messages)
            {
                AuthzOutcome outcome;
                try
                {
                    outcome = pipeline.AuthorizeAsync(message, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"{type.Name} × {StateNames[state]}: AuthorizeAsync threw — totality broken. {ex}");
                }

                if (outcome.Allowed)
                {
                    outcome.Code.Should().BeNull($"{type.Name} × {StateNames[state]}: Allow carries no code");
                    outcome.Message.Should().BeNull($"{type.Name} × {StateNames[state]}: Allow carries no message");
                }
                else
                {
                    outcome.Code.Should().NotBeNullOrEmpty($"{type.Name} × {StateNames[state]}: Deny carries a code");
                    outcome.Message.Should().NotBeNullOrEmpty($"{type.Name} × {StateNames[state]}: Deny carries a message");
                }

                if (state == Anonymous)
                {
                    outcome.Allowed.Should().BeFalse(
                        $"{type.Name}: an anonymous principal must be denied — auditability-critical arm");
                }
            }
        });
    }

    [Fact]
    public void The_capture_decision_is_invariant_under_arbitrary_captured_data_states()
    {
        var knownEntry = EntryId.New();
        var capture = new CaptureMeasurement(knownEntry, FlightSequence: 1, Metric: "flightTime", MeasuredValue.Of(99m));

        // One principal that must be allowed (the list names them) and one
        // that must be denied (a competitor the list does not name): both
        // decisions are pinned, so the invariance cannot pass vacuously.
        var allowed = new FakeCurrentUser(IsAuthenticated: true, PersonId: ListOwner, Provider: "mock", Subject: "allow");
        var denied = new FakeCurrentUser(IsAuthenticated: true, PersonId: PersonId.New(), HeldRoles: [PersonRole.Competitor]);

        var capturedDataState =
            from noiseCount in Gen.Int[0, 20]
            from phase in Gen.Int[0, 3]
            from round in Gen.Int[1, 5]
            from taskRound in Gen.Int[1, 5]
            select (Noise: Enumerable.Range(0, noiseCount).Select(_ => EntryId.New()).ToArray(),
                Phase: phase, Round: round, TaskRound: taskRound);

        capturedDataState.Sample(state =>
        {
            var target = new EntrySummary(
                knownEntry, Competition, state.Phase, state.Round, state.TaskRound,
                GroupId.New(), CompetitorId.New(), ReflightRole.Original);

            var noiseRows = state.Noise
                .Select(id => new EntrySummary(
                    id, Competition, PhaseOrdinal: 0, RoundOrdinal: 1, TaskRoundOrdinal: 1,
                    GroupId.New(), CompetitorId.New(), ReflightRole.Original))
                .ToArray();

            // The three operations the invariant names: insertion (baseline
            // plus the whole generated set), removal (the noise stripped back
            // out) and reordering (the noise reversed).
            var baseline = new[] { target };
            var inserted = baseline.Concat(noiseRows).ToArray();
            var removed = baseline;
            var reordered = noiseRows.Reverse().Prepend(target).ToArray();

            var baselineOutcome = Decide(capture, allowed, baseline);
            var deniedBaselineOutcome = Decide(capture, denied, baseline);

            // The pair is pinned: the allowed principal is allowed and the
            // denied one denied — on an empty-other-streams index.
            baselineOutcome.Allowed.Should().BeTrue("the allow-list names this principal");
            deniedBaselineOutcome.Allowed.Should().BeFalse("the list does not name this principal");

            foreach (var variant in new[] { inserted, removed, reordered })
            {
                Decide(capture, allowed, variant).Should().Be(baselineOutcome,
                    "inserting, removing or reordering other streams' capture data must not move the decision");
                Decide(capture, denied, variant).Should().Be(deniedBaselineOutcome,
                    "inserting, removing or reordering other streams' capture data must not move the decision");
            }
        });
    }

    private static AuthzOutcome Decide(CaptureMeasurement capture, ICurrentUser user, IReadOnlyList<EntrySummary> rows)
    {
        var competitions = new FakeCompetitionsQuery();
        competitions.SeedCapturePolicy(Competition, new CapturePolicy(CapturePolicyMode.AllowList, [ListOwner]));
        var entries = new FakeEntryQuery();
        foreach (var row in rows)
        {
            entries.Seed(row);
        }

        return PipelineFor(user, competitions, entries)
            .AuthorizeAsync(capture, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
    }

    private static ICurrentUser PrincipalFor(int state) => state switch
    {
        Anonymous => new FakeCurrentUser(),
        Unlinked => new FakeCurrentUser(IsAuthenticated: true, Provider: "auth0", Subject: "unlinked"),
        Competitor => new FakeCurrentUser(IsAuthenticated: true, PersonId: PersonId.New(), HeldRoles: [PersonRole.Competitor]),
        Organiser => new FakeCurrentUser(IsAuthenticated: true, PersonId: PersonId.New(), HeldRoles: [PersonRole.Organiser]),
        AllowListed => new FakeCurrentUser(IsAuthenticated: true, PersonId: ListOwner, HeldRoles: [PersonRole.Competitor]),
        _ => new SystemCurrentUser(),
    };

    /// <summary>
    /// The real pipeline with the two read-model ports fixed to fakes — the
    /// only services the policies resolve.
    /// </summary>
    private static AuthorizationPipeline PipelineFor(ICurrentUser user, ICompetitionsQuery competitions, IEntryQuery entries) =>
        new(user, new FakeServiceProvider(new Dictionary<Type, object>
        {
            [typeof(ICompetitionsQuery)] = competitions,
            [typeof(IEntryQuery)] = entries,
        }));

    /// <summary>
    /// Seeds the read-model fakes from each message's own scope markers, so
    /// the C-mapped messages reach a real policy and the allow-listed state
    /// really is on every allow-list the property consults.
    /// </summary>
    private static void SeedPortsFor(
        int state,
        IReadOnlyList<(Type Type, object Message)> messages,
        FakeCompetitionsQuery competitions,
        FakeEntryQuery entries)
    {
        // The policy each state should meet: meaningful for Unlinked (denied
        // at step 4), Competitor (denied at step 6) and AllowListed (allowed
        // at step 6); the organiser and system principals pass before any
        // read; anonymous is denied before any read.
        CapturePolicy PolicyFor(CompetitionId _) => state switch
        {
            Unlinked => new CapturePolicy(CapturePolicyMode.AnyRegisteredPerson, []),
            AllowListed => new CapturePolicy(CapturePolicyMode.AllowList, [ListOwner]),
            Competitor => new CapturePolicy(CapturePolicyMode.AllowList, [ListOwner]),
            _ => new CapturePolicy(CapturePolicyMode.OrganisersOnly, []),
        };

        foreach (var (type, message) in messages)
        {
            _ = type;
            if (message is ICompetitionScopedCommand competitionScoped)
            {
                competitions.SeedCapturePolicy(competitionScoped.CompetitionRef, PolicyFor(competitionScoped.CompetitionRef));
            }

            if (message is IEntryScopedCommand entryScoped)
            {
                entries.Seed(new EntrySummary(
                    entryScoped.EntryRef, Competition, PhaseOrdinal: 0, RoundOrdinal: 1, TaskRoundOrdinal: 1,
                    GroupId.New(), CompetitorId.New(), ReflightRole.Original));
                competitions.SeedCapturePolicy(Competition, PolicyFor(Competition));
            }
        }
    }
}

/// <summary>
/// Reflection constructor for the property's message instances: every public
/// constructor is satisfiable from primitives, the strongly-typed ids' static
/// factories (New/Of/Create/From), enums, and empty lists. Cycles are broken
/// per build.
/// </summary>
internal static class Dummy
{
    private const int MaxDepth = 8;

    private static readonly DateTimeOffset Now = new(2026, 9, 16, 9, 0, 0, TimeSpan.Zero);

    public static object For(Type type) => Build(type, new Dictionary<Type, object>(), 0);

    private static object Build(Type type, Dictionary<Type, object> building, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new Xunit.Sdk.XunitException($"Dummy build for {type.Name} exceeded depth {MaxDepth}.");
        }

        if (building.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return Build(underlying, building, depth);
        }

        if (type == typeof(string))
        {
            return "wi-10";
        }

        if (type == typeof(bool))
        {
            return false;
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
        {
            return Convert.ChangeType(1, type);
        }

        if (type == typeof(decimal))
        {
            return 1m;
        }

        if (type == typeof(double))
        {
            return 1d;
        }

        if (type == typeof(float))
        {
            return 1f;
        }

        if (type == typeof(DateTimeOffset))
        {
            return Now;
        }

        if (type == typeof(Guid))
        {
            return Guid.NewGuid();
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0)!;
        }

        foreach (var factory in type
                     .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                     .Where(m => m is { Name: "New" or "Of" or "Create" or "From" } && !m.ContainsGenericParameters)
                     .OrderBy(m => m.GetParameters().Length))
        {
            if (factory.GetParameters().Length == 0 && factory.ReturnType == type)
            {
                return factory.Invoke(null, [])!;
            }

            var parameters = factory.GetParameters();
            if (parameters.Length == 1
                && factory.ReturnType == type
                && (parameters[0].ParameterType == typeof(decimal)
                    || parameters[0].ParameterType == typeof(int)
                    || parameters[0].ParameterType == typeof(string)))
            {
                return factory.Invoke(null, [Build(parameters[0].ParameterType, building, depth + 1)])!;
            }
        }

        // Immutable collections (System.Collections.Immutable exposes Empty
        // as a static field on ImmutableArray<T>, a static property on the
        // others — and no public constructor anywhere).
        if (type.IsGenericType && type.Name is "ImmutableArray`1" or "ImmutableHashSet`1" or "ImmutableDictionary`2")
        {
            const System.Reflection.BindingFlags staticPublic = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
            return (object?)(type.GetField("Empty", staticPublic)?.GetValue(null) ?? type.GetProperty("Empty", staticPublic)?.GetValue(null))
                ?? throw new Xunit.Sdk.XunitException($"Cannot build a dummy {type.Name}: no Empty member.");
        }

        if (type.IsInterface)
        {
            if (type.IsGenericType
                && type.GetGenericTypeDefinition() is var generic
                && (generic == typeof(IReadOnlyList<>)
                    || generic == typeof(IReadOnlyCollection<>)
                    || generic == typeof(IEnumerable<>)))
            {
                return Array.CreateInstance(type.GetGenericArguments()[0], 0);
            }

            throw new Xunit.Sdk.XunitException($"Cannot build a dummy {type.Name}: no static factory and no concrete type.");
        }

        // Abstract records (e.g. TieBreakDirective) are closed sets of sealed
        // derived records — build a public one, deterministically by name.
        if (type.IsAbstract)
        {
            var derived = type.Assembly.GetTypes()
                .Where(t => t.IsVisible && t.IsSealed && !t.IsAbstract && type.IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new Xunit.Sdk.XunitException($"Cannot build a dummy {type.Name}: no public sealed derived type.");

            return Build(derived, building, depth + 1);
        }

        var constructor = type.GetConstructors()
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new Xunit.Sdk.XunitException($"Cannot build a dummy {type.Name}: no public constructor.");

        var arguments = constructor.GetParameters()
            .Select(p => Build(p.ParameterType, building, depth + 1))
            .ToArray();

        var instance = constructor.Invoke(arguments);
        building[type] = instance;
        return instance;
    }
}
