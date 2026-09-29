// NZ Class N — ALES 123 Open (Altitude Limited Electric Soaring)
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, October 2024 Rev 3.0
//            (NZ.7.5, plus NZ.4.17, NZ.3.6)
//
// One of the two classes that
// found F25: it does not normalise. NZ.7.5(j) — "each flight counts. The final
// score is the total of all points over three flights" — is raw points summed
// across rounds, and there is no normalisation that leaves scores unchanged, so
// `normalise` had to become optional rather than be satisfied with an invented
// `winner 1000`.
//
// It also found F26 with NZ.7.5(i): "no re-flights are permitted" is a definite
// rule, not a rulebook silence.
//
// The 75 m flight cancellation (NZ.4.13(c)) does NOT reach this class: it is
// scoped to the classes that adopt the electric precision-landing table
// (NZ.4.13) and this class uses its own three-step bonus (NZ.7.5(f)) without
// referencing that table. (The superseded March 2024 edition carried the rule
// as a general clause and was read as reaching here — see
// kanban/deferred-decisions.md.) A landing outside 15 m forfeits the bonus
// only; the flight stands.

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
            Metric.Number("flightTime", "s", RoundingMode.Truncate, 1),             // NZ.7.5(g); no precision stated (F12 residual)
            Metric.Number("landingDistance", "m", RoundingMode.Truncate, 0.1m),     // NZ.7.5(f); no capture precision stated
            // The two flags are the NZMAA observation protocol's recorded
            // EXCEPTIONS; absence resolves to compliance.
            Metric.Flag("motorRestarted", whenNotRecorded: false),                  // NZ.7.5(h) — the restart (watch stops, landing points lost) is
                                                                                //   what is recorded; absence ⇒ no restart
            Metric.Flag("airborneAtRoundEnd", whenNotRecorded: false),              // NZ.7.5(k) — the still-airborne-at-round-end ruling is what is
                                                                                //   recorded; absence ⇒ landed within the round
        ],
        Flights = new LastFlight(),                                            // NZ.3.6 one official flight per round
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = NumberOrParam.Param("roundDuration"),                // NZ.7.5(l)
            MaxLaunches = 1,
        },

        // no group: NZ.7.5 never mentions groups and the class is scored
        //   individually — there is no scoring group, rather than a group of one.
        // NO normalise (F25): the raw score below IS the task result, and rounds
        //   aggregate raw points.
        // No flightValidWhen gate: the 75 m cancellation does not reach this
        //   class (see the header).

        Score =
        [
            // Cumulative bands: 400 s scores 360x1 + 40x(−1) = 320.
            ScoreTerm.Piecewise("flightTime",                                          // NZ.7.5(d)
                Bands.From(0)
                     .UpTo(360, 1)                                             // NZ.7.5(d) "one point for each second flown up to 6 minutes
                                                                               //   (i.e. 360 points)"
                     .Rest(-1)),                                               // NZ.7.5(d) "then one point lost for each second flown over
                                                                               //   this time"

            // Two ways to lose the landing bonus. NZ.7.5(h) also stops the watch
            // at the restart, which is a measurement rule the timekeeper applies —
            // only the bonus forfeit is scoring data.
            ScoreTerm.When(Predicate.All(Predicate.Is("motorRestarted", false),                        // NZ.7.5(h) "landing points will be lost"
                         Predicate.Is("airborneAtRoundEnd", false)),                   // NZ.7.5(k) "as well as no landing points awarded"
                   ScoreTerm.Lookup("landingDistance",                                 // NZ.7.5(f)
                       // exact 0 = paper "beyond the tape" → zero landing points (physically impossible reading, reserved)
                       Rows.UpTo(0, 0)
                           .ThenUpTo(7, 50)
                           .ThenUpTo(15, 25)
                           .Rest(0))),
        ],
    };

    public static ClassDefinition Definition => new()
    {
        Name = "ALES 123 Open (Altitude Limited Electric Soaring)",
        FaiDesignation = "",                                                   // a national class; no FAI designation
        Version = "NZMAA Section 5 Soaring, October 2024 Rev 3.0",
        // no finalRanking: one phase, so SinglePhase (NZ.7.5 has no fly-off)

        Parameters =
        [
            Params.Number("roundDuration", "s", boundAt: ParameterBindingPoint.BeforeFlying),
                                                                               // NZ.7.5(l) "the duration of each round will be decided by the
                                                                               //   CD taking into account the number of competitors, weather
                                                                               //   conditions and any other pertinent factors" — entirely open (F12)
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.NotPermitted,                    // NZ.7.5(i) "no re-flights are permitted"
            OthersScore = ReflightSelection.NotPermitted,                       // NZ.7.5(i)
            // no minNewGroup: NZ.7.5(i) permits no re-flight, so no new group is
            //   ever formed and the field is INAPPLICABLE, not unstated (F26).
            //   Adoption rejects a populated minNewGroupSize here (check 13).
        },

        // no penalty definitions — every consequence in NZ.7.5 is derived from
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
                    MaxRounds = 3,                                              // NZ.7.5 "three 6 minutes flights over 3 rounds"
                },
                Validity = new() { MinRounds = 3 },                             // NZ.7.5
                // no drop: NZ.7.5(j) "each flight counts"
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
}
