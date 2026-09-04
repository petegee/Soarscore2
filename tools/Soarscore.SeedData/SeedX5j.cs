// NZ Class X5J — Unlimited electric-powered sailplane
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, March 2024 (NZ.3.14, plus
//            NZ.2.4.5, NZ.2.4.6, NZ.1.6)
//
// Four flights, each a 10-minute working time flown as motor run then glide,
// scored one point per glide second plus an Electric Precision landing bonus,
// all four summed raw. The class is always flown in decentralised (NDC) style —
// the rulebook class as written IS that format — so there is no separate NDC
// twin here (contrast Class M's NZ.3.12 / NZ.3.12.7 pair, two definitions for
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
            M.Number("glideTime", "s", RoundingMode.Truncate, 1),              // NZ.3.14.2 c/d — glide only, motor run excluded;
                                                                               //   no precision stated (F12 residual): Truncate/1 s
                                                                               //   is CHOSEN here, not cited
            M.Number("motorRestartRunTime", "s", RoundingMode.Truncate, 1),    // NZ.3.14.2 e "subsequent run times"; zero when
                                                                               //   there is no restart
            M.Flag("motorRestarted"),                                          // NZ.3.14.2 e
            M.Flag("airborneAtRoundEnd"),                                      // NZ.3.14.2 (second d) — the per-flight working
                                                                               //   time IS the round here; name matches Class P's idiom
            M.Flag("landedWithin75m"),                                         // NZ.2.4.6
            M.Number("landingDistance", "m", RoundingMode.Ceiling, 1),         // NZ.2.4.5 "rounded to the next full metre"
        ],
        Flights = new LastFlight(),                                            // NZ.1.6 one official flight per round
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 600,                                                 // NZ.3.14.2 b "10 minute working time" per flight;
                                                                               //   the 4 flights are the 4 rounds (NZ.3.14.2 a)
            MaxLaunches = 1,
        },
                                                                               // no preparationTime: NZ.3.14 states none

        // no group: NZ.3.14 states no groups, so nothing in this task reads the
        //   group — an absent Group says the class does not group-score.
        // NO normalise (F25). NZ.3.14.2 a: the 4 flights "are summed to get the
        //   contest score" — raw, no normalisation anywhere, so the landing
        //   bonus belongs in the raw score (contrast Class M's normalised parent).

        FlightValidWhen = P.Is("landedWithin75m", true),                       // NZ.2.4.6
        Score =
        [
            // No cap and no over-time rest band: NZ.3.14.2 (second d) stops the
            //   flight watch at the end of working time, so glideTime can never
            //   exceed the window procedurally. Contrast Class M's Rest(-1) —
            //   NZ.3.12.1 n states that deduction; NZ.3.14 states nothing
            //   beyond the watch stop.
            T.Rate("glideTime", 1),                                            // NZ.3.14.2 d "one point for each second flown on
                                                                               //   the glide … up to the end of the 10 minute
                                                                               //   working time"

            // Unconditional: the metric is 0 when the motor was never
            //   restarted, so this term only bites on a restart.
            T.Rate("motorRestartRunTime", -1),                                 // NZ.3.14.2 e "deducted from the glide score at
                                                                               //   1 point per second"

            T.When(P.All(P.Is("motorRestarted", false),                        // NZ.3.14.2 e "no landing points are awarded"
                         P.Is("airborneAtRoundEnd", false)),                   // NZ.3.14.2 (second d) "no landing points are
                                                                               //   awarded"
                   T.Lookup("landingDistance",                                 // NZ.3.14.2 (second c), table at NZ.2.4.5
                       Rows.UpTo(1, 50).Then(2, 45).Then(3, 40).Then(4, 35)
                           .Then(5, 30).Then(6, 25).Then(7, 20).Then(8, 15)
                           .Then(9, 10).Then(10, 5)
                           .Rest(0))),
        ],
    };

    public static ClassDefinition Definition => new()
    {
        Name = "X5J Unlimited",
        FaiDesignation = "",                                                   // a national class; no FAI designation
        Version = "NZMAA Section 5 Soaring, March 2024",
        // no finalRanking: one phase, so SinglePhase (NZ.3.14 has no fly-off)

        Parameters =
        [
            Params.Number("minNewGroup"),                                      // NZ.3.14 states no minimum for a re-flight group (F12),
        ],                                                                     //   as Class M

        // NZ.3.14 is SILENT on re-flights — neither entitlement nor prohibition.
        //   That is the F26 silence case, distinct from Class M's stated
        //   entitlement (NZ.3.12.5 l) and Classes N/P's stated prohibition
        //   (NZ.3.13.1 h / NZ.3.15.1 h). The ruling fields stay Undefined —
        //   a ruling is required before a re-flight can be scored.
        Reflight = new()
        {
            EntitledScores = ReflightSelection.UndefinedRequiresRuling,         // NZ.3.14 silence (F26)
            OthersScore = ReflightSelection.UndefinedRequiresRuling,            // NZ.3.14 silence (F26)
            MinNewGroupSize = NumberOrParam.Param("minNewGroup"),               // (F12), as Class M
        },

        // no penalty definitions — every consequence in NZ.3.14.2 is derived
        //   from something measured; a motor restart is a score term above,
        //   not a penalty

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
                    MaxRounds = 4,                                              // NZ.3.14.2 a "A Contest consists of 4 flights"
                },
                Validity = new() { MinRounds = 4 },                             // NZ.3.14.2 a
                // no drop: NZ.3.14.2 a "all count"
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
    // NZ.3.14.3 states the per-flight maximum: "could be 600 seconds, less run
    //   time, plus 50 landing points" — e.g. 635 with a 15 s motor run. With a
    //   zero run that is 600 + 50 = 650 per flight, per the score block above.
    //   The bound is procedural, not encoded: NZ.3.14.2 (second d) stops the
    //   flight watch at the end of working time, so glideTime can never exceed
    //   600 less the run time already elapsed off the same watch.
    // Contest max: 4 x 650 = 2600 (NZ.3.14.2 a — all four flights count, no
    //   discard, no normalisation to rescale it).
    // Any evaluator that does not produce 2600 for four perfect flights has
    // either normalised something, applied a discard, or awarded a landing the
    //   rules forfeit.
}
