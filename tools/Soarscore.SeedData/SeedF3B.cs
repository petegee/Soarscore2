// F3B — RC Multi-Task Gliders
// Rule refs: FAI Sporting Code Volume F3 Soaring 2025 ed.2 (F3B.x)
//
// The structural outlier, and the
// reason a Task owns normalisation, group constraints, rounding and flight-time
// precision rather than the class or the round: every one of those differs
// between A, B and C inside this one definition.

using System.Collections.Immutable;
using Soarscore.Domain.CompetitionClasses;

namespace Soarscore.SeedData;

public static class SeedF3B
{
    // ---- A, Duration -------------------------------------------------------

    private static TaskDefinition TaskA => new()
    {
        Code = "A",
        Name = "Duration",                                                     // F3B.2.3
        Metrics =
        [
            M.Number("flightTime", "s", RoundingMode.Truncate, 1),             // F3B.2.3 b "each full second"
            M.Number("landingDistance", "m", RoundingMode.Ceiling, 1),         // F3B.2.3 d "rounded to the nearest higher metre"
            M.Flag("landedInDefinedArea"),                                     // F3B.2.3 b
            M.Flag("atRestBy12Min"),                                           // F3B.2.3 e
            M.Flag("touchedByCompetitor"),                                     // F3B.1.7 d
        ],
        Flights = new LastFlight(),                                            // F3B.1.5 unlimited attempts; the last is the attempt
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 720 },    // F3B.2.3 a 12 min from the order of the starter, incl. towing
        Group = new() { MinPerGroup = 5, MinValidResults = 2 },                // minPerGroup F3B.1.8 b; minValidResults F3B.1.8 c (F7)
        Normalise = new()
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,                                                // F3B.2.6 states no rounding precision, so none is applied (F12)
        },
        Score =
        [
            // Flight points and the overtime deduction are ONE cumulative
            // piecewise term over one metric, not two terms. 601 s scores 599.
            T.When(P.Is("landedInDefinedArea", true),                          // F3B.2.3 b "if the model does not land on the defined landing area, the whole flight is zero"
                   T.Piecewise("flightTime",
                       Bands.From(0)
                            .UpTo(600, 1)                                      // F3B.2.3 b max 600 points
                            .Rest(-1))),                                       // F3B.2.3 c one point deducted per full second over 600

            // Landing bonus, forfeited four separate ways. The fourth is stated
            // outside F3B.2.3, in the general flight rules, which is why it is
            // easy to miss — F3J states the identical rule inside its own
            // scoring clause (F3J.10.8).
            T.When(P.All(P.Is("landedInDefinedArea", true),                    // F3B.2.3 b
                         P.Le("flightTime", 630),                              // F3B.2.3 d "no landing bonus if the flight time exceeds 630 seconds"
                         P.Is("atRestBy12Min", true),                          // F3B.2.3 e
                         P.Is("touchedByCompetitor", false)),                  // F3B.1.7 d "touches either the competitor or his helper during
                                                                               //   landing manoeuvres of task A, no landing points will be given"
                   T.Lookup("landingDistance",                                 // F3B.2.3 d
                       Rows.UpTo(1, 100).Then(2, 95).Then(3, 90).Then(4, 85)
                           .Then(5, 80).Then(6, 75).Then(7, 70).Then(8, 65)
                           .Then(9, 60).Then(10, 55).Then(11, 50).Then(12, 45)
                           .Then(13, 40).Then(14, 35).Then(15, 30)
                           .Rest(0))),
        ],
    };

    // ---- B, Distance -------------------------------------------------------

    private static TaskDefinition TaskB => new()
    {
        Code = "B",
        Name = "Distance",                                                     // F3B.2.4
        Metrics =
        [
            M.Number("legs", "legs", RoundingMode.Truncate, 1),                // F3B.2.4 e only full 150 m legs are counted
            M.Flag("landedInDefinedArea"),                                     // F3B.2.4 f
        ],
        Flights = new LastFlight(),
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 420 },    // F3B.2.4 a 7 min incl. towing (F13: the 4 min timed window inside it is not modelled)
        Group = new() { MinPerGroup = 3, MinValidResults = 2 },                // minPerGroup F3B.1.8 b; minValidResults F3B.1.8 c (F7)
        Normalise = new()
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,                                                // F3B.2.6 — no precision stated (F12)
        },
        Score =
        [
            T.When(P.Is("landedInDefinedArea", true),                          // F3B.2.4 f
                   T.Rate("legs", 1)),                                         // F3B.2.6 partial B is the leg count normalised
        ],
    };

    // ---- C, Speed ----------------------------------------------------------
    // The inverted task. Note validWhen rather than a zero score: a raw zero in a
    // LowerIsBetter group is the FASTEST time in the group.

    private static TaskDefinition TaskC => new()
    {
        Code = "C",
        Name = "Speed",                                                        // F3B.2.5
        Metrics =
        [
            M.Number("courseTime", "s", RoundingMode.Truncate, 0.01m),         // F3B.2.5 c "recorded to at least 1/100 sec"
            M.Flag("courseCompleted"),                                         // F3B.2.5 g "models which come to rest before having completed the task will score zero"
            M.Flag("landedInDefinedArea"),                                     // F3B.2.5 f
        ],
        Flights = new LastFlight(),                                            // F3B.2.5 i re-launch permitted only before Base A is first crossed
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 240 },    // F3B.2.5 a 4 min incl. towing
        Group = new() { MinPerGroup = 8, MinValidResults = 2 },                // minPerGroup F3B.1.8 b; minValidResults F3B.1.8 c "minimum 8 competitors or all competitors" (F7)
        Normalise = new()
        {
            Direction = NormalisationDirection.LowerIsBetter,
            WinnerScore = 1000,                                                // F3B.2.6 partial C = 1000 x T_winner / T_own; no precision stated (F12)
        },
        // F19: the class default comes from F3B.1.5 e, which scopes itself to
        // "task A (Duration) … or task B (Distance)" BY NAME and says nothing
        // about Task C. The silence is recorded rather than assumed away.
        Reflight = new()
        {
            EntitledScores = ReflightSelection.UndefinedRequiresRuling,         // F3B.1.5 e covers Tasks A and B only
            OthersScore = ReflightSelection.UndefinedRequiresRuling,            // F3B.1.5 e
            MinNewGroupSize = NumberOrParam.Param("minNewGroup"),
        },
        ValidWhen = P.All(P.Is("courseCompleted", true),                       // F2 + F3
                          P.Is("landedInDefinedArea", true)),
        Score = [T.Rate("courseTime", 1)],                                     // the raw result IS the elapsed time; the direction inverts it
    };

    // ---- the definition ----------------------------------------------------

    public static ClassDefinition Definition => new()
    {
        Name = "RC Multi-Task Gliders",
        FaiDesignation = "F3B",
        Version = "FAI F3 Soaring 2025 ed.2",
        // no finalRanking: one phase, so SinglePhase (F3B has no fly-off)

        Parameters =
        [
            Params.Number("minRounds", @default: 1, allowed: [1, 5]),           // F3B.2.1 b — 1 normally, 5 at World/Continental Championships
            Params.Number("minNewGroup"),                                      // F3B states no minimum for a re-flight group (F12)
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.Replacement,                     // F3B.1.5 e "the repetition is the official score"
            OthersScore = ReflightSelection.BetterOf,                           // F3B.1.5 e
            MinNewGroupSize = NumberOrParam.Param("minNewGroup"),               // F3B states no minimum, so the CD decides at setup (F12)
        },

        Penalties =
        [
            new()
            {
                InfractionType = "safetyPlaneCrossing",
                Effects = [new(PenaltyEffect.DeductPoints, 300)],               // F3B.2.5 h "deduction from the competitor's final score"
            },

            // Two effects at two points in the pipeline (F20): the rule zeroes the
            // flight AND deducts from the final score.
            new()
            {
                InfractionType = "nonConformingWinch",
                Effects =
                [
                    new(PenaltyEffect.ZeroFlight),                              // F3B.2.2 p the flight preceding the test is zeroed
                    new(PenaltyEffect.DeductPoints, 1000),                      // F3B.2.2 p
                ],
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
                    Kind = CompositionKind.FixedSequence,
                    TasksPerRound = 3,                                          // F3B: a round is one flight of each of A, B and C
                },
                Validity = new()
                {
                    MinRounds = NumberOrParam.Param("minRounds"),
                    MinTasks = 1,                                               // F3B.2.1 b "A minimum of one (1) round and one (1) task
                                                                                //   must be flown that the competition is valid"
                },
                Drops =
                [
                    // Both gates, conjunctive (F18). They diverge whenever a group
                    // is annulled under F3B.1.8 c: a competitor can hold six
                    // completed rounds but only five Task-C results, and the rule
                    // requires both counts before the drop applies.
                    new()
                    {
                        Dimension = DropDimension.ByTask,
                        DropCount = 1,
                        ApplyWhenRoundsCompletedAtLeast = 6,                    // F3B.2.8 "if more than five complete rounds are flown,
                        ApplyWhenResultsAtLeast = 6,                            //   the lowest partial score of each task with more than
                    },                                                          //   five results is omitted"
                ],
                Tasks = [TaskA, TaskB, TaskC],
            },
        ],
    };
}
