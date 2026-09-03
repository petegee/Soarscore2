// F5L — RC Electric Thermal Gliders, RES (provisional class)
// Rule refs: FAI Sporting Code Volume F5 Electric 2026 ed.2 (5.5.12.x)
//
// Two things make F5L worth writing
// even though it is the simplest class:
//   - its penalty catalogue is EMPTY. Every consequence in the F5L rules is
//     derived from something measured, so all of them are score terms.
//   - it is the class ReflightSelection.UndefinedRequiresRuling exists for.

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedF5L
{
    // ---- metricSet f5lFlight -----------------------------------------------

    private static ImmutableArray<MetricDefinition> FlightMetrics =>
    [
        M.Number("flightTime", "s", RoundingMode.Truncate, 1),                 // 5.5.12.11.1 recorded in full seconds
        M.Number("landingDistance", "m", RoundingMode.Truncate, 0.1m),         // 5.5.12.11.2 — no capture precision stated (F12 residual)
        M.Number("overflySeconds", "s", RoundingMode.Truncate, 1),             // 5.5.12.11.2
        M.Flag("landedInLandingArea"),                                         // 5.5.12.11.2 (zero for the entire task); stated twice — also
                                                                               //   5.5.12.5 d "Landing outside the boundary shall result in a
                                                                               //   zero score for that flight"
        M.Flag("lostPart"),                                                    // 5.5.12.11.2 a
        M.Flag("touchedByCompetitor"),                                         // 5.5.12.11.2 c
        M.Flag("touchedBeforeMeasuring"),                                      // 5.5.12.11.2 d
        M.Flag("amrtPresetsCorrect"),                                          // 5.5.12.4 flight = 0 if the AMRT settings differ from the presets (30 s / 90 m)
        M.Flag("timingDeviationInFavour"),                                     // 5.5.12.4 d helper-timed flight out by > 3 s in the competitor's favour = zero
    ];

    // ---- the task ----------------------------------------------------------

    private static TaskDefinition TaskD => new()
    {
        Code = "D",
        Name = "Duration",
        Metrics = FlightMetrics,
        Flights = new LastFlight(),                                            // 5.5.12.8 b "entitled to unlimited attempts during the working
                                                                               //   time"; 5.5.12.8 d "the result of the last flight will be
                                                                               //   the official score"
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 540 },    // 5.5.12.11.1 "within nine (9) minutes (540s) working time"
        Group = new() { MinPerGroup = NumberOrParam.Param("groupSize") },      // 5.5.12.4 — not fixed by the rules; see the fly-off below
        Normalise = new()
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,                                                // 5.5.12.11 states no rounding precision, so none is applied (F12)
        },
        Score =
        [
            // 2 pt/s to 390 s, then the overflying time is "deducted from 390 s" —
            // F3B Task A's shape at a different rate. 400 s scores
            // 390x2 + 10x(−2) = 760, i.e. 380 scored seconds.
            T.When(P.All(P.Is("landedInLandingArea", true),                    // 5.5.12.11.2 zero for the entire task
                         P.Le("overflySeconds", 30),                           // 5.5.12.11.2 b
                         P.Is("amrtPresetsCorrect", true),                     // 5.5.12.4
                         P.Is("timingDeviationInFavour", false)),              // 5.5.12.4 d
                   T.Piecewise("flightTime",
                       Bands.From(0)
                            .UpTo(390, 2)                                      // 5.5.12.11.1 two points per second, max 6:30
                            .Rest(-2))),                                       // 5.5.12.11.1 "the overflying time will be deducted from 390 s"

            // Five separate ways to lose the landing bonus, plus the two that zero
            // the whole task. Seven conditions on one term — the clearest case in
            // the corpus for finding F3.
            T.When(P.All(P.Is("landedInLandingArea", true),                    // 5.5.12.11.2 (entire task)
                         P.Is("amrtPresetsCorrect", true),                     // 5.5.12.4
                         P.Is("timingDeviationInFavour", false),               // 5.5.12.4 d
                         P.Eq("overflySeconds", 0),                            // 5.5.12.11.2 b
                         P.Is("lostPart", false),                              // 5.5.12.11.2 a
                         P.Is("touchedByCompetitor", false),                   // 5.5.12.11.2 c
                         P.Is("touchedBeforeMeasuring", false)),               // 5.5.12.11.2 d
                   T.Lookup("landingDistance",                                 // 5.5.12.11.2
                       // The same twenty-four rows F3J.10.5 states, and
                       // deliberately written out again: a fragment is scoped to
                       // one class definition, so the duplication BETWEEN
                       // definitions stays on the page as an honest record of what
                       // that discipline costs (notation §7.1).
                       Rows.UpTo(0.2m, 100).Then(0.4m, 99).Then(0.6m, 98).Then(0.8m, 97)
                           .Then(1.0m, 96).Then(1.2m, 95).Then(1.4m, 94).Then(1.6m, 93)
                           .Then(1.8m, 92).Then(2.0m, 91).Then(3.0m, 90).Then(4.0m, 85)
                           .Then(5, 80).Then(6, 75).Then(7, 70).Then(8, 65)
                           .Then(9, 60).Then(10, 55).Then(11, 50).Then(12, 45)
                           .Then(13, 40).Then(14, 35).Then(15, 30)
                           .Rest(0))),
        ],
    };

    // F5L changes nothing about the task in the fly-off — the only class of the
    // seven for which that is true.
    private static TaskDefinition FlyoffTaskD => TaskD with { Name = "Duration (fly-off)" };

    // ---- the definition ----------------------------------------------------

    public static ClassDefinition Definition => new()
    {
        Name = "RC Electric Thermal Gliders, RES",
        FaiDesignation = "F5L",
        Version = "FAI F5 Electric 2026 ed.2",
        FinalRanking = FinalRankingKind.SplitByPromotion,                      // 5.5.12.12 qualifiers ranked by the fly-off, non-qualifiers by qualifying

        Parameters =
        [
            Params.Number("groupSize"),                                        // 5.5.12.4 — group size not fixed by the rules (F12);
                                                                               //   declared once because the fly-off must match it
            Params.Number("flyoffSize"),                                       // 5.5.12.4 "the top normalised qualifiers" — number not stated (F12)
            Params.Flag("carryPenalties"),                                     // F5L states nothing (F12)
            Params.Number("minNewGroup"),                                      // 5.5.12.9 states no minimum for a re-flight group (F12)
        ],

        // 5.5.12.9 states entitlement and the claim/waiver conditions and NOTHING
        // about placement or which score counts. Borrowing the parent pattern would
        // be a CD ruling, not an F5L rule — so the class says it does not know.
        Reflight = new()
        {
            EntitledScores = ReflightSelection.UndefinedRequiresRuling,         // 5.5.12.9
            OthersScore = ReflightSelection.UndefinedRequiresRuling,            // 5.5.12.9
            MinNewGroupSize = NumberOrParam.Param("minNewGroup"),               // 5.5.12.9 states no minimum, so the CD decides at setup (F12)
        },

        // no penalty definitions — see the header note

        Phases =
        [
            new()
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new() { MinRounds = 4 },                             // 5.5.12.4 "at least 4 qualifying rounds"
                Drops =
                [
                    new()
                    {
                        Dimension = DropDimension.ByRound,
                        DropCount = 1,
                        ApplyWhenRoundsCompletedAtLeast = 6,                    // 5.5.12.12 "if more than 5, the lowest round score is dropped"
                    },
                ],
                // Tie-breaks: 5.5.12.12 states classification and stops — silence
                //   (UndefinedRequiresRuling), both phases.
                TieBreaks = [new UndefinedRequiresRuling()],                    // 5.5.12.12
                                                                                //   encoding: kanban/completed/tie-break-policy-in-class-definition.md
                Tasks = [TaskD],
            },

            new()
            {
                Ordinal = 2,
                Type = PhaseType.Flyoff,                                        // 5.5.12.4
                Promotion = new()
                {
                    Kind = PromotionKind.TopN,
                    TopN = NumberOrParam.Param("flyoffSize"),
                    // "The fly-off group size equals the preliminary group size" is
                    // why groupSize is declared once and referenced twice.
                    MinGroupSize = NumberOrParam.Param("groupSize"),
                    MaxGroupSize = NumberOrParam.Param("groupSize"),
                    CarryPenalties = FlagOrParam.Param("carryPenalties"),       // 5.5.12.4; F5L is SILENT on carry-over (F12)
                },
                Validity = new() { MinRounds = 2 },                             // 5.5.12.4 "minimum 2 rounds"
                // no drop: 5.5.12 states no fly-off discard
                // no tie-breaks: 5.5.12.12 states classification and stops —
                //   silence (UndefinedRequiresRuling), both phases
                TieBreaks = [new UndefinedRequiresRuling()],                    // 5.5.12.12
                                                                                //   encoding: kanban/completed/tie-break-policy-in-class-definition.md
                Tasks = [FlyoffTaskD],
            },
        ],
    };
}
