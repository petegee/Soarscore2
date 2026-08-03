// NZ Class M — ALES 200 (Altitude Limited Electric Soaring)
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, March 2024 (NZ.3.12, plus
//            NZ.2.4.5 electric landing table, NZ.2.4.6, NZ.2.8, NZ.1.6)
//
// Transcribed from seed-data/80-nz-m-ales200.class. The first non-FAI class in
// the corpus, and the class that found F24: it adds its landing bonus to the
// NORMALISED flight score, where F5J and F5L add theirs to the raw score and
// normalise the sum. Both are coherent rules; the model could express only F5J's
// until this class was written.
//
// It also found F27: the +1/−1 turning point is the target time the CD announces
// on the day, not a rule constant, so a Band bound has to read a parameter.
//
// The NDC variant of this class scores raw and is a separate definition.

using Soarscore.Domain.CompetitionClasses;

namespace Soarscore.SeedData;

public static class SeedNzMAles200
{
    private static TaskDefinition TaskD => new()
    {
        Code = "D",
        Name = "Thermal Duration",
        Metrics =
        [
            M.Number("flightTime", "s", RoundingMode.Truncate, 1),             // NZ.3.12.3 a "truncated for scoring purposes"
            M.Number("landingDistance", "m", RoundingMode.Ceiling, 1),         // NZ.2.4.5 "rounded to the next full metre"
            // The metric name carries the WHOLE of NZ.3.12.2 d, which is
            // conjunctive: "No landing points will be given if the plane sustains
            // significant damage during the landing AND, IN THE OPINION OF THE
            // CONTEST DIRECTOR OR HIS DESIGNATE, IS NOT SAFELY FLYABLE." Named for
            // the damage alone it asked the scorer a question the rule does not,
            // and a flyable broken canopy lost 50 points — unscaled, because Class
            // M adds landing points after normalising. Deliberately NOT two flags:
            // the CD's opinion is not independently observable, and a second flag
            // would invite the damage to be recorded on its own.
            M.Flag("damagedAndNotSafelyFlyable"),                              // NZ.3.12.2 d
            M.Flag("touchedByCompetitor"),                                     // NZ.3.12.2 e "touches EITHER THE PILOT OR HIS HELPER"
            M.Flag("landedWithin75m"),                                         // NZ.2.4.6
        ],
        Flights = new LastFlight(),                                            // NZ.1.6 one official flight per round
        Timing = new()
        {
            Kind = WorkingTimeKind.UntilAllFlightsComplete,                    // NZ.3.12.1 h one mass launch on a 10 s buzzer; NZ.3.12 sets no
            MaxLaunches = 1,                                                   //   working time, the group flies until the last model is down
        },
        Group = new() { MinPerGroup = NumberOrParam.Param("groupSize") },      // NZ.3.12 "Man-On-Man (Group scored)"
        Normalise = new()
        {
            Direction = NormalisationDirection.HigherIsBetter,
            WinnerScore = 1000,                                                // NZ.3.12.3 c "the ratio of the contestants score to that of the
        },                                                                     //   highest score for that flight group and multiplying by
                                                                               //   1000"; no precision stated (F12)
        FlightValidWhen = P.Is("landedWithin75m", true),                       // NZ.2.4.6 "the flight is cancelled and recorded as a zero score"

        // Raw score — flight points only. Cumulative bands, F3B Task A's shape at a
        // parameterised turning point (F27): at target 600 a 700 s flight scores
        // 600x1 + 100x(−1) = 500, not 600. Both sides of the join are the SAME
        // parameter, which the band list carries rather than restates (check 8).
        Score =
        [
            T.Piecewise("flightTime",                                          // NZ.3.12.3 b
                Bands.From(0)
                     .UpTo(NumberOrParam.Param("targetTime"), 1)               // NZ.3.12.1 m "1 point/second for each second up to and
                                                                               //   including the target time"
                     .Rest(-1)),                                               // NZ.3.12.1 n "for each second beyond the target time the score
                                                                               //   will be decreased by 1 point/second"
        ],

        // Landing points, added AFTER normalising (F24). This is the whole reason
        // the second term list exists — NZ.3.12.1 e "landing points will be added
        // to the normalized flight score", NZ.3.12.3 d "the sum of the pilot's
        // normalized flight score and the landing score".
        ScoreNormalised =
        [
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
        Name = "ALES 200 (Altitude Limited Electric Soaring)",
        FaiDesignation = "",                                                   // a national class; no FAI designation
        Version = "NZMAA Section 5 Soaring, March 2024",
        // no finalRanking: one phase, so SinglePhase (NZ.3.12 has no fly-off)

        Parameters =
        [
            Params.Number("targetTime", "s", 600, boundAt: ParameterBindingPoint.BeforeFlying),
                                                                               // NZ.3.12.1 f "a target time announced by the CD. 10 minutes is
                                                                               //   recommended"; NZ.3.12.1 g the CD may change it "based on
                                                                               //   local conditions" — announced at the contestants meeting
                                                                               //   (NZ.2.5.1), hence BeforeFlying
            Params.Number("groupSize"),                                        // NZ.3.12 states no group size at all (F12)
            Params.Number("minRounds"),                                        // NZ.3.12 states no round count (F12); only the NDC variant fixes one
            Params.Number("minNewGroup"),                                      // NZ.3.12.5 l states no minimum for a re-flight group (F12)
        ],

        // NZ.3.12.5 l grants the re-flight and stops: nothing about placement or
        // which score counts. That is F5L's case exactly, and it is why F26's
        // NotPermitted had to be a separate value rather than a re-reading of this
        // one — Classes N and P, two clauses away in the same rulebook, need the
        // opposite.
        Reflight = new()
        {
            EntitledScores = ReflightSelection.UndefinedRequiresRuling,         // NZ.3.12.5 l
            OthersScore = ReflightSelection.UndefinedRequiresRuling,            // NZ.3.12.5 l
            MinNewGroupSize = NumberOrParam.Param("minNewGroup"),               // NZ.3.12.5 l states no minimum, so the CD decides at setup (F12)
        },

        Penalties =
        [
            ZeroRound("launchOutsideBuzzerWindow"),                             // NZ.3.12.1 h "launched before or after the launch buzzer will
                                                                                //   receive 0 points for the round"
            ZeroRound("landedOutsideFieldBounds"),                              // NZ.3.12.4 a "landing beyond the field boundaries will receive
                                                                                //   0 points for the round"

            // NO launchHeightExceeded. NZ.2.8.3 and NZ.2.8.6 both say the CD "MAY
            // assign a score of zero" for a 10% launch overrun — a DISCRETION, not
            // a rule the class can state, in the same category as F3B.2.3 b's
            // midair exception (notation §6). NZ.2.8 applies to all ALES models and
            // is incorporated by all three classes (NZ.3.12.1 c, NZ.3.13.1 d,
            // NZ.3.15.1 d), so recording it here and not in Classes N and P made
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
                // no drop: NZ.3.12 states no discard
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
