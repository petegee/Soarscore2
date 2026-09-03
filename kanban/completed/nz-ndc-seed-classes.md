# Story — NZ NDC seed classes: X5J (Class O), F5J NDC, F5K NDC

**Status:** Completed (2026-09-04) · **Raised:** 2026-09-04 (from the `Soaring_NDC_Scoresheet_v3.xlsm`
cross-reference: the club scoresheet covers six NDC pipelines; the corpus had four of
them — M NDC and F3K NDC as definitions, N and P as their own raw-sum classes — and
was missing these three) · **Rulings:** none needed — see "Interpretations" for the
three calls this story makes on the rulebook's behalf.

## Completion note (2026-09-04)

Built per the plan (WI-1..WI-6) with the three seed files authored by parallel
sub-agents and integrated by hand. `SeedX5j.cs` (86-nz-x5j), `SeedF5jNdc.cs`
(85c-nz-f5j-ndc), `SeedF5kNdc.cs` (85d-nz-f5k-ndc); corpus at sixteen
definitions; seed tool emits all three JSONs and passes round-trip /
source-gen / depth checks. Suite green on BOTH stores: Domain 686, Application
275, Architecture 7, Infrastructure 144 (72 sqlite-filtered + 72
postgres/Testcontainers), Acceptance 72 sqlite + 72 postgres. `graphify update .`
run.

Deviations and findings:

1. **Interpretation 2's fallback fired.** Adoption check 19
   (`class-definition.check-19.best-dropped-score-without-drop-policy`) rejects
   `BestDroppedScore` without a drop policy, so `85d-nz-f5k-ndc` ships
   `TieBreaks = [TieBreakFlyoff]` — NZ.3.16.26 a's best-dropped-score half is
   not merely vacuous under the NDC frame, it is UNENCODABLE. Recorded in the
   seed comment and `deferred-decisions.md`.
2. **A sub-agent's caveat was wrong and was corrected in review:** it claimed
   the Task A PerTask cap went unapplied by the evaluator; PerTask caps are
   applied by `FlightSelector.ApplyPerTaskCaps` (FlightSelector.cs:78). The
   footer comment now states that correctly.
3. **The FAI F5K below-bonus finding is filed in `tech-debt.md`**: the
   unsigned-width evaluator scores `SeedF5K`'s `Below(0, -0.5m)` bonus as a
   deduction; no test covered a below-origin launch. This story's
   `NzNdcSeedArithmeticTests` (30 cases, including NZ.3.16.29's full worked
   table) lock the NZ below-origin POSITIVE-rate encoding and are the tripwire
   for whichever way the engine fix lands.
4. A corpus-wide comment-only sweep updated the stale
   `kanban/in-progress/tie-break-policy-in-class-definition.md` citation to its
   `completed/` location in all fifteen seed files that carried it (pre-existing
   staleness; zero behaviour change).
5. `ScoringCorpusPropertyTests` premise 12→15 (plus its stale doc comment),
   `CatalogueDrawPropertyTests` floor 3→4, `BindParameterPropertyTests`
   property 5 set gains `85d-nz-f5k-ndc`, `RequiredBindings` gains its
   `minPerGroup` binding.

Nothing further deferred beyond the tech-debt entry above.

## What

Three new seed definitions in `tools/Soarscore.SeedData/`, wired into `Corpus.All`:

1. **`SeedX5j.cs` → `86-nz-x5j`** — NZMAA **Class O: X5J Unlimited** (`NZ.3.14`).
   Four flights, each within a 10-minute working time, all summed raw: glide time
   (motor stop → landing) at 1 pt/s up to the end of working time, NZ.2.4.5 landing
   table, motor restart deducts 1 pt/s of subsequent run and forfeits landing.
   The class as written IS its decentralised format — there is no other §3.14
   pipeline, so this is one definition, not an NDC twin.
2. **`SeedF5jNdc.cs` → `85c-nz-f5j-ndc`** — **F5J NDC** (`NZ.0.3`): FAI F5J's task and
   scoring carry (`NZ.0.3 c`), the pipeline does not — 4 rounds (`0.3 b`), sum of raw
   scores with 5.5.11.12.m normalisation disregarded (`0.3 d`), no fly-off.
3. **`SeedF5kNdc.cs` → `85d-nz-f5k-ndc`** — **F5K NDC** (`NZ.0.4` → `NZ.3.16.37`):
   NZ Class Q tasks A/B/C/E only, 4 rounds, sum of raw scores, "Do NOT Normalize"
   (`3.16.37 c`), timing to 0.1 s (`3.16.37 d`). The base rules are **NZ Class Q
   (`NZ.3.16`), not FAI F5K** — the launch adjustment, landing consequences and task
   numbers differ, see below.

## Why it matters

The scoresheet cross-reference showed clubs actively score all three; each probes
the model somewhere the corpus has not been probed:

- **X5J** is the first class whose scored time is a *derived interval* (glide only —
  motor run excluded, `NZ.3.14.2 c`) and the first with a *measured deduction*
  (restart seconds at −1 pt/s, `3.14.2 e`). It is also the first NZ class whose
  re-flight position is total silence (M states entitlement, N/P state none — F26's
  third case for a NZ class).
- **F5J NDC** exercises "same task, different pipeline" a second way: unlike M NDC
  (which changes the task too — 10 min fixed), F5J NDC is SeedF5J's task verbatim
  with the normalisation stage removed.
- **F5K NDC** brings the first *non-ALES NZ class* into the corpus and the first
  class whose launch adjustment is a **symmetric dead-zone band pair**
  (`NZ.3.16.29`: ±2 m free, then ±1 s/m to 6 m, then ±2 s/m) and whose landing-window
  overfly **zeroes the flight** where FAI F5K deducts 100 (`NZ.3.16.21 b` vs
  `5.5.10.12 a`).

## Interpretations made (no ruling requested; Pete may veto any)

1. **No fly-off in either F5J NDC or F5K NDC.** `NZ.0.3 d` fixes the contest score
   at "the sum of the Raw Scores from the four rounds"; `NZ.3.16.37 a+c` likewise.
   A fly-off (FAI 5.5.11.13 / NZ.3.16.27) would nullify that sum, so the fly-off
   phases are varied away. `NZ.3.16.26`'s tie-break fly-off is different machinery
   and is retained (below).
2. **F5K NDC tie-breaks: `BestDroppedScore` then `TieBreakFlyoff`** (`NZ.3.16.26 a`,
   carried by `3.16.37 b`). With no discard the first clause is vacuous and the
   ladder resolves to the fly-off. If adoption rejects a `BestDroppedScore` with no
   drops, fall back to `[TieBreakFlyoff]` with a comment saying exactly that.
3. **`NZ.3.16.31 a-vi` "9.45 min" read as 9 min 45 s = 585 s** (the m.ss reading,
   parallel to FAI 5.5.10.2's "9.59 min" → 599 in `SeedF5K`). Independently
   confirmed by arithmetic: 600 s of targets − 3 × 5 s turnarounds
   (`3.16.31 a-v`) = 585.

## Rulebook defects found (left as written; NZMAA's to fix)

- `NZ.3.16.35` Task E: the worked example's subtotals (95 + 197 + 205) do not match
  its own sum line ("95 + 187 + 195 = 487"); and `.n`'s maximum (9:50 at 3 launches)
  contradicts its parenthetical ("two nominations has a maximum possible of 9:55").
  The seed caps the credited target at 599 (`3.16.35 l` "all in … 9.59 minute" — the
  one clean number) and flags both defects in comments.
- `NZ.3.16.34` Task D lettering is garbled in the source (not seeded — D is not in
  the NDC catalogue).

## Related finding (out of scope here, filed in tech-debt)

The **FAI F5K seed's below-NLH launch bonus scores as a deduction** under the
current evaluator: `Below(0, -0.5m)` + `FlightInterpreter.EvaluatePiecewise`'s
unsigned-width walk yields −5 at 10 m below NLH where `5.5.10.4` states a +0.5 pt/m
bonus. No test covers a below-origin launch (`FlightInterpreterTests` tests +15 and
exactly-at-NLH only). **This story encodes NZ's below-origin bonus with POSITIVE
rates — correct under the evaluator as it is — and the targeted arithmetic test
locks those numbers.** If the engine is ever corrected to signed-width semantics,
the FAI seed becomes correct as written AND this story's below-origin rates must
flip sign; the test will catch either drift. See `kanban/tech-debt.md`.

## Plan

**WI-1 — `SeedX5j.cs`.** One task, one phase. Metrics: `glideTime` (s, Truncate 1 —
precision unstated, F12 residual), `motorRestartRunTime` (s, Truncate 1),
`motorRestarted`, `airborneAtRoundEnd`, `landedWithin75m`, `landingDistance`
(m, Ceiling 1 — `NZ.2.4.5` "next full metre"). Score: `Rate("glideTime", 1)` with NO
cap and no rest band (the working-time watch-stop is procedural — contrast M NDC's
`Rest(-1)`, which `NZ.3.12.1 n` states); `Rate("motorRestartRunTime", -1)`
(`3.14.2 e`); landing lookup gated on `P.All(motorRestarted=false,
airborneAtRoundEnd=false)`. `FlightValidWhen = landedWithin75m`. Timing Fixed 600,
MaxLaunches 1. No group, no normalise (F25), no penalties (comment). Reflight
`UndefinedRequiresRuling` ×2 + `minNewGroup` param (F12 — 3.14 is silent). Phase:
FixedSequence, MaxRounds 4, MinRounds 4, no drop, `TieBreaks =
[UndefinedRequiresRuling]`. Arithmetic check per `3.14.3`: 4 × (600 − run + 50).

**WI-2 — `SeedF5jNdc.cs`.** Transplant `SeedF5J.TaskD` (metrics, score terms,
`FlightValidWhen`, timing, preparation) minus the `Normalise` block; keep
`Group { MinPerGroup = 6 }` (5.5.11.8 carries for the draw; the .l/.m normalisation
sentences are superseded by `0.3 d` — SeedNzF3kNdc's comment pattern). Reflight
Replacement/BetterOf/6 (5.5.11.6 carries per `0.3 c`). Penalties: transplant
`SeedF5J`'s block verbatim with citations. No FinalRanking, no fly-off params, no
drops. Phase: FixedSequence, MaxRounds 4 (`0.3 b`), MinRounds 4, no drop
(`0.3 d`), `TieBreaks = [UndefinedRequiresRuling]` (5.5.11.13 h covers fly-off
placing only; NZ silence otherwise). FaiDesignation "F5J" (`0.3 c` — the class IS
FAI F5J).

**WI-3 — `SeedF5kNdc.cs`.** Tasks A/B/C/E per `NZ.3.16.31/.32/.33/.35` (NOT FAI
5.5.10.2). Shared: metrics (`flightTime` Truncate 0.1 per `3.16.37 d`; `launchAltitude`
Truncate 1 per `3.16.29 c`; flags `landedInLandingArea`, `overflewLandingWindow`,
`launchedInWindow`, `touchedBeforeStop`), `FlightValidWhen` =
all(landedInLandingArea, ¬overflewLandingWindow, launchedInWindow,
¬touchedBeforeStop) (`3.16.10 b/d`, `3.16.21 b`, `3.16.17 d`), the shared launch
adjustment `T.Piecewise("launchAltitude", Bands.Below(-6, 2).UpTo(-2, 1).UpTo(2, 0)
.UpTo(6, -1).Rest(-2), 60)` (`3.16.29 e-g`, NLH fixed 60 per `3.16.29 b` — no
parameter; POSITIVE rates below origin per the evaluator note above; verify against
the clause's own examples 41/50/51/53/54/55/57/58/61/63/65/66/67/69/70),
`RawScore = (Truncate, 0.1)` (F12 residual), `Group { MinPerGroup =
param("minPerGroup") }` (`3.16.19` states no minimum, F12; draw only), no normalise
(F25). Task numbers: **A** BestNFlights{4, RankByMetric "flightTime", AnyOrder,
[60,120,180,240]}, Fixed 600, MaxLaunches 4, `Rate("flightTime", 1, cap: 585,
capScope PerTask)` (`3.16.31 a-vi`); **B** LastFlight, Fixed 420, MaxLaunches 3,
`Rate cap 300` (`3.16.32 b`) + `Lookup(Intrinsic.FlightSequence, UpTo(1,0).Then(2,-10)
.Rest(-20))` (`3.16.32 e`); **C** AllFlights, Fixed 258 (`3.16.21 c` 4:18 window —
`3.16.33 b` states no working time; per-attempt reading), MaxLaunches 3,
`Rate cap 240` (`3.16.33 d`); **E** Poker: `targetTime` declared metric, AllFlights,
Fixed 600, MaxLaunches 3, `When(Ge(flightTime, targetTime), Rate("targetTime", 1,
cap: 599))` (`3.16.35 f, l`), launch adjustment nested under the same When
(`3.16.35 m`), unconditional `FlightSequence` rows −10/−20 (`3.16.35 o`).
Ref:light Replacement/BetterOf/4 (`3.16.23`). Penalties: ZeroRound
motorRestartInFlight (`3.16.9 i` "zero for the task"), ZeroRound
hitPersonOtherThanTimer (`3.16.9 b`), ZeroFlight nlhSettingDeviation (`3.16.9 h`),
ZeroRound launchAfterMaxFlights (`3.16.9 j`), ZeroFlight lostPart (`3.16.5 a`),
ZeroFlight forbiddenAirspace (`3.16.14 a`), Excluded safety trio object −100 /
person-in-area −300 / person-outside −100 with the `safetyInfraction` exclusion
group (`3.16.13 c-d`; mid-air exception `3.16.13 a/e` is a field ruling — comment).
Phase: ChooseFromCatalogue, TasksPerRound 1, RequireDistinctTaskPerRound,
MaxRounds 4 (`3.16.37 a`; 4 tasks → one each), MinRounds 4, no drop (`3.16.37 c`;
`3.16.25 b`'s discard can never fire), tie-breaks per Interpretation 2.
FaiDesignation "" — Class Q is a NZ class, not FAI F5K.

**WI-4 — corpus + counts.** `Corpus.cs`: three entries between `85b-nz-f3k-ndc` and
`90-aggregate`; header comment thirteen → sixteen definitions, NZ block five →
eight. `ScoringCorpusPropertyTests`: `drawable.Length` 12 → 15, premise comment
"12 of the 13" → "15 of the 16". Check `CatalogueDrawPropertyTests`' floor and any
other corpus-count premises.

**WI-5 — targeted arithmetic tests** (`tests/Soarscore.Domain.Tests/NzNdcSeedArithmeticTests.cs`,
modelled on `FlightInterpreterTests`' harness): NZ.3.16.29's own table against
`85d-nz-f5k-ndc`'s launch adjustment (51→+10, 53→+6, 54→+4, 57→+1, 58→0, 61→0,
63→−1, 66→−4, 67→−6, 69→−10, 70→−12); X5J restart deduction (glide 300 +
restart 20 → 280, landing forfeited); F5J NDC raw-sum identity (600 + 50 − 100 at a
200 m launch = 550, no normalisation). These are the lock on the band-sign
convention — see the tech-debt note.

**WI-6 — emit + verify.** `dotnet run --project tools/Soarscore.SeedData` (emits
`86-nz-x5j.json`, `85c-nz-f5j-ndc.json`, `85d-nz-f5k-ndc.json`; round-trip /
source-gen / depth checks are the first gate), then Domain, Architecture,
Infrastructure (sqlite) and Acceptance (sqlite) suites; postgres suites when Docker
is up. `graphify update .` after the tree settles.

## Before starting / cross-references (house rule 2)

- **No-new-concepts gate:** nothing new is needed — glide/derived intervals are a
  metric + rate, the restart deduction is a negative RateTerm, mass-launch windows
  are a flag + `FlightValidWhen`, flight-sequence cost is the F6 intrinsic,
  exclusion groups are F20. Stop and surface if the build suggests otherwise.
- `kanban/completed/nz-f3k-ndc-seed-class.md` — the direct precedent (raw-sum NDC
  frame, group-carries-draw-only, ruling style). Its "4 rounds one per task" ruling
  shape is reused where `3.16.37 a` + the 4-task catalogue make it arithmetic.
- `kanban/backlog/tie-break-policy-in-class-definition.md` — gains three more
  NZ-silence instances (X5J, F5J NDC) and one stated-but-vacuous ladder (F5K NDC).
- `docs/rules/nz/00-nz-general-rules.md` §1 says the §0.0 NDC formats are "not
  modelled here" — F5J/F5K NDC now are; any doc update is a separate approval-gated
  ask. `docs/` is otherwise untouched (house rules 3-4).
- Scoresheet discrepancies already reported to Pete separately (ALES 123 landing
  table; F5J/X5J overtime shape); none block this story.
