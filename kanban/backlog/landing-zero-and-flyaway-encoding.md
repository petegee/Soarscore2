# Story — Paper-convention encodings: an entered 0 on landing scores zero; a flight that reaches the horn earns no landing

**Status:** Backlog (raised 2026-09-27, unblocking NdcScore
`kanban/blocked/ss_landing-zero-scores-no-landing-points.md` and
`kanban/blocked/ss_f5j-flight-time-cap-at-959.md` — one ambiguity family,
one design answer: **the definition interprets paper language**). Sibling
story: `minimum-score-floor.md` — the three scoring fixes it shares the F5J
score sum with are to land in one review, one seed drift-guard run.

## What

Two paper-scoresheet conventions collide with metric readings the model
treats as legal values, and in both cases the engine today silently scores
the wrong thing:

1. **Entered 0 on landing.** A **0** in a landing-distance cell is the paper
   convention for "landed beyond the tape" — it must score **zero landing
   points**. Today a captured 0 falls in every landing table's first band
   `[0, 1]` and earns the *best* award (50 in NZ-M 81) — the opposite of
   intent. A blank cell stays "no result" (unchanged, accepted).
2. **A stopwatch that reads the horn (600).** On F5J paper a reading of
   **10:00** means the model flew away and never landed; a sheet showing a
   600 s flight *together with landing points* is an impossible state. Today
   the engine scores exactly that: 600 flight points *plus* whatever landing
   was entered. The organiser's steer: **reinterpret, not reject** — cap the
   flight at 9:59 and let 600 mean zero landing.

Both fixes are **definition data, not engine code** (the flyaway one plus at
most a small expression gap, below). The client must never branch per class
on either (NdcScore law 3).

## Verified facts (SoarScore2, traced 2026-09-27 from the two NdcScore blocked stories)

**Landing zero.**

- NdcScore posts the entered text verbatim: `parseCellText("0", Number, "m")`
  → `{kind: "Number", number: 0}` (NdcScore `src/grid/parse.ts`), pinned by
  regression tests at the parse layer and through `capture-measurement`;
  blank stays "no result". The client mangles nothing.
- `FlightInterpreter.EvaluateLookup`
  (`src/Soarscore.Domain/Scoring/FlightInterpreter.cs:180-187`) walks rows
  ascending and awards the first row with `metricValue <= UpTo`. A captured
  0 falls in the first band of every corpus landing table — verified in
  `85c-nz-f5j-ndc.json:288-291` (`{upTo: 1, points: 50}`) and
  `81-nz-m-ndc.json:156-160` (same). No engine code special-cases 0 anywhere.
- Adoption check 9 (`ClassDefinitionValidation.cs:311-335`,
  `class-definition.check-9.rows-not-ascending`) compares only *consecutive*
  bounded rows (`:332`) — a leading `{upTo: 0, points: 0}` row is legal
  today: **no engine change for the distance path**.
- Landing tables exist across the corpus in 20-f3b, 30-f5j, 50-f3j, 60-f5l,
  80, 81, 83, 85, 85c, 86 — every first band starts at 0, none reserves a
  meaning for an exact 0.
- Composed-tape caveat: a declared instrument's first mark band is
  `[0, UpTo[0]]` and `ComposeBand` awards by the band's *upper* bound
  (`ReadingScale.cs:12-13`, `:188-189`) — so a tape-declared competition
  would still award reading 0 the first band's points. NDC competitions
  declare no instruments, so this path never engages for them; the
  carve-out there is a decision of this story (see design questions).

**Flyaway (600 on the stopwatch).**

- Client split — the F5J stopwatch pair (`flightTime` + `overflySeconds`,
  `85c-nz-f5j-ndc.json:133-177`) has NdcScore split one reading:
  `10:00` → flight `min(600, 600)` = **600**, overfly `max(0, 600−600)` =
  **0** — and 0 is the metric's declared absence (`whenNotRecorded: 0`,
  `:173-176`), so **no overfly is captured at all**. Worse: any reading in
  **[600, 601)** loses its sub-second overfly the same way (Truncate/1) —
  e.g. 600.4 s carries a 0.4 s overfly the declared precision truncates to
  0. The engine receives a *perfect in-window flight*.
- Capture gates nothing here by design: `Entry.CaptureMeasurement` gates
  only flight-not-found, metric-not-declared and kind-mismatch; "flight
  times are NOT checked against any working time at capture" (comment at
  the head of `Soarscore.Domain/Entries/Entry.cs`). No metric carries a
  range or cap at capture.
- Wire: `MetricDefinition` (the `GET /class-definition` projection) carries
  name/kind/unit/declaredBeforeLaunch/precision/whenNotRecorded only — no
  min/max/allowed-range (verified in `85c-nz-f5j-ndc.json`'s metric blocks
  and the NdcScore `openapi/v1.json`). Nothing for NdcScore to render a cap
  from.
- Scoring today (85c): rate `flightTime × 1 pt/s`, cap 600 PerFlight
  (`:236-243`) — a 600.0 s flight earns the full **600**; the landing lookup
  is conditional on `overflySeconds == 0 ∧ ¬touchedByCompetitor`
  (`:259-283`), which **holds** in the defect band (the overfly is *absent*,
  not merely zero) — so an entered landing distance awards its table points
  on top. Result: **600 + landing — the impossible state.**
- The rules: `5.5.11.12 b` truncates flight time to the nearest second
  (`f5-electric-2026.md:955`); `c` awards 1 pt/s up to 600 preliminary /
  900 fly-off (`:956-958`); `k` — **"No landing bonus will be awarded if the
  model aircraft overflies the end of the Working Time for the Group"**
  (`:996-997`); `g` — zero for overflying by more than 1 minute
  (`:964-965`). The definition already implements both *for a captured
  overfly* — the defect is precisely the band where an overfly existed but
  does not survive the declared rounding.

## Why it matters

First real-use feedback, verbatim:

> "I might suggest that a zero entry on the landing is for a zero landing
> score. If I leave the landing distance blank, the raw score is 'no result'"
> "…an entered zero being scored as a >10m (or 15m depending on class). To
> be stupid pedantic, it is impossible to get an exact 0m landing. There
> will always be a small delta…"

> "F5J, may be best to cap at 9:59 instead of 10:00" — "That is, a 600 sec
> flight results in a zero landing."

The pedantic point is the licence: an exact 0 m reading is physically
impossible, so reserving exact 0 as "zero landing score" loses nothing real
and matches what paper-takers mean. The paper convention ("600 means
flyaway") collides with the metric convention ("600.00 s is a legal
reading"), exactly as 0-on-landing does.

## Cross-reference (house rule 2)

- **NFR-1/NFR-2** — both fixes live in the published class definitions; no
  engine branch on any class. The flyaway landing test reuses the existing
  `conditional` `when` predicate vocabulary; nothing new is added to the
  wire.
- **NFR-4** — reject-at-capture was considered and set aside (below);
  capture remains ungated.
- **Rulebooks** — the landing table edits match the tables as printed
  (`5.5.11.12 h`, `f5-electric-2026.md:968-987`) plus the
  physically-impossible-reading convention; the flyaway reading is the
  organiser's *reinterpret* steer, to be settled against `b`/`c`/`k` (design
  question 1) before seeding. `docs/rules/` is untouched (house rule 1).
- Sibling `minimum-score-floor.md`: all three fixes edit the same F5J score
  sum — land in one review so the seed drift guard runs once. No overlap in
  mechanism (floor = engine datum; these = definition data).

## Plan

- **WI-1 — landing zero rows.** Insert a leading `{upTo: 0, points: 0}` row
  in the landing lookup tables of the NDC definitions (81, 83, 85, 85c, 86 —
  and 80's table for its distance path), and the same row in the FAI
  classes' tables (20, 30, 50, 60) — the exact-0 reading is equally
  impossible there, and stating the convention in every definition is what
  stops future classes diverging silently (confirm scope with the owner,
  Before starting). Band `[0, 0]` awards zero; the tables are otherwise
  untouched; adoption check 9 passes as-is.
- **WI-2 — the flyaway landing test.** The landing conditional's `when`
  gains a flight-time test so a flight that reached the horn earns no
  landing: `overflySeconds == 0 ∧ ¬touchedByCompetitor ∧ flightTime < W`
  (85c: `flightTime < 600`, `:259-283`). For a Fixed working time this is
  expressible today as a literal `Comparison`
  (`ScoringVocabulary.cs:156-166` — metric ↔ literal; there is no
  ParameterRef slot in predicates). The same test covers the sub-second
  band [W, W+1): a 600.4 s reading truncates to flight 600 → fails
  `flightTime < 600` → no landing — pinned by test.
- **WI-3 — the flyaway flight cap.** Drop the rate cap 600 → 599 on the
  affected tasks ("cap at 9:59") so a flyaway reading cannot out-score a
  landed 9:59 flight — 85c first, then the same examination for every
  definition carrying the stopwatch pair: 30-f5j and 50-f3j (flight 0.1 s,
  overfly Truncate/1 — readings in [600, 601) lose the overfly the same
  way; their landing/overfly-penalty conditionals need the same flight-time
  test), 60-f5l (2 pt/s, cap 390 within a 540 s window), and the fly-off
  tasks (W 900). Only edit where the class's own rules/convention state it —
  FAI classes normalise, so each edit must be argued from that class's text.
- **WI-4 — the working-time expression gap (decide, maybe defer).** A
  parameterised or `UntilAllFlightsComplete` task cannot write
  `flightTime < W` (no task-timing operand; `TaskTiming.WorkingTime` is a
  `NumberOrParam`, predicates take no ParameterRef). The whole corpus's
  affected tasks are Fixed (600/900), so nothing needs it today — record
  the decision and the gap in this story (and `deferred-decisions.md` on
  landing) rather than inventing vocabulary speculatively.
- **WI-5 — seeds and fixtures.** Regenerate `tools/Soarscore.SeedData/json/`
  with the SeedData tool (integrity checks green); mirror the edited
  definitions into the NdcScore fixtures (`src/test/fixtures/85c-nz-f5j-ndc.json`
  et al.); run the golden/parallel-run drift guard — fixtures whose class
  does not change must be byte-silent. Drift-guard discipline per
  `kanban/backlog/ci-seed-corpus-drift-guard.md`.
- **WI-6 — tests.** **Property tests (CsCheck)** — named invariants:
  (i) *zero row*: for any distance d > 0 the award is unchanged from the
  legacy table; at d = 0 it is exactly 0 — `new(d) == old(d) ∀ d > 0`,
  `new(0) == 0`; blank (absence) still yields "no result" at every grain.
  (ii) *horn*: for any stopwatch reading s and any declared precision in the
  corpus, a reading ≥ W earns no landing award and its flight term never
  exceeds 599 × rate (85c) — the flyaway can never out-score a landed
  9:59. Unit tests pin each edited table and each edited `when`.

## Design questions to settle

- **Semantics of exact 0:** "exact 0 = zero points" vs "exact 0 = the
  terminal band's award". They coincide (0 points) in every corpus class;
  pick one reading and state it in the definitions so future classes can't
  diverge silently.
- **Reject vs clamp vs reinterpret** was the organiser question; the steer
  is reinterpret (this plan). Confirm with the owner before seeding.
- **Is 600.00 s a legal *time* under `5.5.11.12 b/c`, or is the max awarded
  time 599 s by rule?** If the latter, the rate-cap change 600 → 599 is the
  *correction* and 600-⇒-no-landing its consequence — settle from the FAI
  wording, not the convention.
- **Generalisation:** is "a reading that reaches the horn with no surviving
  overfly = flyaway" one definition idiom shared by every stopwatch-pair
  class, or per-class data? Either way the client never branches (NdcScore
  law 3) — but the answer decides how WI-3's sweep is argued.
- **The composed-tape carve-out:** if the exact-0 convention must hold on a
  tape-declared competition too, the scale's first mark band or
  `ComposeBand` needs the same exact-zero rule (`ReadingScale.cs:12-13`).
  No NDC competition declares instruments; decide whether to close it now
  (the tape witness, `jerilderie-2010-tape-witness.md`, is still backlog) or
  defer with the reasoning recorded.
- **Negative or sub-1 m landing entries:** with the leading row, a negative
  distance also falls in `[0, 0]` and awards zero silently. Should capture
  warn? (The source story's open question — keep open, not blocking.)

## Before starting

- Confirm the organiser steer (reinterpret) and the FAI-class sweep scope
  with the owner.
- Settle the 600-legality question from `5.5.11.12 b/c` wording.
- Land together with `minimum-score-floor.md` (same review; one seed
  drift-guard run).

## Alternative homes considered and set aside

- **Engine capture validation (reject/clamp the 600 reading; reject the 0
  landing)** — needs new `MetricDefinition` range data on the wire plus a
  capture gate, more machinery, and it contradicts the organiser's expressed
  preference (the reading is legitimate *paper language*; it is the scoring
  that must interpret it). Also an NFR-4 tension.
- **Client-side interpretation** — forbidden: NdcScore law 2 (no score
  arithmetic in the client) and law 3 (no per-class branching).

## NdcScore's part once it lands

Nothing structural. The client already posts the 0 verbatim and blank stays
"no result"; scores flow from `GET /competition-result` untouched. The
Calculate-time horn warning (landed as the stopgap, `splitStopwatchCell` in
NdcScore `src/sheet/calculate.ts`) should be reworded or retired once the
definitions interpret the flyaway, so the sheet never contradicts live
engine behaviour. The faint "0 = no landing points" placeholder on distance
columns (`SheetPage.tsx`, via the existing assumption-placeholder mechanism)
becomes consistent to add once this ships.