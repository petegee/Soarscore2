# Story — Model tie-break policy as class data

**Status:** In progress · **Raised:** 2026-08-29 (board discussion alongside
`kanban/completed/ranking-secondary-rawscore-key.md`; the gap is already
recorded in `docs/soaring-domain-class-diagram.md`, closing note "Tie-breaking
is not yet modelled", and notation finding F15) · **Fleshed out:** 2026-08-30
(rule text read at source; design settled, decisions D1–D10 below — WI-0
carries the sign-off gates, docs are approval-gated per house rule 4)

## What

Tie-breaking becomes a property of the Competition Class: an ordered list of
tie-break directives that the engine reads generically, as the policy layer
above the hardcoded two-rung display ladder (Score, PreDropScore). The corpus
shows the mechanisms genuinely vary per class: comparators (best dropped
score — `F3K.10`, `5.5.10.16–10.18`; qualifying position — `F3J.11`,
`5.5.11.13`) and operational directives (an additional full round —
`F3B.2.8`; a one-task tie-break fly-off — `F3K.10`), with rulebook silence
for F5L and every NZ class (CD decision).

The class diagram's closing note is the design constraint, kept verbatim as
the design contract: tie-breaking needs **two kinds of directive, not one** —
comparison against another figure *and* scheduling more flying. The engine
evaluates comparator rungs; an unsatisfied operational rung surfaces to
contest flow ("class policy requires a tie-break fly-off") rather than
resolving anything. An ordered list of comparators alone cannot express the
second kind — that is the whole reason this is a discriminated union, not a
sort-key list.

## Why it matters

The rule map's Tie-break row varies across every class — per the core
architectural law that makes it a field of the class model, and today it is
modelled nowhere. The ranking story hardcodes rungs 1–2 in the engine as a
class-agnostic display ladder, which is correct as far as it goes but cannot
express F3B's extra round, a tie-break fly-off, or qualifying position. The
deferred-decisions entry born from that story (Scoring and ranking, D2)
explicitly defers both rungs to *this* policy layer and is absorbed here when
it lands.

## Decisions (pre-answered during flesh-out 2026-08-30; D1, D8, D10 and the
docs list get Pete's sign-off at WI-0)

**D1 — the policy lives on the phase.** `PhaseDefinition` gains
`TieBreaks : ImmutableArray<TieBreakDirective>` (ordered; defaults empty).
Per-phase, not class-level, because the corpus's directives attach to *phase
rankings* and genuinely differ within one class: F3J's preliminary is silent
while its fly-off states qualifying position (`F3J.11.4`); F3K's preliminary
states best-dropped-score-then-fly-off while its fly-off is silent (D10).
Class-level would need an override mechanism to express that; per-phase needs
nothing extra. Same grain and reasoning as `DropPolicy`. Mirrors the diagram's
own split: "the class owns only what is genuinely true of the whole event;
phases own their scoring rules".

**D2 — the directive vocabulary: six kinds, one closed hierarchy.** An
`abstract record TieBreakDirective` with `private protected` constructor and
`[JsonPolymorphic($kind)]` / `[JsonDerivedType]` — exactly the
`FlightSelection` idiom (`ScoringVocabulary.cs:60-69`: "the kind IS the
type"). New file `src/Soarscore.Domain/PublishedClassDefinition/TieBreakPolicy.cs`,
header citing this story. The kind IS the type; no tag enum (the
ScoreTermKind/SelectionKind precedent).

Comparators — engine-evaluable, each compares a figure and narrows the tie
group:

| Kind | Operand | Source |
|---|---|---|
| `BestDroppedScore` | the best (max) dropped score, higher wins | `F3K.10.2`, `5.5.10.17` "the best dropped score defines the ranking" |
| `QualifyingPosition` | `int SourcePhaseOrdinal` — the competitor's placing in that phase's ranking | `F3J.11.4`, `5.5.11.13 h` "their respective position in the qualifying rounds" |

Operational — never engine-resolved; surfacing is their whole effect:

| Kind | Meaning | Source |
|---|---|---|
| `AdditionalFullRound` | the tied competitors fly one additional full round (all the class's tasks) | `F3B.2.8` |
| `TieBreakFlyoff` | a separate fly-off for the tied competitors; the CD defines one task | `F3K.10.2`, `5.5.10.17` |
| `ClassificationRounds` | more rounds of the class's task are flown until the ties break | `F3F.1.13` |

Silence:

| Kind | Meaning | Source |
|---|---|---|
| `UndefinedRequiresRuling` | the rulebook is silent; the tie stands (shared places) and a CD ruling is required | F5L `5.5.12.12` states classification and stops; NZ rules state no tie-break anywhere (`docs/rules/nz/00-nz-general-rules.md:117`); FAI General `C.15.6.1` likewise states none |

Note there is deliberately **no `NotPermitted`-analogue value** ("ties are
definitely never broken"): no rulebook in either corpus states one, and the
notation admits a construct only when a rule requires it (the F11 / no-`anyOf`
precedents). D8 covers what silence means for the engine.

**D3 — ladder semantics (the core design).** Rung 1 is always `Score`
descending — that is what "rank" means and it is core-owned, not class data.
Then:

- **A phase that states no tie-breaks** (empty `TieBreaks`, the default):
  the hardcoded display rung 2, `PreDropScore` descending, applies — the
  class-agnostic established practice settled by
  `kanban/completed/ranking-secondary-rawscore-key.md` and pinned by every
  frozen fixture oracle. Absence is the statement "unstated", and the display
  ladder is its established-practice resolution. **Zero fixture impact.**
- **A phase that states a list**: the list *is* the complete tie-break ladder
  after Score — the display rung 2 is superseded, not appended to. The stated
  list is walked in order: comparator rungs narrow the tie group; the first
  operational or `UndefinedRequiresRuling` rung reached with the group still
  tied halts evaluation, surfaces (D5), and the group shares places. Rungs
  *after* an operational one stay dormant — they are what the rulebook does
  once contest flow has acted (F3F's `bestDroppedScore` fallback exists in the
  list for exactly that future), and the engine never evaluates past the halt.
- A comparator ladder fully exhausted with the group still tied → shared
  places, settled (F3J fly-off: equal aggregate *and* equal qualifying place
  share the final place — the rulebook states nothing further).

Why supersede rather than append: under append, F3B's stated
`additionalFullRound` would never fire — the PreDropScore countback at rung 2
would separate every Score tie first, deciding by a mechanism `F3B.2.8` does
not state (the rulebook's answer to a tie is *fly*, not count back). The
class-data ladder must be able to say "no countback". Conversely, silence
must *keep* rung 2: the frozen fixture oracles are built on it (jerilderie-2010's
P4/P21; f3k-june-2020's ladder oracle is explicitly built from rungs 1–2), and
it is the completed ranking story's signed-off settlement. Supersede-when-stated
is the only reading that satisfies both.

**D4 — `BestDroppedScore` is the max dropped *cell*, not the sum.**
`F3K.10.2`'s "the best dropped score" is explicit; `F3F.1.13`'s fallback "the
result of the discarded round" is singular and reads as the same figure when
two rounds are discarded. This diverges from `PreDropScore` (the *sum* of
dropped cells) exactly where drops are plural — jerilderie-2010 drops two, and
max(a,b) vs max(c,d) need not order like a+b vs c+d. The two keys coexist:
`PreDropScore` stays the *unstated-policy* fallback rung; `BestDroppedScore`
is the *stated* comparator. Naming: neither reuses `RawScore` (taken —
per-flight, pre-normalisation, `TaskResult.RawScore`) — the comparator reads
`PhaseScores.DroppedScores` cells, normalised-scale. Penalty handling: the
dropped cell is a round-level figure and aggregate penalties deduct once from
the final score, so **no** penalty adjustment on this key (unlike
`PreDropScore`, which loses the same deduction as `Score` — the prior art
subtracts from total-scale keys only).

**D5 — operational rungs surface as data on the result.**
`CompetitionResult` gains `PendingTieBreaks : ImmutableArray<PendingTieBreak>`
(default empty), `PendingTieBreak(ImmutableArray<string> CompetitorRefs,
TieBreakDirective Directive)` — one per tie group whose next unevaluated rung
is operational or `UndefinedRequiresRuling`. Shared places are assigned
exactly as today while the tie stands (NFR-4: this is a read-side annotation,
never a write gate). HTTP/read-model exposure is out of scope (no route, DTO
or projection change) — the surface exists when contest flow wants it, as
data.

**D6 — team tie-breaks are out of scope.** `C.15.6.2` ranks *national teams*,
not competitors — a different ranking over derived figures. This matches the
already-recorded scope decision (`docs/competition-class-notation.md` §6 "No
team classification": a reporting rule, no national teams at club scale). The
directives here rank competitors only. Nothing lands for teams; do not admit
constructs for `C.15.6.2`.

**D7 — F3F.1.13's "five best scores" scoping is not modelled.** The clause
flies classification rounds only for ties "concerning the five best scores";
the directives are encoded unscoped. Scoping is contest-flow guidance (which
ties justify more flying) and no second class needs scoping — readmitted as a
scope field the day one does (the F11 precedent). Recorded as a known
deviation in the seed comment and the notation edit; flagged to Pete at WI-0.

**D8 — silence handling: `UndefinedRequiresRuling` suppresses the countback.**
This is the mirror of the re-flight enum's precedent (notation re-flight
block): the field is stateable so that grepping a definition finds the silence
(the F12 philosophy). Engine semantics when a phase states it: the tie stands
— shared places display, and `PendingTieBreaks` surfaces the ruling
requirement; **the display-ladder PreDropScore countback does not apply**
(nothing has ruled, and applying the countback would be the software deciding
what the CD has not). Consequence, to be blessed at WI-0: the five NZ/F5L
*library* classes score Score-ties as shared places, while frozen GS fixture
classes of the same families keep the countback (their JSON is a GS
transcription and stays untouched — trap 1). Both are honest in their own
frame: the seed records the rulebook's silence, the fixture records what GS
actually did. No fixture or BDD scenario adopts a seed class, so nothing moves.

**D9 — `QualifyingPosition` is unreachable end-to-end today, and that is
fine.** No second phase can be drawn (`Competition.DrawPhase` refuses
`!Phases.IsEmpty`; deferred-decisions "Phase-scope finalisation and
PromotionRule"). The comparator is implemented generically in the engine,
adoption-checked, proven by unit tests that construct the context directly;
the end-to-end wiring (orchestrator computing a prior phase's placings) waits
for multi-phase contest flow, same stance as the ranking story's "multi-phase
policies are unreachable today, so do not invent special handling". Adoption
check 17 (below) makes the rung unwritable on the first phase, so no reachable
class can request a figure the single-phase world cannot supply.

**D10 — `F3K.10.2` / `5.5.10.17` scope: preliminary only.** The tie clause
sits between 10.1 (final score of the preliminary) and 10.3 (the fly-off),
and its second mechanism — "a *separate* fly-off for the relevant competitors"
— presupposes the regular fly-off has not absorbed them. The directives are
therefore stated on the **preliminary** phase; the fly-off phase states
nothing and falls back to the display ladder (where `PreDropScore ≡ Score` —
fly-off phases declare no drops across the whole corpus — so ties share).
The rulebook does not spell the scope out; this reading is flagged to Pete at
WI-0 alongside D8.

### The corpus encoding (what the eleven seed classes state)

| Class (phase) | `TieBreaks` | Source |
|---|---|---|
| F3B (preliminary) | `[additionalFullRound]` | `F3B.2.8` |
| F3F (preliminary) | `[classificationRounds, bestDroppedScore]` | `F3F.1.13` — operational first, comparator is the "if this is not possible" fallback |
| F3K (preliminary) | `[bestDroppedScore, tiebreakFlyoff]` | `F3K.10.2` |
| F3K (fly-off) | absent | D10 |
| F5K (preliminary) | `[bestDroppedScore, tiebreakFlyoff]` | `5.5.10.17` |
| F5K (fly-off) | absent | D10 |
| F3J (preliminary) | absent | `F3J.11` covers fly-off placing only — silence |
| F3J (fly-off) | `[qualifyingPosition 1]` | `F3J.11.4` |
| F5J (preliminary) | absent | silence for the qualifying aggregate |
| F5J (fly-off) | `[qualifyingPosition 1]` | `5.5.11.13 h` |
| F5L (both phases) | `[undefined]` | `5.5.12.12` states classification and stops — silence |
| NZ M ALES 200, M NDC, N, P, F3K NDC (all phases) | `[undefined]` | `docs/rules/nz/00-nz-general-rules.md:117` — no tie-break, anywhere |

Single-drop equivalence note (why this is fixture-safe beyond the frozen-class
argument): for a single-drop phase, `PreDropScore − Score` **is** the one
dropped cell, so the stated `bestDroppedScore` rung orders every tie exactly
as the fallback rung 2 does. The supersession is observable only for
multi-drop stated classes (F3F — no fixture) and for F3B (countback removed —
no fixture; `f3b-international` is skip-listed on the multi-task gap).

## Engine design

1. **`src/Soarscore.Domain/PublishedClassDefinition/TieBreakPolicy.cs`** (new)
   — the six-kind directive hierarchy per D2, header citing this story and the
   diagram note it retires.
2. **`src/Soarscore.Domain/PublishedClassDefinition/ClassDefinition.cs`** —
   `PhaseDefinition.TieBreaks : ImmutableArray<TieBreakDirective>`, default
   empty; doc comment states the D3 ladder semantics (absent = display-ladder
   fallback; stated = supersedes rung 2).
3. **`src/Soarscore.Domain/Scoring/ScoringResultTypes.cs`** —
   `PhaseScores` gains `BestDroppedAggregate => DroppedScores.Length == 0 ? 0
   : DroppedScores.Max(s => s.Score)` (D4: no penalty adjustment); 
   `FinalCompetitorScore` gains `decimal BestDroppedScore` after
   `PreDropScore`, doc comment citing D4 and this story;
   `CompetitionResult` gains `PendingTieBreaks` + the `PendingTieBreak`
   record (D5).
4. **`src/Soarscore.Domain/Scoring/RankingEngine.cs`** — the sort keys become
   (Score, then each stated comparator's figure in order) when a policy is in
   force, else today's (Score, PreDropScore) pair; tie-group detection
   generalises to "all stated keys equal"; groups halted at an operational or
   ruling rung produce `PendingTieBreaks` entries and share places; the
   place-assignment/skip loop is otherwise unchanged. New signature carries
   the policy and the qualifying-position figures (e.g. a `TieBreakContext`
   record: the directives + `ImmutableDictionary<string,int>` source-phase
   positions) — exact shape is implementer latitude, but the **default path
   must be byte-identical to today** (Invariant T, clause 2).
5. **`src/Soarscore.Domain/Scoring/ScoringService.cs`** — in the phase loop
   beside `preDropTotals` (:540-541), mirror-accumulate
   `bestDroppedScores[competitorRef] = Math.Max(existing,
   phaseScores.BestDroppedAggregate)` — same loop, same "multi-phase
   combination semantics are unreachable; do not invent special handling"
   stance as the ranking story's mirror-accumulate. The Rank call passes
   `Phases[0].TieBreaks` (the only reachable ranking) and an empty
   qualifying-positions map (D9). Construction sites of
   `FinalCompetitorScore`/`CompetitionResult` outside production:
   `RankingEngineTests` / `RankingEnginePropertyTests` — they will not
   compile until updated; that is WI-1 work, expected.

## Adoption checks 17–19 (the inventory grows by three)

Implement in `src/Soarscore.Application/Commands/CompetitionClasses/ClassDefinitionValidation.cs`,
one line each added to the exhaustive inventory in
`docs/high-level-architecture.md` "Validated at adoption" (approved wording at
WI-0):

- **17.** `QualifyingPosition.SourcePhaseOrdinal` names an existing phase with
  a strictly lower ordinal (`F3J.11.4` — the figure is a *previous* phase's
  placing; unwritable on phase 1, which is what makes D9 safe).
- **18.** A phase's `TieBreaks` containing `UndefinedRequiresRuling` contains
  *only* it — mixing "the rulebook is silent" with stated rungs is a
  self-contradiction (the re-flight block's NotPermitted/Undefined distinction
  applied to lists).
- **19.** `BestDroppedScore` on a phase that declares no `DropPolicy` is
  rejected — no drop policy means no dropped cell ever exists, so the rung is
  provably inert (the check-13 precedent: reject what the rules have already
  ruled out).

Directives *after* an operational rung are deliberately **not** rejected —
`F3F.1.13` requires exactly that shape (D3's dormant rungs).

## Known traps (pre-answered — do not reopen inside this story)

1. **Never touch `tests/GliderscoreFixtures/**` class definitions or
   oracles.** They are frozen GS transcriptions; none states a tie-break
   policy, the engine's fallback for absent is today's ladder, so the expected
   result is **zero new fixture diffs**. A new diff means stop, triage,
   surface to Pete with the numbers — do not "fix" by relaxing anything.
2. **jerilderie-2010's P4/P21 must still place 8/9**, ales-sample-comp's
   seven-pilot zero-tie group still shares: the fallback path is untouched by
   construction (clause 1 of Invariant T is the test).
3. **`UndefinedRequiresRuling` is not `NotPermitted`.** No rulebook states a
   definite "no tie-break"; the corpus is silent, not negative. Inventing the
   second value would put a statement in front of the rules that never made
   it (the re-flight block's exact warning, mirrored).
4. **Do not model F3F's "five best" scope** (D7) and **do not admit team
   tie-breaks** (`C.15.6.2`, D6) — both are recorded decisions, not gaps.
5. **Multi-phase stays unreachable; do not restructure the phase loop.** The
   engine reads one phase's list; per-phase orchestration belongs to the
   future multi-phase contest-flow work (D9), not to this story.
6. **Content hashes shift by design.** New fields change the canonical JSON
   (and so the printed content hash) of definitions that state them;
   `tools/Soarscore.SeedData/README.md`'s hash printout changes. Nothing
   stores or compares old hashes (green-field; fixture JSONs are inputs whose
   hashes are computed at adoption-time, never pinned) — verify with a grep
   for hash literals in tests, and update any stragglers found.
7. **Serializer precedents are load-bearing.** The `$kind` discriminated-union
   idiom (`ScoringVocabulary.cs:60-66`) and the source-generated JSON context
   must both learn the new subtypes, or the seed round-trip check fails
   (`dotnet run --project tools/Soarscore.SeedData` — byte-identical JSON →
   records → JSON, context-vs-reflection agreement).
8. **Engine constructor sites.** `Rank`'s signature change breaks
   `RankingEngineTests` / `RankingEnginePropertyTests` positionally — WI-1
   work, expected (the ranking story's trap-5 pattern).

## Invariant T — the property, named here per CLAUDE.md (goes verbatim into
the property test's doc comment)

*For any field of active competitors and any phase policy: (1) placings
realise the total preorder induced by the full ladder — Score DESC, then each
stated comparator rung in order — for any two active competitors a, b:
ladder(a) >lex ladder(b) ⇒ place(a) < place(b); equal full ladders ⇒ equal
place; placings drawn from 1..n with standard skip-ahead numbering; (2)
**regression clause** — a phase whose `TieBreaks` is empty produces placings
identical to the two-rung display ladder (Score DESC, PreDropScore DESC) and
an empty `PendingTieBreaks`; (3) comparator rungs only refine Score ties —
they never invert a Score ordering; (4) operational and ruling rungs never
separate a tie and surface exactly one `PendingTieBreak` per halted group.
Useful generator relationship (mirrors the ranking story's): for single-drop
fields, `PreDropScore − Score` equals the dropped cell, so the stated
`bestDroppedScore` rung and the fallback rung coincide there — the generator
should cover both single-drop (equivalence) and multi-drop (divergence) input
classes.*

## Work items

Each WI lands compiling with its checkpoint green; WI-1 → WI-2 are strictly
sequential; WI-3 and WI-4 depend on WI-0's approvals, not on each other;
WI-5 closes out. Context budgets are deliberate — a sub-agent given one WI
needs only the files listed as *read*.

### WI-0 — Board and gates

`git mv` the story to `in-progress/`, update the status header in the same
commit. Get Pete's sign-off on:

- **D8 and D3 together** (the only decision pair with user-visible bite):
  stated silence suppresses the countback for library classes; absent keeps
  it for frozen fixture classes. The pitch: "the seed classes record what the
  rulebook says; the fixtures record what GS did; both stay honest."
- **D10** (F3K/F5K tie clauses read as preliminary-scoped).
- **D7** (F3F five-best scoping left unmodelled, recorded as a deviation).
- **The docs edits** (house rule 4 — ask, then apply): diagram §2 gains the
  directive hierarchy and the closing "Tie-breaking is not yet modelled" note
  is retired; notation §4 gains the `tiebreak` phase block, §6's "No
  tie-breaking" bullet and §10's F15 row are rewritten as resolved;
  `high-level-architecture.md` gains inventory lines 17–19; glossary —
  default **none**, optional one-sentence extension to the *Phase Definition*
  entry ("…and how ties in its ranking are to be broken") only if Pete wants
  it. Proposed wording for each edit is presented at WI-0, not improvised in
  WI-4.
- The corpus encoding table above as the seed source of truth.

### WI-1 — Domain: vocabulary, engine, unit + property tests

*Read:* `TieBreakPolicy`-relevant precedent files only:
`PublishedClassDefinition/ScoringVocabulary.cs` (the `$kind` idiom, :60-66),
`PublishedClassDefinition/ClassDefinition.cs`, `Scoring/ScoringResultTypes.cs`,
`Scoring/RankingEngine.cs` (84 lines), `Scoring/ScoringService.cs:439-580`,
`RankingEngineTests.cs`, `RankingEnginePropertyTests.cs`. *Do not open:* the
fixture corpus, completed harness stories, or any VB source — pure Domain
work. *Touch:* the five `src` edits above plus the two test files.

- Unit tests: comparator rungs separate Score ties (BestDroppedScore — max
  not sum, with a two-drop witness that orders differently from PreDropScore;
  QualifyingPosition — higher prior placing wins, equal prior place stays
  shared); stated silence surfaces `UndefinedRequiresRuling` and shares;
  operational rungs surface and never separate (AdditionalFullRound,
  ClassificationRounds first-in-list, TieBreakFlyoff after a comparator);
  dormant rungs after an operational one are never evaluated; exhausted
  comparator ladder shares with *no* pending entry; absent-policy fallback is
  byte-identical to the current ladder (trap 2's shapes included); qualified
  exclusion unchanged.
- `RankingEnginePropertyTests`: extend the generator to the stated-policy
  input class (directives drawn from the six kinds, figures consistent with
  the D4/BEST-dropped identity) and assert all four clauses of **Invariant T**;
  update the doc comment to cite Invariant T and this story (keep the
  Invariant R lineage mention).

**Checkpoint:** `dotnet build Soarscore.sln`; then
`dotnet test tests/Soarscore.Domain.Tests tests/Soarscore.Application.Tests
tests/Soarscore.Architecture.Tests` green.

### WI-2 — Adoption checks 17–19 (Application)

*Read:* `ClassDefinitionValidation.cs` (the sixteen checks; the check-5/6
header explains the numbered-sequence discipline),
`docs/high-level-architecture.md:151-229` (the inventory). *Touch:* the
validation file, its test file, and the three inventory lines (wording fixed
at WI-0).

**Checkpoint:** full solution test run green, including the new validation
tests (one per check: a violating definition yields exactly the expected
defect code).

### WI-3 — Seed data: the eleven classes state their policies

*Read:* the seed README, the per-class seed files, `Corpus.cs` / `Authoring.cs`
for the authoring helpers. *Touch:* the seed C# files only — per the table
under D2's encoding — plus nothing else.

- Add the directives with rule citations as comments (seed convention:
  citations stop at the repository boundary).
- `SeedF3F`'s comment records the D7 deviation (five-best scoping unmodelled);
  `SeedF3K`/`SeedF5K` comments record the D10 scope reading.
- Run `dotnet run --project tools/Soarscore.SeedData`: all four checks pass
  (round-trip, context agreement, depth, hashes); the `json/` outputs are
  generated artifacts (gitignored) — nothing to commit from them.

**Checkpoint:** seed run green; `dotnet build Soarscore.sln` green.

### WI-4 — Docs (approved wording only, house rule 4)

*Touch:* exactly the files and wording approved at WI-0 — diagram §2 (+ close
the unmodelled-gap note), notation §4/§6/§10, the three inventory lines if
not already landed by WI-2, glossary sentence only if approved. Nothing else
in `/docs` changes; `docs/rules/` is read-only and untouched.

**Checkpoint:** the notation still passes its own rule 1 (one keyword per
model element — every `tiebreak` token names a `TieBreakDirective` subtype);
grep `docs/soaring-domain-class-diagram.md` confirms the gap note is gone.

### WI-5 — Acceptance regression and close-out

- `SOARSCORE_TEST_STORE=sqlite dotnet test tests/Soarscore.Acceptance.Tests`
  (fast loop), then `postgres` wherever Docker exists. **Expected: zero new
  diffs on every fixture** (trap 1); any diff → trap 1: stop, triage, surface.
- `kanban/deferred-decisions.md`: **absorb** the "ranking ladder stops at
  rung 2" entry — its own text says this story absorbs it when it lands;
  delete the entry and carry anything still true (e.g. a dormant-runks note)
  into this story's record. Do not add a replacement entry for work this
  story did.
- `kanban/tech-debt.md`: reconcile; nothing is expected (house rules 5–6).
- `git mv` to `completed/`, status header same commit.

**Finish line:** `dotnet test Soarscore.sln` green, plus
`tests/Soarscore.Acceptance.Tests` under both `SOARSCORE_TEST_STORE` values
with zero new fixture diffs. Known flake: solution-wide Marten migration race
(`tech-debt.md` last item) — re-run the project alone before diagnosing.

## Out of scope (restated for sign-off)

- **Contest flow for operational rungs** — scheduling the additional round /
  tie-break fly-off / classification rounds, the CD's one-task choice for the
  fly-off, recording a tie-break ruling: this story *surfaces* the
  requirement (`PendingTieBreaks`); acting on it is a future story with its
  own board entry if raised.
- **HTTP/read-model exposure** of `PendingTieBreaks` and of the directives
  themselves (D5 — additive data, no route or DTO change).
- **Team classification tie-breaks** (`C.15.6.2`, D6).
- **F3F's five-best scoping field** (D7).
- **Multi-phase orchestration** — per-phase contexts, the fly-off
  qualifying-position wiring (D9; unit-tested only here).
- **The `.class` notation parser** — still a settled non-decision
  (deferred-decisions, Competition class model); the notation gains a *grammar*
  section, not an input format.
- **Glossary changes beyond the optional approved sentence** (WI-0 list).

## Story invariant for sign-off

The tie-break ladder is read once, generically, from `PhaseDefinition.TieBreaks`:
absent ⇒ the display ladder of the completed ranking story, byte-identical
(fixture parity); stated ⇒ the stated comparators supersede rung 2, narrow
ties in order, and the first operational or ruling rung halts with shared
places plus a `PendingTieBreaks` entry naming the group and the directive;
`UndefinedRequiresRuling` is stateable and states the rulebook's silence for
F5L and the NZ classes; all eleven seed classes state exactly the corpus
table; adoption checks 17–19 guard the vocabulary; every frozen fixture
replays byte-identical at all three grains with no ledger change; both stores
pass; no `/docs` change beyond the WI-0-approved wording; no `src/` file
names the prior art.
