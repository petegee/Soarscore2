// F3J — RC Thermal Duration Gliders
// Rule refs: FAI Sporting Code Volume F3 Soaring 2025 ed.2 (F3J.x)
//
// Penalty application point: F3J contradicts itself. Each penalty clause says
// "deduction from the competitor's final score" (F3J.2.4 d, F3J.7 d, F3J.8.3),
// while F3J.10.10's group-winner formula puts penalties inside the group total.
// Ruled: the specific clauses govern, and F3J.10.10's "minus penalty points"
// means the DERIVED −30 overfly deduction (F3J.10.3), which is a score term.

using System.Collections.Immutable;
using Soarscore.Domain.CompetitionClasses;

namespace Soarscore.SeedData;

public static class SeedF3J
{
    // ---- metricSet f3jFlight -----------------------------------------------

    private static ImmutableArray<MetricDefinition> FlightMetrics =>
    [
        M.Number("flightTime", "s", RoundingMode.HalfUp, 0.1m),                // F3J.10.2 "recorded to one decimal place" — the MODE is not
                                                                               //   stated. F3K states truncation explicitly, which suggests
                                                                               //   F3J is not truncated. Chosen, not cited (F12 residual).
        M.Number("landingDistance", "m", RoundingMode.Truncate, 0.1m),         // F3J.10.6 — no capture precision stated (F12 residual)
        M.Number("overflySeconds", "s", RoundingMode.Truncate, 1),             // F3J.10.3, 10.4 — seconds flown past the end of working time
        M.Flag("touchedByCompetitor"),                                         // F3J.10.8
        M.Flag("restedWithin75m"),                                             // F3J.5.1 e
    ];

    // The landing table of F3J.10.5, declared once and used by both phases
    // (notation §7.1). One rulebook clause, one table: the fly-off scores against
    // exactly the preliminary's twenty-four rows and differs only in the working
    // time and the flight-points cap. Written out twice it was a hand-maintained
    // duplicate — the F22/F24 failure shape, where one drifted row still adopts,
    // still runs and still produces a plausible number.
    private static ImmutableArray<LookupRow> LandingRows =>                    // F3J.10.5
        Rows.UpTo(0.2m, 100).Then(0.4m, 99).Then(0.6m, 98).Then(0.8m, 97)
            .Then(1.0m, 96).Then(1.2m, 95).Then(1.4m, 94).Then(1.6m, 93)
            .Then(1.8m, 92).Then(2.0m, 91).Then(3.0m, 90).Then(4.0m, 85)
            .Then(5, 80).Then(6, 75).Then(7, 70).Then(8, 65)
            .Then(9, 60).Then(10, 55).Then(11, 50).Then(12, 45)
            .Then(13, 40).Then(14, 35).Then(15, 30)
            .Rest(0);

    // ---- the preliminary task ----------------------------------------------

    private static TaskDefinition TaskD => new()
    {
        Code = "D",
        Name = "Duration",
        Metrics = FlightMetrics,
        Flights = new LastFlight(),
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },    // F3J.6.2 b "exactly ten (10) minutes duration"
        Group = new() { MinPerGroup = 6 },                                     // F3J.6.1 minimum 6, preferably 8-10
        Normalise = new()
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,
            Round = new(RoundingMode.Truncate, 0.1m),                          // F3J.10.11 "recorded (truncated) to one place after the decimal point"
        },
        // F3J.10.4 zeroes THE FLIGHT, not the flight-points term (F17). As a term
        // wrapper it left the −30 of F3J.10.3 standing, so a 90 s overfly scored
        // −30 rather than the zero the rule states. The gate is explicit here and
        // the −30 can only apply within 0 < overfly <= 60.
        // Two flight voids, and the second sat unwritten until the ADR-0002 §6
        // review. F3B and F3F both carry their landing-area gate; F3J's lives in
        // F3J.5.1 rather than in the F3J.10 scoring clause, which is why it was
        // missed. FlightValidWhen and not ValidWhen: F3J is HigherIsBetter, so a
        // zero is a truthful worst score, and `Flights = LastFlight` means the
        // flight must stay selected rather than promote its predecessor.
        FlightValidWhen = P.All(
            P.Le("overflySeconds", 60),                                        // F3J.10.4 "a zero score will be recorded for overflying … by more than one (1) minute"
            P.Is("restedWithin75m", true)),                                    // F3J.5.1 e "the flight is cancelled and recorded as a zero score if,
                                                                               //   during landing, some part of the model aircraft does not come to
                                                                               //   rest within 75 metres of the centre of the competitor's
                                                                               //   designated landing circle"
        Score =
        [
            T.Rate("flightTime", 1, cap: 600),                                 // F3J.10 1 pt/s; timed to the end of working time (F3J.10.1 c)

            // A DERIVED deduction, not a Penalty: nobody records an infraction, it
            // falls out of the measured overfly.
            T.When(P.Gt("overflySeconds", 0), T.Constant(-30)),                // F3J.10.3

            T.When(P.All(P.Eq("overflySeconds", 0),                            // F3J.10.9
                         P.Is("touchedByCompetitor", false)),                  // F3J.10.8
                   T.Lookup("landingDistance", LandingRows)),
        ],
    };

    // Two numbers change, the working time and the flight-points cap;
    // `flightValidWhen` and everything else comes with `like`. The `score` block
    // is restated whole because a restated block replaces the parent's entirely —
    // but the twenty-four landing rows are declared once above, so restating the
    // block no longer re-transcribes them.
    private static TaskDefinition FlyoffTaskD => TaskD with
    {
        Name = "Duration (fly-off)",
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 900 },    // F3J.11.2 "fifteen (15) minutes duration" for fly-off qualifiers
        Score =
        [
            T.Rate("flightTime", 1, cap: 900),
            T.When(P.Gt("overflySeconds", 0), T.Constant(-30)),                // F3J.10.3
            T.When(P.All(P.Eq("overflySeconds", 0),
                         P.Is("touchedByCompetitor", false)),
                   T.Lookup("landingDistance", LandingRows)),
        ],
    };

    // ---- the definition ----------------------------------------------------

    public static ClassDefinition Definition => new()
    {
        Name = "RC Thermal Duration Gliders",
        FaiDesignation = "F3J",
        Version = "FAI F3 Soaring 2025 ed.2",
        FinalRanking = FinalRankingKind.SplitByPromotion,                      // F3J.11 qualifiers ranked on the fly-off, the rest on qualifying

        Parameters =
        [
            Params.Number("flyoffSize", @default: 9),                          // F3J.11 top >= 9; the CD may raise the number
            Params.Flag("carryPenalties"),                                     // F3J states nothing (F12 — silence is a parameter)
            Params.Number("flyoffMinRounds"),                                  // F3J.11 states no fly-off minimum (F12)
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.Replacement,                     // F3J.4
            OthersScore = ReflightSelection.BetterOf,                           // F3J.4
            MinNewGroupSize = 4,                                                // F3J.4
        },

        Penalties =
        [
            Deduct("towlineNotClearedWithin30s", 100),                          // F3J.8.3
            Deduct("nonConformingWinch", 1000),                                 // F3J.8.2 p
            Deduct("unauthorisedTransmission", 300),                            // F3J.7 d

            // F3J.2.4 c "for each attempt only one penalty can be given, if a
            // person and at the same attempt an object is touched the 1000 points
            // penalty is applied" — one exclusion group, largest wins (F20).
            // READING: c) is written inside F3J.2.4 Safety Rules and is taken to
            // govern the whole clause, so d) joins the group. The rule does not say
            // so in as many words; nothing else in F3J.2.4 suggests d) stands apart.
            Excluded("safetyAreaObjectContact", 300),                           // F3J.2.4 a
            Excluded("safetyAreaPersonContact", 1000),                          // F3J.2.4 b
            Excluded("safetySpaceNotLeft", 300),                                // F3J.2.4 d
        ],

        Phases =
        [
            new()
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new() { MinRounds = 4 },                             // F3J.3.1 a
                Drops =
                [
                    new()
                    {
                        Dimension = DropDimension.ByRound,
                        DropCount = 1,
                        ApplyWhenRoundsCompletedAtLeast = 8,                    // F3J.3.1 a "if more than 7 qualification rounds are flown"
                    },                                                          //   — the highest threshold of the six
                ],
                Tasks = [TaskD],
            },

            new()
            {
                Ordinal = 2,
                Type = PhaseType.Flyoff,                                        // F3J.11
                Promotion = new()
                {
                    Kind = PromotionKind.TopN,
                    TopN = NumberOrParam.Param("flyoffSize"),
                    MinGroupSize = 9,                                           // F3J.11 top >= 9, a single group
                    MaxGroupSize = null,                                        // `..unlimited`
                    CarryPenalties = FlagOrParam.Param("carryPenalties"),       // F3J states nothing about carry-over, so the CD decides
                },                                                              //   at setup and the choice is logged (F12)
                Validity = new() { MinRounds = NumberOrParam.Param("flyoffMinRounds") },  // not stated for F3J (F12)
                // no drop: F3J.3.1 a's discard applies to the qualifying aggregate
                Tasks = [FlyoffTaskD],
            },
        ],
    };

    private static PenaltyDefinition Deduct(string infraction, decimal points) => new()
    {
        InfractionType = infraction,
        Effects = [new(PenaltyEffect.DeductPoints, points)],
    };

    private static PenaltyDefinition Excluded(string infraction, decimal points) => new()
    {
        InfractionType = infraction,
        ExclusionGroups = ["safetyRules"],
        Effects = [new(PenaltyEffect.DeductPoints, points)],
    };
}
