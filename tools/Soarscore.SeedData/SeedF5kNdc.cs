// NZ F5K (NZ Class Q) — RC Hand Launch Electric Glider, NDC format
// Rule refs: NZMAA Flying Rules, Section 5: Soaring, March 2024 — NZ.3.16
//            (Class Q, hand-launch electric), varied by NZ.3.16.37 (the NDC
//            frame).
//
// NZ.3.16.37 is the National Decentralised Contest frame over Class Q: 4 rounds
// (a); Tasks A, B, C and E only, scored as "the sum of the rounds RAW Scores
// only. Do NOT Normalize scores" (c); timing to 1/10th of a second (d). The
// same pipeline split as SeedNzMNdc: a different scoring frame over the same
// rulebook class, hence its own CompetitionClass.
//
// NZ Class Q is a NEW ZEALAND class, NOT FAI F5K — nothing in NZ.3.16 nominates
// the FAI F5 rulebook (contrast SeedNzF3kNdc, whose class IS FAI F3K via S5
// §3.8.1, so the FAI F3K numbers carry there). The FAI F5K numbers therefore do
// NOT carry, and the genuine NZ variations are visible in the encoding:
//   - the NLH is a rule constant, 60 m with a 7 s maximum motor run
//     (NZ.3.16.29 b), not the FAI parameterised 60/70 m choice (5.5.10.3);
//   - landing validity is the Launch and Landing Area (NZ.3.16.10 c/d): there
//     is no 75 m rule in Class Q and no pilot-area −10 tier;
//   - overflying the landing window ZEROES the flight (NZ.3.16.21 b) where FAI
//     F5K deducts 100 (5.5.10.12 a), and 3.16.21 a states NO deduction for
//     flying over the maximum time or past the end of the working time;
//   - the launch adjustment is in SECONDS per metre added to the raw score
//     (NZ.3.16.29), not FAI F5K's points relative to a parameter.
//
// Task E's rule text carries two defects, flagged where they bite and not
// editorialised further: clause .n is internally inconsistent (a stated 9:50
// against its own parenthetical's 9:55 arithmetic), and the worked example's
// sum line (95 + 187 + 195 = 487) contradicts its own subtotals
// (95 + 197 + 205 = 497).

using System.Collections.Immutable;
using Soarscore.Domain.PublishedClassDefinition;

namespace Soarscore.SeedData;

public static class SeedF5kNdc
{
    // ---- metricSet nzF5kFlight ---------------------------------------------

    private static ImmutableArray<MetricDefinition> FlightMetrics =>
    [
        // NZ.3.16.37 d "Timing to be to 1/10th of a second" — 0.1 s precision.
        // The ROUNDING MODE is unstated (F12 residual); Truncate follows the
        // FAI 5.5.10.6 f culture and the SeedNzF3kNdc precedent ("59.99 seconds
        // is recorded at 59.9 seconds").
        M.Number("flightTime", "s", RoundingMode.Truncate, 0.1m),              // NZ.3.16.37 d
        M.Number("launchAltitude", "m", RoundingMode.Truncate, 1),             // NZ.3.16.29 c: recorded in the AMRT, measured during the
                                                                               //   10 s after motor stop; whole metres (the worked table)
        M.Flag("landedInLandingArea"),                                         // NZ.3.16.10 c/d; e: any part inside the boundary = inside
        M.Flag("overflewLandingWindow"),                                       // NZ.3.16.21 b: landing after the 15 s window zeroes the flight
        M.Flag("launchedInWindow"),                                            // NZ.3.16.17 d for the self-paced Task A; the 3 s mass-launch
                                                                               //   window for B/C/E — one flag, per-task citations on the tasks
        M.Flag("touchedBeforeStop"),                                           // NZ.3.16.10 b: the flight continues until grounded and
                                                                               //   stopped, then scores zero for the flight
    ];

    // The flight-validity conjunction shared by all four tasks. NZ.3.16.21 b
    // ZEROES what FAI F5K deducts 100 for (5.5.10.12 a) — a genuine NZ
    // variation, so there is no OverflyDeduction term anywhere in this class.
    // Likewise no pilot-area deduction: landing outside the Launch and Landing
    // Area is a flight zero via landedInLandingArea (NZ.3.16.10 d), and the
    // FAI −10 tier does not carry.
    private static AllOf FlightValidWhen =>
        P.All(
            P.Is("landedInLandingArea", true),                                 // NZ.3.16.10 c/d
            P.Is("overflewLandingWindow", false),                              // NZ.3.16.21 b
            P.Is("launchedInWindow", true),                                    // NZ.3.16.17 d (Task A); 3 s mass-launch window (B/C/E)
            P.Is("touchedBeforeStop", false));                                 // NZ.3.16.10 b

    // ---- the NZ launch adjustment (NZ.3.16.29 e-g) -------------------------
    // One PiecewiseTerm, declared once and reused by all four tasks (notation
    // §7.1): every task scores the same altitude adjustment, only the GUARD
    // varies (Task E wraps it in the target-achieved condition). Written out in
    // each task it would be four hand-maintained copies of one table — the
    // F22/F24 failure shape.
    //
    // The origin is the NLH, fixed at 60 m with a 7 s maximum motor run
    // (NZ.3.16.29 b) — a rule constant, not a parameter. e: no penalty while
    // the zoom after motor stop is within 2 m of the NLH; f: 1 s per metre for
    // 2-6 m above, 2 s per metre beyond 6 m; g: 1 s per metre for 2-6 m below,
    // 2 s per metre beyond 6 m below. Rates are SECONDS per metre —
    // NZ.3.16.29's examples express the adjustment in seconds, added to the
    // flight-time points.
    //
    // SIGN CONVENTION. FlightInterpreter.EvaluatePiecewise integrates each
    // band's rate over the UNSIGNED width of its overlap with
    // [0, metric − origin] (width = overlapEnd − overlapStart; both sides of
    // the origin count positively). A BONUS below the origin therefore needs a
    // POSITIVE rate here, and a penalty above it a negative one. A future
    // signed-width evaluator would flip these signs — cross-ref
    // kanban/tech-debt.md (the FAI F5K below-bonus finding, same evaluator
    // fact).
    //
    // Bands (rate, s/m): (−∞, −6] +2, [−6, −2] +1, [−2, +2] 0, [+2, +6] −1,
    // [+6, ∞) −2. Verification against NZ.3.16.29's own examples (adjusted =
    // altitude − 60; each side integrated separately over its unsigned width):
    //   41 → −19: 13×2 + 4×1 = +30        50 → −10: 4×2 + 4×1   = +12
    //   51 →  −9:  3×2 + 4×1 = +10        52 →  −8: 2×2 + 4×1   = +8
    //   53 →  −7:  1×2 + 4×1 = +6         54 →  −6: 0×2 + 4×1   = +4
    //   55 →  −5:  3×1       = +3         56 →  −4: 2×1         = +2
    //   57 →  −3:  1×1       = +1         58 →  −2: width 0     = 0
    //   59 →  −1:  zero band  = 0         61 →  +1: zero band   = 0
    //   62 →  +2:  zero band  = 0         63 →  +3: 1×(−1)      = −1
    //   64 →  +4:  2×(−1)    = −2         65 →  +5: 3×(−1)      = −3
    //   66 →  +6:  4×(−1)    = −4         67 →  +7: 4×(−1)+1×(−2) = −6
    //   68 →  +8:  −4 + 2×(−2) = −8       69 →  +9: −4 + 3×(−2) = −10
    //   70 → +10:  −4 + 4×(−2) = −12
    private static PiecewiseTerm LaunchAdjustment =>                       // NZ.3.16.29 e-g
        T.Piecewise("launchAltitude",
            Bands.Below(-6, 2).UpTo(-2, 1).UpTo(2, 0).UpTo(6, -1).Rest(-2),
            60);                                                           // NLH fixed 60 — NZ.3.16.29 b

    // ---- Task A, the task the other three derive from ----------------------
    // Four targets in any order; every flight counts whether or not its target
    // is reached, so the target is a clamp, not a Poker condition.

    private static TaskDefinition TaskA => new()
    {
        Code = "A",
        Name = "1, 2, 3, 4 minute flights in any order",                       // NZ.3.16.31
        Metrics = FlightMetrics,
        Flights = new BestNFlights
        {
            Count = 4,
            // Load-bearing (F16): the assigned target clamps the raw flight
            // time ("Time flown will be entered into the scoring, with a
            // maximum of the target time per flight", NZ.3.16.31), so a
            // flight's score is a function of its rank and ranking the
            // candidates by score is circular — rank by the raw metric. Same
            // reasoning as SeedNzF3kNdc's TaskH. The targets fly in any order
            // and a pilot need not achieve the current target before the next
            // flight (NZ.3.16.31).
            RankByMetric = "flightTime",
            Targets = TargetAssignment.AnyOrder,
            TargetValues = [60, 120, 180, 240],                                // NZ.3.16.31 a-ii
        },
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 600,                                                 // NZ.3.16.31 a-i: four launches max within a 10 min window
            PreparationTime = 180,                                             // NZ.3.16.22 a "at least 3 minutes" — the stated floor
            MaxLaunches = 4,                                                   // NZ.3.16.31 a-i
        },
        Group = new() { MinPerGroup = NumberOrParam.Param("minPerGroup") },    // NZ.3.16.19 states no group minimum (F12); governs the
                                                                               //   draw only — its normalisation sentence is superseded
                                                                               //   by NZ.3.16.37 c's raw-sum total
        // NO normalise (F25): NZ.3.16.37 c "the sum of the rounds RAW Scores
        //   only. Do NOT Normalize scores" — there is no normalisation scale
        //   anywhere in this class.
        RawScore = new(RoundingMode.Truncate, 0.1m),                           // F12 residual: raw-score rounding unstated; 0.1 s per NZ.3.16.37 d
        FlightValidWhen = FlightValidWhen,                                     // NZ.3.16.10 b/d; 3.16.21 b; 3.16.17 d
        Score =
        [
            // 1 pt/s. NZ.3.16.31 a-vi "Maximum total flight time used for
            // scoring: 9.45 min" — read as m.ss, 9 min 45 s = 585 s, the m.ss
            // reading parallel to FAI 5.5.10.2's "9.59 min" -> 599 in SeedF5K.
            // Independently confirmed: 600 s of targets less the three 5 s
            // turnarounds (NZ.3.16.31: minimum 5 s between landing and start)
            // = 585. Flag: a-vi's notation is ambiguous exactly where FAI's
            // is; here the arithmetic corroborates the m.ss reading.
            T.Rate("flightTime", 1, cap: 585, capScope: CapScope.PerTask),     // NZ.3.16.31 a-vi

            LaunchAdjustment,                                                  // NZ.3.16.29 e-g
        ],
    };

    // ---- B -----------------------------------------------------------------
    // Only the last flight counts, so the start penalty must be read off that
    // flight's own sequence number. B restates `score`, which replaces A's
    // block entirely (A's PerTask cap does not reach B); everything B does not
    // restate — Metrics, Group, RawScore, FlightValidWhen, no normalise —
    // comes from A through the `with`.

    private static TaskDefinition TaskB => TaskA with
    {
        Code = "B",
        Name = "Last flight",                                                  // NZ.3.16.32
        Flights = new LastFlight(),                                            // NZ.3.16.32: the last flight counts; max flight 5 min
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 420,                                                 // NZ.3.16.32 d: three launches max within a 7 min window
            PreparationTime = 180,                                             // NZ.3.16.22 a — the stated floor
            MaxLaunches = 3,                                                   // NZ.3.16.32 d
        },
        Score =
        [
            T.Rate("flightTime", 1, cap: 300),                                 // NZ.3.16.32 b: max flight 5 minutes

            // NZ.3.16.32 e: start penalties CUMULATIVE at the last flight's
            // own start number — 1st 0, 2nd −10, 3rd −20 (the worked example
            // shows flight 2 → −10, flight 3 → −20). B selects only the last
            // flight, so the rows are the cumulative cost at that flight's
            // sequence number. Character-identical to Task E's rows and
            // deliberately not one shared list — see the note there.
            T.Lookup(Intrinsic.FlightSequence,                                 // intrinsic ref (F6)
                Rows.UpTo(1, 0).Then(2, -10).Rest(-20)),                       // NZ.3.16.32 e

            LaunchAdjustment,                                                  // NZ.3.16.29 e-g
        ],
    };

    // ---- C -----------------------------------------------------------------

    private static TaskDefinition TaskC => TaskA with
    {
        Code = "C",
        Name = "All up, Last down, 4 minutes maximum (3x)",                    // NZ.3.16.33
        Flights = new AllFlights(),                                            // NZ.3.16.33: 3 flights total, all count
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            // NZ.3.16.33 b "No working time is necessary" — the mass-launch
            // signal structure replaces it. 258 s is 4:18, the per-attempt
            // signal window of NZ.3.16.21 c (3 s launch window + 240 s flight
            // + 15 s landing window), the F13 per-launch reading — compare
            // SeedF5K TaskC's 241.
            WorkingTime = 258,
            PreparationTime = 180,                                             // NZ.3.16.22 a — the stated floor
            MaxLaunches = 3,                                                   // NZ.3.16.33: 3 flights
        },
        Score =
        [
            // Max measured flight time 240 s; the time stops at landing or at
            // the 4-minute acoustic signal (NZ.3.16.33 i).
            T.Rate("flightTime", 1, cap: 240),                                 // NZ.3.16.33 d

            LaunchAdjustment,                                                  // NZ.3.16.29 e-g
        ],
    };

    // ---- E -----------------------------------------------------------------
    // Poker. Unlike FAI 5.5.10.2 E, NZ.3.16.35 states no 30-second inner rule
    // and nothing about unachieved flights carrying the height penalty, so the
    // target-achieved guard encodes .m exactly as written — no FAI-style
    // inner bonus-only conditional. NZ.3.16.35 o puts the start penalty on
    // EVERY launch, so the selection stays `all` (an unachieved flight scores
    // no flight points and no height adjustment, but still carries its start
    // penalty).

    private static TaskDefinition TaskE => TaskA with
    {
        Code = "E",
        Name = "Poker",                                                        // NZ.3.16.35
        Metrics = [.. FlightMetrics,
                   M.Number("targetTime", "s", RoundingMode.Truncate, 0.1m,
                       declared: true)],                                       // NZ.3.16.35 d: the target is announced before each launch
        Flights = new AllFlights(),                                            // NZ.3.16.35: max 3 flights to achieve up to three targets
        Timing = new()
        {
            Kind = WorkingTimeKind.Fixed,
            WorkingTime = 600,                                                 // NZ.3.16.35: working time 10 minutes
            PreparationTime = 180,                                             // NZ.3.16.22 a — the stated floor
            MaxLaunches = 3,                                                   // NZ.3.16.35
        },
        Score =
        [
            // NZ.3.16.35 f: if the flight reaches/exceeds the target, the
            // target time is credited. Cap 599 from NZ.3.16.35 l's "all in"
            // call — "target and maximum result is 9.59 minute" (9:59 = 599 s),
            // the one clean stated maximum. DEFECTS FLAGGED, not resolved:
            // clause .n states "The maximum flight time is 9.50 minutes" while
            // its own parenthetical computes 9:55 for two nominations (10 min
            // less 5 s per additional nomination) — internally inconsistent;
            // and the worked example's sum line (95 + 187 + 195 = 487)
            // contradicts its own subtotals (95 + 197 + 205 = 497). Neither
            // 9:50 nor 9:55 is encoded: l's 9:59 is the only self-consistent
            // number in the clause.
            T.When(P.Ge("flightTime", "targetTime"),
                   T.Rate("targetTime", 1, cap: 599)),                         // NZ.3.16.35 f/l

            // NZ.3.16.35 m: the launch adjustment attaches to ACHIEVED targets
            // only — the same guard wraps the whole shared PiecewiseTerm.
            // Nested rather than conjoined with the term above: both clauses
            // share one condition, and the guard's meaning is "the adjustment
            // exists only on a target-achieved flight".
            T.When(P.Ge("flightTime", "targetTime"), LaunchAdjustment),        // NZ.3.16.35 m

            // NZ.3.16.35 o: start penalties on EVERY launch — 1st 0, 2nd −10,
            // 3rd −20 as per-flight increments (the worked example shows them
            // on each flight), unconditional on target achievement. These rows
            // are character-identical to Task B's and are deliberately NOT one
            // shared list: they are two different statements that happen to
            // agree. B selects only the last flight, so its rows are the
            // CUMULATIVE cost at that flight's sequence number (−20 total over
            // three launches); E selects every flight, so its rows are the
            // per-launch INCREMENT (−30 total). Same pattern as SeedF5K
            // Tasks B/E.
            T.Lookup(Intrinsic.FlightSequence,                                 // intrinsic ref (F6)
                Rows.UpTo(1, 0).Then(2, -10).Rest(-20)),                       // NZ.3.16.35 o
        ],
    };

    private static ImmutableArray<TaskDefinition> Catalogue => [TaskA, TaskB, TaskC, TaskE];

    // ---- the definition ----------------------------------------------------

    public static ClassDefinition Definition => new()
    {
        Name = "NZ F5K Hand Launch Electric Glider (NDC format)",
        // Blank: NZ Class Q (NZ.3.16) is a NZ-defined class, NOT FAI F5K —
        // nothing in NZ.3.16 nominates the FAI F5 rulebook. Contrast
        // SeedNzF3kNdc, whose class IS FAI F3K and whose FaiDesignation is set.
        FaiDesignation = "",
        Version = "NZMAA Section 5 Soaring, March 2024",
        // no finalRanking: NZ.3.16.37 a+c fix the contest at 4 rounds scored as
        //   their raw sum; a fly-off (NZ.3.16.27, carried by 3.16.37 b) would
        //   nullify that total and is varied away. One phase, so SinglePhase.

        Parameters =
        [
            Params.Number("minPerGroup"),                                      // NZ.3.16.19 states no group minimum (F12)
        ],

        Reflight = new()
        {
            EntitledScores = ReflightSelection.Replacement,                    // NZ.3.16.23
            OthersScore = ReflightSelection.BetterOf,                          // NZ.3.16.23
            MinNewGroupSize = 4,                                               // NZ.3.16.23
        },

        Penalties =
        [
            // NZ.3.16.9 i: motor restart after the initial climb -> "zero score
            // for the TASK". NZ says task — one task per round — so FAI F5K's
            // flight-level zero does not carry.
            ZeroRound("motorRestartInFlight"),                                 // NZ.3.16.9 i
            ZeroRound("hitPersonOtherThanTimer"),                              // NZ.3.16.9 b "zero for the task"
            ZeroFlight("nlhSettingDeviation"),                                 // NZ.3.16.9 h: NLH/motor-time setting deviation ->
                                                                               //   "zero score for the flight"
            ZeroRound("launchAfterMaxFlights"),                               // NZ.3.16.9 j "zero score for the task"

            // NZ.3.16.5 a. The mid-air and first-ground-contact exceptions are
            // field rulings, not encodable predicates.
            ZeroFlight("lostPart"),                                            // NZ.3.16.5 a

            // NZ.3.16.14 a: zero flight if told to leave the forbidden airspace
            // and not complying immediately — CD adjudication, encoded as a
            // zero-flight penalty definition.
            ZeroFlight("forbiddenAirspace"),                                   // NZ.3.16.14 a

            // NZ.3.16.13 c i-iii, the deduction "from the competitor's final
            // score"; 3.16.13 d: only the highest single penalty per attempt —
            // one exclusion group, largest wins (F20). All three effects are
            // deductions, which is what the group requires (check 16). The
            // mid-air exception (3.16.13 a/e: no re-flights and no penalties)
            // is a field ruling, not an encodable predicate.
            Excluded("safetyAreaObjectContact", 100),                          // NZ.3.16.13 c i
            Excluded("safetyAreaPersonContact", 300),                          // NZ.3.16.13 c ii
            Excluded("personContactOutsideSafetyArea", 100),                   // NZ.3.16.13 c iii
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
                    RequireDistinctTaskPerRound = true,                        // NZ.3.16.37 a: 4 rounds and the catalogue has exactly
                                                                               //   4 tasks (A, B, C, E) — one each, by arithmetic
                    MaxRounds = 4,                                             // NZ.3.16.37 a "will comprise 4 rounds"
                },
                Validity = new() { MinRounds = 4 },                            // NZ.3.16.37 a
                // no drop: NZ.3.16.37 c "the sum of the rounds RAW Scores only"
                //   — and 3.16.25 b's discard (6+ rounds) can never fire at a
                //   fixed four
                // NZ.3.16.26 a ("the best dropped score defines the ranking. If
                //   the tie still exists, a separate fly-off ...") carries into
                //   NDC by 3.16.37 b "Contest rules as per 3.16 except scoring".
                //   Its best-dropped-score half is vacuous here — the NDC has a
                //   fixed four rounds and no discard (3.16.37 c; 3.16.25 b's
                //   6+ discard can never fire) — and adoption check 19 refuses
                //   BestDroppedScore without a drop policy, so the vacuous
                //   clause is UNENCODABLE rather than silently no-oped. The
                //   ladder is therefore the fly-off half alone: a tie at four
                //   raw-summed rounds goes straight to the CD's tie-break
                //   fly-off (the CD defines one task, NZ.3.16.26).
                //   encoding: kanban/completed/tie-break-policy-in-class-definition.md
                TieBreaks = [new TieBreakFlyoff()],                            // NZ.3.16.26 a via 3.16.37 b (see above)
                Tasks = Catalogue,                                             // NZ.3.16.37 c: tasks A, B, C & E only
            },
        ],
    };

    private static PenaltyDefinition ZeroRound(string infraction) => new()
    {
        InfractionType = infraction,
        Effects = [new(PenaltyEffect.ZeroRound)],
    };

    private static PenaltyDefinition ZeroFlight(string infraction) => new()
    {
        InfractionType = infraction,
        Effects = [new(PenaltyEffect.ZeroFlight)],
    };

    private static PenaltyDefinition Excluded(string infraction, decimal points) => new()
    {
        InfractionType = infraction,
        ExclusionGroups = ["safetyInfraction"],
        Effects = [new(PenaltyEffect.DeductPoints, points)],
    };

    // ---- arithmetic check --------------------------------------------------
    // NZ.3.16.37 c fixes the identity: the contest total is exactly the sum of
    // the four rounds' RAW scores — no normalisation rescale (3.16.37 c "Do NOT
    // Normalize scores") and no discard. Per-task flight-time maxima, before
    // the NZ.3.16.29 launch adjustment (seconds per metre, adding on top of
    // every flight):
    //   A: cap 585                          NZ.3.16.31 a-vi "9.45 min" = 9:45
    //                                       (600 s of targets − 3 × 5 s turnarounds)
    //   B: cap 300                          NZ.3.16.32 b, 5 minutes
    //   C: 3 × 240                  = 720   NZ.3.16.33 d, 4 min × 3 attempts
    //   E: credited targets, ≤ 599 each     NZ.3.16.35 f/l — .n's own task total
    //                                       is internally inconsistent (see
    //                                       Task E), so no clean task maximum
    //                                       is asserted from it
    // A's cap is a PerTask cap (F4a), applied by FlightSelector.ApplyPerTaskCaps
    // across the four selected flights.
    // Timing to 0.1 s throughout (NZ.3.16.37 d).
    //
    // NZ.3.16.29's own worked table (NLH 60; adjusted = altitude − 60), which
    // the LaunchAdjustment bands must reproduce:
    //   41 → +30   50 → +12   51 → +10   52 → +8    53 → +6    54 → +4
    //   55 → +3    56 → +2    57 → +1    58 → 0    59 → 0     61 → 0
    //   62 → 0     63 → −1    64 → −2    65 → −3   66 → −4    67 → −6
    //   68 → −8    69 → −10   70 → −12
}
