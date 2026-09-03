// NZ Class N — ALES 123 Open (Altitude Limited Electric Soaring)
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, March 2024 (NZ.3.13, plus
//            NZ.2.4.6, NZ.2.8, NZ.1.6)
//
// One of the two classes that
// found F25: it does not normalise. NZ.3.13.1 i — "each flight counts. The final
// score is the total of all points over three flights" — is raw points summed
// across rounds, and there is no normalisation that leaves scores unchanged, so
// `normalise` had to become optional rather than be satisfied with an invented
// `winner 1000`.
//
// It also found F26 with NZ.3.13.1 h: "no re-flights are permitted" is a definite
// rule, not a rulebook silence.

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedNzNAles123
{
    private static TaskDefinition TaskD => new()
    {
        Code = "D",
        Name = "Duration",
        Metrics =
        [
            M.Number("flightTime", "s", RoundingMode.Truncate, 1),             // NZ.3.13.1 f; no precision stated (F12 residual)
            M.Number("landingDistance", "m", RoundingMode.Truncate, 0.1m),     // NZ.3.13.1 e; no capture precision stated
            M.Flag("motorRestarted"),                                          // NZ.3.13.1 g
            M.Flag("airborneAtRoundEnd"),                                      // NZ.3.13.1 j
            M.Flag("landedWithin75m"),                                         // NZ.2.4.6
        ],
        Flights = new LastFlight(),                                            // NZ.1.6 one official flight per round
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = NumberOrParam.Param("roundDuration"),                // NZ.3.13.1 k
            MaxLaunches = 1,
        },

        // no group: NZ.3.13 never mentions groups and the class is scored
        //   individually — there is no scoring group, rather than a group of one.
        // NO normalise (F25): the raw score below IS the task result, and rounds
        //   aggregate raw points.

        FlightValidWhen = P.Is("landedWithin75m", true),                       // NZ.2.4.6 "the flight is cancelled and recorded as a zero score"
        Score =
        [
            // Cumulative bands: 400 s scores 360x1 + 40x(−1) = 320.
            T.Piecewise("flightTime",                                          // NZ.3.13.1 c
                Bands.From(0)
                     .UpTo(360, 1)                                             // NZ.3.13.1 c "one point for each second flown up to 6 minutes
                                                                               //   (i.e. 360 points)"
                     .Rest(-1)),                                               // NZ.3.13.1 c "then one point lost for each second flown over
                                                                               //   this time"

            // Two ways to lose the landing bonus. NZ.3.13.1 g also stops the watch
            // at the restart, which is a measurement rule the timekeeper applies —
            // only the bonus forfeit is scoring data.
            T.When(P.All(P.Is("motorRestarted", false),                        // NZ.3.13.1 g "landing points will be lost"
                         P.Is("airborneAtRoundEnd", false)),                   // NZ.3.13.1 j "as well as no landing points awarded"
                   T.Lookup("landingDistance",                                 // NZ.3.13.1 e
                       Rows.UpTo(7, 50).Then(15, 25).Rest(0))),
        ],
    };

    public static ClassDefinition Definition => new()
    {
        Name = "ALES 123 Open (Altitude Limited Electric Soaring)",
        FaiDesignation = "",                                                   // a national class; no FAI designation
        Version = "NZMAA Section 5 Soaring, March 2024",
        // no finalRanking: one phase, so SinglePhase (NZ.3.13 has no fly-off)

        Parameters =
        [
            Params.Number("roundDuration", "s", boundAt: ParameterBindingPoint.BeforeFlying),
                                                                               // NZ.3.13.1 k "the duration of each round will be decided by the
                                                                               //   CD taking into account the number of competitors, weather
                                                                               //   conditions and any other pertinent factors" — entirely open (F12)
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.NotPermitted,                    // NZ.3.13.1 h "no re-flights are permitted"
            OthersScore = ReflightSelection.NotPermitted,                       // NZ.3.13.1 h
            // no minNewGroup: NZ.3.13.1 h permits no re-flight, so no new group is
            //   ever formed and the field is INAPPLICABLE, not unstated (F26).
            //   Adoption rejects a populated minNewGroupSize here (check 13).
        },

        // no penalty definitions — every consequence in NZ.3.13 is derived from
        // something measured, as in F5L

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
                    MaxRounds = 3,                                              // NZ.3.13 "three 6 minutes flights over 3 rounds"
                },
                Validity = new() { MinRounds = 3 },                             // NZ.3.13
                // no drop: NZ.3.13.1 i "each flight counts"
                // no tie-breaks: the NZ rules state none anywhere
                //   (docs/rules/nz/00-nz-general-rules.md:117) — silence
                TieBreaks = [new UndefinedRequiresRuling()],                   // docs/rules/nz/00-nz-general-rules.md:117
                                                                               //   encoding: kanban/completed/tie-break-policy-in-class-definition.md
                Tasks = [TaskD],
            },
        ],
    };
}
