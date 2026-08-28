// Aggregate — MFNZ free flight power duration ("aggy")
// Rule refs: MFNZ Flying Rules, Section 2: Free Flight (NZFF.x.y). NZFF.* is a
// THIRD rulebook prefix: MFNZ Section 2, neither NZMAA Section 5 Soaring (NZ.*)
// nor the FAI Sporting Code — the two MFNZ volumes share clause numbering
// (both have a §4), so they are kept apart the same way NZ.* keeps apart from
// the FAI.
//
// The twelfth class, and the first outside RC soaring — a free-flight POWER
// class. Structurally the simplest in the corpus: one phase, one round, one
// task, one metric, unlimited flights inside one 30-minute window, raw seconds
// summed. Nobody's score depends on anybody else's (no group), nothing is
// scaled (no normalise, F25), and every flight counts (no drop). It needed no
// extension the NZ probe (F24-F27) had not already won, and no soaring concept
// anywhere — the model's discipline-agnosticism tested rather than asserted
// (NFR-2).
//
// Not modelled, per the "not scoring data" rule: motor capacity 1.5cc (4.3 a),
// no motor-run limit (4.3 b), launch optional (4.3 c), one model / no major
// component replacement (4.3 g), self-launch and retrieve on foot (4.3 h), the
// 5 m launch point (4.3 i), and the RDT prohibition (1.6 A e) — equipment and
// conduct rules that never reach the scorer. Records (4.3 j) are reporting.

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedAggregate
{
    private static TaskDefinition TaskA => new()
    {
        Code = "A",
        Name = "Aggregate",
        Metrics =
        [
            M.Number("flightTime", "s", RoundingMode.Truncate, 1),  // NZFF.1.7 "recorded to the nearest whole second below";
                                                                    //   the precision is the granularity step, so 1 = whole seconds
        ],
        Flights = new AllFlights(),  // NZFF.4.3 f — every flight in the window accrues toward the total (4.3 j)
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 1800,      // NZFF.4.3 f "flying is for a period of 30 minutes"
            // no MaxLaunches: unlimited — "the object being to accrue the maximum
            //   possible flying time in this period" (NZFF.4.3 f)
        },

        // no group: nobody's score depends on anybody else's — the whole field
        //   flies the one window and placings come from total flying time (NZFF.4.3 j)
        // NO normalise (F25): the score is raw seconds; nothing is scaled (NZFF.4.3 j)

        FlightValidWhen = P.Ge("flightTime", 20),  // NZFF.4.3 d "flights of less than 20 seconds are not recorded";
                                                   //   the same threshold the general no-flight rule states (NZFF.1.4.1 a)
        Score =
        [
            // PerFlight scope is the default: 4.3 e caps EACH flight. At 1 pt/s the
            // cap on the metric consumed and the cap on the points produced coincide.
            T.Rate("flightTime", 1, cap: NumberOrParam.Param("maxFlight")),  // NZFF.4.3 e "180 seconds maximum per flight"
        ],
    };

    public static ClassDefinition Definition => new()
    {
        Name = "Aggregate",
        FaiDesignation = "",  // a national class; no FAI designation
        Version = "MFNZ Section 2: Free Flight, May 2022",
        // no finalRanking: one phase, so SinglePhase (NZFF.4.3 j — placings from total flying times)

        Parameters =
        [
            // NZFF.4.3 e states the 180 s. Reducing it on the day is CD PRACTICE on
            // safety grounds, not a rulebook clause, so 180 is the DEFAULT and the
            // on-the-day choice reaches the event log as a BeforeFlying binding —
            // the same path NZ-P's roundDuration takes for a choice the rules DO
            // leave open.
            Params.Number("maxFlight", "s", @default: 180m, boundAt: ParameterBindingPoint.BeforeFlying),
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.NotPermitted,  // NZFF.4.3 defines no attempt to re-fly: a flight under the
            OthersScore = ReflightSelection.NotPermitted,     //   threshold is simply "not recorded" (4.3 d) and the
                                                              //   competitor relaunches within the window
            // no minNewGroup: no re-flight is ever granted, so no new group is ever
            //   formed and the field is INAPPLICABLE, not unstated (F26)
        },

        // no penalty definitions — every consequence in NZFF.4.3 is derived from
        //   something measured, and the rest is conduct (see the header)

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
                    MaxRounds = 1,  // NZFF.4.3 f — one 30-minute period
                },
                Validity = new() { MinRounds = 1 },  // NZFF.4.3 f — the one period IS the round
                // no drop: NZFF.4.3 j — placings come from the total of ALL flights
                Tasks = [TaskA],
            },
        ],
    };
}
