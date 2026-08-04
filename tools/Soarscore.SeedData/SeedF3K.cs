// F3K — RC Hand-Launch Gliders
// Rule refs: FAI Sporting Code Volume F3 Soaring 2025 ed.2 (F3K.x)
//
// 14 authored tasks become 27 fully
// written ones under expansion — the corpus's hardest test of `like`, five of
// the six selection kinds, both target-assignment orders, and its only
// UntilAllFlightsComplete.

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedF3K
{
    // ---- metricSet f3kFlight -----------------------------------------------

    private static ImmutableArray<MetricDefinition> FlightMetrics =>
    [
        M.Number("flightTime", "s", RoundingMode.Truncate, 0.1m),  // F3K.7 recorded to 0.1 s, truncated
        M.Flag("landedWithinWindow"),                              // F3K.9.3 the 30 s landing window
        M.Flag("launchedInWorkingTime"),                           // F3K.7 launched before the working time = zero score
    ];

    // ---- A, the task every other one derives from --------------------------

    private static TaskDefinition TaskA => new()
    {
        Code = "A",
        Name = "Last flight",                                                  // F3K.11.1
        Metrics = FlightMetrics,
        Flights = new LastFlight(),                                            // "only the last flight is taken into account"
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = NumberOrParam.Param("workingTime.A") },
        Group = new() { MinPerGroup = 5 },                                     // F3K.9.1
        Normalise = new()
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,
            Round = new(RoundingMode.HalfUp, 0.1m),                            // F3K.9.1
        },
        // Two class-wide flight voids (F17). They zero this flight's contribution
        // WITHOUT deselecting it, which is what F3K.11.1 needs: a late-landing
        // last flight must score zero, not promote its predecessor. Every task
        // below inherits this through `like`.
        FlightValidWhen = P.All(
            P.Is("landedWithinWindow", true),                                  // F3K.9.3 "if a model glider lands later, that flight will score zero"
            P.Is("launchedInWorkingTime", true)),                              // F3K.7
        Score = [T.Rate("flightTime", 1, cap: 300)],                           // F3K.11.1
    };

    private static TaskDefinition TaskB => TaskA with
    {
        Code = "B",
        Name = "Next to last and last flight",                                 // F3K.11.2
        Flights = new LastNFlights(2),
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = NumberOrParam.Param("workingTime.B") },
        Score = [T.Rate("flightTime", 1, cap: NumberOrParam.Param("maxFlight.B"))],
    };

    // The only UntilAllFlightsComplete task in the corpus: the working time IS
    // the sum of the flights ("the aggregate of all 3 (or 5) flight times means
    // the working time"), so no working time is class data at all.
    private static TaskDefinition TaskC => TaskA with
    {
        Code = "C",
        Name = "All up, last down",                                            // F3K.11.3
        Metrics = [.. FlightMetrics, M.Flag("launchedOnSignal")],              // F3K.11.3 within 3 s of the acoustic signal
        Flights = new AllFlights(),
        Timing = new()
        {
            Kind = WorkingTimeKind.UntilAllFlightsComplete,
            PreparationTime = 60,
            MaxLaunches = NumberOrParam.Param("launches.C"),
        },
        // The launch-signal void is a third condition here, not a score-term
        // wrapper (F17): with `flights all` a wrapper would have to be repeated on
        // every term, and this task's landing window is its own (3:03-3:33 per
        // attempt) rather than the general 30 s one. F3K.9.3's cascade — a model
        // still airborne during the 60 s preparation time zeroes the NEXT attempt
        // — is recorded by the timekeeper as the next attempt's landedWithinWindow
        // flag; the notation sees one flight at a time.
        FlightValidWhen = P.All(
            P.Is("landedWithinWindow", true),                                  // F3K.9.3 3:03-3:33 window for Task C
            P.Is("launchedInWorkingTime", true),                               // F3K.7
            P.Is("launchedOnSignal", true)),                                   // F3K.11.3 early or >3 s late = zero for the flight
        Score = [T.Rate("flightTime", 1, cap: 180)],                           // F3K.11.3 "the maximum measured flight time is 180 seconds"
    };

    // D restates no `score`, so A's whole term list comes with `like`:
    // `rate flightTime 1 pt/s cap 300 s` is what F3K.11.4 states for D as well
    // (300 s each, both flights summed).
    private static TaskDefinition TaskD => TaskA with
    {
        Code = "D",
        Name = "Two flights",                                                  // F3K.11.4
        Flights = new AllFlights(),
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 600, MaxLaunches = 2 },
    };

    // Poker. An achieved target credits the TARGET, not the flight time; a missed
    // one credits nothing. `bestN 3` is what enforces "up to three (3) target
    // times" — F3K allows unlimited launches, so a launch cap will not.
    // NO rankBy: the candidates must be ranked by SCORE (see the trap on H).
    private static TaskDefinition TaskE => TaskA with
    {
        Code = "E",
        Name = "Poker — variable target time",                                 // F3K.11.5
        Metrics = [.. FlightMetrics,
                   M.Number("targetTime", "s", RoundingMode.Truncate, 1, declared: true)],  // F3K.11.5 announced before release
        Flights = new BestNFlights { Count = 3 },
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = NumberOrParam.Param("workingTime.E") },
        Score = [T.When(P.Ge("flightTime", "targetTime"),
                        T.Rate("targetTime", 1))],                             // F3K.11.5 "the target time is credited"
    };

    private static TaskDefinition TaskF => TaskA with
    {
        Code = "F",
        Name = "3 out of 6",                                                   // F3K.11.6
        Flights = new BestNFlights { Count = 3 },
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 600, MaxLaunches = 6 },
        Score = [T.Rate("flightTime", 1, cap: 180)],
    };

    private static TaskDefinition TaskG => TaskA with
    {
        Code = "G",
        Name = "Five longest flights",                                         // F3K.11.7
        Flights = new BestNFlights { Count = 5 },
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = [T.Rate("flightTime", 1, cap: 120)],
    };

    // No cap on the term: the assigned target IS the cap. `rankBy flightTime`
    // (F16) is load-bearing and is the ONE place in the corpus that needs it —
    // F3K.11.8 assigns targets to the four longest FLIGHTS, and no flight has a
    // score until a target has been assigned, so the default ranking (by score) is
    // circular here. Task E above is the exact opposite and must NOT have it;
    // check both against the worked examples, F3K.11.5 -> 142 s, F3K.11.8 -> 569 s.
    private static TaskDefinition TaskH => TaskA with
    {
        Code = "H",
        Name = "1, 2, 3 and 4 minute targets, any order",                      // F3K.11.8
        Flights = new BestNFlights
        {
            Count = 4,
            RankByMetric = "flightTime",
            Targets = TargetAssignment.AnyOrder,
            TargetValues = [60, 120, 180, 240],
        },
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = [T.Rate("flightTime", 1)],
    };

    private static TaskDefinition TaskI => TaskA with
    {
        Code = "I",
        Name = "Three longest flights",                                        // F3K.11.9
        Flights = new BestNFlights { Count = 3 },
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = [T.Rate("flightTime", 1, cap: 200)],
    };

    private static TaskDefinition TaskJ => TaskA with
    {
        Code = "J",
        Name = "Three last flights",                                           // F3K.11.10
        Flights = new LastNFlights(3),
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = [T.Rate("flightTime", 1, cap: 180)],
    };

    // "The competitors do not have to reach or exceed the target times to count
    // each flight time" — so this is a clamp, not a Poker-style conditional.
    private static TaskDefinition TaskK => TaskA with
    {
        Code = "K",
        Name = "Big Ladder — increasing by 30 s",                              // F3K.11.11
        Flights = new ExactlyNInOrder { Count = 5, TargetValues = [60, 90, 120, 150, 180] },
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 600, MaxLaunches = 5 },
        Score = [T.Rate("flightTime", 1)],
    };

    private static TaskDefinition TaskL => TaskA with
    {
        Code = "L",
        Name = "One flight",                                                   // F3K.11.12
        // `flights last` comes from A; only the timing and the cap differ.
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = NumberOrParam.Param("workingTime.L"),
            MaxLaunches = 1,                                                   // F3K.11.12 one launch
        },
        Score = [T.Rate("flightTime", 1, cap: NumberOrParam.Param("maxFlight.L"))],
    };

    private static TaskDefinition TaskN => TaskA with
    {
        Code = "N",
        Name = "Best flight",                                                  // F3K.11.14
        Flights = new BestNFlights { Count = 1 },
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 600 },
        Score = [T.Rate("flightTime", 1, cap: 599)],
    };

    // Fly-off only. Only the ladder and the working time differ from K; the
    // uncapped `rate flightTime 1 pt/s` comes with `like` and is right for the
    // same reason — the assigned target is the cap (F3K.11.13).
    private static TaskDefinition TaskM => TaskK with
    {
        Code = "M",
        Name = "Huge Ladder — increasing by 2 min",                            // F3K.11.13
        Flights = new ExactlyNInOrder { Count = 3, TargetValues = [180, 300, 420] },
        Timing = new() { Kind = WorkingTimeKind.Fixed, WorkingTime = 900, MaxLaunches = 3 },
    };

    private static ImmutableArray<TaskDefinition> Catalogue =>
        [TaskA, TaskB, TaskC, TaskD, TaskE, TaskF, TaskG, TaskH, TaskI, TaskJ, TaskK, TaskL, TaskN];

    // ---- the definition ----------------------------------------------------

    public static ClassDefinition Definition => new()
    {
        Name = "RC Hand-Launch Gliders",
        FaiDesignation = "F3K",
        Version = "FAI F3 Soaring 2025 ed.2",
        FinalRanking = FinalRankingKind.LastPhaseReplaces,                     // F3K.10 "fly-off replaces preliminary scores for its participants"

        // The rules offer the organiser a choice at each of these points and state
        // the permitted values, so each carries `allowed` (F8). A parameter with no
        // default is one the rules leave open entirely (F12).
        Parameters =
        [
            Params.Number("workingTime.A", "s", 600, [420, 600], ParameterBindingPoint.PerRound),  // F3K.11.1
            Params.Number("workingTime.B", "s", 600, [420, 600], ParameterBindingPoint.PerRound),  // F3K.11.2
            Params.Number("maxFlight.B", "s", 240, [180, 240], ParameterBindingPoint.PerRound),    // F3K.11.2
            Params.Number("launches.C", @default: 3, allowed: [3, 4, 5]),                          // F3K.11.3
            Params.Number("workingTime.E", "s", 600, [600, 900], ParameterBindingPoint.PerRound),  // F3K.11.5
            Params.Number("workingTime.L", "s", 600, [420, 600], ParameterBindingPoint.PerRound),  // F3K.11.12
            Params.Number("maxFlight.L", "s", 599, [419, 599], ParameterBindingPoint.PerRound),    // F3K.11.12
            Params.Number("flyoffSize"),                                                           // F3K.9.1 size not fixed by the rules
            Params.Flag("carryPenalties"),                                                         // F3K states nothing (F12)
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.Replacement,                     // F3K.9.6 the re-flyer's new attempt is official even if worse
            OthersScore = ReflightSelection.BetterOf,                           // F3K.9.6
            MinNewGroupSize = 4,                                                // F3K.9.6
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

            // F3K.4.1 does two things, and only ONE of them is stated here.
            //
            // The deduction half is NOT written on this definition. F3K.4.1 says
            // "the competitor will receive a penalty ACCORDING TO PARAGRAPH
            // F3K.4.3" — it hands the deduction back to F3K.4.3's schedule rather
            // than stating one, and F3K.4.3's own "Each flight attempt may only
            // incur a single penalty" then governs it. The clause that applies is
            // limb 3: F3K.4.1 scopes itself to contact "on the start and landing
            // field", and F3K.4.3 opens "The organiser may define safety areas
            // OUTSIDE OF the start and landing field", so such a contact is never
            // inside a safety area and can only be "with a person … anywhere
            // outside the defined safety area … 100 points". That is exactly
            // `personContactOutsideSafetyArea` above, already in the group at 100.
            //
            // So a launch contact is recorded as TWO infractions: that one for the
            // deduction, and this one for the round zero F3K.4.1 adds on top.
            // Writing it as one definition carrying both effects is what the rule
            // reads like and is unrepresentable: the deduction belongs in the
            // exclusion group, adoption check 16 admits only DeductPoints effects
            // into a group, and this definition's ZeroRound is not one. The split
            // is what the model can say; the cost is that the scorer records two.
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
                    RequireDistinctTaskPerRound = true,                         // F3K.10 five rounds, each a different task
                },
                Validity = new() { MinRounds = 5 },                             // F3K.10
                Drops =
                [
                    // F3K needs only the first gate.
                    new()
                    {
                        Dimension = DropDimension.ByRound,
                        DropCount = 1,
                        ApplyWhenRoundsCompletedAtLeast = 6,                    // F3K.10 "if 6 or more rounds are flown"
                    },
                ],
                Tasks = Catalogue,
            },

            // Mandatory at World / Continental Championships and at the organiser's
            // discretion elsewhere — conditional on the event level, which the model
            // has no notion of, so nothing on the phase records it. If fewer than 3
            // fly-off rounds complete, the preliminary results stand: that is
            // `validity` plus LastPhaseReplaces, not a separate rule.
            new()
            {
                Ordinal = 2,
                Type = PhaseType.Flyoff,                                        // F3K.9.1
                Promotion = new()
                {
                    Kind = PromotionKind.TopN,
                    TopN = NumberOrParam.Param("flyoffSize"),
                    MinGroupSize = 5,
                    MaxGroupSize = null,                                        // `..unlimited`
                    CarryPenalties = FlagOrParam.Param("carryPenalties"),       // F3K.9.1; F3K states nothing about carry-over (F12)
                },
                Rounds = new()
                {
                    Kind = CompositionKind.ChooseFromCatalogue,
                    TasksPerRound = 1,
                    MaxRounds = 6,                                              // F3K.10.3 "at least three (3) rounds with a maximum of six (6)" (F21)
                },
                Validity = new() { MinRounds = 3 },                             // F3K.10.3
                // no drop: F3K.10's discard applies to the preliminary aggregate
                //   only, so the fly-off has no discard rule to state
                Tasks = [.. Catalogue, TaskM],
            },
        ],
    };

    private static PenaltyDefinition Excluded(string infraction, decimal points) => new()
    {
        InfractionType = infraction,
        ExclusionGroups = ["safetyInfraction"],
        Effects = [new(PenaltyEffect.DeductPoints, points)],
    };
}
