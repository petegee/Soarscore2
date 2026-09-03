// NZ F5J — RC Electric Powered Thermal Duration Gliders, NDC format
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, March 2024 (NZ.0.3);
//            FAI Sporting Code Volume F5 Electric 2026 ed.2 (5.5.11.x), carried
//            wholesale by NZ.0.3 c "Contest rules as per FAI Section 4 –
//            Aeromodelling Volume F5 Radio Control Electric Powered Model Aircraft".
//
// NZ.0.3 varies the scoring frame, not the tasks: the task, its metrics, the two
// scoring tables of 5.5.11.12 (NZ.0.3 f restates both numbers), the re-flight rule
// (5.5.11.6) and the penalty schedule all carry per NZ.0.3 c. What changes is the
// pipeline — NZ.0.3 b fixes the contest at 4 rounds, NZ.0.3 d disregards
// 5.5.11.12.m (normalisation) and scores "the sum of the Raw Scores from the four
// rounds", and the FAI fly-off (5.5.11.13) is varied away: it would nullify the
// stated raw-sum total, so there is no second phase, no promotion and no drop.
// NZ.0.3 e demands the 10:00 working time be "strictly and accurately enforced" —
// an operational duty on the contest, not a different number, so the task's 600 s
// stands. NZ.0.3 h "No points if landing more than 75m from the landing spot"
// restates FAI 5.5.11.7 d; SeedF5J does not encode it, so this definition makes it
// explicit.
//
// That is a different pipeline, not a different number, so it is a different
// CompetitionClass — same reasoning as SeedNzMNdc / SeedNzF3kNdc.

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedF5jNdc
{
    // ---- metricSet f5jFlight -----------------------------------------------
    // SeedF5J's flight metrics verbatim, plus landedWithin75m (NZ.0.3 h).

    private static ImmutableArray<MetricDefinition> FlightMetrics =>
    [
        M.Number("flightTime", "s", RoundingMode.Truncate, 1),                 // 5.5.11.12 b truncated to the nearest second
        M.Number("startHeight", "m", RoundingMode.Truncate, 1),                // 5.5.11.12 d truncated to the nearest metre
        M.Flag("startHeightRecorded"),                                         // 5.5.11.7 e "the AMRT does not record any Start Height data"
                                                                               //   (5.5.11.12 d is the truncation rule, not the zeroing one)
        M.Number("landingDistance", "m", RoundingMode.Truncate, 0.1m),         // 5.5.11.12 i — the rules state no capture precision, and a
                                                                               //   MetricDefinition precision is not coverable by a Parameter
                                                                               //   (F12 residual). Chosen, not cited.
        M.Number("overflySeconds", "s", RoundingMode.Truncate, 1),             // 5.5.11.12 g, k — seconds flown past the end of working time
        M.Flag("touchedByCompetitor"),                                         // 5.5.11.12 j
        M.Flag("landedWithin75m"),                                             // NZ.0.3 h "No points if landing more than 75m from the
                                                                               //   landing spot"; FAI 5.5.11.7 d — the FAI seed omits it
                                                                               //   (0.3 h restates 5.5.11.7 d for the NDC, so it is
                                                                               //   encoded here)
    ];

    // The two scoring tables of 5.5.11.12, carried per NZ.0.3 c and restated by
    // NZ.0.3 f: start height "as per 5.5.11.12.e", landing "max of 50 as per
    // 5.5.11.12.h" (notation §7.1). Declared once, used by the one task.

    private static ImmutableArray<Band> StartHeightBands =>                    // 5.5.11.12 e
        Bands.From(0)
             .UpTo(200, -0.5m)
             .Rest(-3);

    private static ImmutableArray<LookupRow> LandingRows =>                    // 5.5.11.12 h
        Rows.UpTo(1, 50).Then(2, 45).Then(3, 40).Then(4, 35)
            .Then(5, 30).Then(6, 25).Then(7, 20).Then(8, 15)
            .Then(9, 10).Then(10, 5)
            .Rest(0);

    // ---- the preliminary task ----------------------------------------------

    private static TaskDefinition TaskD => new()
    {
        Code = "D",
        Name = "Duration",
        Metrics = FlightMetrics,
        Flights = new LastFlight(),
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 600,                                                 // 5.5.11.8.2 b working time 10 minutes
            PreparationTime = 300,                                             // 5.5.11.8.2 a "competitors are entitled to five (5) minutes preparation time"
        },
        Group = new() { MinPerGroup = 6 },                                     // 5.5.11.8 carries per NZ.0.3 c and governs the DRAW only —
                                                                               //   its normalisation sentences (5.5.11.12 l/m) are
                                                                               //   superseded by NZ.0.3 d's raw-sum total
        // NO normalise (F25): NZ.0.3 d "Disregard 5.5.11.12.m and score the sum
        //   of the Raw Scores from the four rounds" — there is no normalisation
        //   scale anywhere in this class.
        // All three conditions zero THE FLIGHT, not one term (F17). Written as
        // term wrappers they had to be repeated on all three score terms, and the
        // start-height deduction below is negative — so a long overfly scored 0
        // flight points, 0 landing bonus and a NEGATIVE height deduction, where
        // 5.5.11.12 g says "a zero score will be recorded".
        FlightValidWhen = P.All(
            P.Le("overflySeconds", 60),                                        // 5.5.11.12 g "zero score … for overflying by more than one (1) minute"
            P.Is("startHeightRecorded", true),                                 // 5.5.11.7 e
            P.Is("landedWithin75m", true)),                                    // NZ.0.3 h; FAI 5.5.11.7 d
        Score =
        [
            T.Rate("flightTime", 1, cap: 600),                                 // 5.5.11.12 c 1 pt per full second, max 600 points

            // Start-height deduction. Cumulative bands: 0.5/m for the first 200 m
            // and 3/m thereafter, so 220 m deducts 100 + 60 = 160, not 660.
            T.Piecewise("startHeight", StartHeightBands),

            // Landing bonus — the coarser 50->0 table, forfeited two ways.
            T.When(P.All(P.Eq("overflySeconds", 0),                            // 5.5.11.12 k
                         P.Is("touchedByCompetitor", false)),                  // 5.5.11.12 j
                   T.Lookup("landingDistance", LandingRows)),
        ],
    };

    // ---- the definition ----------------------------------------------------

    public static ClassDefinition Definition => new()
    {
        Name = "RC Electric Powered Thermal Duration Gliders (NDC format)",
        FaiDesignation = "F5J",                                                // NZ.0.3 c: this rulebook class IS FAI F5J
        Version = "NZMAA Section 5 Soaring, March 2024",
        // no finalRanking: NZ.0.3 b + d fix the contest at four rounds scored as
        //   their raw sum; the FAI fly-off (5.5.11.13) would nullify that and is
        //   varied away — one phase, so SinglePhase

        // No parameters: the fly-off parameters (flyoffMaxGroup, flyoffMinRounds)
        //   do not carry — there is no fly-off — and the group minimum is the
        //   literal 6 (5.5.11.8), not a binding.
        Reflight = new()
        {
            EntitledScores = ReflightSelection.Replacement,                     // 5.5.11.6 carries per NZ.0.3 c
            OthersScore = ReflightSelection.BetterOf,                           // 5.5.11.6
            MinNewGroupSize = 6,                                                // 5.5.11.6 — 6, not the 4 used by F3J/F3K/F5K
        },

        // 5.5.11.4 a: all safety penalties are deducted from the final score and
        // listed on the round's score sheet; 5.5.11.12 n makes them cumulative and
        // deducted at the end of the preliminary rounds. Both are `deduct`, and a
        // deduction lands at the final aggregate by definition — F5J is where that
        // is most explicitly stated. The counter-cases are 5.5.11.10 d and
        // 5.5.11.7 g, which annul an ATTEMPT and so can only act at the raw score,
        // where a flight is still a distinguishable thing to zero.
        Penalties =
        [
            Deduct("wrongLaunchDirection", 100),                                // 5.5.11.10 b
            Deduct("motorBeforeStartSignal", 100),                              // 5.5.11.10 c
            Deduct("launchNotStraightAhead3s", 100),                            // 5.5.11.10 e
            Deduct("wrongLandingDirection", 100),                               // 5.5.11.11 b "A penalty of 100 points will be applied for any
                                                                                //   breach of this rule" (final-approach direction set by the CD)
            new()
            {
                InfractionType = "launchOutsideCorridor",
                // ZeroFlight, not ZeroRound: 5.5.11.10 d says "An ATTEMPT is
                // annulled and recorded as zero", and an attempt is a flight. In
                // F5J the two coincide numerically — one task, one scoring flight
                // per round — but they are not the same statement, and the
                // difference would bite the moment a round held two tasks.
                Effects = [new(PenaltyEffect.ZeroFlight)],                      // 5.5.11.10 d — a zero score, NOT a 100-point deduction
            },
            new()
            {
                InfractionType = "propellerTurningAfterRun",
                Effects = [new(PenaltyEffect.ZeroFlight)],                      // 5.5.11.7 g
            },
            Deduct("safetyAreaInfringement", 300),                              // 5.5.11.4 c
            Deduct("restsInAccessCorridor", 300),                               // 5.5.11.4 d
            Deduct("contactInAccessCorridor", 1000),                            // 5.5.11.4 e
        ],

        Phases =
        [
            new()
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Rounds = new()
                {
                    Kind = CompositionKind.FixedSequence,
                    TasksPerRound = 1,
                    MaxRounds = 4,                                             // NZ.0.3 b "A NDC contest will comprise 4 rounds"
                },
                Validity = new() { MinRounds = 4 },                            // NZ.0.3 b — the four rounds; no fewer makes the stated sum
                // no drop: NZ.0.3 d "the sum of the Raw Scores from the four
                //   rounds" — a dropped round would contradict it
                // no tie-breaks: 5.5.11.13 h covers fly-off placing only and
                //   there is no fly-off here; the NZ rules state none anywhere
                //   (docs/rules/nz/00-nz-general-rules.md:117) — silence
                TieBreaks = [new UndefinedRequiresRuling()],                   // docs/rules/nz/00-nz-general-rules.md:117
                                                                                //   encoding: kanban/completed/tie-break-policy-in-class-definition.md
                Tasks = [TaskD],
            },
        ],
    };

    private static PenaltyDefinition Deduct(string infraction, decimal points) => new()
    {
        InfractionType = infraction,
        Effects = [new(PenaltyEffect.DeductPoints, points)],
    };

    // ---- arithmetic check --------------------------------------------------
    // Per-round max: 600 flight (5.5.11.12 c) + 50 landing (5.5.11.12 h) - 0
    //   start-height deduction at a 0 m launch (5.5.11.12 e) = 650. NZ.0.3 d
    //   scores the raw sum of the four rounds: 4 x 650 = 2600.
    // Realistically a perfect round is not launched at 0 m: a 200 m launch costs
    //   100 (0.5/m to 200 m), so a perfect 200 m-launch round scores 550.
    // Any evaluator that normalises (per 5.5.11.12 m) or drops a round does not
    //   produce these.
}
