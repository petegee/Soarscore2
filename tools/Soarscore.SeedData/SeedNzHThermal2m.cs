// NZ Class H — Thermal 2 Metre ("New Zealand Thermal 2 Metre Rules")
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, October 2024 Rev 3.0
//            (NZ.5.5; general clauses cited as NZ.3.x / NZ.4.x)
//
// Five flights against fixed targets of 3, 4, 5, 6 and 7 minutes, flown in any
// order inside a CD-set contest window, each flight nominated to its task only
// AFTER it lands (NZ.5.5(d)(ii)); all flight and landing scores summed RAW
// (NZ.5.5(e)(iv)) — no normalisation, no groups, no drop. The class as written
// is already the NDC-eligible frame (raw sum, no groups) and NZ.5.5 states no
// NDC variant (contrast Class M's NZ.7.4(h)), so this is one definition, not a
// twin (SeedX5j's reasoning). The nomination is the recording act: a flight is
// recorded against the task it is nominated to; the draw's round order is a
// running-order convenience only (NFR-4).
//
// Class H is the first modelled NZ class that is TOW-LAUNCHED (NZ.5.5(c)(i)
// hand tow / hand-operated pulley tow), so NZ.3.6(b)'s repeat-attempt grounds
// reach it — contrast the self-launched ALES classes, which that clause
// excludes. Its landing bonus is the class's OWN single-step rule
// (NZ.5.5(f)(i)), not the NZ.4.12 gliding table (100→30) or the NZ.4.13
// electric table.
//
// OWNER DECISIONS 2026-09-27 (Pete) — kanban/in-progress/nz-class-h-thermal-2m-seed.md:
//   1. Validity MinRounds = 5, fixed: (d)(ii) "required to complete flights of
//      3,4,5,6 & 7 minutes" is treated as stated (the F3K-NDC ruling shape;
//      the F5K/NZ-Class-M no-default-param pattern was considered and
//      declined). Consequence accepted: a weather-truncated contest
//      ((d)(iii)) scores invalid for a competitor with fewer than five
//      flights.
//   2. The (e)(iii) >60 s forfeit zeroes the WHOLE flight — flight points AND
//      the landing bonus. Encoded as a FlightValidWhen gate (F17). The
//      alternative reading (flight points only, landing survives, via a
//      ConditionalTerm wrapper) was rejected; (f)'s explicit "flight and
//      landing points" enumeration was read as scope-clarifying, not
//      contrastive.
//   3. Repeat attempts (NZ.3.6(b)) are encoded as NOTHING: the grounds reach
//      this class but state no scoring outcome, so Reflight stays
//      UndefinedRequiresRuling (the M-NDC precedent for stated entitlement,
//      unstated outcome — NZ.7.4(f)(xii)) and MaxLaunches is unset. A repeat
//      is CD workflow; its recorded evidence is an additional attempt that
//      LastFlight selection ignores. The system does not enforce the
//      NZ.3.6(c)(vii) once-per-official-flight limit or its grounds.
//   4. contestTime is a no-default parameter (NZ.5.5(d)(i) "the contest
//      director may determine", "suggested that 3 hours"): the CD chooses at
//      setup and the choice reaches the event log (F12). NO scoring stage
//      consumes it — the expiry rule (d)(iii) is contest flow, not scoring —
//      which is legal by the F3F unconsumed-parameter precedent
//      (SeedF3F's reflightAfterNPilots).

using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedNzHThermal2m
{
    private static TaskDefinition TaskFor(int minutes) => new()
    {
        Code = $"{minutes}",
        Name = $"{minutes}-minute flight",
        Metrics =
        [
            Metric.Number("flightTime", "s", RoundingMode.Truncate, 1),             // NZ.5.5(e)(i)-(ii) — points are per second; no capture precision
                                                                                    //   stated (F12 residual): Truncate/1 s is CHOSEN here, not cited
            Metric.Number("landingDistance", "m", RoundingMode.Ceiling, 1),         // NZ.5.5(f)(i) — measured to the nose; no capture precision stated
                                                                                    //   (F12 residual): Ceiling/1 m is CHOSEN (the NZ.4.13
                                                                                    //   "next full metre" convention; never awards a bonus to a
                                                                                    //   genuinely-outside reading at the 15 m boundary)
        ],
        Flights = new LastFlight(),                                            // NZ.5.5(d)(ii) — the nominated flight is the flight; a NZ.3.6(b)
                                                                               //   repeat is an additional attempt this selection ignores
                                                                               //   (owner decision 3)
        Timing = new()
        {
            Kind = WorkingTimeKind.UntilAllFlightsComplete,                    // NZ.5.5(d)(ii) "when to launch is up to the individual" — there is
                                                                               //   no per-flight window to state; the only boundary is the CD's
                                                                               //   contest window (NZ.5.5(d)(i), the contestTime parameter)
            // MaxLaunches unset: NZ.5.5 states no launch limit; repeat attempts
            //   are NZ.3.6(b)'s CD workflow (owner decision 3)
        },

        // no group: NZ.5.5 never groups pilots — individual scoring throughout
        //   ((e)(iv)), so nothing in this task reads the group.
        // NO normalise (F25). NZ.5.5(e)(iv): "All flight and landing scores will
        //   count towards the individual's total. The competitor with the
        //   highest accumulated score wins." Raw points, no normalisation
        //   anywhere — so the landing bonus belongs in the RAW score here (the
        //   ScoreNormalised list would have no stage to land at; check 14).

        // The (e)(iii) forfeit: "More than 60 secs more than the target time
        //   will result in all the points for that flight being forfeited."
        //   flightValidWhen states the COMPLIANT condition and zeroes the
        //   flight when it fails (F17's zero-the-flight gate — the same
        //   mechanism as the >75 m cancellation). Owner decision 2: the whole
        //   flight — flight points and landing bonus — zeroes. Strictly
        //   greater than 60 s over forfeits: at exactly 60 s over the flight
        //   still scores target − 60.
        FlightValidWhen = Predicate.LessThanOrEqual("flightTime", minutes * 60 + 60),   // NZ.5.5(e)(iii)

        Score =
        [
            ScoreTerm.Piecewise("flightTime",                                          // NZ.5.5(e)(i)-(ii)
                Bands.From(0)
                     .UpTo(minutes * 60, 1)                                    // NZ.5.5(e)(i) one point per second up to the target
                     .Rest(-1)),                                               // NZ.5.5(e)(ii) one point deducted per second over

            // Ungated: NZ.5.5 adopts no observation-protocol clause (contrast
            //   NZ.7.4(c)); landing conduct is NZ.4.11 with no scoring
            //   consequence. Outside the 15 m spot the bonus is zero; the
            //   flight stands.
            ScoreTerm.Lookup("landingDistance",                                 // NZ.5.5(f)(i) — the class's own single-step bonus
                // exact 0 = paper "beyond the tape" → zero landing points (physically impossible reading, reserved)
                Rows.UpTo(0, 0)
                    .ThenUpTo(15, 50)
                    .Rest(0)),
        ],
    };

    public static ClassDefinition Definition => new()
    {
        Name = "NZ Thermal 2 Metre (Class H)",
        FaiDesignation = "",                                                   // a national class; no FAI designation
        Version = "NZMAA Section 5 Soaring, October 2024 Rev 3.0",
        // no finalRanking: one phase, so SinglePhase (NZ.5.5 has no fly-off)

        Parameters =
        [
            Params.Number("contestTime", "s", boundAt: ParameterBindingPoint.BeforeFlying),
                                                                                // NZ.5.5(d)(i) "the contest director may determine a maximum
                                                                                //   contest time … suggested that 3 hours is a suitable contest
                                                                                //   time" — no default (F12): the suggestion is advisory, the CD
                                                                                //   chooses at the contestants meeting (NZ.4.14(a)). Consumed by
                                                                                //   no scoring stage — (d)(iii) expiry is contest flow (F3F
                                                                                //   precedent)
            Params.Number("minNewGroup"),                                      // NZ.5.5 states no minimum for a re-flight group (F12); the
        ],                                                                     //   re-flight fields are Undefined (owner decision 3)

        Reflight = new()
        {
            EntitledScores = ReflightSelection.UndefinedRequiresRuling,         // NZ.5.5 states no re-flight rule of its own; NZ.3.6(b)'s
                                                                                //   tow-launched repeat-attempt grounds reach the class but
                                                                                //   state no scoring outcome — owner decision 3
            OthersScore = ReflightSelection.UndefinedRequiresRuling,            // as above
            MinNewGroupSize = NumberOrParam.Param("minNewGroup"),               // (F12)
        },

        Penalties =
        [
            ZeroRound("landedOutsideFieldBounds"),                              // NZ.5.5(f)(i) "All flight and landing points will be forfeited if
                                                                                //   the model lands outside the appointed flying field" — the
                                                                                //   Class M reading of NZ.7.4(e)(i): zero for that flight (a round
                                                                                //   here IS one flight), not the whole contest
        ],

        Phases =
        [
            new()
            {
                Ordinal = 1,
                Type = PhaseType.Preliminary,
                Rounds = new()
                {
                    Kind = CompositionKind.ChooseFromCatalogue,
                    TasksPerRound = 1,
                    RequireDistinctTaskPerRound = true,                         // NZ.5.5(d)(ii) — one flight per target, in any order
                    MaxRounds = 5,                                              // NZ.5.5(d)(ii) — five flights
                },
                Validity = new() { MinRounds = 5 },                             // owner decision 1 — NZ.5.5(d)(ii) "required to complete flights
                                                                                //   of 3,4,5,6 & 7 minutes"
                // no drop: NZ.5.5(e)(iv) "All flight and landing scores will
                //   count towards the individual's total"
                // no tie-breaking: the NZ rules state none anywhere
                //   (docs/rules/nz/00-nz-general-rules.md:117), and Pete's
                //   2026-09-04 ruling fixes what that silence left open:
                //   ties are never broken — equal places ("1st equal") at
                //   every placing
                TieBreaks = [new EqualPlaces()],                               // Pete 2026-09-04: ties stand equal, every placing
                                                                               //   encoding: kanban/completed/tie-break-policy-in-class-definition.md
                Tasks = [TaskFor(3), TaskFor(4), TaskFor(5), TaskFor(6), TaskFor(7)],  // NZ.5.5(d)(ii)
            },
        ],
    };

    private static PenaltyDefinition ZeroRound(string infraction) => new()
    {
        InfractionType = infraction,
        Effects = [new(PenaltyEffect.ZeroRound)],
    };

    // ---- arithmetic check --------------------------------------------------
    // NZ.5.5's own numbers: one point per second to the target ((e)(i)), minus
    // one per second over ((e)(ii)), forfeit beyond 60 s over ((e)(iii)),
    // landing 50 within 15 m ((f)(i)), all five flights counted ((e)(iv)):
    //   per-task max = target + 50   ->   230 / 290 / 350 / 410 / 470
    //   contest max  = (180 + 240 + 300 + 360 + 420) + 5 x 50 = 1750
    // Any evaluator that produces more has normalised something, applied a
    // discard, or awarded a landing bonus to a forfeited flight.
}