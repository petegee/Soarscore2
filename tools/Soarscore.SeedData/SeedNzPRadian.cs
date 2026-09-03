// NZ Class P — ALES Radian (or similar 2 m all-foam electric glider)
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, March 2024 (NZ.3.15, plus
//            NZ.2.4.6, NZ.2.8, NZ.1.6)
//
// Class N's shape with a 7
// minute target and a 200 m limit, written out in full rather than derived from
// Class N: `like` is a task-level shortcut within ONE class definition and these
// are two classes. That is a real cost of PhaseDefinition owning its tasks, and
// it is worth seeing.
//
// Its own contribution to the corpus is a gap rather than an extension: NZ.3.15
// makes group scoring a CD choice, and no ParameterRef slot can reach a
// Normalisation. See the foot of this file.

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedNzPRadian
{
    private static TaskDefinition TaskD => new()
    {
        Code = "D",
        Name = "Duration",
        Metrics =
        [
            M.Number("flightTime", "s", RoundingMode.Truncate, 1),             // NZ.3.15.1 f; no precision stated (F12 residual)
            M.Number("landingDistance", "m", RoundingMode.Truncate, 0.1m),     // NZ.3.15.1 e; no capture precision stated
            M.Flag("motorRestarted"),                                          // NZ.3.15.1 g
            M.Flag("airborneAtRoundEnd"),                                      // NZ.3.15.1 j — but see the note at the foot
            M.Flag("landedWithin75m"),                                         // NZ.2.4.6
        ],
        Flights = new LastFlight(),                                            // NZ.1.6 one official flight per round
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = NumberOrParam.Param("roundDuration"),                // NZ.3.15.1 k
            MaxLaunches = 1,
        },

        // no group: individual scoring, so there is no scoring group; a
        //   group-scored Class P is not writable — see the note at the foot.
        // NO normalise (F25). NZ.3.15.1 i: "the final score is the total of all
        //   points over three flights". This is the NDC-eligible form of the class.

        FlightValidWhen = P.Is("landedWithin75m", true),                       // NZ.2.4.6
        Score =
        [
            // Cumulative bands: 450 s scores 420x1 + 30x(−1) = 390.
            T.Piecewise("flightTime",                                          // NZ.3.15.1 c
                Bands.From(0)
                     .UpTo(420, 1)                                             // NZ.3.15.1 c "one point for each second flown up to 7 minutes
                                                                               //   (i.e. 420 points)"
                     .Rest(-1)),                                               // NZ.3.15.1 c "then one point lost for each second flown over
                                                                               //   this time"

            T.When(P.All(P.Is("motorRestarted", false),                        // NZ.3.15.1 g "landing points will be lost"
                         P.Is("airborneAtRoundEnd", false)),                   // NZ.3.15.1 j, read per NZ.3.13.1 j — see below
                   T.Lookup("landingDistance",                                 // NZ.3.15.1 e
                       Rows.UpTo(7, 50).Then(15, 25).Rest(0))),
        ],
    };

    public static ClassDefinition Definition => new()
    {
        Name = "ALES Radian (2 m all-foam electric glider)",
        FaiDesignation = "",                                                   // a national class; no FAI designation
        Version = "NZMAA Section 5 Soaring, March 2024",
        // no finalRanking: one phase, so SinglePhase (NZ.3.15 has no fly-off)

        Parameters =
        [
            Params.Number("roundDuration", "s", boundAt: ParameterBindingPoint.BeforeFlying),
                                                                               // NZ.3.15.1 k "decided by the CD taking into account the number
                                                                               //   of competitors, weather conditions etc. For example each
                                                                               //   round could be 1 hour" (F12)
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.NotPermitted,                    // NZ.3.15.1 h "no re-flights are permitted"
            OthersScore = ReflightSelection.NotPermitted,                       // NZ.3.15.1 h
            // no minNewGroup: NZ.3.15.1 h permits no re-flight, so no new group is
            //   ever formed and the field is INAPPLICABLE, not unstated (F26)
        },

        // no penalty definitions — every consequence in NZ.3.15 is derived from
        // something measured

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
                    MaxRounds = 3,                                              // NZ.3.15 "three 7 minutes flights over 3 rounds"
                },
                Validity = new() { MinRounds = 3 },                             // NZ.3.15
                // no drop: NZ.3.15.1 i "each flight counts"
                // no tie-breaks: the NZ rules state none anywhere
                //   (docs/rules/nz/00-nz-general-rules.md:117) — silence
                TieBreaks = [new UndefinedRequiresRuling()],                   // docs/rules/nz/00-nz-general-rules.md:117
                                                                               //   encoding: kanban/completed/tie-break-policy-in-class-definition.md
                Tasks = [TaskD],
            },
        ],
    };

    // ---- RULES QUERY, not a model gap --------------------------------------
    // NZ.3.15.1 j reads: "The model must be airborne at the end of the round the
    // flight time for the flight & landing to count." As written that requires a
    // model to be STILL FLYING for its landing to score, which cannot be meant.
    // The parallel Class N clause NZ.3.13.1 j says the opposite and makes sense:
    // "if the model is still airborne at the end of the round the flight time
    // stops at that point as well as no landing points awarded."
    // This definition follows Class N. The reading should be confirmed with the
    // NZMAA before this class is used to score a contest, and per house-keeping
    // rule 1 the rule document itself is NOT to be edited to match.
    //
    // ---- unresolved --------------------------------------------------------
    // GROUP SCORING IS A CD CHOICE AND IS NOT WRITABLE. NZ.3.15 preamble: "A
    // Contest Director may decide to mass launch groups of pilots … The CD may use
    // group scoring in this instance but points will not be eligible for any
    // record claims or NDC." Whether the task normalises is therefore bound at
    // setup, and Normalisation is a value object, so no ParameterRef slot reaches
    // it (the same residual F12 hit on Rounding). This definition writes the
    // individual form, which is the one that counts for NDC.
}
