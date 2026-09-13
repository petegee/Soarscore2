# Story — Teams grain in the parallel-run comparison

**Status:** Backlog — plan written 2026-09-13; decisions 1–7 proposed,
owner confirmation gates WI-1.
**Raised:** 2026-09-10 — closing the deliberate no-team-grain stance now that
teams scoring is implemented (see `Features/ScoringTeams.feature` acceptance
coverage); surfaced by the f3j-international parallel run.
**Researched:** 2026-09-13 — every dependency verified in the tree; the story's
"expected inert" prediction corrected (finding F1 below); no board collisions
(`kanban/in-progress/` empty at planning).

## What

Add a **teams grain** to the parallel-run comparator
(`tests/Soarscore.Acceptance.Tests/Support/Gliderscore/ParallelRunComparator.cs`).
Today it compares raw, normalised and ranking grains per pair and
deliberately does not compare team standings — a disclosed stance (the ales
precedent, restated in the f3j-international ledger's provenance notes,
`parallel-run/50-f3j.json` note 3). Teams are now implemented and
acceptance-covered, so the comparator compares the seed-run's team standings
against the GS oracle's team ladder (`expected-teams.json`, already loaded as
`GliderscoreFixture.ExpectedTeams`) the way it already compares final
placings: ledgered triaged differences, fully enumerated where small.

First candidate pair: `f3j-international` — `UseTeams=true`, 30 pilots across
8 teams (sizes 4,3,4,4,4,3,4,4), `NbrForTeamScore=3`, carrying the corpus's
only `expected-teams.json` (the reconstructed, transcript-verified GS team
ladder). Its existing ledger pins the individual grains; this story adds the
team-grain entries on top, never rewriting them.

## Why it matters

Team standings are a real product the club sees on the day. The parallel-run
discipline's product is "what the club would have seen differently"; without a
teams grain, a team-standings split can hide behind an individually-triaged
ledger even when every pilot cell is triaged. The f3j-international pair is
the right *first* pair — but for a corrected reason (F1): the team
*method* agrees on both sides (proven on the parity path), so every
team-grain difference is a **consequence** of the already-triaged individual
classes and no new difference *cause* can hide there. Proving the grain on a
pair whose team differences are fully explained by triaged causes is exactly
the inert-at-the-cause-level proof the discipline wants.

## Research findings (2026-09-13 — corrections to the stub's assumptions)

- **F1 — "expected inert" is wrong at the difference-set level.** The stub
  read the ledger note ("team standings are report-time aggregations of
  unchanged individual normalised scores") as predicting no team split. It
  predicts no *independent cause*, not an empty grain. The individual final
  aggregates differ by the triaged classes (20-pilot placing split; ledger
  entries state aggregate deltas: p42 +493.2, p2 +570.4, p13 +188.6, p30
  +146.1 …), and a team total is the sum of its top-3 members' aggregates —
  so the seed-run's team ladder **will** split from GS's. Planning arithmetic
  (ground truth below) predicts all 8 totals split and at least two team-place
  swaps (T3↔T4, T8↔T1). The grain is expected **non-empty**; WI-2 curates
  from the measured run, never from this prediction.
- **F2 — no ledger schema widening is needed.** The stub predicted widening
  "following the null-tolerant `ExcludedOracleCells` precedent". Unnecessary:
  `ParallelRunDifferenceEntry.Covers`
  (`ParallelRunLedger.cs:201-207`) matches grain + round + group + pilot, and
  `pilotNo` already accepts any number — a teams entry is
  `grain: "teams"`, `round`/`group` null, `pilotNo: <GS team number>`. The
  ales and christchurch ledgers deserialise unchanged with zero changes to
  `ParallelRunLedger.cs`.
- **F3 — blast radius is exactly one landed pair.** The grain's gate is the
  parity `TeamGrainOverlap` predicate (`Comparator.cs:1037-1040`):
  `UseTeams=true` ∧ `NbrForTeamScore==3` ∧ populated teams. Among the landed
  pairs only f3j-international gates it on: ales is `UseTeams=false` (GS
  computes no team standings either); christchurch is `UseTeams=true` but all
  18 `CompPilots.Team='0'` **and** `NbrForTeamScore=2` (double-skip — its
  ledger note already says the team mapping no-ops). Forward obligations
  recorded in WI-4: f5j-hawkes-bay-trials (planned pair, `NbrForTeamScore=3`,
  teams populated) will need an `expected-teams.json` oracle authored when its
  story lands or the new grain's guard throws; christchurch's `NbrForTeamScore=2`
  method split is a separate future story (house rule 6 stub).
- **F4 — the method comparison is already settled, not a new candidate.** The
  stub asked "whether GS team arithmetic matches them or is another
  rulebook-vs-local-practice candidate". Answered: GS's ladder
  (`expected-teams.json` notes; `GetTeamScores`) sums the top-3 **final
  aggregates** by score — exactly the MVP's
  `bestThreeScoreSum` over `competitionFinalAggregate`
  (`TeamClassification.cs:128,131`) — and the parity ladder grain
  (`Comparator.cs:1294`, grow-corpus-team-parity-fixtures.md WI-1D) already
  proves the agreement on this very fixture. Teams entries cite that
  agreement; they are consequences, kind 1, permanent.
- **F5 — the harness already runs the teams mapping in parallel-run mode.**
  `ReplayDriver.MapGliderscoreTeamsAsync` is called from the shared path
  (`ReplayDriver.cs:525`), mode-independent, and configures classification
  when `NbrForTeamScore==3` (`:966-974`). The seed-run publishes team
  standings at `/competition-team-result`
  (`ScoreTeamStandings.cs:37`) with no new plumbing. The stub's stale path
  reference ("tests/GliderscoreAcceptance") is actually
  `tests/Soarscore.Acceptance.Tests`.
- **F6 — house rule 2 cross-reference (done at planning):** `docs/users.md`
  carries no team-roles content (the stub's pointer was stale — nothing to
  contradict); NFR-1/NFR-2 are *supported* (no `src/` change; the grain is
  harness-side); no new domain concepts — "teams grain" extends the
  comparator's existing grain vocabulary (raw/normalised/ranking), harness
  corpus vocabulary like "sentinel" and the count pins before it; nothing in
  `/docs` changes (house rules 3–4).

## Settled decisions (2026-09-13, proposed — owner may veto any before WI-1)

1. **Gate = the parity `TeamGrainOverlap` predicate verbatim**, made
   `internal` (the plumbing note at `Comparator.cs:1524-1528` already shares
   internals with `ParallelRunComparator`). Skip arms documented in the file
   header, never silent in code-reading terms: `UseTeams=false` → GS computes
   no team standings, nothing to compare (the ales precedent); `NbrForTeamScore≠3`
   or no populated teams → a different (or absent) GS method is never emulated
   (R1 discipline). No provenance-verification additions — no skip needs
   disclosure until a pair exists whose gate arms differ from the landed
   three (none does; F3).
2. **Oracle required where the gate opens.** A team-bearing overlap pair
   without `expected-teams.json` throws before anything is compared — the
   parity ladder grain's guard discipline (`Comparator.cs:1306-1313`): a
   curation bug, never a skip.
3. **Computed-difference shape = reuse `GrainMismatch`** with a documented
   convention: `grain: "teams"`, `PilotNo: <GS team number>`,
   `RoundNo: 0`, `GroupNo: 0` (the teams grain has no round/group scope —
   the same generic-coordinate pattern as the ranking grain's null
   round/group on the ledger side). `Ours`/`Expected` carry the compared
   decimals for total and place kinds; null for the counted-pilots kind
   (the Detail carries both sets). No new record, no ledger widening (F2).
   A ledger entry covering a teams mismatch is `grain: "teams"`,
   `round`/`group` null, `pilotNo` = the team number.
4. **Comparison kinds — exactly three per oracle standing, plus the
   universe:** total (`standing.Total` vs `TeamScore`, exact-decimal);
   place (`standing.Placing` vs the rank string's numeric part, `'='`
   trimmed); counted pilots (contributors → pilot numbers, compared as a
   SET — GS's trim order is a display artefact, never compared; an
   unmapped contributor is its own mismatch). Universe checks: derived
   standings count vs oracle count, and every derived team maps to an
   oracle standing (extras named, never silently absorbed). **Never
   compared** (documented in the walk's doc comment): counted-pilot order,
   display order inside a shared place, GS's overlay keys
   (SumOfIndividualRankings / HighestTeamPilotPlacing / TeamRawScore —
   checkpoint 4, not carried by the oracle), the Pcnt column (display-only),
   and the MVP-contract walk (contributors-from-members, member states,
   rung order) — that is the parity grain's job and is engine-internal;
   the parallel grain compares products, not derivations. The parity ladder
   grain's place-GROUP membership check is deliberately **not** ported: a
   seed-side shared place always surfaces through the displaced team's
   direct place comparison, so porting it would double-report one split.
5. **Ledger authoring = fully enumerated per (team, kind); no wildcards, no
   count pins.** Eight teams is small (the story's "fully enumerated where
   small"); each entry kind 1, disposition `permanent`, cited. A kind that
   does not fire authors no entry — its absence is witnessed by the
   two-direction set-equality plus the non-empty assertion, the same
   discipline that leaves the ranking grain unpinned.
6. **Expected outcome, stated so nobody "fixes" it:** the f3j teams grain is
   **non-empty** — planning predicts all 8 totals split plus place swaps
   (F1, ground truth below). Every difference is a consequence of the four
   triaged individual classes (landing-0 sentinel, −30 absence, R1 540
   decay, rounding grid) propagating through the individual aggregates;
   the team method agrees (F4). An *unexplained* teams difference — one
   whose citation cannot name its contributing member-cell classes — is the
   escalation case: kind 3 suspicion, human triage, never a ledger edit.
7. **Sequencing: the f3j scenario goes red in WI-1 by design.** Unlike the
   retriage's inert widenings, the grain's first run computes untriaged
   teams mismatches — that red run IS the measured-run source WI-2 curates
   from. WI-1's done-when says exactly this so no agent panics and edits
   the ledger to fit (the anti-goal).

## Verified ground truth (2026-09-13, planning arithmetic — re-pin every count from the measured run)

**GS side** (from `expected-teams.json`, transcript-verified):
T5 44471.7 · T4 43413.6 · T3 43229.1 · T7 42670.6 · T1 41194.4 · T8 41127.6 ·
T2 38068.6 · T6 37094.3, ranks 1–8 distinct, no `'='` strings, counted sets
T5{62,56,21} T4{47,22,11} T3{26,12,28} T7{49,54,46} T1{30,29,42} T8{13,3,2}
T2{32,52,44} T6{43,39,7}.

**Seed side** — every team carries ≥1 counted member with a ledger-stated
non-zero aggregate delta, so **all 8 totals split** (predicted): T5 via 21
(−17.0) and 56 (down 2); T4 via 47 (+1.0), 22 (−13.4); T3 via 26 (+29.7),
12 (+209.6); T7 via 54 (up 3); T1 via 30 (+146.1), 29 (−20.5), 42 (+493.2);
T8 via 13 (+188.6), 2 (+570.4); T2 via 32 (+143.5), 52 (+10.7); T6 via 39
(+288.3), 43 (+3.8). Applying the stated deltas: T1 ≈ 41813.2, T8 ≈ 41886.6+
(p3's delta unstated), T3 ≈ 43468.4+ (p28 unstated) vs T4 ≈ 43401.2+ (p11
unstated) → **T3/T4 place swap predicted** (margin ~67, unstated members ride
the ±0.1/cell rounding grid and cannot close it); **T8/T1 swap predicted**
(margin ~73, same reasoning). Counted-set flips are *not* predicted (no
trimmed member's stated delta overtakes its team's third counted score) —
verify against the run. Place swaps beyond the two are possible where the
unlisted members' deltas accumulate — enumerate what the run shows.

Compared universe: 8 oracle standings × 3 kinds + the universe checks; the
teams grain adds no oracle-key coverage pass (the walk iterates the oracle
standings — each either compares or mismatches, so the
`EnsureOracleCoverage` discipline is structural here, not a separate pass).

## Before starting

- **Board check (done 2026-09-13):** `kanban/in-progress/` is empty; no
  collisions. `f3j-international-flyoff-witness` (backlog) shares the
  *fixture*, not the comparator — no file overlap with this story's edits.
- **ScoringTeams semantics (done):** `Features/ScoringTeams.feature` proves
  trickle-in derivation under NFR-4, the `bestThreeScoreSum` method,
  protection groups, and declared-at-finalisation freezing. The GS team
  arithmetic matches that method (F4) — not another rulebook-vs-local-practice
  candidate. The team ladder is a pure read-model over the same store state
  the individual grains read (`ScoreTeamStandings.cs` header), so the grain
  cannot perturb them (teams-mvp.md WI-9 property 1).
- **Corpus discipline (done):** teams fixtures already carry
  `expected-teams.json` + `team-results-transcript.csv`; no corpus re-shape.
  The only fixture-tree files this story edits are the two parallel-run
  ledgers (f3j entries + note; christchurch note clause) — never
  `expected-teams.json` (it is the oracle: a disagreement with it is a
  difference to triage, never an oracle edit), never fixture data.
- **`fai-rules` re-confirm before citations enter the ledger:** the team
  method is a GS comp option, not an FAI rule — F3J's rulebook legislates no
  team scoring, so teams entries cite *local practice* (GS `GetTeamScores`
  via the oracle's own notes) + the parity-proven method agreement, never a
  fabricated F3J clause.

## Plan

### WI-1 — the teams grain (`tests/Soarscore.Acceptance.Tests`)

All changes in the parallel-run harness; the parity path, the engine and
`src/` are untouched.

1. **`Comparator.TeamGrainOverlap` → `internal`** (`Comparator.cs:1037`), the
   one predicate both harnesses share.
2. **`CompareTeamsGrainAsync`** in `ParallelRunComparator.cs` (private
   static, beside the ranking grain's plumbing), per decisions 2–4:
   - gate on `TeamGrainOverlap(fixture)`; return empty when it does not hold;
   - throw when the gate holds and `fixture.ExpectedTeams` is null (decision
     2, citing the ladder grain's guard);
   - fetch `/competition-team-result?competitionRef=…`;
     `Derived` null → one computed mismatch (never silent);
   - universe checks (decision 4);
   - the `"Team {n}"` → `ScoringTeamId` bridge (the ladder grain's pattern,
     `Comparator.cs:1341-1351`) and the
     `outcome.CompetitorByPilotNo` pilot bridge;
   - per oracle standing: total, place, counted-pilots (decision 4), building
     `GrainMismatch` rows per decision 3's convention, Detail prefixed
     `team {n} {kind}: …`.
3. **Wire into `CompareAsync`** after the ranking grain
   (`ParallelRunComparator.cs:227`): concat the teams mismatches into
   `computed` (`:262`) — the set-equality, untriaged/missing logic, verdict
   and provenance checks then work unchanged (F2).
4. **`ParallelRunReport`** gains two positional counters at the end:
   `TeamStandingsCompared` (oracle standings compared through to completion)
   and `OracleTeamStandings` (`fixture.ExpectedTeams.Standings.Count`).
   `Render()` needs no new section — teams rows ride the generic difference
   table under their own grain name; the Detail prefix is the
   disambiguator.
5. **File-header rewrite** (`ParallelRunComparator.cs:45-49`): the
   out-of-scope note becomes the four-grain contract — the teams grain's
   gate arms (decision 1), its product-only scope (decision 4), and the
   consequence-only expectation for f3j-international (decision 6).

**Done-when:** ales and christchurch scenarios green unchanged (the grain
skips both — F3); the parity feature and self-check green untouched; the f3j
scenario **red, failing only on untriaged teams-grain mismatches** whose
`Render()` output is the WI-2 curation source (decision 7 — the ledger is
never edited to fit in WI-1).

### WI-2 — ledger re-triage, scenario step, measured-first curation

1. **Curate from the WI-1 red run** (never from the ground-truth prediction):
   enumerate the computed teams set; author one ledger entry per (team,
   kind) per decision 5 — `grain: "teams"`, `round`/`group` null,
   `pilotNo` = team number, kind 1, `disposition: "permanent"`,
   Difference = "Team-ladder split, team N: seed-run X vs GS Y" plus the
   contributing member-cell classes (the member aggregates' triaged causes:
   sentinel +100s, −30 absence, R1 decay, rounding cascade — the same
   contributing-classes discipline as the ranking entries); Citation = the
   consequence citation (decision 5/F4): the method agreement (GS
   `NbrForTeamScore=3` top-3-of-final-aggregate ≡ `bestThreeScoreSum` over
   `competitionFinalAggregate`, parity-proven by the ladder grain,
   grow-corpus-team-parity-fixtures.md WI-1D; the oracle's own notes) and
   the four triaged classes as causes — no independent team-cause exists on
   this pair. A difference the citation cannot explain that way escalates
   (decision 6).
2. **Re-author the f3j ledger's teams provenance note**
   (`parallel-run/50-f3j.json` note 3, the "no-team-grain stance holds"
   clause): the stance is retired by this story; the note now discloses that
   the teams grain runs, that every team-grain difference is a consequence
   of the triaged individual classes, and that the method agrees (F4).
   Authored *with* the new entries as one re-triage act — never a silent
   edit.
3. **One-clause re-author of the christchurch ledger note**
   (`f5j-christchurch-2019/parallel-run/30-f5j.json:59`): "the comparator's
   deliberate no-team-grain stance holds (the ales precedent)" → "no teams
   are populated (Team='0' everywhere) and the declared NbrForTeamScore=2 is
   never emulated, so the teams grain's gate does not open"
   (teams-grain-parallel-comparison.md). Provenance-only edit — the pair's
   triaged set is byte-untouched.
4. **Scenario step**: the f3j scenario in
   `Features/ParallelRunningAGliderscoreFixture.feature` gains
   `And the team standings split from the GS team ladder exactly as the
   ledger triages`, implemented in `Steps/ParallelRunSteps.cs` mirroring the
   ranking-split step (`ParallelRunSteps.cs:219-256`): teams computed
   mismatches **non-empty** (the totals split is this pair's teams product —
   an empty grain means the run did not witness it); every computed teams
   mismatch covered by a triaged teams entry; every teams entry witnessed;
   `TeamStandingsCompared == OracleTeamStandings` (the compared-count pin —
   the two-direction set-equality is the real shrink guard, the counters
   report it).
5. **Re-run to green; re-pin.** Every ground-truth prediction (F1, the
   swaps, the counted sets) verified against the measured run and recorded
   as measured; a contradiction is enumerated as what the run actually
   shows (the escalation law governs).

**Done-when:** the f3j scenario green on sqlite; verdict
`MatchesTriagedSet`; the teams grain non-empty and fully enumerated; every
teams entry kind 1 + permanent + cited; the ales/christchurch scenarios and
ledgers otherwise unchanged.

### WI-3 — full verification

1. `@gliderscore` and the full acceptance suite on **both stores**
   (`SOARSCORE_TEST_STORE=sqlite` fast loop; postgres via Testcontainers).
2. Domain, Application, Architecture suites green — the no-`src/`-change
   proof.
3. Corpus discipline: in `tests/GliderscoreFixtures/` only the two ledger
   files change; `expected-teams.json`, `team-results-transcript.csv`,
   `divergences.json`, every fixture data file and `validate.py --index`
   (11/11) are untouched; `Corpus.ExpectedCount` unchanged.

### WI-4 — board reconciliation

1. **Mapping-table flip** (`tests/GliderscoreFixtures/parallel-run-mapping.md`):
   the f3j-international row's `why` folds the teams-grain outcome (8 totals
   split, the measured place swaps, counted sets as measured — all
   consequences of the triaged classes; method agrees); the ledger citation
   is unchanged (same file).
2. **Forward obligations recorded:** the f5j-hawkes-bay-trials row's `why`
   gains one clause — its story must author `expected-teams.json` (the GS
   team ladder, transcript-verified, grow-corpus-team-parity-fixtures.md
   WI-1C procedure) or the teams grain's guard will throw. New backlog stub
   `christchurch-teams-method-split.md` (house rule 6): extending the teams
   grain to `NbrForTeamScore≠3` pairs — a method split is its own witnessed
   difference class and needs a decided shape before any run.
3. `tech-debt.md` / `deferred-decisions.md` reconciled (expected: nothing —
   the retired stance lived in ledger notes and the comparator header, both
   re-authored above).
4. Move this story to `completed/` with `git mv`, set the status header;
   `graphify update .`.

## Testing approach

- **Pair comparator stays example-driven** (the retriage's reasoning holds
  verbatim): the product is set-equality between a computed difference set
  and a curated triaged set, per hand-curated pair; the invariant ("the
  report is exactly the triaged differences") is structural and
  example-asserted per pair. No new CsCheck property — generating fixture
  mutations would test the comparator, not the product.
- **The engine's team classification is already covered** — the
  `ScoringTeams.feature` BDD workflow and the parity team/ladder grains prove
  the derivation contract and the GS-method agreement; the parallel grain
  witnesses products on real data rather than re-proving them.
- **What each failure must name:** an untriaged teams mismatch whose citation
  cannot name contributing member-cell classes → kind-3 suspicion, escalate
  with the full `Render()`; a teams entry that fails to appear → the ledger
  no longer describes the pair, re-triage; `TeamStandingsCompared` short of
  the oracle count → a skipped standing (always accompanied by its own
  mismatch) or a harness bug; a provenance break → unchanged semantics, the
  run did not run under the disclosed provenance.

## Scope guards and standing constraints

- **Anti-goal, stated hard:** the seed classes are never tuned to GS; the
  oracle is never edited to fit the run; the ledger is never edited to fit a
  failing comparison (the escalation law). Every teams difference is triaged
  with citations — reported, never reconciled.
- **Ledger semantics:** the parallel-run ledger remains a different contract
  from the fixture divergence ledger — distinct schema, distinct comparator,
  never merged. The parity team grains keep their own contract ("a mismatch
  here is always a defect, never a triaged divergence"); the parallel teams
  grain is ledgerable precisely because its inputs already differ by the
  triaged set.
- **No `src/` changes, no seed changes, no engine changes.** A contradiction
  found in the run escalates as a finding, not a local implementation. Any
  genuinely new feature found during implementation becomes a backlog stub
  under house rule 6 — never silent scope growth.
- **Regression proof:** the ales and christchurch pairs are the inert proofs
  (the grain skips both); the parity path is untouched; the full suite green
  on both stores is the discipline.
