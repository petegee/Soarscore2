# Story — NZ F3K NDC seed class

**Status:** Completed (2026-08-30) · **Raised:** 2026-08-29 (three-way
cross-reference of seed `10-f3k.json` vs FAI F3K.11 vs NZMAA S5 during the
seed-vs-corpus conformance analysis) · **Rulings received:** 2026-08-30 (Pete,
recorded below)

## Completion note (2026-08-30)

Built per the plan (WI-1..WI-4): `SeedNzF3kNdc.cs` (4 tasks, 2 parameters, 4
rounds, no drop), corpus entry `85b-nz-f3k-ndc`, test counts raised (11→12
drawable; catalogue-choice floor 2→3), seed tool emits `85b-nz-f3k-ndc.json`
and passes round-trip/source-gen/depth checks. Full suite green (522 Domain,
218 Application, 7 Architecture, 64 Infrastructure, 64 Acceptance/sqlite) with
the `Category!=Storage` filter for the two Docker-dependent suites — Docker was
not running in this session; run them against both stores when Docker is up.
One test edit beyond the plan: `CatalogueDrawPropertyTests`'s catalogue-choice
sanity floor is corpus-derived and needed raising (the story's WI-3 only
predicted `ScoringCorpusPropertyTests`). Nothing deferred; no new tech debt.
The `graphify` CLI quirk noted under "Before starting" persists —
`graphify update .` skipped.

## What

Author the seed-data Competition Class for the NZMAA **F3K National
Decentralised Contest (NDC)** format — `NZ.0.2` / `NZ.0.2.1` in
`docs/rules/nz/source-docs/nzmaa-s5-soaring-2024.md`:

- **Task catalogue: B, D, G and H only** (`NZ.0.2.1 a`), with the task
  arithmetic quoted verbatim from FAI 5.7.11.2 / 5.7.11.4 / 5.7.11.7 /
  5.7.11.8 — identical numbers to `SeedF3K.cs`'s tasks.
- **Scoring frame: sum of raw scores** (`NZ.0.2.1 a` "Total is sum of raw
  scores") — no per-group normalisation, therefore no drop-worst.
- **Timing to 0.1 s, truncated** (`NZ.0.2.1 a`: "59.99 seconds is recorded
  at 59.9 seconds") — already the seed F3K metric precision.

It becomes a new `SeedNzF3kNdc.cs` (canonical JSON `85b-nz-f3k-ndc.json` slot)
and a seed-corpus test presence like its siblings.

## Why it matters

S5 §3.8.1 retired NZ's own HLG class and nominated FAI §5.7 as *the* F3K
rulebook; §0.2 is its only NZ variation, and it changes the scoring frame,
not the tasks. The seed corpus is the model's test — a real NZ format it
cannot express would be a gap in the model. This one is expected to express
as pure data: the no-normalisation raw-sum frame already exists
(`SeedNzMNdc.cs` for Class M, citing `NZ.3.12.7 c`; Classes N/P per
`NZ.3.13.1 i` / `NZ.3.15.1 i`), and the four task bodies already exist in
`SeedF3K.cs`.

## Rulings (2026-08-30 — Pete; these supersede the story's earlier
"do not assume four-rounds-one-per-task" caution, which was written before
the ruling)

1. **Round count: fixed at 4 rounds, one per task** (B, D, G, H each once) —
   the Class M NDC / F5J NDC shape, chosen by ruling despite NZ.0.2's silence.
   Encode: `Rounds { Kind = ChooseFromCatalogue, TasksPerRound = 1,
   RequireDistinctTaskPerRound = true, MaxRounds = 4 }` and
   `Validity = { MinRounds = 4 }`. No `minRounds` parameter — the F5K
   no-default-param pattern was considered and the CD-frees-it option declined.
2. **FAI F3K competition rules carry** into the NDC (§3.8.1 nominates FAI §5.7
   wholesale; NZ.0.2 is the only NZ variation). Specifically:
   - **Re-flights:** F3K.9.6 carries — `Replacement` / `BetterOf` /
     `MinNewGroupSize = 4`. Stated rule, not silence — NOT
     `UndefinedRequiresRuling` (that is the Class M NDC shape, which arose
     because NZ.3.12.5 l is silent; F3K.9.6 is not).
   - **Groups:** F3K.9.1 carries — `Group = { MinPerGroup = 5 }` on each task.
     Only F3K.9.1's *normalisation sentence* is superseded by the raw-sum
     frame; the group-size and draw side still governs.
   - **Penalties:** the FAI F3K penalty schedule carries — transplant
     `SeedF3K.cs`'s penalty block verbatim (F3K.1.2 zero-round unsigned
     scorecard, F3K.9.5 100-pt testing window, the F3K.4.3
     `safetyInfraction` exclusion group 100/300/100/100, F3K.4.1
     `personContactAtLaunch` zero-round) with their citations.
3. **No drop** — NZ.0.2.1 a "Total is sum of raw scores" restates the total
   computation; a dropped round would contradict it. `Drops` stays empty with
   a citation comment, in the style of `SeedNzNAles123.cs` ("no drop: NZ.3.13.1 i").

## Rulebook findings (verified against the corpus this session)

- **The rulebook context:** S5 §3.8.1 defers F3K to FAI §5.7 wholesale; the
  PREFACE points F3K NDC at §0.2. NZ.0.2 varies only (a) team references do
  not apply, (b) catalogue B/D/G/H + raw-sum total, (c) 0.1 s truncated
  timing. Everything else is FAI F3K. NZ.0.3 (F5J NDC) states "4 rounds"
  explicitly and NZ.3.12.7 (Class M NDC) states 4 explicitly — NZ.0.2's
  silence on rounds is real in the text, and Pete has now ruled it 4.
- **F3K.10's "minimum of five rounds each with different tasks" cannot carry**
  — the NDC catalogue has only 4 tasks, so the FAI round structure is
  unachievable and is varied away by NZ.0.2's catalogue restriction (and now
  by the 4-round ruling). This is why the round structure is restated on the
  NDC definition rather than inherited.
- **F25 applies** (notation §13): `normalise` is optional; the four tasks
  transplant WITHOUT their `Normalise` block. There is no normalisation scale
  anywhere in the class (relevant to `kanban/backlog/tie-break-policy-in-class-definition.md`).
- **Adoption checks that bite:** check 14 (`ScoreNormalised` with no
  normalise stage is rejected — we have no `ScoreNormalised` at all, fine);
  check 16 (exclusion group admits only DeductPoints effects — the transplanted
  `safetyInfraction` group qualifies); check 13 (only if `NotPermitted`
  were used — not our case).
- **Precedents to copy:** `SeedNzMNdc.cs` (structure, comment and citation
  style; its arithmetic-check closing comment is the template — note NZ.0.2
  states no maxima to check against, so the analogous closing comment records
  the raw-sum identity instead); `SeedF3K.cs` (the four task bodies, the
  metrics block, the penalty block).

## Build plan (WI-1 .. WI-4)

**WI-1 — `tools/Soarscore.SeedData/SeedNzF3kNdc.cs`.** Class-level:
`Name = "RC Hand-Launch Gliders (NDC format)"`; `FaiDesignation = "F3K"` —
this rulebook class IS FAI F3K per §3.8.1, unlike ALES 200 which has no FAI
counterpart (the four NZ definitions' blank designation does not transfer);
`Version = "NZMAA Section 5 Soaring, March 2024"`; no `FinalRanking`
(SinglePhase — NZ.0.2 has no fly-off); `Reflight` per ruling 2; penalties per
ruling 2. Parameters: only `workingTime.B` (600, allowed [420, 600], PerRound)
and `maxFlight.B` (240, allowed [180, 240], PerRound) — NZ.0.2.1 b restates
both the 10- and 7-minute variants; nothing else in B/D/G/H is a parameter
(D/G/H working times are literal 600). One phase: Preliminary, ordinal 1,
`ChooseFromCatalogue`, `TasksPerRound = 1`, `RequireDistinctTaskPerRound = true`,
`MaxRounds = 4`, `Validity MinRounds = 4` (ruling 1), `Drops` empty (ruling 3),
`Tasks = [B, D, G, H]`.

Task transplant notes (all four share `FlightMetrics` as in `SeedF3K.cs`, with
the NZ.0.2.1 a timing citation added alongside F3K.7 — same Truncate/0.1s):

- **B:** `LastNFlights(2)`, `Timing = Fixed param("workingTime.B")`,
  `T.Rate("flightTime", 1, cap: param("maxFlight.B"))`. NZ.0.2.1 b restates
  the worked example (55 + 85 = 140).
- **D:** `AllFlights`, `Fixed 600, MaxLaunches = 2`, `Rate 1 cap 300`
  (SeedF3K's D inherits A's score term — restate it explicitly here).
- **G:** `BestNFlights { Count = 5 }`, `Fixed 600`, `Rate 1 cap 120`.
- **H:** `BestNFlights { Count = 4, RankByMetric = "flightTime",
  Targets = AnyOrder, TargetValues = [60, 120, 180, 240] }`, `Fixed 600`,
  `Rate 1` uncapped. The `rankBy` comment is load-bearing — carry it.
  NZ.0.2.1 e restates the worked example (569).
- Each task: `FlightValidWhen = P.All(landedWithinWindow, launchedInWorkingTime)`
  (F3K.9.3 / F3K.7 carry per ruling 2); `Group = { MinPerGroup = 5 }`
  (F3K.9.1 carries per ruling 2 — group governs the draw even though nothing
  normalises; note the superseded normalisation sentence in a comment); NO
  `Normalise` (F25).

**WI-2 — corpus wiring.** `Corpus.cs`: add `new("85b-nz-f3k-ndc",
SeedNzF3kNdc.Definition)` between `85-nz-p-radian` and `90-aggregate`
("85-" sorts before "85b"); update the header comment counts (now thirteen
definitions; the NZ block is five).

**WI-3 — test presence.** `tests/Soarscore.Domain.Tests/ScoringCorpusPropertyTests.cs`:
`drawable.Length.Should().Be(11)` → `12`, and the premise comment "11 of the
12 corpus classes" → "12 of the 13". Everything else that iterates
`Corpus.All` (adoption/validation limits, canonical JSON emit, property
tests) picks the class up automatically. No `RequiredBindings` entry is
needed: `workingTime.B`/`maxFlight.B` resolve from defaults and `MinRounds`
is a literal 4 now.

**WI-4 — emit + verify.** `dotnet run --project tools/Soarscore.SeedData`
(emits `json/85b-nz-f3k-ndc.json`; `json/` is gitignored), then the full test
suite. The seed tool's round-trip / source-gen / depth checks are the
definition's first gate; `ClassDefinitionValidationTests` (adoption checks)
is the second.

## Before starting (residual items for the builder)

- **No-new-concepts gate:** nothing new is needed — NDC, raw-sum aggregation
  and the task vocabulary all exist. Stop and surface if the build suggests
  otherwise.
- **Cross-references checked (house rule 2), no contradictions found:**
  `docs/users.md`, `docs/non-functional-requirements.md`,
  `kanban/backlog/tie-break-policy-in-class-definition.md` (its "rulebook
  silence for every NZ class" note gains this class as a fourth instance;
  no action), `kanban/backlog/fai-conformant-f3k-fixture-hunt.md` (an NDC
  fixture replays against *this* class; either story can land first).
- **Do not touch** `docs/` (house rules 3–4). `docs/rules/nz/` needs no
  change; the corpus's `00-nz-general-rules.md` §1 already says the §0.0 NDC
  formats are "not modelled here" — this story models the F3K one, so if a
  doc update is wanted it is a separate approval-gated ask.
- **Board discipline:** `git mv` the story to `in-progress/` before writing
  code (lane is currently empty); on completion move to `completed/`, set the
  status header, and reconcile `tech-debt.md` / `deferred-decisions.md`
  (nothing deferred is known at raise time; add anything found).
- **Known environment quirk:** the `graphify` CLI is currently broken
  (`ModuleNotFoundError: No module named 'graphify'`), so `graphify query`
  and the post-change `graphify update .` cannot run — skip them and mention
  it in the completion note rather than debugging it inside this story.
