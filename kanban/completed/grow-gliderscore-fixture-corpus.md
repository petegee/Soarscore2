# Story — Grow the Gliderscore fixture corpus

**Status:** Completed · **Raised:** 2026-08-25 · **Planned:** 2026-08-26 ·
**Completed:** 2026-08-26 (WI-1–WI-8; survey corrections found and recorded
during WI-3 — Jerilderie re-scoped from skip-list to active fixture WI-8 on
Pete's call)

## What

Carry out the remainder of WI-4 of
`kanban/completed/gliderscore-golden-fixture-pipeline.md`: add more real
GliderScore competitions as fixtures. Each addition is small — extract → curate →
validate → index — using the tooling already in
`tests/GliderscoreFixtures/extract/`.

Source material supplied 2026-08-26:
`tests/GliderscoreFixtures/GliderScoreDownload/GliderScoreDownload.txt` (Jet
bytes, `.txt` camouflage) — GliderScore's shipped example competitions, one
export containing **five** comps. Multi-comp exports are split at curation
(schema-v1 rule 4); one shared source file serves all five fixtures.

## Why it matters

Corpus diversity (classes, multi-group rounds, re-flights, drop thresholds,
`Decs ≥ 1`) is what makes the replay/compare harness's gap-hunt valuable — see
the parent story's *Why it matters*.

## Survey of the export (2026-08-26)

Extracted to scratch with `extract.py`; profile per comp (`CompNo`):

| # | Comp | Class | Shape | Diversity value | Triage flags |
|---|------|-------|-------|-----------------|--------------|
| 1 | F3J International | F3J | 16 rounds × 5 groups, 30 pilots, 1579 score rows | multi-group ✓; 1 re-flight row ✓; `Decs=1` ✓; Drop1@8 crossed over 16 rounds ✓ | `UseTeams=True` (8 teams populated), series `'1'` |
| 2 | F3J International Flyoff | DurGeneral | 8 rounds × 6 groups, 7 pilots, 188 rows | fly-off-shaped comp; multi-group ✓ | series `'2'` only (`UseTeams=False`, all Team=0) |
| 3 | F3B International | F3B | 9 rounds × 1 group, 23 pilots | witnesses Speed+Distance family rows | multi-task-per-round → standing skip; also `UseTeams=True` |
| 4 | Jerilderie 2010 | DurGeneral | 63 pilots, **zero score rows** | nothing to replay | moot — no scores |
| 5 | F3K Sample Comp | F3K | 9 rounds × 1 group, 10 pilots, 90 rows | configured drops crossed (Drop1@5, Drop2@9 over 9 scored rounds) ✓; first F3K-family witness; per-round task variation (G, A(1), F, D, C(3), X×4 via `F3KTaskByRound`) | `UseTeams=True` (4 teams), series |

> **Survey corrections (2026-08-26, verified against the extraction of record
> during WI-3 curation; the table above is kept as first written):**
>
> | # | Survey said | Verified reality |
> |---|-------------|------------------|
> | 1 | 1579 rows; 1 re-flight row | 485 rows (the 1579 was an inverted filter); **no re-flight row** — instead a phantom round-1 group 5 of five zero-score duplicate slots, neutralised by GS's best-per-original-round aggregation and kept verbatim |
> | 2 | 188 rows; 8 rounds × 6 groups | 28 rows = 7 pilots × 4 scored rounds (matches Pete's R1–R4 transcript); single-group rounds |
> | 4 | zero score rows | **882 rows across 14 rounds × 5 groups**, 843 with flight times, and the export's **only re-flight row** (R13 pilot 29, OriginalRoundNo=12); Drop1@6 **and** Drop2@12 both crossed over 14 rounds |
>
> Consequence: comp 1 does *not* witness a re-flight; the corpus's only
> re-flight witness is Jerilderie. WI-6's planned "no scores captured" skip
> reason for jerilderie-2010 was false. Pete approved curating it as an active
> fixture in this story — see WI-8.

Other facts established by the survey:

- **PII clean**: all pilots are GliderScore's ZZ-prefixed shipped test names;
  every `Pilots` contact column empty; `CompPilots` has no contact columns.
  Re-check remains mandatory at each curation.
- `Scores` carries **no team columns at all**; `CompSeries` is an **empty
  table** — every series link in this export is a dead reference.
- Producer DBVersion 6.78 (same producer version as fixture #1).
- Environment drift: the pinned extractor path `/var/data/python` (Py 3.13)
  no longer exists; `access_parser==0.0.6` (same pinned version) installed
  under pip user-site on system Python 3.10 and extraction ran identically.
  README must be updated to match reality.

## Decision — evidence-based triage refinement (2026-08-26, Pete approved)

Rule 5 read mechanically would skip-list comps 1, 2 and 5 on team/series flags
alone and the corpus would gain nothing active. Approved refinement: **a
team/series triage flag forces skip-listing only when it plausibly alters the
individual-score oracle.** Activation requires recorded evidence in
`competition.json`:

- *series*: dead link — zero `CompSeries` rows reference the comp;
- *teams*: `Scores` has no team columns (team results are report-time
  aggregations of unchanged individual normalised scores); note any populated
  `CompPilots.Team` assignments honestly.

If a future export shows team/series data reaching persisted scores, that
comp is skip-listed without ceremony. The replay/compare harness backstops
this: if GS's oracle ever disagrees, it fails loudly there.

This amends the standing skip reason inherited from the completed parent
story (never edited); the amendment lives here, in `index.md`, and in
`validate.py`.

## Work items

### WI-1 — Refine validation rule 5 + index standing-skip wording

`validate.py`: a concept-gap triage flag forces skip-listing *unless*
`competition.json` carries a `triageJustification` recording the evidence
above (mechanically checkable parts: series dead-link count; absence of team
columns in `scores-raw`). With justification present and sound → pass; absent
or unsound → fail naming the requirement. Negative self-tests for both
directions. `index.md` standing-skip §6 wording updated to match. README
updated for the extractor-environment drift (Py 3.10 + pip user-site, same
pin).

**Done when:** negative self-tests pass; `ales-sample-comp` still validates
clean against the amended checker.

### WI-2 — Commit the shared source export

Byte-verified copy to `tests/GliderscoreFixtures/sources/gliderscore-example-comps.mdb`
(replacing the `.txt`-camouflaged download dir), sha256 recorded; PII findings
documented. Each of the five fixtures' `provenance.json` cites this one file
by repo-relative path + sha256.

**Done when:** sha256 recorded in each provenance file; scratch download dir
removed from the tree.

> **As built 2026-08-26:** Done (working tree). Source committed as
> `tests/GliderscoreFixtures/sources/gliderscore-example-comps.mdb`
> (806 912 bytes, sha256
> `89e8f2cbec91af6bf7f994c4e15fc2c0214e7c31944273ecbf3ca31d40d790df`,
> byte-verified copy — `cmp` clean against `GliderScoreDownload.txt`, identical
> digests; source permissions untouched). One shared evidence-of-record
> extraction committed beside it, `sources/gliderscore-example-comps-extract/`
> — one export serves five comps, so one extraction, not per-fixture copies;
> deterministic: `diff -r` clean against the survey extraction, 29/29 tables
> identical. PII re-check clean: all seven `Pilots` contact columns (Street,
> Town, State, PostCode, Email, PrivatePhone, WorkPhone) empty across all 74
> rows; all 74 pilots are GliderScore's shipped ZZ-prefixed test names (Max
> ZZKroger … Eric ZZSmith, zero non-ZZ); `CompPilots` carries no contact
> columns at all; case-insensitive sweep (email|phone|mobile|street|town|city|
> postcode|zip|address) of every other user table surfaced only empty-table
> false positives (`AudioSettings.Phonetic`, `TimerSettings.TimerState`) — no
> populated personal datum anywhere. Scratch download dir removed from the
> tree.

### WI-3 — Fixture `f3j-international` (comp 1)

Split at curation from the shared extract. Six fixture files per schema v1.
Curation checkpoints: verify the single re-flight row end-to-end
(`OriginalRoundNo ≠ RoundNo`) — first re-flight witness; `Decs=1` makes this
the first fixture to witness the float32 persist casts (arithmetic story,
Precision & storage §4); Drop1@8 crossed across 16 rounds; 8 teams populated
→ honest `triageJustification`. Decide ranking-oracle source per the hybrid
rule — prefer `gs-report-transcript` if Pete can run GS Overall Results on
the install; else `reconstructed-ladder` with its caveat noted.

**Done when:** `validate.py --index` PASS; oracle spot-check against a
hand-run ladder sample; indexed active.

> **As built 2026-08-26:** Done (working tree). Fixture
> `tests/GliderscoreFixtures/f3j-international/` split from the shared extract,
> CompNo 1 only: 30 pilots, 16 scored rounds × 4 groups, 485 scores-raw rows =
> 485 expected-score keys, 30 compPilots + 30 pilots entries rows, landing
> scheme LndgNo=2 carried with its 23-point table. Oracle source
> `gs-report-transcript`: Pete ran GS Overall Results R1–R16
> (`overall-results-transcript.csv`, sha256
> `14b6e1d9f0be1f6c438695560bd0e77824a1ffd5ea235afe89fb2a1f8b8e3ae0`); all 480
> transcript round cells match persisted NormalisedScore exactly. Checkpoint
> findings: **A — re-flight: none.** The survey row was wrong; all 485 rows
> have OriginalRoundNo = RoundNo and the export's single re-flight row belongs
> to comp 4 (Jerilderie R13) — this fixture witnesses no re-flight. What round
> 1 does carry is a phantom zero-score group 5 (five duplicate pilot slots for
> pilots already flown in G1/G4); GS's best-per-original-round aggregation
> neutralises it (verified against the oracle) and the rows are kept verbatim.
> **B — Decs=1 confirmed but no float32 witness:** no binary32 widening appears
> anywhere in the export (every persisted RawScore/NormalisedScore reprs clean
> at its decimal count; extracted column type is Double; the only long tails
> are comp-3 Speed averaged seconds, i.e. binary64 arithmetic). Reported as a
> plain null result; the persist-cast question stays open. **C — Drop1@8
> crossed:** 16 scored rounds ≥ 8 → exactly one drop per pilot; all 30
> transcript `*` marks accounted for, landing on each pilot's worst score
> wherever it sits (histogram R1:1, R2:2, R3:2, R4:1, R7:1, R9:1, R10:3,
> R12:1, R13:7, R14:7, R16:4) — not confined to rounds ≥ 8.
> **D — oracle sanity vs ladder:** independent recompute (best-per-round sum,
> one lowest-cell drop with latest-equally-bad-first tie-break, report-time
> re-round to 1 decimal, Score DESC/RawScore DESC) agrees with all 30 ranks,
> all 30 dropped-round picks and the Score column. Also corrected en route:
> the survey's "~1579 score rows" for comp 1 was an inverted filter
> (2064 total − 485); actual is 485. Triage activated via sound
> `triageJustification` (series dead link, teams never reaching persisted
> scores, 8 populated CompPilots.Team assignments noted honestly).
> Validation: `validate.py ../f3j-international --index ../index.md` → PASS
> (rules 1–5, integrity; scores-raw rows=485, expected-score keys=485), no
> warnings; ales regression still PASS.

### WI-4 — Fixture `f3j-international-flyoff` (comp 2)

DurGeneral-class fly-off-shaped comp, 8 rounds × 6 groups, no drops, Decs=0.
Simplest of the three activations; series dead-link justification only.

**Done when:** validate PASS; indexed active.

> **As built 2026-08-26:** Done (working tree). Fixture
> `tests/GliderscoreFixtures/f3j-international-flyoff/` split from the shared
> extract, CompNo 2 only: 7 pilots, 4 scored rounds × 1 group, 28 scores-raw
> rows = 28 expected-score keys, 7 compPilots + 7 pilots entries rows, landing
> scheme LndgNo=2 carried with its 23-point table. Oracle source
> `gs-report-transcript`: Pete ran GS Overall Results R1–R4
> (`overall-results-transcript.csv`, sha256
> `6582f7448899b962b843a80d12373c5a8c53db3da5ab518a6c8c269fc42d7aea`); it
> covers every scored round of this four-round comp, and all 28 transcript
> round cells match persisted NormalisedScore exactly. Checkpoint findings:
> **A — shape:** exactly 4 scored rounds × 7 pilots × 1 group confirmed from the
> Scores rows themselves (rounds 1–4 only, TaskNo 1, no re-flight rows); the
> survey's "8 rounds × 6 groups / 188 rows" was wrong again — the comp's Dur
> draw state agrees (NbrPlts=7, GrpsInRnd=1, NbrRnds=4). It is the fly-off tail
> of the same Milang event as comp 1 (CompDate two days later), so its value is
> a small single-group integral-scored comp with a full transcript oracle, not
> fly-off mechanics (prelim/merged stays an unconditional skip elsewhere).
> **B — drop config reality:** DropScoreOption=0 with Drop1AtRound..Drop5AtRound
> all 99 and F3QDrop6to10='99,99,99,99,99' against 4 scored rounds → no drop can
> ever activate (matches the DurGeneral factory default of no-drop); the
> transcript carries zero '*' marks, reconciled. **C — oracle sanity vs
> ladder:** independent recompute (best-per-original-round normalised sum, no
> drops, report-time re-round to 0 decimals, Score DESC/RawScore DESC) agrees
> with all 7 transcript ranks, all 7 Score values and all 28 round cells; all
> totals distinct → plain ranks 1..7, no '=n'. **D — PII re-check clean** for
> this comp's 7 members: every Pilots contact column empty, all ZZ-prefixed
> test names, CompPilots carries no contact columns. Decs=0 ⇒ integral scores ⇒
> the float32 persist casts are unwitnessed here too (same null result as the
> other two fixtures). Triage activated via a sound series-only
> `triageJustification` (CompSeriesNo='2' dead link against the zero-row
> CompSeries table, deadLinkCount=0); UseTeams=false with every CompPilots.Team
> = 0 (verified), so no teams justification exists or is needed.
> Validation: `validate.py ../f3j-international-flyoff --index ../index.md` →
> PASS (rules 1–5, integrity; scores-raw rows=28, expected-score keys=28), no
> warnings; ales-sample-comp + f3j-international regressions still PASS.

### WI-5 — Fixture `f3k-sample-comp` (comp 5)

First F3K-family fixture. Curation checkpoints: per-round task schedule (G,
A(1), F, D, C(3), X×4) — confirm the fixture schema carries
`F3KTaskByRound` faithfully and **surface immediately** if our class model /
engine cannot express task-varying rounds within one F3K comp (that is a
model gap → new backlog story, not silent scope growth here); configured
drops crossed (Drop1@5, Drop2@9 with 9 scored rounds) — first live drop
witness, adjacent to divergence D6; four teams populated → honest
justification.

**Done when:** validate PASS; indexed active; any model gap raised as its own
backlog stub.

> **As built 2026-08-26:** Built (working tree), **blocked on validator rule 2
> — not yet indexed, needs Pete's call below**; nothing committed. Fixture
> `tests/GliderscoreFixtures/f3k-sample-comp/` split from the shared extract,
> CompNo 5 only: 10 pilots, 90 scores-raw rows = 90 expected-score keys (9
> rounds × 10 pilots × 1 group; Scores.TaskNo constant at 5), 10 compPilots +
> 10 pilots entries rows, F3K family row carried verbatim, F3KTaskByRound
> carried whole (9 rows — the fixture's reason to exist), landingSchemes
> empty. Oracle source `gs-report-transcript`: Overall Results R1–R9
> (`overall-results-transcript.csv`, sha256
> `b122b52a5fed31638c8d8205a94832ad71c31ea739810d3e9579bd865de4bc2c`); the
> transcript Pilot column equals CompPilots.StartNo (UsePilotNumbering=true),
> corroborating the name→PilotNo mapping on all 10. Checkpoint findings:
> **A — task schedule confirmed:** G, A(1), F, D, C(3), X×4 exactly as
> surveyed; transcript captions agree (Best5 2:00max / L1 5max in 10m / Best3
> 3:00max / "Ladder  (Not FAI)" / AllUp 3:00\*5 / NoTaskSet×4). Task identity
> lives wholly in F3KTaskByRound, not in the Scores rows.
> **B — drops crossed, with a twist:** DropScoreOption=0, Drop1AtRound=5,
> Drop2AtRound=9 (Drop3..5=14/19/24 also staged). Option-0 activation counts
> DISTINCT rounds with RawScore>0 (arithmetic story, Drop-worst §3): only
> rounds 1–5 count, so exactly one drop activates and **Drop2 never bites —
> not even on a zero cell**, because the denominator never reaches 9. The one
> active drop lands on Rnd9 — a zero placeholder from an unflown round
> (latest-equally-bad-first tie-break), never on a real score; every pilot
> keeps all five real normalised scores. Transcript reconciled: exactly one
> '*' per pilot, all ten on Rnd9. Survey nuance corrected: comp 5 has 5
> scored rounds plus four NoTaskSet placeholder rounds (rounds 6–9, ten
> all-zero rows each), not 9 scored rounds. What the fixture witnesses is the
> option-0 mechanism over F3K task cells — zeros of unflown rounds don't count
> toward activation yet sit in the candidate pool — not divergence D6 itself
> (thresholds here are custom 5/9, not the factory-default 12 vs FAI's 6).
> **C — oddities explained:** rows-with-any-time (39) vs NS≠0 (49) — the gap
> is exactly round 2 (task A(1)), whose ten rows store results in Laps with
> all Time columns 0. Three R4 rows carry Landing=145: F3K ladder inputs in a
> repurposed column, NOT landing-scheme lookups (the F3K row has no
> landing-reference field at all — following the data finds nothing to
> resolve). Penalty=100 on four rows (pilots 42, 56×2, 65); cells are
> pre-penalty and GS subtracts per-pilot totals post-sum (transcript shows
> ZZPotter 100, ZZNancarrow 200, ZZIrvin 100). GroupScoreDecimals=0 ⇒ float32
> persist casts again unwitnessed (all 90 values binary32-round-trip clean).
> **D — oracle sanity vs ladder:** independent recompute (best-per-original-
> round NS sum over all nine rounds, one lowest-cell drop latest-equally-bad-
> first, minus per-pilot penalty totals, floor ≥ 0, re-round to Decs=0, Score
> DESC/RawScore DESC) agrees with all 10 ranks, all 10 Score values and all
> 10 dropped-round picks. Triage activated via sound `triageJustification`
> for both flags ('1;2' double dead link; 4 populated teams, sizes 2,2,4,2).
> **E — model capacity: EXPRESSIBLE, no gap, no stub.**
> `PhaseDefinition.Rounds.Kind=ChooseFromCatalogue` publishes the phase's task
> catalogue (`ClassDefinition.cs:91`, notation §2) and
> `Competition.ResolveSchedule` (`Competition.cs:998–1031`) takes a CD-named
> task for every round at draw time, validating count/catalogue/distinctness;
> repeats are allowed (`RequireDistinctTaskPerRound=false`, X×4), and the flow
> is acceptance-tested (`DrawingACatalogueChoicePhaseSteps`). The GS schedule
> is event-level data (per-comp F3KTaskByRound rows), which is exactly where
> Soarscore puts it.
>
> **The blocker:** `validate.py ../f3k-sample-comp --index ../index.md`
> fails exactly one rule — `rule 2: competition.json has no Dur family row to
> reference a landing scheme`. Rule 2 hardcodes a Duration-family assumption;
> comp 5 truthfully has no Dur row (Dur exists for comps 1–4 only) and no
> scheme reference, and fabricating either would break the corpus's verbatim
> discipline. Rules 1, 3, 4, 5 and integrity (90/90 keys) all pass; ales +
> f3j-international + flyoff regressions all still PASS. Proposed, NOT
> applied (validate.py was out of bounds this WI): amend `check_rule_2` to
> apply the durLndg off-table check only when the fixture actually has a Dur
> row (F3K/F5K-family fixtures carry no landing scheme by construction; the
> repurposed-Landing fact stays documented in provenance). On approval:
> amend → re-validate → add the index line → WI-5 done. Pete approved the
> amendment the same day: `check_rule_2` now runs the durLndg off-table landing
> check only when the fixture actually carries a Dur family row, so Dur-less
> (F3K/F5K-shaped) fixtures pass without any landing scheme — with that change
> f3k-sample-comp validates clean (self-test 12/12 including a new Dur-less
> case) and was indexed active.

### WI-8 — Fixture `jerilderie-2010` (comp 4) — added 2026-08-26 (Pete approved)

Raised by the survey corrections above: Jerilderie 2010 is a real scored comp
(63 pilots, 14 rounds × 5 groups, 882 score rows, 843 with flight times), and
the export's **only re-flight row** lives here (R13, pilot 29,
`OriginalRoundNo=12`). Config: DurGeneral, Decs=0, GroupScoreOption=1,
Drop1@6 + Drop2@12 both crossed over 14 rounds — first two-drop witness;
`UseTeams=True`, series `'1;2'` dead link → sound `triageJustification` for
both. Oracle: `reconstructed-ladder` unless Pete later supplies a GS Overall
Results transcript (none exists yet); the fixture must carry the ladder caveat.

**Done when:** validate PASS; indexed active; re-flight row verified
end-to-end; both drop thresholds' activation reconciled against a hand-run
ladder sample.

> **As built 2026-08-26:** Done (working tree), nothing committed. Fixture
> `tests/GliderscoreFixtures/jerilderie-2010/` split from the shared extract,
> CompNo 4 only: 63 pilots, 14 scored rounds × 5 groups (70 normalisation
> groups of 11–14 rows, group sizes drifting as pilots missed rounds),
> 882 scores-raw rows = 882 expected-score keys (RawScore/NormalisedScore
> stripped, everything else verbatim including `Updated` and
> `OriginalRoundNo`), 63 compPilots + 63 pilots entries rows, landing scheme
> LndgNo=3 ("F3J/F3L/F5L Enter Points" — distance≡points table 30…100)
> carried with its 23 points. Oracle source `reconstructed-ladder`: **no GS
> transcript exists for this comp**; if Pete later runs Overall Results R1–R14
> on his install, dropping `overall-results-transcript.csv` beside the fixture
> upgrades it to `gs-report-transcript`. Checkpoint findings:
> **D — ladder recompute reconciles 882/882 (the oracle's foundation):** an
> independent implementation of the documented arithmetic — packed-mmss decode,
> symmetric over-target decay vs target 600 ×1 pt/s (`durFlightPenalty=0`, so
> no deduction enters raw), exact-match landing lookup, option-1
> `1000·Raw/MaxRaw` per `(Task,Round,Group,ReFlight)` with explicit max-scan,
> half-up round to Decs=0, zero-max guard, float32 persist-cast emulation via
> struct pack/unpack — reproduced **all 882 persisted RawScore AND all 882
> NormalisedScore values exactly**; option-1 invariants hold everywhere (every
> group max maps to NS=1000, no NS>1000). Final ranks are THE LADDER applied to
> those reconciled cells: best-per-original-round sum − per-pilot penalty
> totals (one witness: pilot 2, Penalty=100 on R11/G3), two lowest-cell drops,
> report-time re-round (identity at Decs=0), Score DESC/RawScore DESC.
> **A — re-flight end-to-end:** the export's only `OriginalRoundNo≠RoundNo`
> row is R13/G1/SeqNo=14 pilot 29 `OriginalRoundNo=12` (packed 958.0 = 598 s ≤
> target → Raw 598+91=689, NS ⌊1000·689/696⌋=990); pilot 29 has **no R12 row at
> all** and flew twice inside R13 — the orig-12 re-flight in G1 and his regular
> orig-13 slot R13/G2/SeqNo=10 (906.0 = 546 s, Raw 637, NS 913); resulting
> group sizes R13 G1=14 / G2=13. Because ReFlightNo=0 the slot normalises
> within ordinary R13/G1, and aggregation keys on OriginalRoundNo, so it simply
> supplies his round-12 cell (no de-dup needed — no other orig-12 candidate).
> Both slots carried verbatim. WI-3-style phantom sweep came back clean:
> no duplicate (R,G,pilot) slots, no duplicate (R,G,SeqNo), no duplicate
> (pilot,OriginalRoundNo); the 39 all-zero-time rows are genuine zeros
> (Updated='True', persisted Raw/NS 0), not placeholders like comp 1's or
> comp 5's.
> **B — two-drop witness:** scored rounds = DISTINCT rounds with RawScore>0
> for task 1 = all 14; Drop1AtRound=6 crossed once round 6 completes, Drop2@12
> once round 12 completes — final state activates both, and every one of the
> 63 pilots loses exactly two worst cells (latest-equally-bad-first tie-break;
> distribution {2: 63}), landing wherever the worst sits across R1–R14 (e.g.
> leader pilot 62 drops R6's 997 and R2's 999, keeping twelve perfect 1000s →
> Score exactly 12000 = theoretical maximum of 12 kept rounds × 1000).
> Final-Score agreement with GS itself remains transcript-pending (see oracle);
> what is already GS-exact is every cell the drops choose among.
> **C — rule-2 landing audit:** all 759 non-zero Landing values fall within
> the scheme's 23 distances {30…90 step 5, 91…100} — validator enforces, plus
> manual sweep found nothing F3K-like (no repurposed values; DurGeneral landings
> are genuine, matching the "Enter Points" scheme where distance ≡ points).
> **E — sanity:** 63 pilots ranked, each entries member exactly once; all
> (Score, RawScore) pairs distinct → plain ranks 1..63, no "=n".
> Triage activated via sound `triageJustification` for both flags ('1;2'
> double dead link against the zero-row CompSeries table, deadLinkCount=0;
> UseTeams=true + UseTeamProtection=true + NbrForTeamScore=4 with all 63
> pilots assigned across 14 teams (sizes 3,5,4×4,5×7) that never reach
> persisted scores). PII re-check clean for all 63 members. Dur row carried as
> the sibling 9-field scoring subset; populated-but-display/draw-shape fields
> omitted there are recorded verbatim in provenance (durGroupsPerRound=5,
> durPilotsMax=13, durPilotsMin draw-state string confirming 63/5/14, etc.).
> Validation: `validate.py ../jerilderie-2010 --index ../index.md` → PASS
> (rules 1–5, integrity; scores-raw rows=882, expected-score keys=882), no
> warnings; ales-sample-comp + f3j-international + f3j-international-flyoff +
> f3k-sample-comp regressions all still PASS; validate self-test 12/12.

### WI-6 — Skip-list f3b-international

Index entry (listed forever): `f3b-international — skipped —
multi-task-per-round (Duration+Speed+Distance) hits unsupportedRoundComposition`.
(Originally "skip-list the other two"; jerilderie-2010 is curated active by
WI-8 instead, per Pete's 2026-08-26 call on the false survey row.)

> **As built 2026-08-26:** Index entry added (Competitions section, after
> jerilderie-2010; skip-listed forever). Facts verified against
> `sources/gliderscore-example-comps-extract/` before writing: Comps row 3 =
> F3B International, UseTeams=true, CompSeriesNo='1' against the zero-row
> CompSeries table (dead link); 23 pilots; 9 rounds; 579 score rows — rounds
> 1–8 fly all three tasks, round 9 is speed-only. Spd.json and Dis.json each
> hold exactly one row, CompNo 3 alone: the export's only Speed/Distance
> family-row witness, as claimed. One survey correction en route: "9 rounds ×
> 1 group" was wrong — grouping is per task (duration in 4 groups, distance in
> 6 small groups, speed one all-pilot group); the index line records the
> verified shape. No f3b-international fixture directory exists or was
> curated. Jerilderie moved to WI-8 instead (already built and indexed
> active).

### WI-7 — Reconcile the diversity hunt-list

Update `index.md` *Diversity wanted* to reflect what is now witnessed
(multi-group, re-flight, drops crossed, Decs≥1, F3K family, Speed/Distance
rows seen but comp skipped) and what remains open. This is how the next
gap-hunt target gets chosen.

> **As built 2026-08-26:** Done (working tree). `index.md` *Diversity wanted*
> rewritten from "none witnessed yet" to witnessed (9 items) vs **Still open**
> (6 targets), each item grounded in a fixture file or the shared extract
> before writing: multi-group (f3j-international, jerilderie-2010);
> re-flight (jerilderie-2010 only); drops crossed (f3j-international Drop1@8,
> f3k-sample-comp Drop1@5 with the never-biting Drop2@9, jerilderie-2010
> Drop1@6+Drop2@12); F3K task catalogue; Decs=1-vs-0 contrast; placeholder
> zero rounds; penalty deductions; jerilderie's perfect-maximum 12000
> (re-verified in the extract: pilot 62 = twelve kept 1000s after dropping
> 997/999); fly-off-shaped comp. Open list keeps the two original stragglers —
> float32 persist-cast artifacts (null result restated plainly:
> f3j-international is Decs=1 with no binary32 widening anywhere; needs a
> future GS export storing Singles) and Speed/Distance rows in an ACTIVE
> fixture (f3b-international stays skip-listed) — and adds four new targets:
> divergence D6 proper (all crossed thresholds so far are custom, not the
> factory-default-12-vs-6 pair), F5J/F5L/F5B families absent entirely,
> merged/prelim comps absent (PrelimCompNo=-1/MergedComps empty throughout),
> and multi-timekeeper scoring (every Dur row durNumberOfTimekeepers=1).
> Validation re-run after the edit: self-test green, all five fixtures PASS
> against `--index` (rule-5 parsing unaffected).

## Before starting

- Take into `in-progress/` per board rule 3 before WI-1 code.
- PII re-check at each curation (survey finding is necessary, not sufficient).
- Respect the amended standing skip reasons; anything tripping them without a
  sound `triageJustification` stays skipped.
- Ranking-oracle transcripts: ask Pete early whether GS can be run against
  these example comps on his install — it changes three fixtures' oracle
  source.
