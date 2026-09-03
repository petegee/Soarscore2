// NZ F3K — RC Hand-Launch Gliders, NDC format
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, March 2024 (NZ.0.2 / NZ.0.2.1);
//            FAI Sporting Code Volume F3 Soaring 2025 ed.2 (F3K.x), which S5 §3.8.1
//            nominates as *the* F3K rulebook — NZ.0.2 is its only NZ variation.
//
// NZ.0.2 varies the scoring frame, not the tasks: catalogue B/D/G/H only, "Total is
// sum of raw scores" (NZ.0.2.1 a) — no per-group normalisation, therefore no
// drop-worst — and timing recorded to 0.1 s truncated ("59.99 seconds is recorded at
// 59.9 seconds"), already the F3K metric precision. Everything else is FAI F3K,
// carried wholesale per the ruling of 2026-08-30: re-flights (F3K.9.6), group
// minima (F3K.9.1, draw side only) and the F3K penalty schedule.
//
// F3K.10's "minimum of five rounds each with different tasks" cannot carry — the
// NDC catalogue has only 4 tasks, so the FAI round structure is unachievable and is
// varied away by NZ.0.2's catalogue restriction. The round count is fixed at 4, one
// per task, by ruling (the Class M NDC / F5J NDC shape).

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedNzF3kNdc
{
    // ---- metricSet f3kFlight -----------------------------------------------
    // Same as SeedF3K, with the NZ.0.2.1 a timing citation alongside F3K.7 —
    // same Truncate / 0.1 s precision.

    private static ImmutableArray<MetricDefinition> FlightMetrics =>
    [
        M.Number("flightTime", "s", RoundingMode.Truncate, 0.1m),  // F3K.7; NZ.0.2.1 a recorded to 0.1 s, truncated
        M.Flag("landedWithinWindow"),                              // F3K.9.3 the 30 s landing window
        M.Flag("launchedInWorkingTime"),                           // F3K.7 launched before the working time = zero score
    ];

    // ---- tasks: B, D, G, H only (NZ.0.2.1 a) -------------------------------

    private static TaskDefinition TaskB => new()
    {
        Code = "B",
        Name = "Next to last and last flight",                                 // F3K.11.2
        Metrics = FlightMetrics,
        Flights = new LastNFlights(2),
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = NumberOrParam.Param("workingTime.B") },
        Group = new() { MinPerGroup = 5 },                                     // F3K.9.1 carries (ruling); governs the draw —
                                                                               //   its normalisation sentence is superseded by
                                                                               //   NZ.0.2.1 a's raw-sum total
        // NO normalise (F25): NZ.0.2.1 a "Total is sum of raw scores" — there is no
        //   normalisation scale anywhere in this class.
        FlightValidWhen = P.All(
            P.Is("landedWithinWindow", true),                                  // F3K.9.3
            P.Is("launchedInWorkingTime", true)),                              // F3K.7
        Score = [T.Rate("flightTime", 1, cap: NumberOrParam.Param("maxFlight.B"))],  // F3K.11.2; NZ.0.2.1 b worked example 55 + 85 = 140
    };

    // SeedF3K's D inherits A's score term through `like`; this definition
    // restates it explicitly since it has no `like` parent to inherit from.
    private static TaskDefinition TaskD => new()
    {
        Code = "D",
        Name = "Two flights",                                                  // F3K.11.4
        Metrics = FlightMetrics,
        Flights = new AllFlights(),
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 600, MaxLaunches = 2 },
        Group = new() { MinPerGroup = 5 },                                     // F3K.9.1 carries (ruling); see TaskB
        // NO normalise (F25) — see TaskB
        FlightValidWhen = P.All(
            P.Is("landedWithinWindow", true),                                  // F3K.9.3
            P.Is("launchedInWorkingTime", true)),                              // F3K.7
        Score = [T.Rate("flightTime", 1, cap: 300)],                           // F3K.11.4 "300 s each, both flights summed"
    };

    private static TaskDefinition TaskG => new()
    {
        Code = "G",
        Name = "Five longest flights",                                         // F3K.11.7
        Metrics = FlightMetrics,
        Flights = new BestNFlights { Count = 5 },
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Group = new() { MinPerGroup = 5 },                                     // F3K.9.1 carries (ruling); see TaskB
        // NO normalise (F25) — see TaskB
        FlightValidWhen = P.All(
            P.Is("landedWithinWindow", true),                                  // F3K.9.3
            P.Is("launchedInWorkingTime", true)),                              // F3K.7
        Score = [T.Rate("flightTime", 1, cap: 120)],                           // F3K.11.7
    };

    // No cap on the term: the assigned target IS the cap. `rankBy flightTime`
    // (F16) is load-bearing — F3K.11.8 assigns targets to the four longest
    // FLIGHTS, and no flight has a score until a target has been assigned, so
    // the default ranking (by score) is circular here. Worked example NZ.0.2.1 e:
    // 569.
    private static TaskDefinition TaskH => new()
    {
        Code = "H",
        Name = "1, 2, 3 and 4 minute targets, any order",                      // F3K.11.8
        Metrics = FlightMetrics,
        Flights = new BestNFlights
        {
            Count = 4,
            RankByMetric = "flightTime",
            Targets = TargetAssignment.AnyOrder,
            TargetValues = [60, 120, 180, 240],
        },
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Group = new() { MinPerGroup = 5 },                                     // F3K.9.1 carries (ruling); see TaskB
        // NO normalise (F25) — see TaskB
        FlightValidWhen = P.All(
            P.Is("landedWithinWindow", true),                                  // F3K.9.3
            P.Is("launchedInWorkingTime", true)),                              // F3K.7
        Score = [T.Rate("flightTime", 1)],                                     // F3K.11.8
    };

    // ---- the definition ----------------------------------------------------

    public static ClassDefinition Definition => new()
    {
        Name = "RC Hand-Launch Gliders (NDC format)",
        FaiDesignation = "F3K",                                                // S5 §3.8.1: this rulebook class IS FAI F3K
        Version = "NZMAA Section 5 Soaring, March 2024",
        // no finalRanking: one phase, so SinglePhase (NZ.0.2 has no fly-off)

        // NZ.0.2.1 b restates both the 10- and 7-minute variants; nothing else in
        // B/D/G/H is a parameter (D/G/H working times are literal 600).
        Parameters =
        [
            Params.Number("workingTime.B", "s", 600, [420, 600], ParameterBindingPoint.PerRound),  // F3K.11.2; NZ.0.2.1 b
            Params.Number("maxFlight.B", "s", 240, [180, 240], ParameterBindingPoint.PerRound),    // F3K.11.2; NZ.0.2.1 b
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.Replacement,                    // F3K.9.6 carries (ruling 2)
            OthersScore = ReflightSelection.BetterOf,                          // F3K.9.6
            MinNewGroupSize = 4,                                               // F3K.9.6
        },

        Penalties =
        [
            new()
            {
                InfractionType = "unsignedScoreCard",
                Effects = [new(PenaltyEffect.ZeroRound)],                       // F3K.1.2 "the score for the round will be 0"
            },
            new()
            {
                InfractionType = "flewOutsideTestingWindow",
                Effects = [new(PenaltyEffect.DeductPoints, 100)],               // F3K.9.5
            },

            // F3K.4.3 "each flight attempt may only incur a single penalty … only
            // the highest penalty will be applied" — one exclusion group, largest
            // wins (F20). All four effects are deductions, which is what the group
            // requires (check 16).
            Excluded("safetyAreaObjectContact", 100),                           // F3K.4.3 1) an object, including the ground, inside the safety area
            Excluded("safetyAreaPersonContact", 300),                           // F3K.4.3 2) airborne contact with a person inside the safety area
            Excluded("personContactOutsideSafetyArea", 100),                    // F3K.4.3 3) airborne contact with a person anywhere outside it
            Excluded("landedInSafetyArea", 100),                                // F3K.4.3

            // F3K.4.1: the deduction half is F3K.4.3's schedule (see SeedF3K for
            // the full analysis); this definition adds the round zero on top. A
            // launch contact is recorded as TWO infractions — that one for the
            // deduction, this one for the zero round.
            new()
            {
                InfractionType = "personContactAtLaunch",
                Effects = [new(PenaltyEffect.ZeroRound)],                       // F3K.4.1 "in addition … a zero score for the whole round"
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
                    TasksPerRound = 1,
                    RequireDistinctTaskPerRound = true,                        // ruling: 4 rounds, one per task (B, D, G, H each once)
                    MaxRounds = 4,                                             // ruling; NZ.0.2 is silent, NZ.0.3/NZ.3.12.7 state 4
                },
                Validity = new() { MinRounds = 4 },                            // ruling
                // no drop: NZ.0.2.1 a "Total is sum of raw scores" — a dropped
                //   round would contradict it
                // no tie-breaks: the NZ rules state none anywhere
                //   (docs/rules/nz/00-nz-general-rules.md:117) — silence
                TieBreaks = [new UndefinedRequiresRuling()],                   // docs/rules/nz/00-nz-general-rules.md:117
                                                                               //   encoding: kanban/completed/tie-break-policy-in-class-definition.md
                Tasks = [TaskB, TaskD, TaskG, TaskH],                          // NZ.0.2.1 a catalogue B/D/G/H only
            },
        ],
    };

    private static PenaltyDefinition Excluded(string infraction, decimal points) => new()
    {
        InfractionType = infraction,
        ExclusionGroups = ["safetyInfraction"],
        Effects = [new(PenaltyEffect.DeductPoints, points)],
    };

    // ---- arithmetic check --------------------------------------------------
    // NZ.0.2 states no maxima to check against (unlike NZ.3.12.7 c), so the
    // identity to verify is the raw-sum one: the contest total is exactly the
    // sum of the per-round raw scores, no normalisation rescale and no discard.
    // The per-round maxima are the FAI task numbers quoted verbatim:
    //   B: 2 x maxFlight.B (240)          = 480
    //   D: 2 x 300                        = 600
    //   G: 5 x 120                        = 600
    //   H: 60 + 120 + 180 + 240           = 600
    // Four perfect rounds -> 480 + 600 + 600 + 600 = 2280, one per task, each
    // raw. Any evaluator that does not produce 2280 has either normalised
    // something or applied a discard.
}
