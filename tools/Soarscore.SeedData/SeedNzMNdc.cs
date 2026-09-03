// NZ Class M — ALES 200, NDC format
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, March 2024 (NZ.3.12.7,
//            which incorporates NZ.3.12.1-3.12.6 except for the scoring)
//
// NZ.3.12.7 is the National
// Decentralized Contest format of the same rulebook class as ALES 200. It fixes
// the round count at four, fixes the target time at ten minutes, and — the reason
// it cannot be a parameter binding on the parent — scores "the sum of the four
// rounds RAW scores", with no normalisation at all.
//
// That is a different pipeline, not a different number, so it is a different
// CompetitionClass. Additive, and consistent with the law in CLAUDE.md. The cost
// is recorded in notation §12: nothing in the model says these two definitions
// are one class in the rulebook.

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
            M.Number("flightTime", "s", RoundingMode.Truncate, 1),             // NZ.3.12.3 a
            M.Number("landingDistance", "m", RoundingMode.Ceiling, 1),         // NZ.2.4.5
            M.Flag("damagedAndNotSafelyFlyable"),                              // NZ.3.12.2 d — conjunctive; see the parent for the full clause
            M.Flag("touchedByCompetitor"),                                     // NZ.3.12.2 e "touches either the pilot or his helper"
            M.Flag("landedWithin75m"),                                         // NZ.2.4.6
        ],
        Flights = new LastFlight(),                                            // NZ.1.6
        Timing = new()
        {
            Kind = WorkingTimeKind.UntilAllFlightsComplete,                    // NZ.3.12.1 h
            MaxLaunches = 1,
        },

        // no group: NZ.3.12.7 c scores raw, so nothing in this task reads the
        //   group — it affects the running order and never a score.
        // NO normalise (F25). NZ.3.12.7 c: "for NDC only, scoring will be the sum
        //   of the four rounds Raw Scores." Because nothing normalises, the landing
        //   bonus belongs in the RAW score here — the parent's ScoreNormalised list
        //   would have no stage to land at, and adoption rejects it (check 14).
        //   Same rulebook class, opposite answer to F24's question.

        FlightValidWhen = P.Is("landedWithin75m", true),                       // NZ.2.4.6
        Score =
        [
            T.Piecewise("flightTime",                                          // NZ.3.12.3 b
                Bands.From(0)
                     .UpTo(600, 1)                                             // NZ.3.12.7 a 10 minute target;
                                                                               //   NZ.3.12.7 c i "flight time max is 10min (600 points)"
                     .Rest(-1)),                                               // NZ.3.12.1 n

            T.When(P.All(P.Is("damagedAndNotSafelyFlyable", false),            // NZ.3.12.2 d
                         P.Is("touchedByCompetitor", false)),                  // NZ.3.12.2 e
                   T.Lookup("landingDistance",                                 // NZ.3.12.2 b, table at NZ.2.4.5
                       Rows.UpTo(1, 50).Then(2, 45).Then(3, 40).Then(4, 35)
                           .Then(5, 30).Then(6, 25).Then(7, 20).Then(8, 15)
                           .Then(9, 10).Then(10, 5)
                           .Rest(0))),
        ],
    };

    public static ClassDefinition Definition => new()
    {
        Name = "ALES 200 (NDC format)",
        FaiDesignation = "",
        Version = "NZMAA Section 5 Soaring, March 2024",
        // no finalRanking: one phase, so SinglePhase (NZ.3.12.7 has no fly-off)

        // No targetTime parameter: NZ.3.12.7 a fixes the rounds at "4 rounds, each
        // of 10 minutes", so the parent's CD discretion (NZ.3.12.1 g) does not
        // apply and the turning point is a rule constant again.
        Parameters =
        [
            Params.Number("minNewGroup"),                                      // NZ.3.12.5 l states no minimum for a re-flight group (F12),
        ],                                                                     //   as the parent

        Reflight = new()
        {
            EntitledScores = ReflightSelection.UndefinedRequiresRuling,         // NZ.3.12.5 l, as the parent
            OthersScore = ReflightSelection.UndefinedRequiresRuling,            // NZ.3.12.5 l
            MinNewGroupSize = NumberOrParam.Param("minNewGroup"),               // NZ.3.12.5 l, as the parent (F12)
        },

        Penalties =
        [
            ZeroRound("launchOutsideBuzzerWindow"),                             // NZ.3.12.1 h
            ZeroRound("landedOutsideFieldBounds"),                              // NZ.3.12.4 a
            // no launchHeightExceeded: NZ.2.8.3/2.8.6 are a CD discretion — see the parent
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
                    MaxRounds = 4,                                              // NZ.3.12.7 a "an NDC contest will comprise 4 rounds"
                },
                Validity = new() { MinRounds = 4 },                             // NZ.3.12.7 a
                // no drop: NZ.3.12.7 c "the sum of the four rounds"
                // no tie-breaks: the NZ rules state none anywhere
                //   (docs/rules/nz/00-nz-general-rules.md:117) — silence
                TieBreaks = [new UndefinedRequiresRuling()],                   // docs/rules/nz/00-nz-general-rules.md:117
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
    // NZ.3.12.7 c states its own maxima, which is rare and useful:
    //   "Flight time max is 10min (600 points) plus landing max of 50. Max round
    //    score of 650."  ->  600 + 50 = 650, per the score block above.
    //   "Max NDC score is 2600 points"  ->  4 x 650 = 2600, per maxRounds 4 and no
    //    discard, with no normalisation to rescale it.
    // Any evaluator that does not produce 2600 for four perfect rounds has either
    // normalised something or applied a discard.
}
