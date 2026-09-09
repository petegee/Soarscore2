# Fixture → seed parallel-run mapping

Corpus-wide pairing of every fixture in `tests/GliderscoreFixtures/` with a
seed class from `tools/Soarscore.SeedData/json/` — or its skip reason. This is
the plan of record for parallel runs, in the spirit of `index.md`: one primary
pair per fixture (defensible alternates live inside the row's `why`, never as
rows); the seed-side view lives in Seed coverage below. It turns "which pair
next?" into a lookup and exposes which seed classes have no witnessing fixture.

## Update contract

Nothing consumes this file mechanically today, so there is no validator. It is
kept honest by: the story that runs a pair flips its row to `done` and folds
the measured outcome into `why`; corpus growth adds its row in the same
change; seed growth extends Seed coverage in the same change. Revisit if
tooling ever consumes it.

## Row schema

    | fixture | seed | character | status | why |

`seed` is the seed JSON file name. `why` is ≤2 lines of prose + citations from
the closed set below.

## Closed vocabularies

Character — what the pair is FOR (one axis, exactly three):

- `near-twin` — the seed's scoring-relevant definition and the fixture's
  effective definition coincide (landing lookup/composition, duration
  piecewise, normalisation grid, drop config vs rule applicability); expected
  difference set empty or unwitnessed candidates only; expected final placings
  identical to the GS oracle.
- `witness` — at least one rulebook-vs-local-practice difference provable on
  the fixture's config or data; the pair exists to surface it.
- `not-expressible` — a runnability gate (below) fails; the harness would
  refuse loudly; terminal.

"near-exact" is retired as a character — it was an outcome word. Outcomes live
in `why`, never in the character column.

Status — orthogonal to character: `done` (parallel-run ledger landed),
`stubbed` (a sibling stub owns the pair), `planned` (this table only),
`refused` (terminal not-expressible). `refused` ⟺ character
`not-expressible`.

## Citations (closed set)

- `config <path>` — verified field-level fact (file + field named)
- `rule <ref>` — FAI `<doc> x.y.z` or `NZ.x.y.z` per the fai-rules skill
- `refusal <code>` — recorded harness refusal + the doc recording it
- `ledger <path>` — landed parallel-run ledger (`done` rows)
- `stub <path>` — sibling stub owning the pair
- `index <slug>` — the fixture's index.md line (corpus-skip-listed comps)

## Runnability gates

Checked in order; first failure ⇒ character `not-expressible`, status
`refused`:

- **G1 task catalogue** — every task code in the fixture's per-round schedule
  is in the seed's catalogue (F3K/F5K family); failure refuses with
  `prescribeDraw.taskNotInCatalogue`.
- **G2 round composition** — single task per round; multi-task refuses with
  `unsupportedRoundComposition`.
- **G3 metric observability** — every seed-declared metric is either captured
  by the fixture's capture map or `whenNotRecorded`-declared by the seed
  (`metric-absence-semantics`); otherwise the engine throws
  (`PredicateEvaluator.cs:38`, `FlightInterpreter.cs:217`).
- **G4 penalty infractions** — every fixture penalty row's infraction type is
  declared by the seed.
- **G5 landing evidence** — the fixture's recorded landing evidence is
  capturable against the seed's landing metric under the declared-instrument
  model: a distance names no instrument (the existing path, unchanged); a tape
  reading names a declared instrument whose scale composes with the seed's
  rulebook landing table (refinement — every class-table boundary a tape
  boundary — checked at declaration, never guessed). A reading with no
  composable declared scale refuses loudly; a points-kind metric is never
  declared (readings are validated against the tape's reading set, not stored
  as awards).
- **G6 standing concept gaps** — series without `triageJustification`,
  merged/prelim.

## Evidence bar

Static triage, no harness runs — the table is the plan of record; runs belong
to the pair stories. Every row cites ≥1 item from the citation set. A
`witness`/`near-twin` character is justified by verified-config or rule
citation, never class-name resemblance; a `refused` row names its failed gate
with a citation. A recorded refusal stands as evidence; re-running the harness
is not required to write a row.

## Pairs

| fixture | seed | character | status | why |
| --- | --- | --- | --- | --- |
| ales-sample-comp | 80-nz-m-ales200.json | near-twin | done | Landed parallel run: exact final placings; difference set = three triaged kind-1 raw-grain entries (GS folds landing into pre-normalisation raw, seed-run adds it post-normalisation; final cells identical). `ledger tests/GliderscoreFixtures/ales-sample-comp/parallel-run/80-nz-m-ales200.json` |
| f5j-christchurch-2019 | 30-f5j.json | witness | stubbed | Guaranteed final-aggregate split: fixture's drop thresholds null (GS summed everything) vs rulebook drop-from-5 — the seed drops the lowest round score. `stub kanban/backlog/f5j-christchurch-parallel-run-witness.md` |
| f3j-international | 50-f3j.json | witness | stubbed | Rounding-grid candidate: GS's proven HalfUp-1dp normalisation vs rulebook Truncate-0.1; the original guaranteed drop-divergence claim was withdrawn (thresholds agree). `stub kanban/backlog/f3j-international-parallel-run-retriage.md` |
| f3j-international-flyoff | 50-f3j.json | witness | planned | Local fly-off shape: GS scored all 4 rounds to the 900 s target (flights to 898 s, R1 raw 996) vs the seed Preliminary's 600 pt cap — provable at the raw grain; second candidate: GS HalfUp-integer grid vs rulebook Truncate-0.1 (981 vs 980.9). Drops agree at 4 rounds. `config tests/GliderscoreFixtures/f3j-international-flyoff/competition.json familyRows.Dur.durTargetTime` `config tools/Soarscore.SeedData/json/50-f3j.json phases[0].tasks[0].score[0].cap` `rule f3j.md F3J.10.11` |
| jerilderie-2010 | 50-f3j.json | not-expressible | refused | G5: the fixture's scheme-3 landings are readings on the NZ F3J-side tape — the rulebook distance lookup pre-composed on the tape (scheme-3 identity lookup; scores-raw Landing 93 → GS raw 262+93=355 is the tape's identity read, not a distance) — capturable only by declaring tape-nz-f3j-side for landingDistance and submitting the marks verbatim naming that tape, which no replay has yet run. The seed's metres metric and rulebook table stand unchanged and no tape-points seed exists: the instrument is declared, not derived. Status stays refused until WI-6 lands that witness. `config tests/GliderscoreFixtures/jerilderie-2010/competition.json lookups.landingSchemes[0].Name` `config tools/Soarscore.SeedData/json/50-f3j.json phases[0].tasks[0].metrics.landingDistance` `stub kanban/backlog/tape-points-landing-seeds.md` |
| f5j-hawkes-bay-trials | 30-f5j.json | witness | planned | Christchurch precedent (F5J family; launch height captured). Witness split: rulebook drop-from-5 fires at 10 scored rounds vs GS thresholds unset/99 (configProvenance: none can fire). Candidates: scheme-11 landing scale (55–100→5–50) vs the nose-to-spot table; HalfUp-1dp effective grid vs the seed's exact values. `config tests/GliderscoreFixtures/f5j-hawkes-bay-trials/competition.json configProvenance.note` `rule f5j.md 5.5.11.13` |
| f5j-nz-south-island | 30-f5j.json | witness | planned | Per-pair NZ decision: FAI seed over the loser 85c-nz-f5j-ndc.json — a 16-drawn/11-scored-round club F5J comp, not 85c's 4-round NDC format (maxRounds 4; its drops [] would erase the split); comps 45/135 pair identically. Witness: rulebook drop-from-5 at 11 rounds vs unset thresholds; candidates: scheme-11 landing scale; clamp row NS 0 and motor-restart row 0 on both sides. `config tools/Soarscore.SeedData/json/85c-nz-f5j-ndc.json phases[0].rounds.maxRounds` `config tests/GliderscoreFixtures/f5j-nz-south-island/competition.json scoring.Drop1AtRound` `rule f5j.md 5.5.11.13` |
| f5k-ni-round-2 | 40-f5k.json | witness | planned | Two config-provable rulebook-vs-local splits: GS Drop1At=5 (fired R5–6) vs the rulebook drop-from-7 the seed encodes; GS HalfUp-1dp (17/55 cells fractional) vs rulebook whole-point rounding. LaunchBands ≡ SeedF5K; the parity drop-pool divergence stays place-identical. `config tests/GliderscoreFixtures/f5k-ni-round-2/competition.json scoring.Drop1At` `config tools/Soarscore.SeedData/json/40-f5k.json phases[0].drops` `rule f5k.md 5.5.10.16` `index f5k-ni-round-2` |
| f3k-sample-comp | 10-f3k.json | not-expressible | refused | G1: nine-round schedule G, A(1), F, D, C(3), X×4 vs the seed's FAI catalogue A–L,N — the GS sub-numbered variants A(1), C(3) and X are not catalogue codes (the hunt story records the mechanism refusing on the same variant encoding, A(2)). Not NZ-sourced (example-comps export), so no NDC candidate. `refusal prescribeDraw.taskNotInCatalogue kanban/backlog/fai-conformant-f3k-fixture-hunt.md` `config tests/GliderscoreFixtures/f3k-sample-comp/competition.json scheduleTables.F3KTaskByRound.rows[*].Task` `config tools/Soarscore.SeedData/json/10-f3k.json phases[0].tasks[*].code` |
| f3k-southern-fling | 10-f3k.json + 85b-nz-f3k-ndc.json | not-expressible | refused | G1, NZ-sourced (NZContests.mdb slice): 15-round catalogue K,I,A(2),E,H,G,J,C(1),D,F,B(1),H,G,B(2),C(1) — vs 10-f3k's FAI A–L,N the variants A(2), C(1), B(1), B(2) refuse; vs 85b's NDC four (B,D,G,H) K, I, A(2), E, J, C(1), F, B(1), B(2) refuse (incl. index.md's first-sighting letters E/I/J/K) — no seed in the catalogue expresses the comp's task encoding. `refusal prescribeDraw.taskNotInCatalogue kanban/backlog/fai-conformant-f3k-fixture-hunt.md` `config tests/GliderscoreFixtures/f3k-southern-fling/competition.json scheduleTables.F3KTaskByRound.rows[*].Task` `config tools/Soarscore.SeedData/json/10-f3k.json phases[0].tasks[*].code` `config tools/Soarscore.SeedData/json/85b-nz-f3k-ndc.json phases[0].tasks[*].code` |
| f3k-june-2020 | 10-f3k.json + 85b-nz-f3k-ndc.json | not-expressible | refused | G1, NZ-sourced (NZContests.mdb slice): 13-round schedule B(1), D(1), G, H, M, A(2), E(1), C(1), I, J, K, L, F — vs 10-f3k's FAI A–L,N the variants B(1), D(1), A(2), E(1), C(1) and M refuse (the seed defines M only in its Flyoff phase); vs 85b's NDC four (B,D,G,H) all but G, H refuse. `refusal prescribeDraw.taskNotInCatalogue kanban/backlog/fai-conformant-f3k-fixture-hunt.md` `config tests/GliderscoreFixtures/f3k-june-2020/competition.json scheduleTables.F3KTaskByRound.rows[*].Task` `config tools/Soarscore.SeedData/json/10-f3k.json phases[0].tasks[*].code` `config tools/Soarscore.SeedData/json/85b-nz-f3k-ndc.json phases[0].tasks[*].code` |
| f3b-international | 20-f3b.json — moot | not-expressible | refused | G2: multi-task-per-round — 23 pilots × 9 rounds × 3 tasks/round (rounds 1–8 fly all three tasks, round 9 is speed-only) hits the deferred unsupportedRoundComposition rejection; standing skip. No fixture directory — no config to verify; the pair is moot because the comp cannot run. `index f3b-international` |

## Seed coverage

One line per seed file in `tools/Soarscore.SeedData/json/` — its witnessing
fixture(s) per the table above, or `uncovered` + one-line why. A seed named
only by `refused` rows is `uncovered (refused rows only)`; a seed named in a
`why` as a rejected alternate is not a witnessing fixture.

- `10-f3k.json` — uncovered (refused rows only) — named by all three refused F3K
  rows (sample-comp, southern-fling, june-2020): the GS sub-numbered variants
  (A(1)/A(2), B(1)/B(2), C(1)/C(3), D(1), E(1)), X and M refuse its FAI
  preliminary catalogue A–L,N under G1.
- `20-f3b.json` — uncovered (refused rows only) — named only by the refused
  f3b-international row, where the pairing is moot (G2 multi-task refusal; no
  fixture directory was curated).
- `30-f5j.json` — f5j-christchurch-2019 (witness, stubbed),
  f5j-hawkes-bay-trials (witness, planned), f5j-nz-south-island (witness,
  planned; won its per-pair NZ decision over 85c).
- `40-f5k.json` — f5k-ni-round-2 (witness, planned).
- `50-f3j.json` — f3j-international (witness, stubbed),
  f3j-international-flyoff (witness, planned); also named by refused
  jerilderie-2010 (G5 landing-evidence refusal — a rejection, not a witness).
- `60-f5l.json` — uncovered — F5L family absent corpus-wide (index.md "Still
  open": F5L/F5B families absent entirely); no F5L fixture exists to pair.
- `70-f3f.json` — uncovered — F3F family absent corpus-wide: no indexed comp is
  pure slope racing (index.md "Still open" records the sibling F5L/F5B absence;
  f3b-international's Speed/Distance rows are F3B and skip-listed).
- `80-nz-m-ales200.json` — ales-sample-comp (near-twin, done — the landed
  parallel-run ledger).
- `81-nz-m-ndc.json` — uncovered — NZ class M in NDC format (maxRounds 4,
  drops []); the corpus's only class-M-shaped fixture (ales-sample-comp) pairs
  with 80 (done), and no NDC-format ALES comp exists in the corpus.
- `83-nz-n-ales123.json` — uncovered — NZ class N (ALES 123, maxRounds 3); no
  class-N fixture in the corpus (the NZ-master growth brought F5J/F3K fixtures
  only).
- `85-nz-p-radian.json` — uncovered — NZ class P (ALES Radian, maxRounds 3);
  no Radian/class-P fixture in the corpus.
- `85b-nz-f3k-ndc.json` — uncovered (refused rows only) — named only by the
  refused NZ-sourced F3K rows (southern-fling, june-2020) as the rejected NDC
  candidate: its four-task NDC catalogue (B,D,G,H) is narrower than every
  corpus F3K schedule.
- `85c-nz-f5j-ndc.json` — uncovered — lost the per-pair seed decision for
  f5j-nz-south-island (the row chose 30-f5j over 85c's 4-round NDC format,
  whose empty drops would erase the drop split); no other F5J fixture is
  NDC-format.
- `85d-nz-f5k-ndc.json` — uncovered — the corpus's only F5K fixture
  (f5k-ni-round-2) pairs with 40-f5k.json (single Preliminary phase, catalogue
  A–D); no NZ F5K NDC fixture exists.
- `86-nz-x5j.json` — uncovered — NZ X5J Unlimited (maxRounds 4); all three
  corpus F5J-family fixtures (christchurch, hawkes-bay, south-island) pair
  with 30-f5j.json; none is an X5J comp.
- `90-aggregate.json` — uncovered by design — NZ free-flight Aggregate (D5):
  the corpus is RC gliding; the class cannot witness here.
