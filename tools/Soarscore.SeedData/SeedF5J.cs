// F5J — RC Electric Powered Thermal Duration Gliders
// Rule refs: FAI Sporting Code Volume F5 Electric 2026 ed.2 (5.5.11.x)
//
// Transcribed from seed-data/30-f5j.class. The class that shows why a
// PhaseDefinition owns its tasks: the fly-off is the same task with a different
// working time and a different points cap.

using System.Collections.Immutable;
using Soarscore.Domain.CompetitionClasses;

namespace Soarscore.SeedData;

public static class SeedF5J
{
    // ---- metricSet f5jFlight -----------------------------------------------

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
    ];

    // The two scoring tables of 5.5.11.12, declared once and used by both phases
    // (notation §7.1). One rulebook clause, one table: the fly-off scores against
    // the same start-height bands and the same landing table as the preliminary,
    // and only the working time and the flight-points cap differ. Written out in
    // each phase they were a hand-maintained duplicate — the F22/F24 failure
    // shape, where a drifted row still adopts, still runs and still produces a
    // plausible number.

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
        Group = new() { MinPerGroup = 6 },                                     // 5.5.11.8
        Normalise = new()
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,                                                // 5.5.11.12 m states no rounding precision, so none is applied (F12)
        },
        // Both conditions zero THE FLIGHT, not one term (F17). Written as term
        // wrappers they had to be repeated on all three, and the start-height
        // deduction below is negative — so a long overfly scored 0 flight points,
        // 0 landing bonus and a NEGATIVE height deduction, where 5.5.11.12 g says
        // "a zero score will be recorded".
        FlightValidWhen = P.All(
            P.Le("overflySeconds", 60),                                        // 5.5.11.12 g "zero score … for overflying by more than one (1) minute"
            P.Is("startHeightRecorded", true)),                                // 5.5.11.7 e
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

    // Same task; two numbers change — the working time and the flight-points cap.
    // The metrics, `flights`, `group`, `normalise` and `flightValidWhen` all come
    // with `like`. The whole `score` block is restated only because one term in it
    // moved: a restated block replaces the parent's entire block. What the
    // restatement no longer re-transcribes is the two scoring tables — both are
    // declared once above, so the fly-off cannot drift from the preliminary.
    private static TaskDefinition FlyoffTaskD => TaskD with
    {
        Name = "Duration (fly-off)",
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 900,                                                 // 5.5.11.13 e "The Working Time for the fly-off rounds will be fifteen (15) minutes"
            PreparationTime = 300,                                             // 5.5.11.8.2 a
        },
        Score =
        [
            T.Rate("flightTime", 1, cap: 900),                                 // 5.5.11.12 c 900 points for the fly-off rounds
            T.Piecewise("startHeight", StartHeightBands),
            T.When(P.All(P.Eq("overflySeconds", 0),
                         P.Is("touchedByCompetitor", false)),
                   T.Lookup("landingDistance", LandingRows)),
        ],
    };

    // ---- the definition ----------------------------------------------------

    public static ClassDefinition Definition => new()
    {
        Name = "RC Electric Powered Thermal Duration Gliders",
        FaiDesignation = "F5J",
        Version = "FAI F5 Electric 2026 ed.2",
        FinalRanking = FinalRankingKind.SplitByPromotion,                      // 5.5.11.13 qualifiers ranked on the fly-off, the rest on qualifying

        Parameters =
        [
            Params.Number("flyoffMaxGroup", @default: 14),                     // 5.5.11.8 max 14; the CD may set a lower maximum

            // NOT an F12 residual, and it was written as one until the ADR-0002
            // §6 review. 5.5.11.13 d states the number: "A minimum of three (3)
            // or maximum of four (4) fly-off rounds should be flown. Exceptionally
            // the CD may reduce to two (2) in the case of bad weather or poor
            // visibility." So the default is 3 and the CD's exceptional reduction
            // is the reason this stays a parameter rather than becoming a literal
            // — but `allowed` now stops it being set to 1, which the rule forbids
            // and which a no-default parameter accepted.
            //
            // The "maximum of four (4)" half has no home: ValidityRule carries
            // MinRounds and MinTasks only, and RoundComposition.MaxRounds bounds
            // what may be SCHEDULED rather than what is valid. Recorded as a model
            // gap, not transcribed.
            Params.Number("flyoffMinRounds", @default: 3, allowed: [2, 3]),    // 5.5.11.13 d
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.Replacement,                     // 5.5.11.6
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
                Validity = new() { MinRounds = 4 },                             // 5.5.11.5 a "A minimum of four qualification rounds must be
                                                                                //   flown for the competition to be valid"
                Drops =
                [
                    new()
                    {
                        Dimension = DropDimension.ByRound,
                        DropCount = 1,
                        ApplyWhenRoundsCompletedAtLeast = 5,                    // 5.5.11.13 "if more than 4, the lowest round score is dropped"
                    },
                ],
                Tasks = [TaskD],
            },

            new()
            {
                Ordinal = 2,
                Type = PhaseType.Flyoff,                                        // 5.5.11.13
                Promotion = new()
                {
                    Kind = PromotionKind.TopPercent,
                    TopPercent = 30,                                            // 5.5.11.13 top 30% rounded down
                    MinGroupSize = 6,                                           // 5.5.11.8 single group 6-14
                    MaxGroupSize = NumberOrParam.Param("flyoffMaxGroup"),
                    CarryPenalties = false,                                     // 5.5.11.12 n — F5J is the one class that STATES it
                },
                Validity = new() { MinRounds = NumberOrParam.Param("flyoffMinRounds") },  // not stated for F5J (F12)
                // no drop: 5.5.11.13's discard applies to the qualifying aggregate
                Tasks = [FlyoffTaskD],
            },
        ],
    };

    private static PenaltyDefinition Deduct(string infraction, decimal points) => new()
    {
        InfractionType = infraction,
        Effects = [new(PenaltyEffect.DeductPoints, points)],
    };
}
