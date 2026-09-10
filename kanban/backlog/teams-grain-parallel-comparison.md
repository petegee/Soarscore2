# Story stub - Teams grain in the parallel-run comparison

**Status:** Backlog
**Raised:** 2026-09-10 — closing the deliberate no-team-grain stance now that
teams scoring is implemented (see `Features/ScoringTeams.feature` acceptance
coverage); surfaced by the f3j-international parallel run.

## What

Add a **teams grain** to the parallel-run comparator. Today the comparator
compares raw, normalised and ranking grains per pair but deliberately does not
compare team standings — a disclosed stance (the ales precedent, restated in
the f3j-international ledger's provenance notes). Teams are now implemented
and acceptance-covered, so the comparator can compare the seed-run's team
standings against the GS oracle's team results
(`expected-teams.json` / `team-results-transcript.csv`) the way it already
compares final placings: ledgered triaged differences, count pins where
wildcarded, fully enumerated where small.

First candidate pair: `f3j-international` — `UseTeams=true`, 30 pilots across
8 teams (sizes 4,3,4,4,4,3,4,4, honestly populated but reaching no persisted
score today). Its existing ledger pins the individual grains; this story would
add the team-grain entries on top, not rewrite them.

## Why it matters

Team standings are a real product the club sees on the day. The parallel-run
discipline's product is "what the club would have seen differently"; without a
teams grain, a team-standings split can hide behind an individually-triaged
ledger even when every pilot cell is triaged — team normalisation or
team-scoring conventions could diverge in ways the individual grains never
show. The f3j-international ledger explicitly notes team standings are
report-time aggregations of unchanged individual normalised scores, which
predicts no team split there — making it the right *first* pair: expected
inert, proving the grain without new noise.

## Before starting

- Check the comparator's no-team-grain stance and the ales precedent notes
  (`tests/GliderscoreAcceptance` harness, `ParallelRunComparator.cs`) and the
  two in-flight in-progress stories for board collisions.
- `ScoringTeams` acceptance coverage defines the implemented team semantics —
  identify the team scoring/normalisation rules it proves and whether GS team
  arithmetic (from `expected-teams.json`) matches them or is another
  rulebook-vs-local-practice candidate.
- Corpus discipline: teams fixtures carry `expected-teams.json` and a team
  transcript already — no corpus re-shape should be needed; ledger schema
  widening follows the null-tolerant `ExcludedOracleCells` precedent
  (ales/christchurch ledgers deserialise unchanged).
- House rule 2 cross-reference against `docs/users.md` (team roles) and the
  domain model's team concepts; nothing in `/docs` changes without approval.
