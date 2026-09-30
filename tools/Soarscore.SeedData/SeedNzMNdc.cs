// NZ Class M — ALES 200, NDC format
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, October 2024 Rev 3.0
//            (NZ.7.4(h), which incorporates NZ.7.4(b)–(g) except for the scoring)
//
// NZ.7.4(h) is the National
// Decentralized Contest format of the same rulebook class as ALES 200. It fixes
// the round count at four, fixes the target time at ten minutes, and — the reason
// it cannot be a parameter binding on the parent — scores "the sum of the four
// rounds RAW scores", with no normalisation at all.
//
// That is a different pipeline, not a different number, so it is a different
// CompetitionClass. Additive, and consistent with the law in CLAUDE.md. The cost
// is recorded in notation §12: nothing in the model says these two definitions
// are one class in the rulebook.
//
// DEVIATION FROM THE RULEBOOK — the 75 m flight cancellation. NZ.4.13(c) would
// otherwise reach this class (it adopts the electric precision-landing table via
// NZ.7.4(c)(ii), which is what scopes NZ.4.13(c)); the parent definition
// (SeedNzMAles200) carries it as a flightValidWhen gate. Per Joe Wurts (senior
// MFNZ Soaring SIG member), verbal ruling 2026-09-27, the NZ NDC format is scored
// WITHOUT the 75 m zero: an outside-75 m landing forfeits the landing bonus and
// keeps its flight points. This definition therefore has NO landedWithin75m flag
// and NO flightValidWhen gate. Recorded in kanban/deferred-decisions.md — revisit
// if the NZMAA next revises Section 5 or if the ruling is withdrawn. The rule
// documents are untouched (house-keeping rule 1); this is a product decision.

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedNzMNdc
{
    private static TaskDefinition TaskD => new()
    {
        Code = "D",
        Name = "Thermal Duration (NDC)",
        Metrics =
        [
            Metric.Number("flightTime", "s", RoundingMode.Truncate, 1),             // NZ.7.4(d)(i)
            Metric.Number("landingDistance", "m", RoundingMode.Ceiling, 1),         // NZ.4.13
            // The two flags are the NZMAA observation protocol's recorded
            // EXCEPTIONS (NZ.7.4(c)(iv)/(v) — see the parent class for the
            // full protocol reading); absence resolves to compliance. The
            // parent's third flag, landedWithin75m (NZ.4.13(c)), is deliberately
            // absent here — see the DEVIATION note in the header.
            Metric.Flag("damagedAndNotSafelyFlyable", whenNotRecorded: false),      // NZ.7.4(c)(iv) — conjunctive; see the parent for the full clause
            Metric.Flag("touchedByCompetitor", whenNotRecorded: false),             // NZ.7.4(c)(v) "touches either the pilot or his helper"
        ],
        Flights = new LastFlight(),                                            // NZ.3.6
        Timing = new()
        {
            Kind = WorkingTimeKind.UntilAllFlightsComplete,                    // NZ.7.4(b)(viii)
            MaxLaunches = 1,
        },

        // no group: NZ.7.4(h)(iii) scores raw, so nothing in this task reads the
        //   group — it affects the running order and never a score.
        // NO normalise (F25). NZ.7.4(h)(iii): "for NDC only, scoring will be the sum
        //   of the four rounds Raw Scores." Because nothing normalises, the landing
        //   bonus belongs in the RAW score here — the parent's ScoreNormalised list
        //   would have no stage to land at, and adoption rejects it (check 14).
        //   Same rulebook class, opposite answer to F24's question.
        //
        // No flightValidWhen gate: NZ.4.13(c) would otherwise apply here, but
        //   the NZ NDC format is scored without it — see the DEVIATION note in
        //   the header (Joe Wurts, senior MFNZ Soaring SIG member,
        //   2026-09-27).

        // Local practice (Joe Wurts, verbal 2026-09-30: "I would go with the
        //   assumption of no negative scores", in reply to max(0, raw) per
        //   round for M+NDC/N/P where NZ.7.4 states no floor): floor the
        //   task-round score at zero. docs/rules/nz/ untouched — cf. 75 m NDC
        //   deviation above.
        FloorAtZero = true,

        Score =
        [
            ScoreTerm.Piecewise("flightTime",                                          // NZ.7.4(d)(ii)
                Bands.From(0)
                     .UpTo(600, 1)                                             // NZ.7.4(h)(i) 10 minute target;
                                                                               //   NZ.7.4(h)(iv) "flight time max is 10min (600 points)"
                     .Rest(-1)),                                               // NZ.7.4(b)(xiv)

            ScoreTerm.When(Predicate.All(Predicate.Is("damagedAndNotSafelyFlyable", false),            // NZ.7.4(c)(iv)
                         Predicate.Is("touchedByCompetitor", false)),                  // NZ.7.4(c)(v)
                   ScoreTerm.Lookup("landingDistance",                                 // NZ.7.4(c)(ii), table at NZ.4.13
                       // exact 0 = paper "beyond the tape" → zero landing points (physically impossible reading, reserved)
                       Rows.UpTo(0, 0)
                           .ThenUpTo(1, 50)
                           .ThenUpTo(2, 45)
                           .ThenUpTo(3, 40)
                           .ThenUpTo(4, 35)
                           .ThenUpTo(5, 30)
                           .ThenUpTo(6, 25)
                           .ThenUpTo(7, 20)
                           .ThenUpTo(8, 15)
                           .ThenUpTo(9, 10)
                           .ThenUpTo(10, 5)
                           .Rest(0))),
        ],
    };

    public static ClassDefinition Definition => new()
    {
        Name = "ALES 200 (NDC format)",
        FaiDesignation = "",
        Version = "NZMAA Section 5 Soaring, October 2024 Rev 3.0",
        // no finalRanking: one phase, so SinglePhase (NZ.7.4(h) has no fly-off)

        // No targetTime parameter: NZ.7.4(h)(i) fixes the rounds at "4 rounds, each
        // of 10 minutes", so the parent's CD discretion (NZ.7.4(b)(vii)) does not
        // apply and the turning point is a rule constant again.
        Parameters =
        [
            Params.Number("minNewGroup"),                                      // NZ.7.4(f)(xii) states no minimum for a re-flight group (F12),
        ],                                                                     //   as the parent

        Reflight = new()
        {
            EntitledScores = ReflightSelection.UndefinedRequiresRuling,         // NZ.7.4(f)(xii), as the parent
            OthersScore = ReflightSelection.UndefinedRequiresRuling,            // NZ.7.4(f)(xii)
            MinNewGroupSize = NumberOrParam.Param("minNewGroup"),               // NZ.7.4(f)(xii), as the parent (F12)
        },

        Penalties =
        [
            ZeroRound("launchOutsideBuzzerWindow"),                             // NZ.7.4(b)(viii)
            ZeroRound("landedOutsideFieldBounds"),                              // NZ.7.4(e)(i)
            // no launchHeightExceeded: NZ.4.17(c)/2.8.6 are a CD discretion — see the parent
        ],

        // Competition-total floor: NZ rulebook silent — applied per owner decision
        //   2026-09-30 (universal corpus-wide application). The round-grain task
        //   datum (Joe Wurts local practice) is untouched — different datum,
        //   different grain.
        FloorTotalAtZero = true,

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
                    MaxRounds = 4,                                              // NZ.7.4(h)(i) "an NDC contest will comprise 4 rounds"
                },
                Validity = new() { MinRounds = 4 },                             // NZ.7.4(h)(i)
                // no drop: NZ.7.4(h)(iii) "the sum of the four rounds"
                // no tie-breaking: the NZ rules state none anywhere
                //   (docs/rules/nz/00-nz-general-rules.md:117), and Pete's
                //   2026-09-04 ruling fixes what that silence left open:
                //   ties are never broken — equal places ("1st equal") at
                //   every placing
                TieBreaks = [new EqualPlaces()],                               // Pete 2026-09-04: ties stand equal, every placing
                                                                               //   encoding: kanban/completed/tie-break-policy-in-class-definition.md
                Tasks = [TaskD],
            },
        ],
    };

    private static PenaltyDefinition ZeroRound(string infraction) => new()
    {
        InfractionType = infraction,
        Effects = [new(PenaltyEffect.ZeroRound)],
    };

    // ---- arithmetic check --------------------------------------------------
    // NZ.7.4(h)(iv)–(v) state their own maxima, which is rare and useful:
    //   "Flight time max is 10min (600 points) plus landing max of 50. Max round
    //    score of 650."  ->  600 + 50 = 650, per the score block above.
    //   "Max NDC score is 2600 points"  ->  4 x 650 = 2600, per maxRounds 4 and no
    //    discard, with no normalisation to rescale it.
    // Any evaluator that does not produce 2600 for four perfect rounds has either
    // normalised something or applied a discard.
}
