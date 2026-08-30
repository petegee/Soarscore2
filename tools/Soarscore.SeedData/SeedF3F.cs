// F3F — RC Slope Soaring Gliders
// Rule refs: FAI Sporting Code Volume F3 Soaring 2025 (F3F.x)
//
// The seventh class, and the first the
// notation was not designed against (notation §11). Structurally the simplest in
// the corpus — one phase, one task, one flight, one metric — and shape-identical
// to F3B Task C. It is interesting only for the three things it broke (F22, F23,
// F28) and the one it still cannot say.

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedF3F
{
    // ---- Task S, Speed -----------------------------------------------------
    // The inverted task, as F3B Task C is. A raw zero is the FASTEST time in the
    // group, so every "scores zero" clause in F3F.1.6 reaches validWhen — never a
    // zero score term, and never a zeroFlight penalty effect.

    private static TaskDefinition TaskS => new()
    {
        Code = "S",
        Name = "Speed",                                                        // F3F.1.8 ten (10) legs of a 100 m closed course
        Metrics =
        [
            M.Number("courseTime", "s", RoundingMode.Truncate, 0.01m),         // F3F.1.12 "the time in seconds and hundredths of seconds"
            M.Flag("courseCompleted"),                                         // F3F.1.6 e "the flight is not carried through"
            M.Flag("landedInDefinedArea"),                                     // F3F.1.6 f
            M.Flag("launchedWithin30s"),                                       // F3F.1.6 g "not launched within 30 seconds from the moment the starting order is given"
            M.Flag("modelIntact"),                                             // F3F.1.6 b "the model loses any part while airborne"
            M.Flag("clearedPlaneWithin5s"),                                    // F3F.1.6 h
            M.Flag("seenEnteringCourse"),                                      // F3F.1.6 i "not seen entering the course by the Judge at Base A"
            M.Flag("flownWithinRules"),                                        // F3F.1.6 a, c, d — non-conforming model, helper advice during the
                                                                               //   timed flight, control by anyone other than the competitor:
                                                                               //   three officials' rulings, one recorded observation
        ],
        Flights = new LastFlight(),                                            // F3F.1.5 "the competitor has one (1) attempt on each flight"
        Timing = new()
        {
            Kind = WorkingTimeKind.UntilAllFlightsComplete,
            PreparationTime = 180,                                             // F3F.1.7 "three (3) minutes of preparation time"
            MaxLaunches = 1,
        },
        Group = new() { MinPerGroup = 10 },                                    // F3F.1.7 "at least ten (10) competitors in one group";
                                                                               //   no annulment threshold is stated, so minValidResults is unset (F12)
        Normalise = new()
        {
            Direction = NormalisationDirection.LowerIsBetter,
            WinnerScore = 1000,                                                // F3F.1.12 Ri = 1000 x Tw / Ti; no precision stated (F12)
        },
        ValidWhen = P.All(                                                     // F3F.1.6 — "official but gets a zero score" means NO RESULT here
            P.Is("courseCompleted", true),
            P.Is("landedInDefinedArea", true),
            P.Is("launchedWithin30s", true),
            P.Is("modelIntact", true),
            P.Is("clearedPlaneWithin5s", true),
            P.Is("seenEnteringCourse", true),
            P.Is("flownWithinRules", true),
            P.Gt("courseTime", 0)),                                           // a captured 0 is a mis-capture, not a course flown in zero
                                                                               //   time — courseCompleted=true alone does not rule that out,
                                                                               //   and a raw 0 would otherwise be crowned the group's winner
                                                                               //   (this file's own header note on the inverted task)
        Score = [T.Rate("courseTime", 1)],                                     // the raw result IS the elapsed time; the direction inverts it
    };

    // ---- the definition ----------------------------------------------------

    public static ClassDefinition Definition => new()
    {
        Name = "RC Slope Soaring Gliders",
        FaiDesignation = "F3F",
        Version = "FAI F3 Soaring 2025",
        // no finalRanking: one phase, so SinglePhase (F3F has no fly-off)

        Parameters =
        [
            // F3F.1.5 "after a fixed number of pilots (e.g. 5), pre-defined and
            // announced by the organiser". Declared so the CD's choice reaches the
            // event log; NOTHING READS IT, which is legal — the resolution check
            // runs one way only (notation §3, adoption check 3).
            Params.Number("reflightAfterNPilots", boundAt: ParameterBindingPoint.BeforeFlying),
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.Replacement,                     // F3F.1.5 — the repeat replaces; no better-of provision
            OthersScore = ReflightSelection.UndefinedRequiresRuling,            // F3F.1.5 re-flies one pilot; it says nothing about the rest of the group
            // F3F.1.5's "provisional re-flight" is NOT a third selection value:
            //   under protest both attempts are flown and the jury afterwards
            //   decides which counts. That is one instance's ruling, not the
            //   class's rule, and it is recorded by annulling the Entry that did
            //   not count.
            // no minNewGroup: F3F.1.5 re-flies into the running order, not into a
            //   new group, so the field is INAPPLICABLE, not unstated
        },

        // F3F.1.10 is the corpus's only clause that both MULTIPLIES a penalty (F23)
        // and states exclusion PAIRWISE rather than as one group (F28). A person
        // contact supersedes both other infractions; the crossing and the object
        // contact ADD, on the clause's own wording — "if there was an ADDITIONAL
        // penalty of 100 points because of crossing the safety plane only 1000
        // points will be deducted". Two groups say all three facts; one group says
        // two of them. Two crossings and an object contact deduct 300; a person
        // contact with anything else deducts 1000 and no more.
        Penalties =
        [
            new()
            {
                InfractionType = "safetyPlaneCrossing",
                ExclusionGroups = ["safetyMax"],
                Accrual = PenaltyAccrual.PerOccurrence,
                Effects = [new(PenaltyEffect.DeductPoints, 100)],               // F3F.1.10 "penalised by 100 points each"
            },
            new()
            {
                InfractionType = "safetyAreaObjectContact",
                ExclusionGroups = ["contact"],
                Effects = [new(PenaltyEffect.DeductPoints, 100)],               // F3F.1.10 "the number of contacts does not matter (maximum one penalty)"
            },
            new()
            {
                InfractionType = "safetyAreaPersonContact",
                ExclusionGroups = ["contact", "safetyMax"],
                Effects = [new(PenaltyEffect.DeductPoints, 1000)],              // F3F.1.10 "only 1000 points will be deducted"
            },
            new()
            {
                InfractionType = "prohibitedTelemetry",
                Effects = [new(PenaltyEffect.Disqualify)],                      // F3F.1.2 "the competitor will be disqualified from the contest"
            },
        ],

        Phases =
        [
            new()
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                // rounds: F3F.1.7 "the flights are to be performed round by round"
                //   — the default composition, one task per round, and no ceiling
                //   is stated (notation §7.2)
                Validity = new() { MinRounds = 4 },                             // F3F.1.13 "a minimum of four (4) rounds must be flown"
                Drops =
                [
                    // Two tiers, most selective first (F22). Reversed, a 15-round
                    // contest would match the >= 4 line and discard one — adoption
                    // rejects non-descending gates (check 10).
                    new()
                    {
                        Dimension = DropDimension.ByRound,
                        DropCount = 2,
                        ApplyWhenRoundsCompletedAtLeast = 15,                   // F3F.1.13 "if more than fourteen (14) rounds were flown,
                    },                                                          //   the two (2) lowest round scores will be discarded"
                    new()
                    {
                        Dimension = DropDimension.ByRound,
                        DropCount = 1,
                        ApplyWhenRoundsCompletedAtLeast = 4,                    // F3F.1.13 "in this case the lowest round score of each
                    },                                                          //   competitor will be discarded"
                ],
                // Tie-breaks: F3F.1.13 — fly more rounds of the task until the tie
                // breaks (operational first); if that is not possible, the best
                // dropped score defines the ranking (the comparator fallback).
                // D7 deviation: F3F.1.13's "concerning the five best scores"
                // scoping is deliberately unmodelled — readmitted as a scope field
                // the day a second class needs one.
                TieBreaks = [new ClassificationRounds(), new BestDroppedScore()],  // F3F.1.13
                                                                                //   encoding: kanban/in-progress/tie-break-policy-in-class-definition.md
                Tasks = [TaskS],
            },
        ],
    };
}
