// NZ Class X5J — Unlimited electric-powered sailplane
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, October 2024 Rev 3.0
//            (NZ.7.6, plus NZ.4.13 and its 75 m flight cancellation at
//            NZ.4.13(c), NZ.3.6)
//
// Four flights, each a 10-minute working time flown as motor run then glide,
// scored one point per glide second plus an Electric Precision landing bonus,
// all four summed raw. The class is always flown in decentralised (NDC) style —
// the rulebook class as written IS that format — so there is no separate NDC
// twin here (contrast Class M's NZ.7.4 / NZ.7.4(h) pair, two definitions for
// one rulebook class). This is the one definition.

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedX5j
{
    private static TaskDefinition TaskD => new()
    {
        Code = "D",
        Name = "Glide Duration",
        Metrics =
        [
            // glideTime and landingDistance are the demanded observations
            // (NZ.7.6(c)(iii)/(iv), NZ.4.13) — no assumption. motorRestartRunTime's
            // content exists ONLY when the NZ.7.6(c)(vii) restart exception occurred
            // (no restart ⇒ no run times to record), so its absence resolves to 0;
            // the other flags are that clause's and NZ.4.13(c)'s recorded EXCEPTIONS.
            Metric.Number("glideTime", "s", RoundingMode.Truncate, 1),              // NZ.7.6(c)(iii)/(iv) — glide only, motor run excluded;
                                                                                //   no precision stated (F12 residual): Truncate/1 s
                                                                                //   is CHOSEN here, not cited
            Metric.Number("motorRestartRunTime", "s", RoundingMode.Truncate, 1,
                whenNotRecorded: 0),                                           // NZ.7.6(c)(vii) "subsequent run times" — only a restart creates
                                                                                //   any; absence ⇒ no restart ⇒ nothing to deduct
            Metric.Flag("motorRestarted", whenNotRecorded: false),                  // NZ.7.6(c)(vii) — the restart (and its landing-points forfeit) is
                                                                                //   what is recorded; absence ⇒ no restart
            Metric.Flag("airborneAtRoundEnd", whenNotRecorded: false),              // NZ.7.6(c)(vi) — the still-airborne-at-round-end ruling
                                                                                //   (watch stops, no landing points) is what is recorded;
                                                                                //   absence ⇒ landed within the round. Name matches Class P's idiom
            Metric.Flag("landedWithin75m", whenNotRecorded: true),                  // NZ.4.13(c) — the outside-75m cancellation is what is recorded;
                                                                                //   absence ⇒ within
            Metric.Number("landingDistance", "m", RoundingMode.Ceiling, 1),         // NZ.4.13 "rounded to the next full metre"
        ],
        Flights = new LastFlight(),                                            // NZ.3.6 one official flight per round
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 600,                                                 // NZ.7.6(c)(ii) "10 minute working time" per flight;
                                                                               //   the 4 flights are the 4 rounds (NZ.7.6(c)(i))
            MaxLaunches = 1,
        },
                                                                               // no preparationTime: NZ.7.6 states none

        // no group: NZ.7.6 states no groups, so nothing in this task reads the
        //   group — an absent Group says the class does not group-score.
        // NO normalise (F25). NZ.7.6(c)(i): the 4 flights "are summed to get the
        //   contest score" — raw, no normalisation anywhere, so the landing
        //   bonus belongs in the raw score (contrast Class M's normalised parent).

        FlightValidWhen = Predicate.Is("landedWithin75m", true),                       // NZ.4.13(c)
        Score =
        [
            // No cap and no over-time rest band: NZ.7.6(c)(vi) stops the
            //   flight watch at the end of working time, so glideTime can never
            //   exceed the window procedurally. Contrast Class M's Rest(-1) —
            //   NZ.7.4(b)(xiv) states that deduction; NZ.7.6 states nothing
            //   beyond the watch stop.
            ScoreTerm.Rate("glideTime", 1),                                            // NZ.7.6(c)(iv) "one point for each second flown on
                                                                               //   the glide … up to the end of the 10 minute
                                                                               //   working time"

            // Unconditional: the metric is 0 when the motor was never
            //   restarted, so this term only bites on a restart.
            ScoreTerm.Rate("motorRestartRunTime", -1),                                 // NZ.7.6(c)(vii) "deducted from the glide score at
                                                                               //   1 point per second"

            ScoreTerm.When(Predicate.All(Predicate.Is("motorRestarted", false),                        // NZ.7.6(c)(vii) "no landing points are awarded"
                         Predicate.Is("airborneAtRoundEnd", false)),                   // NZ.7.6(c)(vi) "no landing points are
                                                                               //   awarded"
                   ScoreTerm.Lookup("landingDistance",                                 // NZ.7.6(c)(v), table at NZ.4.13
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
        Name = "X5J Unlimited",
        FaiDesignation = "",                                                   // a national class; no FAI designation
        Version = "NZMAA Section 5 Soaring, October 2024 Rev 3.0",
        // no finalRanking: one phase, so SinglePhase (NZ.7.6 has no fly-off)

        Parameters =
        [
            Params.Number("minNewGroup"),                                      // NZ.7.6 states no minimum for a re-flight group (F12),
        ],                                                                     //   as Class M

        // NZ.7.6 is SILENT on re-flights — neither entitlement nor prohibition.
        //   That is the F26 silence case, distinct from Class M's stated
        //   entitlement (NZ.7.4(f)(xii)) and Classes N/P's stated prohibition
        //   (NZ.7.5(i) / NZ.7.7(e)(viii)). The ruling fields stay Undefined —
        //   a ruling is required before a re-flight can be scored.
        Reflight = new()
        {
            EntitledScores = ReflightSelection.UndefinedRequiresRuling,         // NZ.7.6 silence (F26)
            OthersScore = ReflightSelection.UndefinedRequiresRuling,            // NZ.7.6 silence (F26)
            MinNewGroupSize = NumberOrParam.Param("minNewGroup"),               // (F12), as Class M
        },

        // no penalty definitions — every consequence in NZ.7.6(c) is derived
        //   from something measured; a motor restart is a score term above,
        //   not a penalty

        // Competition-total floor: NZ rulebook silent — applied per owner decision
        //   2026-09-30 (universal corpus-wide application).
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
                    MaxRounds = 4,                                              // NZ.7.6(c)(i) "A Contest consists of 4 flights"
                },
                Validity = new() { MinRounds = 4 },                             // NZ.7.6(c)(i)
                // no drop: NZ.7.6(c)(i) "all count"
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

    // ---- arithmetic check --------------------------------------------------
    // NZ.7.6(d) states the per-flight maximum: "could be 600 seconds, less run
    //   time, plus 50 landing points" — e.g. 635 with a 15 s motor run. With a
    //   zero run that is 600 + 50 = 650 per flight, per the score block above.
    //   The bound is procedural, not encoded: NZ.7.6(c)(vi) stops the
    //   flight watch at the end of working time, so glideTime can never exceed
    //   600 less the run time already elapsed off the same watch.
    // Contest max: 4 x 650 = 2600 (NZ.7.6(c)(i) — all four flights count, no
    //   discard, no normalisation to rescale it).
    // Any evaluator that does not produce 2600 for four perfect flights has
    // either normalised something, applied a discard, or awarded a landing the
    //   rules forfeit.
}
