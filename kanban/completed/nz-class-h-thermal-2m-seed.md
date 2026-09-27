# Story — NZ Class H seed class: Thermal 2 Metre

**Status:** Completed (2026-09-27) · **Raised:** 2026-09-27 (Pete: "Can we add
the Class H 2m NZ NDC class") · **Rulings:** four owner decisions 2026-09-27
(Pete), recorded below and mirrored in the seed header.

## Completion note (2026-09-27)

Built per the plan (WI-1..WI-8). `SeedNzHThermal2m.cs` authored (corpus slot
`87-nz-h-thermal-2m`: 5 tasks, 10 terms, depth 9); `Corpus.ExpectedCount`
16→17; the seed tool emits `json/87-nz-h-thermal-2m.json` and passes
round-trip / source-gen / depth / count checks. Rule doc
`docs/rules/nz/class-h-thermal-2m.md` (approved in-session; its `NZ.2.2.2`
launching cross-reference is recorded as stale — §2.2.2 does not exist in Rev
3.0, the definitions are `NZ.4.2/4.4/4.6/4.7`), rule-map NZ column and trap
rows, SKILL.md corpus tree line; `check-links` green (81 anchors).

Tests: new `NzClassHSeedArithmeticTests` (14 — partial credit, exact target,
overtime deduction, both sides of the 60 s cliff, the whole-flight forfeit
lock, landing boundary rounding, outside-circle flight-stands, per-task
maxima 230/290/350/410/470); `ScoringCorpusPropertyTests` premise 15→16 (no
`RequiredBindings` entry — nothing is consumed by its scoring stages);
`CatalogueDrawPropertyTests` floor 4→5; `TapeLandingScaleProofTests` and the
acceptance seeding step's corpus pins 16→17. One test-authoring correction
made in flight: `FlightValidWhen` states the COMPLIANT condition (zero when
false — `SeedX5j`'s `Is("landedWithin75m", true)` shape), so the forfeit gate
is `LessThanOrEqual(flightTime, target+60)`, not a greater-than.

Suites: Domain 898 green; Application 441 green; Architecture 18 green;
Infrastructure 91 green (fast loop, `Category!=Storage`);
Acceptance/sqlite 114 passed, 2 failed — **both pre-existing on clean HEAD**
(verified by stash-and-run: `CorsPreflightSmokeTests`
`The_SOARSCORE_CORS_ORIGINS_alias_is_honoured_comma_separated` and
`With_no_origins_configured_no_cors_headers_are_added`; unrelated to this
story — CORS wiring is untouched by it). **Docker was down this session**, so
the Infrastructure `Storage` (postgres/Testcontainers) and
Acceptance/postgres halves did not run — same caveat as
`nz-f3k-ndc-seed-class`'s completion note; run them against both stores when
Docker is up. `graphify update .` run.

## What

Author the seed-data Competition Class for **NZMAA Class H — Thermal 2 Metre**
("New Zealand Thermal 2 Metre Rules"), `NZ.5.5` in
`docs/rules/nz/source-docs/nzmaa-s5-soaring-2024.md`:

- Five flights against fixed targets of **3, 4, 5, 6 and 7 minutes**, flown in
  **any order** inside a CD-set contest window, each flight **nominated to its
  task only after it lands** (`NZ.5.5(d)(ii)`); at contest-time expiry only
  completed flights or models released from tow and being timed count
  (`NZ.5.5(d)(iii)`).
- **+1 point per second up to the target, −1 per second over, all points
  forfeited beyond 60 s over** (`NZ.5.5(e)(i)–(iii)`); **all flight and landing
  scores count, highest accumulated total wins** (`NZ.5.5(e)(iv)`) — raw sum,
  no normalisation, no groups, no drop.
- **Landing: 50 points inside a 15 m radius, measured to the nose**
  (`NZ.5.5(f)(i)`) — the class's OWN single-step bonus, not the `NZ.4.12`
  gliding table (100→30) and not the `NZ.4.13` electric table.
- Model/launching limits (2 m span, 3 servos, hand tow / pulley tow, bungee at
  CD discretion, no winch) are equipment rules — **not scoring data**, same
  discipline as the ALES altitude limits.

It becomes `SeedNzHThermal2m.cs` (corpus slot `87-nz-h-thermal-2m`), the
condensed rule doc `docs/rules/nz/class-h-thermal-2m.md` (approved in-session),
and rule-map rows.

## Why it matters

The seed corpus is the model's test: a real NZ format it cannot express is a
gap in the model. Analysis (2026-09-27) found **no gap** — Class H is
expressible as pure data with the existing vocabulary, and it is the first
modelled NZ class that is **tow-launched**, so `NZ.3.6(b)`'s repeat-attempt
grounds reach it (the ALES classes are self-launched and out of that clause's
scope).

## The model mapping

- **Five rounds, one task each, any order**: `Rounds { Kind =
  ChooseFromCatalogue, TasksPerRound = 1, RequireDistinctTaskPerRound = true,
  MaxRounds = 5 }` — the F3K-NDC shape (`kanban/completed/nz-f3k-ndc-seed-class.md`
  ruling 1). The post-hoc nomination is the recording act: a flight is recorded
  against the task it is nominated to; the draw's round order is a running-order
  convenience only (NFR-4).
- **Per-task score**: `Piecewise("flightTime", From(0).UpTo(S, 1).Rest(-1))` —
  the Classes M/N/P shape; Class N's worked example (400 s → 320, `NZ.7.5(d)`
  doc §4) fixes the −1/s-overtime reading for identical wording. Under-target
  flights score 1/s (partial credit) — the piecewise handles it, no clamping.
- **(e)(iii) forfeit**: `FlightValidWhen = GreaterThan("flightTime", S + 60)` —
  F17's zero-the-whole-flight gate (see decision 2). The score is a cliff: a
  3-min-task flight at 240 s scores 120, at 241 s scores 0.
- **Landing**: `Lookup("landingDistance", Rows.UpTo(15, 50).Rest(0))`, ungated —
  `NZ.5.5` adopts no observation-protocol clause (contrast `NZ.7.4(c)`).
- **Outside the field**: penalty `ZeroRound("landedOutsideFieldBounds")` —
  Class M's reading of `NZ.7.4(e)(i)` (zeroes that flight's flight+landing
  points); a round here IS one flight.
- **No group, no normalise (F25), no drops, `EqualPlaces`, no fly-off** →
  `SinglePhase`. One definition, no NDC twin — `NZ.5.5` states no NDC variant
  and the class as written IS the raw-sum, no-group, NDC-eligible frame
  (`SeedX5j` header reasoning).
- **Timing**: `UntilAllFlightsComplete` per task — "when to launch is up to the
  individual" (`NZ.5.5(d)(ii)`); no per-flight window exists to state.
- **contestTime**: no-default parameter, `BeforeFlying` (`NZ.5.5(d)(i)` CD
  determines, 3 hours suggested), **consumed by no scoring stage** — legal per
  the F3F unconsumed-parameter precedent; `(d)(iii)`'s expiry is contest flow.
- **Arithmetic check**: per-task max = target + 50 → 230 / 290 / 350 / 410 /
  470; contest max = (180+240+300+360+420) + 5×50 = **1750**.

## Owner decisions (2026-09-27 — Pete; in-session Q&A)

1. **Validity `MinRounds = 5`, fixed** — `NZ.5.5(d)(ii)` "required to complete
   flights of 3,4,5,6 & 7 minutes" is treated as stated (F3K-NDC ruling style;
   the F5K/NZ-M no-default-param pattern was considered and declined).
   Consequence, accepted: a weather-truncated contest (`(d)(iii)`) scores
   invalid for a competitor with fewer than five flights.
2. **The (e)(iii) forfeit zeroes the WHOLE flight** — flight points AND the
   landing bonus die. Encoded via `FlightValidWhen`. The alternative reading
   ("the points for that flight" = flight points only, landing survives) was
   rejected; `(f)`'s explicit "flight and landing" enumeration was read as
   scope-clarifying, not contrastive.
3. **Repeat attempts (`NZ.3.6(b)`) are encoded as nothing.** The grounds reach
   Class H (it is tow-launched), but their scoring outcome is unstated, so
   `Reflight` stays `UndefinedRequiresRuling`/`UndefinedRequiresRuling` (M-NDC
   precedent) with the `minNewGroup` no-default parameter; `MaxLaunches` is
   unset. A repeat is CD workflow — recorded evidence being an additional
   attempt that `LastFlight` selection ignores. Cost accepted: the system does
   not enforce the `(c)(vii)` once-per-official-flight limit or its grounds.
4. **Rule doc approved** — `docs/rules/nz/class-h-thermal-2m.md` per
   house-keeping rule 4, plus rule-map rows.

## Plan

- **WI-1** — `kanban/in-progress/nz-class-h-thermal-2m-seed.md` (this file).
- **WI-2** — `tools/Soarscore.SeedData/SeedNzHThermal2m.cs` per the mapping and
  decisions above; every constant carries its `NZ.5.5.x` ref; header records
  the decisions and the F12 residuals (flight-time and landing capture
  precisions unstated → Truncate 1 s / Ceiling 1 m chosen, threshold-safe).
- **WI-3** — `Corpus.cs`: `87-nz-h-thermal-2m` entry + `ExpectedCount` 16→17.
- **WI-4** — seed tool run: emits `json/87-nz-h-thermal-2m.json`, passes
  round-trip / source-gen / depth checks.
- **WI-5** — `NzClassHSeedArithmeticTests.cs` (black-box, `NzNdcSeedArithmeticTests`
  style): partial credit, exact target, over-target deduction, the 60 s cliff
  both sides, landing inside/outside 15 m, per-task maxima.
- **WI-6** — corpus-derived test bumps: `ScoringCorpusPropertyTests` drawable
  premise 15→16 (no `RequiredBindings` entry needed — nothing is consumed by
  its scoring stages; F3F's unbound `reflightAfterNPilots` precedent);
  `CatalogueDrawPropertyTests` sanity floor 4→5 (Class H joins the
  ChooseFromCatalogue/TasksPerRound=1 shape).
- **WI-7** — `docs/rules/nz/class-h-thermal-2m.md` (approved), rule-map NZ rows
  (including the `NZ.3.6(b)`-reaches-Class-H correction), SKILL.md corpus tree
  line.
- **WI-8** — suites green (fast loop; Infrastructure/Acceptance sqlite);
  `graphify update .`.

## Out of scope / follow-ups

- **Contest-time expiry enforcement** (`NZ.5.5(d)(iii)`): which flights count
  after expiry is contest-flow policy (capture/flow), not class data — the
  parameter records the CD's choice only. If flow enforcement is ever wanted,
  it is a new story.
- **`00-nz-general-rules.md` §1 class table is stale** (lists only M/N/P; X5J,
  the NDC formats and now Class H are missing) — flagged for approval, not
  edited silently (house-keeping rule 4).
- Notation-doc corpus counts (`competition-class-notation.md` §1: "sixteen") —
  same flag.