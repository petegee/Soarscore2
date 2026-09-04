// F5K — RC Electric Thermal Duration, Multiple-Task (provisional class)
// Rule refs: FAI Sporting Code Volume F5 Electric 2026 ed.2 (5.5.10.x)
//
// The class that pushes hardest on the
// model: launch points measured relative to a parameter (F5), a per-launch cost
// that must read the flight's own sequence number (F6), and a task-total cap and
// a raw-score rounding with nowhere else to live (F4a, F4b). It also carries the
// corpus's deepest term nesting and its only load-bearing `else` clauses.

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedF5K
{
    // ---- metricSet f5kFlight -----------------------------------------------

    private static ImmutableArray<MetricDefinition> FlightMetrics =>
    [
        M.Number("flightTime", "s", RoundingMode.Truncate, 1),                 // 5.5.10.6 f whole seconds, tenths not rounded
        M.Number("launchAltitude", "m", RoundingMode.Truncate, 1),             // 5.5.10.4 "the highest altitude reached from launch until 10 seconds
                                                                               //   after the motor is stopped" (also 5.5.10.5 b)
        M.Flag("landedInPilotArea"),                                           // 5.5.10.6 h
        M.Flag("landedOnField"),                                               // 5.5.10.12 flight penalty b — landing off the field = 0 for that flight
        M.Flag("overflewLandingWindow"),                                       // 5.5.10.12 flight penalty a
    ];

    // Launch points relative to the announced NLH. 5.5.10.4 is one clause and one
    // table, and every task in the catalogue scores against it — so the bands are
    // declared once and used by A, B, C and E (notation §7.1). Written out in each
    // task they were four hand-maintained copies of a scoring table, which is the
    // F22/F24 failure shape. What varies across the catalogue is the GUARD, not
    // the bands: A, B and C carry the 5.5.10.4 guard alone, while E WRAPS that
    // same guard in 5.5.10.2's target-achieved condition (see Task E).
    //
    // NOTE for notation §7.1. That section argues the reusable unit is the band
    // list rather than the whole score term BECAUSE "F5K Task E scores the same
    // bands under a different guard … so a term-level unit reaches three of that
    // list's four uses". Correcting E to nest rather than conjoin means E now
    // reuses the whole LaunchAltitude term, so a term-level unit would have
    // reached all four and that argument no longer holds on its own facts. The
    // band list is still defensible on the other two grounds §7.1 gives — it is
    // the smaller surface, and it names something the model already treats as an
    // ordered whole — but §7.1's worked example needs revisiting. Flagged, not
    // acted on: it is a docs change.
    //
    // The first
    // band is the bonus — a NEGATIVE rate over a NEGATIVE portion of the
    // measurement, which is how each metre below the NLH adds points. Stated
    // as the engine rule: bands integrate with sign — the walk from the origin
    // (the NLH) to metric − origin accumulates rate × width and multiplies by
    // the direction of travel, so the backwards walk below the NLH flips this
    // band's negative rate into the 5.5.10.4 bonus.
    private static ImmutableArray<Band> LaunchBands =>                         // 5.5.10.4
        Bands.Below(0, -0.5m)
             .UpTo(10, -1.0m)
             .Rest(-3.0m);

    // 5.5.10.4 again — "there will be no bonus points for flights shorter than 30
    // seconds, penalty points still apply" — the same table with the bonus band
    // removed. It repeats two rows of the list above and there is no way to write
    // "those bands less the first"; inventing one would be notation surface no
    // second class needs, so the repetition is left visible.
    private static ImmutableArray<Band> LaunchPenaltyOnlyBands =>              // 5.5.10.4
        Bands.From(0)
             .UpTo(10, -1.0m)
             .Rest(-3.0m);

    /// <summary>The launch-altitude conditional shared by Tasks A, B and C, guard included.</summary>
    private static ConditionalTerm LaunchAltitude =>
        T.When(P.Ge("flightTime", 30),                                         // 5.5.10.4 no bonus for flights shorter than 30 s
               T.Piecewise("launchAltitude", LaunchBands, NumberOrParam.Param("nlh")),
               // Load-bearing `else`: under 30 s the height PENALTIES still apply
               // while the bonus does not (5.5.10.4). It must never be dropped.
               T.Piecewise("launchAltitude", LaunchPenaltyOnlyBands, NumberOrParam.Param("nlh")));

    private static ConditionalTerm PilotAreaDeduction =>
        T.When(P.Is("landedInPilotArea", false), T.Constant(-10));             // 5.5.10.6 h "Landing outside the Pilot Area but within the flying
                                                                               //   field results in a 10 points penalty PER LANDING"

    private static ConditionalTerm OverflyDeduction =>
        T.When(P.Is("overflewLandingWindow", true), T.Constant(-100));         // 5.5.10.12 flight penalty a

    // ---- A, the task every other one derives from --------------------------
    // Four targets in any order; every flight counts whether or not its target is
    // reached, so the target is a clamp (as F3K Task K), not a Poker condition.

    private static TaskDefinition TaskA => new()
    {
        Code = "A",
        Name = "1, 2, 3, 4 minute flights in any order",                       // 5.5.10.2
        Metrics = FlightMetrics,
        Flights = new BestNFlights
        {
            Count = 4,
            Targets = TargetAssignment.AnyOrder,
            TargetValues = [60, 120, 180, 240],                                // 5.5.10.2
        },
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 600,
            PreparationTime = 300,                                             // 5.5.10 preparation time >= 5 min per round
            MaxLaunches = 4,                                                   // 5.5.10.2
        },
        Group = new() { MinPerGroup = NumberOrParam.Param("minPerGroup") },    // F5K states no group minimum (F12)
        Normalise = new()
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,
            Round = new(RoundingMode.HalfUp, 1),                               // 5.5.10.15 rounded to whole points
        },
        RawScore = new(RoundingMode.Truncate, 1),                              // 5.5.10.15 raw truncated down to whole points (F4b)
        // 5.5.10.12 flight penalty b zeroes THE FLIGHT, not the flight-time term
        // (F17). Written as a term wrapper it guarded only the first term, so a
        // model landing off the field still collected its launch-height adjustment
        // and its −10/−100 deductions. Here it zeroes every term at once and still
        // leaves the flight selected — "zero points for that flight only", so Task
        // B's last flight stays last. Inherited by B, C, D and E through `like`.
        FlightValidWhen = P.Is("landedOnField", true),                          // 5.5.10.12 flight penalty b
        Score =
        [
            // 5.5.10.15 1 pt/s; the assigned target caps each flight, and
            // 5.5.10.2's "maximum total flight time used for scoring: 9.59 min"
            // caps the SUM (F4a). Not a cap on the raw score: the launch bonus is
            // added after it.
            T.Rate("flightTime", 1, cap: 599, capScope: CapScope.PerTask),

            // Cumulative bands read from an origin (F5): at NLH+15 the deduction
            // is 10x1.0 + 5x3.0 = 25.
            LaunchAltitude,
            PilotAreaDeduction,
            OverflyDeduction,
        ],
    };

    // ---- B -----------------------------------------------------------------
    // Only the last flight counts — so the launch cost must be read off that
    // flight's own sequence number, which is the whole of finding F6.
    // B restates `score`, which replaces A's block entirely, so A's
    // `cap 599 perTask` does not reach B and 5.5.10.2 states no 9:59 total for it.
    // Everything B does not restate, `rawScore` included, comes from A.

    private static TaskDefinition TaskB => TaskA with
    {
        Code = "B",
        Name = "Last flight, 5 out of 7 minutes",                              // 5.5.10.2
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
            T.Rate("flightTime", 1, cap: 300),                                 // 5.5.10.2 maximum flight time 5 minutes
            LaunchAltitude,

            // 5.5.10.2 Task B launch penalties: CUMULATIVE on the last flight,
            // −20 total over three launches. Character-identical to Task E's rows
            // and deliberately not one shared list — see the note there.
            T.Lookup(Intrinsic.FlightSequence,                                 // intrinsic ref (F6)
                Rows.UpTo(1, 0).Then(2, -10).Rest(-20)),

            PilotAreaDeduction,                                                // 5.5.10.6 h
            OverflyDeduction,                                                  // 5.5.10.12 flight penalty a
        ],
    };

    // ---- C -----------------------------------------------------------------

    private static TaskDefinition TaskC => TaskA with
    {
        Code = "C",
        Name = "All up, 4 minutes maximum (3x)",                               // 5.5.10.2
        Flights = new AllFlights(),
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 241,                                                 // 5.5.10.2 working time 4:01 PER launch (F13)
            // 5.5.10.14 is unconditional — "For each round, the competitors
            // receive AT LEAST 5 minutes of preparation time" — and applies to
            // Task C like every other. The 15 seconds this field used to hold is
            // 5.5.10.2's gap BETWEEN the three all-up flights inside the task
            // ("the preparation time for the next All-up flight is 15 seconds"),
            // which is a different quantity with no field of its own. Recording it
            // here contradicted a stated rule; it is a model gap, not a value.
            PreparationTime = 300,                                             // 5.5.10.14
            MaxLaunches = 3,
        },
        Score =
        [
            T.Rate("flightTime", 1, cap: 240),                                 // 5.5.10.2 maximum measured flight time 4 minutes
            LaunchAltitude,
            PilotAreaDeduction,
            OverflyDeduction,
        ],
    };

    // ---- D -----------------------------------------------------------------
    // D restates no `score` and no `rawScore`, so both come whole from A —
    // including the `cap 599 perTask` on the flight-time term, which D needs for
    // the same reason A does: 180+180+240 = 600 s (5.5.10.2). A's two band
    // references come with it: `like` copies the task as written, and because a
    // fragment is class-scoped and `like` never leaves the class, expanding before
    // or after the copy gives the same bands (notation §7.1).

    private static TaskDefinition TaskD => TaskA with
    {
        Code = "D",
        Name = "3, 3, 4 minute flights in any order",                          // 5.5.10.2
        Flights = new BestNFlights
        {
            Count = 3,
            Targets = TargetAssignment.AnyOrder,
            TargetValues = [180, 180, 240],                                    // 5.5.10.2
        },
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 600,
            PreparationTime = 300,
            MaxLaunches = 3,
        },
    };

    // ---- E -----------------------------------------------------------------
    // Poker. Unlike F3K's, the launch cost applies to every launch made, so the
    // selection must be `all` — an unachieved flight scores no flight points and
    // no height adjustment, but still carries its launch penalty.

    private static TaskDefinition TaskE => TaskA with
    {
        Code = "E",
        Name = "Poker",                                                        // 5.5.10.2
        Metrics = [.. FlightMetrics,
                   M.Number("targetTime", "s", RoundingMode.Truncate, 1, declared: true)],  // 5.5.10.2 announced to, and recorded by, the timekeeper
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
            // 5.5.10.2 marked "Y" — the pilot is credited with the target time;
            // landedOnField now gates the whole flight, not this term.
            T.When(P.Ge("flightTime", "targetTime"),
                   T.Rate("targetTime", 1, cap: 599)),                         // "any time over the target time is not counted";
                                                                               // 5.5.10.2 "the target and maximum allowable flight time is
                                                                               //   9 minutes and 59 seconds" — per flight, not per task

            // Two clauses, NESTED rather than conjoined, and getting that wrong was
            // an under-deduction in the pilot's favour:
            //
            //   5.5.10.2 (Task E)  "The launch altitude bonus or penalty only
            //     applies only where the target time is achieved … The launch bonus
            //     OR PENALTY does not apply where the target time is not achieved."
            //     -> target missed kills BOTH. This is the outer guard.
            //   5.5.10.4           "There will be no bonus points for flights
            //     shorter than 30 seconds, PENALTY POINTS STILL APPLY."
            //     -> under 30 s kills only the bonus. This is the inner one, and it
            //     is exactly the shared LaunchAltitude conditional A, B and C use.
            //
            // ANDing the two into one guard with no `else` made the 30 s rule
            // inherit the target rule's consequence, so a pilot who nominated a
            // sub-30 s target, achieved it, and launched high escaped the height
            // penalty entirely — at a 20 s target launched at NLH+40 that is
            // 10x1.0 + 30x3.0 = 100 points not deducted. Nothing in 5.5.10.2 puts
            // a floor under a self-nominated target.
            T.When(P.Ge("flightTime", "targetTime"), LaunchAltitude),

            // These three rows are character-identical to Task B's and are
            // deliberately NOT declared as one shared list. They are two different
            // statements of 5.5.10.2 that happen to agree: B selects only the last
            // flight, so its rows are the CUMULATIVE cost at that flight's sequence
            // number and come to −20 over three launches; E selects every flight,
            // so its rows are the per-launch INCREMENT and come to −30. The rule
            // states the two totals separately, and naming them one table would
            // assert an agreement the rulebook does not make.
            T.Lookup(Intrinsic.FlightSequence,                                 // intrinsic ref (F6)
                Rows.UpTo(1, 0).Then(2, -10).Rest(-20)),                       // 5.5.10.2 Task E: 2nd launch −10, 3rd a further −20,
                                                                               //   −30 total over three launches
            PilotAreaDeduction,
            OverflyDeduction,
        ],
    };

    private static ImmutableArray<TaskDefinition> Catalogue => [TaskA, TaskB, TaskC, TaskD, TaskE];

    // ---- the definition ----------------------------------------------------

    public static ClassDefinition Definition => new()
    {
        Name = "RC Electric Thermal Duration, Multiple-Task",
        FaiDesignation = "F5K",
        Version = "FAI F5 Electric 2026 ed.2",
        FinalRanking = FinalRankingKind.LastPhaseReplaces,                     // 5.5.10.18 "If a fly-off is flown, the points of the previous
                                                                               //   rounds are not considered for the final score"

        Parameters =
        [
            // The Nominal Launch Height: 60 m in light wind, 70 m in moderate
            // wind, announced by the CD one day before from the mean wind
            // 11:00-17:00. The textbook Parameter — a legitimate local choice,
            // bound from measured conditions before flying.
            // `allowed` because 5.5.10.3's table is exhaustive over the permitted
            // wind range and admits exactly two values. The NLH is the origin of
            // every launch band in the class, so an out-of-range binding silently
            // re-prices every flight in the contest.
            Params.Number("nlh", "m", 60, allowed: [60, 70],
                          boundAt: ParameterBindingPoint.BeforeFlying),        // 5.5.10.3
            Params.Number("flyoffSize"),                                       // 5.5.10 — size not fixed by the rules (F12)
            Params.Number("minPerGroup"),                                      // F5K states no group minimum (F12)
            Params.Flag("carryPenalties"),                                     // F5K states nothing (F12)
            Params.Number("minRounds"),                                        // 5.5.10 states no minimum-rounds rule (F12)
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.Replacement,                     // 5.5.10.13
            OthersScore = ReflightSelection.BetterOf,                           // 5.5.10.13
            MinNewGroupSize = 4,                                                // 5.5.10.13
        },

        Penalties =
        [
            new()
            {
                InfractionType = "motorRestartInFlight",
                Effects = [new(PenaltyEffect.ZeroFlight)],                      // 5.5.10.12 flight penalty c
            },
            new()
            {
                InfractionType = "hitPersonOtherThanTimer",
                Effects = [new(PenaltyEffect.ZeroRound)],                       // 5.5.10.12 safety penalty a
            },
            new()
            {
                InfractionType = "safetyZone",
                Effects = [new(PenaltyEffect.DeductPoints, 300)],               // 5.5.10.12 safety penalty b, c "deducted from the final score"
            },
        ],

        Phases =
        [
            new()
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Rounds = new()
                {
                    Kind = CompositionKind.ChooseFromCatalogue,
                    TasksPerRound = 1,                                          // 5.5.10.2 one task per round from A-E
                },
                Validity = new() { MinRounds = NumberOrParam.Param("minRounds") },  // 5.5.10 defines no minimum-rounds rule, so the CD decides (F12)
                Drops =
                [
                    new()
                    {
                        Dimension = DropDimension.ByRound,
                        DropCount = 1,
                        ApplyWhenRoundsCompletedAtLeast = 7,                    // 5.5.10.16 "if 7 or more rounds are flown"
                    },
                ],
                // Tie-breaks: 5.5.10.17 — best dropped score first, then a separate
                // tie-break fly-off. D10 scope reading: 5.5.10.17's tie clause is
                // preliminary-scoped; the fly-off phase states nothing and falls
                // back to the display ladder (where PreDropScore ≡ Score since the
                // fly-off declares no drops, so ties share).
                TieBreaks = [new BestDroppedScore(), new TieBreakFlyoff()],      // 5.5.10.17
                                                                                //   encoding: kanban/completed/tie-break-policy-in-class-definition.md
                Tasks = Catalogue,
            },

            // Whether this phase is flown at all is not class data. 5.5.10 makes
            // the fly-off mandatory for seniors at World and Continental
            // Championships and leaves it to the organiser everywhere else, so
            // mandatoriness is conditional on the event level — something the model
            // has no notion of. A flag on the phase could only ever have said
            // "optional" here, and would have been wrong at a championship.
            new()
            {
                Ordinal = 2,
                Type = PhaseType.Flyoff,                                        // 5.5.10
                Promotion = new()
                {
                    Kind = PromotionKind.TopN,
                    TopN = NumberOrParam.Param("flyoffSize"),
                    MinGroupSize = NumberOrParam.Param("minPerGroup"),
                    MaxGroupSize = null,                                        // `..unlimited`
                    CarryPenalties = FlagOrParam.Param("carryPenalties"),       // 5.5.10 — F5K is SILENT on carry-over (F12)
                },
                Rounds = new()
                {
                    Kind = CompositionKind.ChooseFromCatalogue,
                    TasksPerRound = 1,
                },
                Validity = new() { MinRounds = 3 },                             // 5.5.10 "if fewer than 3 complete, preliminary results stand"
                // no drop: 5.5.10 states no fly-off discard
                // no tie-breaks: 5.5.10.17's tie clause is preliminary-scoped (D10);
                //   the fly-off states nothing and falls back to the display ladder
                //   (PreDropScore ≡ Score here — no drops — so ties share)
                Tasks = Catalogue,
            },
        ],
    };
}
