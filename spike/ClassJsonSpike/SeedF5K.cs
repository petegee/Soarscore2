// F5K — RC Electric Thermal Duration, Multiple-Task
// Rule refs: FAI Sporting Code Volume F5 Electric 2026 ed.2 (5.5.10.x)
//
// Transcribed from seed-data/40-f5k.class. The `#` citations come across as
// comments on the same construct, per ADR-0002 §6.
//
// Written EXPANDED: `like` and `use` are notation sugar and do not reach the
// model, so the fly-off's five tasks are full copies and each task carries its
// own metric list and its own band list. Where the notation says `like A` the
// C# says `A with { … }` — record copy semantics are §7.1's granularity rule
// exactly, including the edge that a restated block's omitted keyword takes the
// default rather than the parent's value.

using System.Collections.Immutable;

namespace Soarscore.Spike.ClassModel;

public static class SeedF5K
{
    // ---- shared metrics: `metricSet f5kFlight` -----------------------------
    private static ImmutableArray<MetricDefinition> FlightMetrics =>
    [
        new() { Name = "flightTime", Kind = MeasuredKind.Number, Unit = "s",
                Precision = new(RoundingMode.Truncate, 1) },            // 5.5.10.6 f whole seconds, tenths not rounded
        new() { Name = "launchAltitude", Kind = MeasuredKind.Number, Unit = "m",
                Precision = new(RoundingMode.Truncate, 1) },            // 5.5.10.3 highest altitude to 10 s after motor stop
        new() { Name = "landedInPilotArea", Kind = MeasuredKind.Flag },  // 5.5.10.15
        new() { Name = "landedOnField", Kind = MeasuredKind.Flag },      // 5.5.10.12 flight penalty b
        new() { Name = "overflewLandingWindow", Kind = MeasuredKind.Flag },  // 5.5.10.12 flight penalty a
    ];

    // ---- shared band lists: `bands f5kLaunch` / `f5kLaunchPenaltyOnly` -----
    // 5.5.10.4. The first band is the bonus: a negative rate over a negative
    // portion of the measurement, which is how each metre below the announced
    // NLH adds points.
    private static ImmutableArray<Band> Launch =>
        BandList.Below(0, -0.5m).UpTo(10, -1.0m).Rest(-3.0m);

    // 5.5.10.4 again — "no bonus points for flights shorter than 30 seconds,
    // penalty points still apply": the same table with the bonus band removed.
    private static ImmutableArray<Band> LaunchPenaltyOnly =>
        BandList.From(0).UpTo(10, -1.0m).Rest(-3.0m);

    // The launch-altitude conditional, identical in A, B and C — guard, bands,
    // origin and `else` alike. Written out at each site in the notation because
    // the guard is what varies across the catalogue (Task E's differs); a
    // private helper here is not a model element and does not reach the JSON.
    private static ConditionalTerm LaunchAdjustment() =>
        T.When(P.Ge("flightTime", 30),                                   // 5.5.10.4 no bonus below 30 s
               T.Piecewise("launchAltitude", Launch, NumberOrParam.Param("nlh")),
               T.Piecewise("launchAltitude", LaunchPenaltyOnly, NumberOrParam.Param("nlh")));

    private static ConditionalTerm PilotAreaDeduction() =>
        T.When(P.Is("landedInPilotArea", false), T.Constant(-10));       // 5.5.10.15 −10 per landing outside the Pilot Area

    private static ConditionalTerm OverflyDeduction() =>
        T.When(P.Is("overflewLandingWindow", true), T.Constant(-100));   // 5.5.10.12 flight penalty a

    // ---- tasks -------------------------------------------------------------

    private static TaskDefinition TaskA => new()
    {
        Code = "A",
        Name = "1, 2, 3, 4 minute flights in any order",                  // 5.5.10.2
        Metrics = FlightMetrics,
        // Every flight counts whether or not its target is reached, so the
        // target is a clamp (as F3K K) and not a Poker condition. No rankBy.
        Flights = new BestNFlights
        {
            Count = 4,
            Targets = TargetAssignment.AnyOrder,
            TargetValues = [60, 120, 180, 240],                          // 5.5.10.2
        },
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 600,
            PreparationTime = 300,                                       // 5.5.10 preparation time >= 5 min per round
            MaxLaunches = 4,                                             // 5.5.10.2
        },
        Group = new() { MinPerGroup = NumberOrParam.Param("minPerGroup") },  // F5K states no group minimum (F12)
        Normalise = new()
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,
            Round = new(RoundingMode.HalfUp, 1),                         // 5.5.10.15 rounded to whole points
        },
        RawScore = new(RoundingMode.Truncate, 1),                        // 5.5.10.15 raw truncated down to whole points (F4b)
        FlightValidWhen = P.Is("landedOnField", true),                   // 5.5.10.12 flight penalty b — zeroes the flight, not the term (F17)
        Score =
        [
            // 5.5.10.15 1 pt/s; the assigned target caps each flight, and
            // 5.5.10.2's "maximum total flight time used for scoring: 9.59 min"
            // caps the SUM (F4a). Not a cap on the raw score — the launch bonus
            // below is added after it.
            T.Rate("flightTime", 1, cap: 599, capScope: CapScope.PerTask),
            LaunchAdjustment(),
            PilotAreaDeduction(),
            OverflyDeduction(),
        ],
    };

    // `like A`, then: flights, timing and score restated. B's restated `score`
    // replaces A's block entirely, so A's `cap 599 perTask` does not reach it —
    // 5.5.10.2 states no 9:59 total for B. `rawScore`, `group`, `normalise`,
    // `metrics` and `flightValidWhen` are not restated and come whole from A.
    private static TaskDefinition TaskB => TaskA with
    {
        Code = "B",
        Name = "Last flight, 5 out of 7 minutes",                         // 5.5.10.2
        Flights = new LastFlight(),
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 420,
            PreparationTime = 300,
            MaxLaunches = 3,
        },
        Score =
        [
            T.Rate("flightTime", 1, cap: 300),                            // 5.5.10.2 maximum flight time 5 minutes
            LaunchAdjustment(),
            // Only the last flight counts, so the launch cost is read off that
            // flight's own sequence number — the whole of finding F6. These
            // rows are the CUMULATIVE cost at that sequence number: −20 over
            // three launches. Character-identical to Task E's below and
            // deliberately not shared — see the note there.
            T.Lookup("flight.sequence",
                     RowList.UpTo(1, 0).Then(2, -10).Rest(-20)),          // 5.5.10.2 Task B launch penalties
            PilotAreaDeduction(),
            OverflyDeduction(),
        ],
    };

    private static TaskDefinition TaskC => TaskA with
    {
        Code = "C",
        Name = "All up, 4 minutes maximum (3x)",                          // 5.5.10.2
        Flights = new AllFlights(),
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 241,                                            // 5.5.10.2 working time 4:01 PER launch (finding F13)
            PreparationTime = 15,
            MaxLaunches = 3,
        },
        Score =
        [
            T.Rate("flightTime", 1, cap: 240),                            // 5.5.10.2 maximum measured flight time 4 minutes
            LaunchAdjustment(),
            PilotAreaDeduction(),
            OverflyDeduction(),
        ],
    };

    // D restates no `score` and no `rawScore`, so both come whole from A —
    // including `cap 599 perTask`, which D needs for the same reason A does:
    // 180 + 180 + 240 = 600 s. 5.5.10.2.
    private static TaskDefinition TaskD => TaskA with
    {
        Code = "D",
        Name = "3, 3, 4 minute flights in any order",                     // 5.5.10.2
        Flights = new BestNFlights
        {
            Count = 3,
            Targets = TargetAssignment.AnyOrder,
            TargetValues = [180, 180, 240],                               // 5.5.10.2
        },
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 600,
            PreparationTime = 300,
            MaxLaunches = 3,
        },
    };

    // Poker. Unlike F3K's, the launch cost applies to every launch made, so the
    // selection must be `all`: an unachieved flight scores no flight points and
    // no height adjustment, but still carries its launch penalty.
    private static TaskDefinition TaskE => TaskA with
    {
        Code = "E",
        Name = "Poker",                                                   // 5.5.10.2
        // A derived task's own `metric` declarations do not touch what `use`
        // brought in (§7.1), so the expanded list is f5kFlight plus this one.
        Metrics =
        [
            .. FlightMetrics,
            new MetricDefinition
            {
                Name = "targetTime", Kind = MeasuredKind.Number, Unit = "s",
                Precision = new(RoundingMode.Truncate, 1),
                DeclaredBeforeLaunch = true,                              // 5.5.10.2 announced to, and recorded by, the timekeeper
            },
        ],
        Flights = new AllFlights(),
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 600,
            PreparationTime = 300,
            MaxLaunches = 3,
        },
        Score =
        [
            // 5.5.10.2 marked "Y" — the pilot is credited with the target time.
            T.When(P.Ge("flightTime", "targetTime"),
                   T.Rate("targetTime", 1, cap: 599)),                    // "any time over the target time is not counted"
            // 5.5.10.2: "the launch altitude bonus or penalty only applies
            // where the target time is achieved" — a second guard F5K's other
            // tasks lack, and there is no `else` at all.
            T.When(P.All(P.Ge("flightTime", "targetTime"), P.Ge("flightTime", 30)),
                   T.Piecewise("launchAltitude", Launch, NumberOrParam.Param("nlh"))),
            // These three rows are character-identical to Task B's and are
            // deliberately NOT one shared list: B's are the cumulative cost at
            // the last flight's sequence number (−20 over three launches),
            // E's are the per-launch increment (−30 over three). 5.5.10.2
            // states the two totals separately.
            T.Lookup("flight.sequence",
                     RowList.UpTo(1, 0).Then(2, -10).Rest(-20)),          // 5.5.10.2 Task E: 2nd launch −10, 3rd a further −20
            PilotAreaDeduction(),
            OverflyDeduction(),
        ],
    };

    // ---- the definition ----------------------------------------------------

    public static ClassDefinition Definition => new()
    {
        Name = "RC Electric Thermal Duration, Multiple-Task",
        FaiDesignation = "F5K",
        Version = "FAI F5 Electric 2026 ed.2",
        FinalRanking = FinalRankingKind.LastPhaseReplaces,                // 5.5.10.16 "fly-off replaces preliminary points"

        Parameters =
        [
            // The Nominal Launch Height: 60 m in light wind, 70 m in moderate,
            // announced by the CD one day before from the mean wind 11:00–17:00.
            new() { Name = "nlh", DefaultValue = MeasuredValue.Of(60),
                    BoundAt = ParameterBindingPoint.BeforeFlying },        // 5.5.10.3
            new() { Name = "flyoffSize" },                                 // 5.5.10 — size not fixed by the rules (F12)
            new() { Name = "minPerGroup" },                                // F5K states no group minimum (F12)
            new() { Name = "carryPenalties", Kind = MeasuredKind.Flag },   // F5K states nothing (F12)
            new() { Name = "minRounds" },                                  // 5.5.10 states no minimum-rounds rule (F12)
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.Replacement,                // 5.5.10.13
            OthersScore = ReflightSelection.BetterOf,                      // 5.5.10.13
            MinNewGroupSize = 4,                                           // 5.5.10.13
        },

        Penalties =
        [
            new() { InfractionType = "motorRestartInFlight",
                    Effects = [new(PenaltyEffect.ZeroFlight)] },           // 5.5.10.12 flight penalty c
            new() { InfractionType = "hitPersonOtherThanTimer",
                    Effects = [new(PenaltyEffect.ZeroRound)] },            // 5.5.10.12 safety penalty a
            new() { InfractionType = "safetyZone",
                    Effects = [new(PenaltyEffect.DeductPoints, 300)] },    // 5.5.10.12 safety penalty b, c "deducted from the final score"
        ],

        Phases =
        [
            new()
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Rounds = new() { Kind = CompositionKind.ChooseFromCatalogue, TasksPerRound = 1 },  // 5.5.10.2
                Validity = new() { MinRounds = NumberOrParam.Param("minRounds") },  // 5.5.10 defines no minimum-rounds rule (F12)
                Drops =
                [
                    new() { Dimension = DropDimension.ByRound, DropCount = 1,
                            ApplyWhenRoundsCompletedAtLeast = 7 },         // 5.5.10.16 "if 7 or more rounds are flown"
                ],
                Tasks = [TaskA, TaskB, TaskC, TaskD, TaskE],
            },
            // Whether this phase is flown at all is not class data: 5.5.10 makes
            // the fly-off mandatory for seniors at World and Continental
            // Championships and leaves it to the organiser everywhere else, so
            // mandatoriness is conditional on the event level, which the model
            // has no notion of.
            new()
            {
                Ordinal = 2,
                Type = PhaseType.Flyoff,                                   // 5.5.10
                Promotion = new()
                {
                    Kind = PromotionKind.TopN,
                    TopN = NumberOrParam.Param("flyoffSize"),
                    MinGroupSize = NumberOrParam.Param("minPerGroup"),
                    MaxGroupSize = null,                                   // the notation's `..unlimited`
                    CarryPenalties = FlagOrParam.Param("carryPenalties"),  // 5.5.10 — F5K is SILENT on carry-over (F12)
                },
                Rounds = new() { Kind = CompositionKind.ChooseFromCatalogue, TasksPerRound = 1 },
                Validity = new() { MinRounds = 3 },                        // 5.5.10 "if fewer than 3 complete, preliminary results stand"
                // no drop: 5.5.10 states no fly-off discard
                Tasks = [TaskA, TaskB, TaskC, TaskD, TaskE],
            },
        ],
    };
}
