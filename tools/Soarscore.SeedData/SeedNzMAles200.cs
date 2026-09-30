// NZ Class M — ALES 200 (Altitude Limited Electric Soaring)
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, October 2024 Rev 3.0
//            (NZ.7.4, plus NZ.4.13 electric landing table and its 75 m
//            flight cancellation at NZ.4.13(c), NZ.4.17, NZ.3.6)
//
// The first non-FAI class in
// the corpus, and the class that found F24: it adds its landing bonus to the
// NORMALISED flight score, where F5J and F5L add theirs to the raw score and
// normalise the sum. Both are coherent rules; the model could express only F5J's
// until this class was written.
//
// It also found F27: the +1/−1 turning point is the target time the CD announces
// on the day, not a rule constant, so a Band bound has to read a parameter.
//
// The NDC variant of this class scores raw and is a separate definition — and
// drops the 75 m gate by local ruling; see SeedNzMNdc's header.

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedNzMAles200
{
    private static TaskDefinition TaskD => new()
    {
        Code = "D",
        Name = "Thermal Duration",
        Metrics =
        [
            Metric.Number("flightTime", "s", RoundingMode.Truncate, 1),             // NZ.7.4(d)(i) "truncated for scoring purposes"
            Metric.Number("landingDistance", "m", RoundingMode.Ceiling, 1),         // NZ.4.13 "rounded to the next full metre"
            // The metric name carries the WHOLE of NZ.7.4(c)(iv), which is
            // conjunctive: "No landing points will be given if the plane sustains
            // significant damage during the landing AND, IN THE OPINION OF THE
            // CONTEST DIRECTOR OR HIS DESIGNATE, IS NOT SAFELY FLYABLE." Named for
            // the damage alone it asked the scorer a question the rule does not,
            // and a flyable broken canopy lost 50 points — unscaled, because Class
            // M adds landing points after normalising. Deliberately NOT two flags:
            // the CD's opinion is not independently observable, and a second flag
            // would invite the damage to be recorded on its own.
            // The three flags are the NZMAA observation protocol's recorded
            // EXCEPTIONS: NZ.4.13(c) records a cancelled flight when the model does
            // NOT come to rest within 75 m, NZ.7.4(c)(iv)/(v) record "no landing
            // points" rulings when the model is damaged-and-not-flyable or
            // touched. A compliant flight is recorded as its measurements only,
            // so each flag's absence resolves to compliance.
            Metric.Flag("damagedAndNotSafelyFlyable", whenNotRecorded: false),      // NZ.7.4(c)(iv) — recorded exception; absence ⇒ no such damage
            Metric.Flag("touchedByCompetitor", whenNotRecorded: false),             // NZ.7.4(c)(v) "touches EITHER THE PILOT OR HIS HELPER"; absence ⇒ no touch
            Metric.Flag("landedWithin75m", whenNotRecorded: true),                  // NZ.4.13(c) — the outside-75m cancellation is what is recorded; absence ⇒ within
        ],
        Flights = new LastFlight(),                                            // NZ.3.6 one official flight per round
        Timing = new()
        {
            Kind = WorkingTimeKind.UntilAllFlightsComplete,                    // NZ.7.4(b)(viii) one mass launch on a 10 s buzzer; NZ.7.4 sets no
            MaxLaunches = 1,                                                   //   working time, the group flies until the last model is down
        },
        Group = new() { MinPerGroup = NumberOrParam.Param("groupSize") },      // NZ.7.4 "Man-On-Man (Group scored)"
        Normalise = new()
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,                                                // NZ.7.4(d)(iii) "the ratio of the contestants score to that of the
        },                                                                     //   highest score for that flight group and multiplying by
                                                                               //   1000"; no precision stated (F12)
        FlightValidWhen = Predicate.Is("landedWithin75m", true),                       // NZ.4.13(c) "the flight is cancelled and recorded as a zero score"

        // Local practice (Joe Wurts, senior MFNZ Soaring SIG member, verbal
        //   2026-09-30: "I would go with the assumption of no negative scores",
        //   in reply to max(0, raw) per round for M+NDC/N/P where NZ.7.4/7.5/7.7
        //   state no floor): floor the task-round score at zero. docs/rules/nz/
        //   untouched (house-keeping rule 1) — cf. the 75 m NDC deviation.
        FloorAtZero = true,

        // Raw score — flight points only. Cumulative bands, F3B Task A's shape at a
        // parameterised turning point (F27): at target 600 a 700 s flight scores
        // 600x1 + 100x(−1) = 500, not 600. Both sides of the join are the SAME
        // parameter, which the band list carries rather than restates (check 8).
        Score =
        [
            ScoreTerm.Piecewise("flightTime",                                          // NZ.7.4(d)(ii)
                Bands.From(0)
                     .UpTo(NumberOrParam.Param("targetTime"), 1)               // NZ.7.4(b)(xiii) "1 point/second for each second up to and
                                                                               //   including the target time"
                     .Rest(-1)),                                               // NZ.7.4(b)(xiv) "for each second beyond the target time the score
                                                                               //   will be decreased by 1 point/second"
        ],

        // Landing points, added AFTER normalising (F24). This is the whole reason
        // the second term list exists — NZ.7.4(b)(v) "landing points will be added
        // to the normalized flight score", NZ.7.4(d)(iv) "the sum of the pilot's
        // normalized flight score and the landing score".
        ScoreNormalised =
        [
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
        Name = "ALES 200 (Altitude Limited Electric Soaring)",
        FaiDesignation = "",                                                   // a national class; no FAI designation
        Version = "NZMAA Section 5 Soaring, October 2024 Rev 3.0",
        // no finalRanking: one phase, so SinglePhase (NZ.7.4 has no fly-off)

        Parameters =
        [
            Params.Number("targetTime", "s", 600, boundAt: ParameterBindingPoint.BeforeFlying),
                                                                               // NZ.7.4(b)(vi) "a target time announced by the CD. 10 minutes is
                                                                               //   recommended"; NZ.7.4(b)(vii) the CD may change it "based on
                                                                               //   local conditions" — announced at the contestants meeting
                                                                               //   (NZ.4.14(a)), hence BeforeFlying
            Params.Number("groupSize"),                                        // NZ.7.4 states no group size at all (F12)
            Params.Number("minRounds"),                                        // NZ.7.4 states no round count (F12); only the NDC variant fixes one
            Params.Number("minNewGroup"),                                      // NZ.7.4(f)(xii) states no minimum for a re-flight group (F12)
        ],

        // NZ.7.4(f)(xii) grants the re-flight and stops: nothing about placement or
        // which score counts. That is F5L's case exactly, and it is why F26's
        // NotPermitted had to be a separate value rather than a re-reading of this
        // one — Classes N and P, two clauses away in the same rulebook, need the
        // opposite.
        Reflight = new()
        {
            EntitledScores = ReflightSelection.UndefinedRequiresRuling,         // NZ.7.4(f)(xii)
            OthersScore = ReflightSelection.UndefinedRequiresRuling,            // NZ.7.4(f)(xii)
            MinNewGroupSize = NumberOrParam.Param("minNewGroup"),               // NZ.7.4(f)(xii) states no minimum, so the CD decides at setup (F12)
        },

        Penalties =
        [
            ZeroRound("launchOutsideBuzzerWindow"),                             // NZ.7.4(b)(viii) "launched before or after the launch buzzer will
                                                                                //   receive 0 points for the round"
            ZeroRound("landedOutsideFieldBounds"),                              // NZ.7.4(e)(i) "landing beyond the field boundaries will receive
                                                                                //   0 points for the round"

            // NO launchHeightExceeded. NZ.4.17(c) and NZ.4.17(f) both say the CD "MAY
            // assign a score of zero" for a 10% launch overrun — a DISCRETION, not
            // a rule the class can state, in the same category as F3B.2.3 b's
            // midair exception (notation §6). NZ.4.17 applies to all ALES models and
            // is incorporated by all three classes (NZ.7.4(b)(iii), NZ.7.5(e),
            // NZ.7.7(e)(iv)), so recording it here and not in Classes N and P made
            // the four definitions disagree about one rule; both parent rule docs
            // also state it is "not a penalty". Removed rather than propagated.
            // The CD's zero is recorded as a ruling on the Competition.
        ],

        Phases =
        [
            new()
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Validity = new() { MinRounds = NumberOrParam.Param("minRounds") },
                // no drop: NZ.7.4 states no discard
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
}
