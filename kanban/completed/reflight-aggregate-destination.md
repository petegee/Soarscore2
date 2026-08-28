# Story — Re-flight scores: aggregate destination ≠ entry's round

**Status:** Completed · **Raised:** 2026-08-26 (found by
`kanban/completed/gliderscore-replay-and-compare-harness.md` WI-6 — design
recorded in that story file, "WI-6 design" section) ·
**Fleshed out:** 2026-08-28 (owner decisions settled same day — see D1–D4)

## What

An engine concept for scoring a flight in one task-round but aggregating the
result into **another round's ladder slot**. GliderScore keys every report cell
by `OriginalRoundNo` (`Rpt_Results_Overall_MOD.vb:2698-2706`), so a make-up
flight flown inside a later round produces two live aggregate cells from one
task-round: the flight **normalises within the group that hosted it** but
**aggregates into the missed round's slot**.

No current engine concept expresses that:

- `ReflightSelector` collapses a competitor's task-round entries to **ONE**
  score by law (`ReflightSelector.cs:66-112`; shape law `:43-47`; invariant R1
  of `kanban/completed/reflight-groups.md` WI-8), while the faithful replay of
  both corpus witnesses needs **two or three live cells from one task-round**;
- `PhaseAggregator` keys every cell by the entry's **own** round
  (`PhaseAggregator.cs:62-95`), and synthesises a 0 for a seen-but-absent slot
  (`:83-92`) — which is precisely the wrong cell for a make-up.

The design (below): an optional Entry-level datum — `CountsForRoundOrdinal`
(GS's `OriginalRoundNo` analogue) — that names the round whose ladder slot the
score aggregates into. Null means the entry's own round, so ordinary scoring is
untouched. The harness story's WI-6 design ruled out three cheaper mappings
with numeric evidence; this story is the faithful concept it deferred to.

## Why it matters

Two active fixtures carry make-up cells and replay under the lossy mapping (a)
(exclude the row, ledger every arithmetic consequence):

| Fixture | Make-up cells | Ledger today | After this story |
|---|---|---|---|
| `jerilderie-2010` | 1 (pilot 29, R13/G1 → orig-12) | 9 entries | **1** (trap-3 P21 only) |
| `f5j-hawkes-bay-trials` (comp 135) | 4 (pilot 128, R5/G2→orig-1, R5/G3→orig-2, R6/G1→orig-4, R6/G3→orig-3) | 20 entries | **0** |

Comp 135 is the richer witness: a pilot absent from R1–R4 entirely, whose four
make-ups replace those absences, and who holds **three live entries in one
task-round** (his regular slot plus two make-ups) — a shape the current shape
law refuses outright. A faithful replay of either fixture needs this concept;
until then every make-up cell, its Δ on the total, and every displaced
placing is a ledgered divergence.

## Before starting — all settled

- ~~Read the WI-6 design~~ — `gliderscore-replay-and-compare-harness.md`
  §"WI-6 design" rules out mappings (a)–(c) with numeric evidence; this story
  implements the concept that verdict deferred. Mapping (a) remains the
  fallback if implementation stalls, but is not the plan.
- ~~Concept, name, role, conflict policy, Reason~~ — owner-settled 2026-08-28
  (D1–D4 below). Do not relitigate.
- ~~Docs approval~~ — the two `/docs` amendments are approved in principle
  (D1); drafted text is in §Doc amendments below. WI-5 applies it verbatim;
  any wording change beyond that text goes back to the owner first.
- **Rule check (fai-rules skill, 2026-08-28):** no rulebook text was found
  warranting or forbidding a make-up flight counting for a missed round — the
  corpus witness is GS behaviour, not FAI/NZMAA text. The concept is therefore
  entry-level runtime data (like `Role`), valid for any class, never a class
  branch; unwitnessed extensions stay refused (D3). F5L's placement note
  (`docs/rules/f5l.md:113-115` — re-flight placement is a scheduling decision)
  is consistent: this story changes where a score *aggregates*, not where a
  re-flight is *scheduled*.
- Baseline: `SOARSCORE_TEST_STORE=sqlite dotnet test
  tests/Soarscore.Acceptance.Tests` before touching anything (nz-story WI-1
  discipline; avoids Docker). Record the result.

---

# Verified ground truth (planning-time 2026-08-28)

All numbers recomputed from the committed fixture files and the tree. Cite
this section, not older prose, where they disagree. `file:line` cites drift —
re-verify before acting on one.

## The two witnesses, cell-exact

**jerilderie-2010 — pilot 29.** Holds 14 rows across 13 rounds: R1–R11
ordinary; TWO R13 rows — Seq 10 in G2 (his drawn R13 slot, packed 906 → 546 s
+ 91 = raw 637, NS 913) and Seq 14 in G1 carrying `OriginalRoundNo=12` (the
make-up, packed 958 → 598 s + 91 = raw **689**, NS **990** = 689/696 half-up;
basis = R13/G1's 13 original members' max, 696 — the make-up raw does not set
the basis, 689 < 696); and R14/G3. He has **no R12 row at all**. Oracle cells
sit at **RoundNo-keyed** keys `1/13/1/0/29` (make-up) and `1/13/2/0/29`
(regular) — see the keyFormat trap below. GS ladder: 14 real cells, drops
{505@R5, 706@R4} → **10867**, rank "29". Mapping-(a) replay: 13 cells +
synthetic zero at R12 → drops {0, 505} → **10583** (Δ284), place 32; P25/P27/
P52 shift one place up each; trap-3 fires for P21 independently.

**f5j-hawkes-bay-trials (comp 135) — pilot 128.** Absent R1–R4 entirely; his
first four appearances are the make-up rows (`ReFlightNo=0`, identified by
`OriginalRoundNo≠RoundNo`): R5/G2 → orig-1 (raw **225**, NS **400**), R5/G3 →
orig-2 (raw **299**, NS **635.5**), R6/G1 → orig-4 (raw **170.5**, NS
**492.1**), R6/G3 → orig-3 (raw **524**, NS **955.3**). He also holds ordinary
R5/G1 and R6/G2 rows. Under a faithful replay he holds **three live entries in
R5** (G1 Original + G2/G3 make-ups) and **three in R6** (G2 Original +
G1/G3 make-ups) — a shape the current shape law refuses
(`score.reflightShapeUnsupported`). Σ make-up NS = **2482.9** = exactly the
shortfall in his mapped-(a) total (place 15 vs GS "12"; P122/P100/P142 one
place up each).

Each make-up **normalises within its hosting group** (comp 135's four NS values
are per-hosting-group) — normalisation needs no change anywhere in this story.

## The engine surfaces this story touches (tree as of 2026-08-28)

| Surface | Fact | Where |
|---|---|---|
| Entry coordinates + role | `PhaseOrdinal/RoundOrdinal/TaskRoundOrdinal/GroupRef/Role`; no destination datum | `Entry.cs:138-153` |
| `EntryOpened` | carries `Role`; no destination | `EntryEvents.cs:50` |
| Roles | `{ Original, Entitled, Filler }` — data, never adjudicated | `Entry.cs:121` |
| Drawn-check | `openEntry.competitorNotDrawn` for **every** role — no path for a re-flight flown with a group the competitor wasn't drawn into (the glossary's "flown with a later group" shape, F3J.4 priority (a)) | `Competition.cs:1169-1173` |
| Handler duplicate guard | Original open blocked by any live entry; Entitled/Filler blocked by any live reflight-role entry | `OpenEntry.cs:62-105` |
| Shape guard (scoring) | per competitor per task-round, live roles must be 1 entry or Original + one reflight-role | `ScoringService.cs:180-196` |
| Candidates + collapse | per competitor per **task-round**, one `TaskRoundScore` (invariant R1) | `ScoringService.cs:235-301` |
| Zero synthesis | seen-but-absent task-round → cell 0 | `PhaseAggregator.cs:83-92` |
| Cell matching | by `(RoundOrdinal, TaskOrdinal, TaskCode)` | `PhaseAggregator.cs:69-92` |
| Aggregate keying | `"{RoundOrdinal}|{TaskOrdinal}|{index}"` | `ScoringService.cs:333-343` |
| `TaskRoundScore` | `(TaskCode, RoundOrdinal, TaskOrdinal, Score)` | `ScoringResultTypes.cs:103-108` |
| Grain-2 comparator | filters `Role == ReflightRole.Original` — make-up rows never compared nor conserved today | `Comparator.cs:445` |
| Conservation cells | built from grain-2 rows; throws on two cells for one (round, task-round) | `Comparator.cs:461-479` |
| Synthetic slots | all-zero rows appended to `keptRows` → prescribed **and** entry-opened (cell 0) | `ReplayDriver.cs:151-159, 217-230, 329-339` |
| D5 step 1 | re-flight rows filtered from draw derivation | `ReplayDriver.cs:410-414` |
| Oracle key | `keyFormat` = `{TaskNo}/{RoundNo}/{GroupNo}/{ReFlightNo}/{PilotNo}` — **RoundNo**, but `OracleNormalisedScore` builds keys from `OriginalRoundNo` (harmless today; fix in WI-4) | fixture `expected-scores.json`; `ReplayDriver.cs:451-456` |
| Ledger citation regex | `\bD[1-6]\b|\btrap\s*3\b|\bN1\b|\bR1\b` — unchanged by this story | `ReplaySteps.cs:152` |

## Regression surface pinned

- `tests/Soarscore.Domain.Tests/OpenEntryDecideTests.cs:201` asserts
  `openEntry.competitorNotDrawn` (Original-role — must keep failing).
- `tests/Soarscore.Application.Tests/Commands/Entries/OpenEntryHandlerTests.cs:235`
  asserts a second live Entitled is blocked with `openEntry.reflightAlreadyOpen`
  (same implicit destination — must keep failing).
- `tests/Soarscore.Domain.Tests/ReflightSelectionPropertyTests.cs` pins R1–R3;
  **R1 as written is false under make-ups** and is restated by this story
  (R1′, WI-1). `reflight-groups.md` is a completed story — its text stays
  historical; the destination-aware law supersedes it and says so here.

---

# Decisions settled during planning (owner, 2026-08-28 — do not relitigate)

**D1 — The datum is `Entry.CountsForRoundOrdinal` (`int?`), docs approved.**
Carried on `EntryOpened` (nullable, additive — event-shape changes are free;
no real logs exist) and folded verbatim onto `Entry`. `null` ⇒ the entry's own
round (every existing entry, event and test is unchanged). Set ⇒ the round
whose ladder slot the score aggregates into. The two `/docs` amendments it
needs are approved; drafted text in §Doc amendments below, executed in WI-5.

**D2 — A make-up entry's role is `Entitled`.** The CD allocated him the
attempt; the role is semantically inert for make-ups (its destination slot
holds only it — no collapse, no ruling) but drives the entry-open guard and
the shape law. The two-role law ("reflight scoring is per role, not per
class") is untouched.

**D3 — Unwitnessed conflict shapes refuse loudly, never merge.** No corpus
fixture exercises a second cell for one destination slot, and GS's two
candidate behaviours (entitled=replacement vs phantom-dedup=best-of)
contradict each other, so the merge law is unobservable. Refuse with explicit
codes (D7); merge semantics wait for a witness.

**D4 — A make-up open requires a `Reason`** (the entitlement basis — parity
with `AppendReflightGroup.reasonRequired`; F5J 5.5.11.6 b requires the
hindering condition be noted). `OpenEntry` grows an optional `Reason`,
**required exactly when `CountsForRoundOrdinal` is set**.

Planner's calls below (D5–D8) are flag-for-veto, harness-story precedent.

**D5 — Write side: widen `/open-entry`; no new command.** Additive optional
`CountsForRoundOrdinal` + `Reason` on the existing command (the
reflight-groups WI-3/WI-5 precedent: role went onto `OpenEntry` as optional
data). `Competition.OpenEntry` grows the two parameters. The
`openEntry.competitorNotDrawn` check is **relaxed for reflight-role entries
only**: a reflight-role entry may be opened into any group of the addressed
task-round for a registered, non-withdrawn competitor — the CD's allocation is
the act, and `Group.CompetitorRefs` remains "the drawn allocation, not who
flew" (reflight-groups finding 4). This deliberately also opens the
priority-(a) shape (re-fly with a following group — the glossary's "flown
either with a later group" half, unexpressible until now); the
`ReflightingAGroup` scenarios use drawn members and stay green unchanged.

**D6 — The destination-aware scoring law** (restates invariant R1):

- `Original` ⇒ counts-for is the entry's own round, always. Setting
  `CountsForRoundOrdinal` on an Original-role open is refused at the write
  side (`openEntry.destinationOnOriginalRole`).
- A reflight-role entry's counts-for is either `null` (own round — the
  ordinary reflight, unchanged) or an **earlier round of the same phase**
  (`1 ≤ c < RoundOrdinal`; else `openEntry.destinationNotFound` /
  `openEntry.destinationNotEarlier`).
- Per **(competitor, task-round, destination)** the live entries must be
  exactly one entry of any role, or exactly one `Original` plus exactly one
  reflight-role entry — the collapse law, now per destination. This implies at
  most one `Original` per (competitor, task-round) (all Originals share the
  own-round destination), and it **accepts the comp-135 shape** (Original +
  two reflight-role entries with distinct destinations).
- Collapse: candidates group by destination; `ReflightSelector.Select` runs
  per destination with the same `ReflightRule`/ruling logic — when both
  candidates of a destination share it, the old law applies verbatim. A CD
  ruling (`rulingByCompetitor`, keyed task-round+competitor) maps
  unambiguously: at most one two-candidate destination exists per task-round
  (≤ 1 Original), so the ruling's destination is never ambiguous.
- When no entry carries a counts-for, the law reduces exactly to today's
  (single destination class = the task-round) — the R6 regression guarantee.

**D7 — Score-time validation, loud codes** (belt over the write-side braces;
a make-up's destination must resolve or scoring refuses rather than silently
dropping the cell — an unmatched `allScores` entry vanishes silently today,
`PhaseAggregator.cs:69-92`):

| Code | Condition |
|---|---|
| `score.reflightDestinationUnresolved` | a counts-for names a round of the phase that does not exist, or whose task-round is not walked (no entries anywhere in it — finding-5's filter would silently drop the cell) |
| `score.reflightDestinationTaskMismatch` | the destination round's task-round at the make-up's `(TaskOrdinal, TaskCode)` does not match the hosting task-round's (single-task rounds always match; multi-task rounds are unwitnessed — refuse) |
| `score.reflightDestinationConflict` | two walked task-rounds contribute cells for one (competitor, destination round, task) — the cross-task-round shape D3 refuses |

`PhaseAggregator` is **unchanged** (D8): a destination-keyed
`TaskRoundScore(RoundOrdinal: destination, …)` matches the destination round's
walk, enters the drop pool under its destination, and synthesises nothing.

**D8 — Write-side conflict check (cross-task-round).** The handler, when
`CountsForRoundOrdinal` is set, queries `IEntryQuery.FindAsync(competitor,
destination round)` and refuses a **live** entry of the competitor in the
destination round's matching task-round with
`openEntry.reflightDestinationTaken` — a make-up for a round the pilot also
flew is exactly the unwitnessed shape D3 refuses. (The port already takes
every filter optional; no port change. Annulled entries don't block, per the
standing annulment stance.) The scoring-side check (D7, third row) remains as
braces — belt and braces, never a substitute.

---

# Known traps (pre-answered)

1. **Oracle keys are RoundNo-keyed; the driver's dedup helper is not.**
   `keyFormat` says `{TaskNo}/{RoundNo}/…` (jerilderie's make-up oracle cell is
   `1/13/1/0/29`), but `OracleNormalisedScore` (`ReplayDriver.cs:451-456`)
   builds keys from `row.OriginalRoundNo`. Harmless today (the rows it sees
   all have `OriginalRoundNo == RoundNo`), **fatal once make-up rows are
   replayed**. WI-4 fixes it to `RoundNo`, citing the fixture's keyFormat.
2. **Open order matters; the driver must respect it.** The Original-branch
   guard ("any live entry of any role blocks", `OpenEntry.cs:94-97`) stays
   verbatim — so a make-up must never be opened before the competitor's
   Original in the same task-round. Both witnesses satisfy this in **flying
   order** (jerilderie: regular Seq 10/G2 before make-up Seq 14/G1; comp 135:
   regulars G1/G2 before make-ups G2/G3), but the driver's walk is
   group-ascending, which would open jerilderie's G1 make-up first. WI-4:
   per round, **pass 1 opens every regular (keptRows) slot, pass 2 opens the
   make-up rows** (SeqNo order within pass). Do not loosen the domain law for
   this — the ordering is GS's own flying order.
3. **Synthetic slots split in two.** Today `SyntheticSlots` rows are appended
   to `keptRows`, so they are prescribed **and** entry-opened (cell 0). Under
   the faithful mapping the draw-prescription half must stay
   (`prescribeDraw.competitorMissing` still demands the pilot in every round)
   but the **entry must not be opened** — the destination cell fills the slot.
   jerilderie `(12,1,29)` and comp 135 `(1,2),(2,1),(3,2),(4,1)` become
   draw-prescription-only; **f3k-southern-fling's `(9..15,3,89)` stay
   flight-less-entry slots** (a retired pilot's zeros, not make-ups — do not
   touch).
4. **Group membership stays untouched.** GS's score sheet shows the make-up
   pilot inside the hosting group; our `Group.CompetitorRefs` (drawn
   allocation) must not grow him — the completeness indicator counts
   `CompetitorRefs ∧ ¬Withdrawn` as Expected, and scoring is Entry-derived.
   The make-up surfaces in the hosting group's results via its Entry row
   (`CompetitorTaskResultView.Role` already exists). A destination field on
   the view is out of scope (see §Out of scope).
5. **Annulled destination round.** A counts-for into an annulled task-round
   would have its cell zeroed by `PhaseAggregator`'s annulled branch.
   Unwitnessed; do not engineer — noted here so a future fixture trips the
   right wire (it would surface as a ranking diff, then triage).
6. **`NotPermitted` classes and lone reflight-role entries.** A single
   candidate per destination passes through `Select` unchanged (the
   lone-Entitled-after-annulment precedent, reflight-groups WI-6) — so a
   make-up in a `NotPermitted` class would score. Pre-existing stance; the
   write side is where role gating would belong, and it is out of scope here.
7. **`Aggregate`'s `FirstOrDefault`** (`PhaseAggregator.cs:69-72`) silently
   picks one if two scores ever match one walked slot. D7's third check makes
   that state unrepresentable at the orchestrator; do not "fix" the
   FirstOrDefault.
8. **Drop-gate interplay.** A destination round is always a flown round in
   both witnesses (R12; R1–R4), so `completedRounds` accounting is untouched.
   The `score.reflightDestinationUnresolved` refusal is what keeps a
   destination into an unflown placeholder round from inflating a drop gate.
9. **`EntrySummary` stays coordinate-only.** The handler guard loads streams
   for live/annulled truth (its header stance); the destination reaches it the
   same way. Do not widen the index in this story.
10. **Grain 1 needs no comparator change** — it walks `EntryIdBySlot` and keys
    by `slot.RoundNo` already (`Comparator.cs:314-351`); make-up entries join
    the slot universe in WI-4 and compare at hosting coordinates, which is
    where the oracle cells sit.

---

# Work items

Ordering: **WI-1 → WI-2 strictly sequential** (one deep compile unit; WI-2
consumes WI-1's shared law). **WI-3 and WI-4 are parallel** after WI-2,
strictly disjoint files. WI-5 closes.

### WI-0 — Board

`git mv kanban/backlog/reflight-aggregate-destination.md kanban/in-progress/`
and update the status header in the same commit, before the first code commit.

### WI-1 — Domain engine: the counts-for datum + destination-aware law

One agent; `src/Soarscore.Domain/` only.

1. **Event + state.** `EntryOpened` (`EntryEvents.cs`) grows
   `int? CountsForRoundOrdinal = null` and `string? Reason = null`; `Entry`
   (`Entry.cs:131-180`) carries both; `Create` copies them. Update the
   construction sites (positionally — grep `new EntryOpened` / `OpenEntry(`
   decide signature). Nothing else folds them.
2. **The shared law.** `ReflightSelector` grows the destination-aware shape
   function — e.g.
   `ShapePermits(int roundOrdinal, IReadOnlyList<(ReflightRole Role, int? CountsFor)> liveEntries)`
   — implementing D6's three bullets (resolve `null` counts-for to
   `roundOrdinal` first). Keep `EntryKey` and `Select` byte-identical. The
   old two-role `ShapePermits(roles)` either goes (single caller:
   `ScoringService.cs:187`) or delegates; whichever, every existing
   `ReflightSelectionPropertyTests` fact must pass unchanged.
3. **`ScoreCompetition`** (`ScoringService.cs:174-301`):
   - shape guard walk: per competitor, evaluate the destination-aware law
     over live entries' `(Role, CountsFor)` → failure
     `score.reflightShapeUnsupported` (message names destinations seen);
   - candidates become `(ReflightRole Role, int Destination, decimal Score)`;
   - collapse: group candidates by destination; per destination run
     `Select(candidates, rule, ruled)`; emit one `TaskRoundScore` per
     destination (`TaskCode/TaskOrdinal` from the **hosting** task-round,
     `RoundOrdinal` = destination) — comp 135 yields three legitimate
     `TaskRoundScore`s for P128 in R5;
   - D7's three validations after the walk, before aggregation — on failure
     return the code, never silence.
4. **Tests.** Extend `ReflightSelectionPropertyTests` + new
   `ReflightDestinationTests.cs` (mirror `ScoringServiceAnnulmentTests`'
   fixture style): make-up cell keys to the destination round; drop walk
   consumes it (jerilderie-shaped mini-fixture: drops remove real cells, not
   a synthesised zero); each D7 code fires on its shape; the comp-135 shape
   (Original + 2 make-ups, one task-round) scores three cells; the
   ruling maps to the unique two-candidate destination.
5. **Property-based invariants** (CsCheck, named here per CLAUDE.md — new
   `ReflightDestinationPropertyTests.cs`):
   - **R1′ — one score per destination slot.** For any live-entry multiset,
     `ScoreCompetition` yields at most one `TaskRoundScore` per
     (competitor, task-round, destination), and the count per task-round
     equals the number of distinct destinations among the competitor's live
     entries. (Supersedes R1, which is the all-null special case.)
   - **R5 — destination conservation.** Σ `AllScores` == Σ destination-keyed
     cells; every destination-keyed score appears in exactly one drop-pool
     cell keyed to its destination round.
   - **R6 — no-destination equivalence.** For any entry set where every
     `CountsFor` is null, the destination-aware law accepts exactly the
     shapes the old law accepted and produces identical selections — the
     pipeline's output is unchanged.
   - **R7 — loud failures.** Any destination that is not an earlier round of
     the phase, or that fails D7's resolution/matching/uniqueness checks,
     yields a `Result.Failure` with the named code — never a silent cell.

**Checkpoint:** `dotnet build Soarscore.sln`; Domain + Application suites
green; the acceptance suite green **untouched** (no `tests/` edit in this WI
— that is the R6 proof in practice).

### WI-2 — Write side: open a make-up entry

One agent, after WI-1; `OpenEntry` command, `Competition.OpenEntry`,
handler, route.

1. **Command** (`OpenEntry.cs:25-28`): grows
   `int? CountsForRoundOrdinal = null, string? Reason = null` (additive;
   every existing caller unchanged).
2. **Decide** (`Competition.OpenEntry`, `Competition.cs:1112-1210`): new
   checks, in this order, after the existing ones —
   `openEntry.destinationOnOriginalRole` (D6 bullet 1);
   `openEntry.destinationNotFound`; `openEntry.destinationNotEarlier`;
   `openEntry.reasonRequired` (blank Reason when counts-for set — D4, the
   `appendReflightGroup.reasonRequired` wording); the drawn-check
   (`:1169-1173`) becomes: enforced for `Role == Original` **only**
   (registered + non-withdrawn stay enforced for every role — D5).
3. **Handler guard** (`OpenEntry.cs:62-105`): the reflight-role branch blocks
   only on a live reflight-role entry with the **same destination**
   (`openEntry.reflightAlreadyOpen`, message names the round) — the existing
   second-Entitled test keeps passing; a different-destination second
   reflight-role open is allowed (comp 135). The Original branch is
   verbatim. Then the D8 destination-conflict check
   (`openEntry.reflightDestinationTaken`) — one extra `IEntryQuery.FindAsync`
   when counts-for is set, streams loaded for liveness.
4. **Tests.** Domain decide facts (one per new code + the relaxation + the
   unchanged Original-role refusal); handler facts (same-destination
   duplicate refused; two distinct-destination make-ups in one task-round
   allowed; make-up-then-Original refused `alreadyOpen` — trap 2's law);
   Infrastructure store-backed round-trip (destination + reason survive both
   backends; mirror `ReflightGroupEventStoreTests`).

**Checkpoint:** solution green; store-backed leg under sqlite at minimum,
postgres where Docker exists.

### WI-3 — Acceptance BDD (parallel with WI-4)

One agent; new `Features/ReflightingForAMissedRound.feature` +
`Steps/ReflightingForAMissedRoundSteps.cs` only. F3K corpus
(`10-f3k`), `ClosingACompetitionSteps` conventions (self-contained regexes,
unique slugs, exact-arithmetic time values). Scenarios:

1. **Governing principle** — a pilot drawn R1–R3 flies R1's make-up inside
   R2's group (role Entitled, counts-for 1, reason recorded): R1's cell is
   the make-up's normalised score, **not** a synthesised zero; the pilot's
   total reflects it; a competitor with no make-up is untouched (R3
   generalised).
2. **Two make-ups in one round** — the comp-135 shape: Original + two
   Entitled entries with distinct counts-for in one task-round; three cells,
   two destination slots filled.
3. **Same-destination duplicate refused** — `openEntry.reflightAlreadyOpen`.
4. **Write-side refusals** — counts-for on an Original; destination ≥ own
   round; destination round not drawn; missing Reason.
5. **Drawn-check regression** — an Original-role open into a non-drawn group
   still fails `openEntry.competitorNotDrawn`.
6. **Destination conflict** — a make-up for a round the pilot also flew (live
   Original in the destination round) fails
   `openEntry.reflightDestinationTaken`.
7. **Ordinary reflight unchanged** — the same-round Original+Entitled pair
   (destination null both) collapses per the class rule exactly as
   `ReflightingAGroup` has always scored it.

**Checkpoint:** new feature green, both stores; no existing feature edited.

### WI-4 — Harness retirement (parallel with WI-3)

One agent; `tests/Soarscore.Acceptance.Tests/Support/Gliderscore/*`, the two
fixture `divergences.json` files, and the two scenario count-pins in
`ReplayingAGliderscoreFixture.feature`. Nothing else.

1. **Driver** (`ReplayDriver.cs`): D5 step 1 keeps re-flight rows out of the
   **draw**, but now collects them; per round, after the regular walk
   (trap 2's two-pass order), open each make-up row's entry via
   `/open-entry` with `role: "Entitled"`, `countsForRoundOrdinal:
   OriginalRoundNo`, `reason: "Gliderscore re-flight row (OriginalRoundNo=N)"`
   — then flight + captures exactly as D4 prescribes for any row
   (`CaptureInputs` unchanged). `SyntheticSlots` splits per trap 3:
   draw-prescription-only rows for jerilderie + comp 135; flight-less-entry
   rows remain only for f3k-southern-fling. Fix the
   `OracleNormalisedScore` key to `RoundNo` (trap 1), citing keyFormat.
2. **Comparator** (`Comparator.cs`): grain-2's `Role == Original` filter
   (`:445`) widens to all rows — make-up rows compare at their **hosting**
   `(round, group)` (the oracle cells sit there; trap 10); the conservation
   cell (`:461-465`) keys `RoundOrdinal` to the entry's `CountsFor ??
   roundOfView` — destinations read from the already-loaded entries; the
   two-cells throw (`:472-477`) keys on the destination.
3. **Ledgers.** Rewrite the two fixtures' `divergences.json` to the predicted
   residues — **jerilderie-2010: exactly 1 entry** (trap-3 P21, unchanged
   reason); **f5j-hawkes-bay-trials: 0 entries (empty array)**. Update the
   two scenarios' `And the fixture ledger records exactly N accepted
   divergences` pins (9→1, 20→0). All other fixtures' ledgers and pins are
   untouched. The citation regex (`ReplaySteps.cs:152`) is unchanged — no
   new token is minted; anything still ledgered keeps its existing citation.
4. **Discipline (inherited):** any residual mismatch beyond the predictions
   above is a stop-and-triage event — re-derive by hand from the fixture
   inputs before touching anything; never widen an entry to go green. The
   predictions to hold: jerilderie's P29 total 10867 / rank "29" (drops
   {505@R5, 706@R4}); comp 135's P128 place 12 and every displaced pilot
   restored; all 882 + 288 oracle cells compared exact at raw and normalised
   grains; conservation clean everywhere.

**Checkpoint:** `SOARSCORE_TEST_STORE=sqlite dotnet test
tests/Soarscore.Acceptance.Tests` green with the shrunken ledgers; then the
same under postgres.

### WI-5 — Docs, inventories, close-out

Only after WI-1–WI-4 are green on both stores:

1. Apply the approved §Doc amendments verbatim (anything beyond that text
   goes back to the owner first). Same commit as the board move.
2. `kanban/deferred-decisions.md` → Draw bullet: replace the
   "waits on the engine concept parked as …" tail with a resolution line
   citing this story; the **prescription half stands unchanged** (base-draw
   import of re-flight rows is still deferred).
3. `kanban/tech-debt.md`: nothing is expected — add only what
   implementation actually deferred.
4. `git mv` to `completed/`, status header in the same commit. Any newly
   identified feature → `kanban/backlog/` stub (house rule 6), e.g. the
   merge-per-rule semantics for a witnessed same-destination-across-rounds
   shape, or a destination display field on `CompetitorTaskResultView`.

---

# Dispatch model

- **WI-1 is one agent** (Domain only; the deep compile unit).
- **WI-2 is one agent**, strictly after WI-1 (shares the law types).
- **WI-3 and WI-4 are two parallel agents** after WI-2, with **strictly
  disjoint deliverables** (WI-3: two new files; WI-4: driver/comparator/
  ledgers/two count-pins). A parallel agent that finds it needs any other
  file changed must stop and report back instead of editing shared files
  (nz-story D8 discipline; if running concurrently in one tree is unsafe for
  `dotnet test`, use isolated rsync copies and land deliverables in slug
  order — the nz-story as-built pattern).
- **WI-5 is one agent** (close-out) after everything is green.

Each dispatched agent reads: this file (ground truth + its WI + traps), and
nothing else by default — §Verified ground truth exists so no implementer
re-derives fixture arithmetic or re-litigates mappings. Cited `file:line`
values are planning-time; re-verify before acting on one.

## Execution plan

1. WI-0 → WI-1 → WI-2 (sequential; owner decisions settled — no user input
   outstanding).
2. WI-3 ∥ WI-4 (parallel, disjoint files).
3. WI-5 (close-out).

**Finish line:** `dotnet build Soarscore.sln`; `dotnet test Soarscore.sln`;
the acceptance suite under both `SOARSCORE_TEST_STORE` values. Known flake:
solution-wide Marten migration race (`tech-debt.md` last item) — re-run the
failing project alone before diagnosing.

**Story invariant for sign-off:** both make-up fixtures replay exact at all
three grains modulo their predicted ledgers (jerilderie exactly 1 entry —
trap-3 P21; comp 135 empty); no synthesised-zero cell stands in for a
make-up anywhere; no glossary/docs change beyond the approved drafted text;
no `src/` file mentions GliderScore; both stores green; every new failure
code is asserted by at least one test at the layer that owns it.

## Out of scope — deliberately

- **The prescription half** — feeding re-flight rows through base-draw
  prescription (`prescribeDraw.competitorRepeated`) stays deferred
  (`deferred-decisions.md`, Draw). This story makes the *scoring* faithful;
  the draw import half is unchanged, and draw derivation still filters
  make-up rows.
- **Merge semantics across task-rounds** for one destination slot — refused
  (D3) until a fixture witnesses it; backlog stub if implementation
  surfaces demand.
- **A destination field on `CompetitorTaskResultView`** — the harness reads
  destinations from entry streams; HTTP display of the counts-for round
  would be its own additive story (the
  `pre-normalisation-score-view-field.md` precedent).
- **Loosening the Original-branch open order** (trap 2) — the harness
  satisfies it with GS's own flying order; a domain loosening would touch a
  settled, tested law from `reflight-groups.md` WI-5 for no witness.
- **Multi-task-per-round destinations (F3B), annulled destination rounds,
  two make-ups for one round** — unwitnessed; the D7/D8 refusals name them
  loudly instead of modelling them.
- **Ranking secondary key, normalisation lower clamp** — adjacent backlog
  stubs, untouched here.

---

# Doc amendments (approved 2026-08-28 — apply verbatim in WI-5)

**`docs/soaring-domain-glossary.md`** — Entry paragraph (currently ends "…
by the Contest Director's recorded ruling."), append one sentence:

> A re-flight flown with a later round's group may count for the competitor's
> missed round rather than the round it was flown in: the entry records that
> counts-for round, its score aggregates into the missed round's ladder slot,
> and it still normalises within the group that hosted it.

**`docs/soaring-domain-class-diagram.md`** — Entry gains
`+CountsForRoundOrdinal : int?`, and the note for Entry (`:243`, "A reflight
is a second Entry; role decides which one counts") is extended with:

> ; an optional counts-for round moves the score's aggregate destination to
> an earlier round (a make-up flight) while normalisation stays with the
> hosting group

---

# As built (2026-08-28)

Executed WI-0 → WI-5 in plan order; WI-3 ∥ WI-4 via the prescribed isolated-tree
pattern (rsync copies, deliverables landed in slug order). All four checkpoints
green on first full verification; nothing widened to go green.

- **WI-1** — as planned. `ReflightSelector.ShapePermits(roundOrdinal, liveEntries)`
  is the destination-aware law; the old two-role `ShapePermits(roles)` was KEPT,
  not deleted — the story's "single caller" held for production only, tests pin
  the old form (`ReflightSelectorTests`). Non-earlier destinations are refused
  by the shape law as `score.reflightShapeUnsupported` (D7's table has no
  fourth score-side code; R7 asserts this). The D7 task-mismatch check treats
  "destination round walked but no walked slot at the hosting (ordinal, code)"
  as `taskMismatch`, which also covers a structurally-matching-but-unflown
  destination task-round (R7).
- **WI-2** — as planned, plus one new code the plan implied but did not name:
  `openEntry.competitorNotRegistered` (the drawn-check relaxation removed the
  check that implicitly guaranteed registration for reflight-role opens, so
  registration is now explicit for every role). The WI-1 destination tests
  seed the D7 shapes via directly-built `EntryOpened` events (`UnwritableEntry`):
  since WI-2 the write side refuses those shapes at the decide, and D7 stays
  tested as the belt for events already in a log.
- **WI-3** — **plan correction:** scored scenarios run on F5J (`30-f5j`), not
  F3K. Under F3K's `RequireDistinctTaskPerRound` draw (F3K.10) no two rounds
  share a task, so a scored make-up always trips D7's
  `score.reflightDestinationTaskMismatch` — the planner's "single-task rounds
  always match" holds within a class only when rounds share the task. F5J is a
  fixed-sequence single-task class and the comp-135 witness's class; the
  refusal/regression and ordinary-reflight scenarios (4, 7) do run on F3K as
  planned. 7 new scenarios; suite 64 (57+7) on both stores.
- **WI-4** — as planned; converged on the first run with no triage: jerilderie
  P29 10867 / rank "29" (drops {505@R5, 706@R4}), comp 135 P128 rank "12" with
  P122/P100/P142 restored, all 882 + 288 oracle cells exact at both grains,
  conservation clean. Two scenario titles updated (the old titles became false
  statements once the divergences ceased to exist); pins 9→1 and 20→0 exactly.
  Two-pass open order (trap 2) and prescription-only slots (trap 3, load-bearing
  for D8) recorded in driver comments.
- **WI-5** — doc amendments applied verbatim; deferred-decisions.md Draw bullet
  resolved (faithful replay exists; prescription half unchanged); tech-debt:
  nothing added (nothing deferred). No new feature surfaced during
  implementation, so no backlog stubs were created (house rule 6).
- **Sign-off invariant** — both make-up fixtures replay exact at all three
  grains modulo their predicted ledgers; no synthesised-zero cell stands in for
  a make-up anywhere; no glossary/docs change beyond the approved drafted text;
  no `src/` file mentions GliderScore; both stores green; every new failure
  code (`openEntry.destinationOnOriginalRole`, `destinationNotFound`,
  `destinationNotEarlier`, `reasonRequired`, `competitorNotRegistered`,
  `reflightAlreadyOpen` (widened), `reflightDestinationTaken`,
  `score.reflightDestinationUnresolved`, `reflightDestinationTaskMismatch`,
  `reflightDestinationConflict`, plus `score.reflightShapeUnsupported`
  destination-aware) is asserted by at least one test at the layer that owns it.
