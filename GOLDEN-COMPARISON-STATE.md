# GliderScore golden comparison — state after http-grain-one-metric-bridge

*Snapshot 2026-08-30. Point-in-time note, not a status document — open work lives
in `kanban/`.*

## Harness shape

Grain 1 now has exactly one mechanism for all fixtures: fetch
`preNormalisationScore` from `GET /task-round-result`, compose the task's
`scoreNormalised` terms over decoded slot metrics, compare exact-decimal. The
last in-process pipeline copy (`GsEquivalentRaw` etc.) was deleted after the D6
parity gate proved the bridge equals it cell-for-cell across every fixture
(commits `4b7b8b2`, `e477974`, `32d2488`). Re-run 2026-08-30: 12/12
`@gliderscore` scenarios green on sqlite, 38 s.

## Corpus

10 active fixtures — the original five (ales-sample-comp, f3j-international,
f3j-international-flyoff, f3k-sample-comp, jerilderie-2010) plus five
NZ-master fixtures (3× F5J, 2× F3K). All replay through public commands only
and compare exact at all three grains (raw, normalised, ranking).

## Ledgers

`jerilderie-2010`'s ledger is now **empty** — the
ranking-secondary-rawscore-key and reflight-aggregate-destination engine
stories landed, discharging its old D5/trap-3 entries. Of the six remaining
ledgers, every entry is structural, not arithmetic: excluded
phantom/cancelled/re-flight cells (D5), manufactured slots for retired/absent
pilots, and three binary64 representation artefacts (deltas of 5e-14 to 1e-13,
washed out by the 1-dp normalisation grid). **Zero ledgered arithmetic
divergences anywhere.**

## Confidence

**High** for the current scope: duration-family scoring (ALES/F3J/F3K/F5J)
across multi-group normalisation, drop policies (including two-drop and
option-0 counting), penalties, reflight aggregation, mid-comp re-draws,
retirement, 1-dp and integral grids, and winners >1000 — at three grains,
exact-decimal, against independently verified oracles.

**Not yet proven** (per `tests/GliderscoreFixtures/index.md` "Still open" and
story out-of-scopes): Speed/Distance rows in an active fixture (F3B still
skip-listed — multi-task rounds), F5L/F5B, real fly-off mechanics /
merged-prelim comps, two-timekeeper averaging, divergence D6 (factory-default
drop thresholds), fractional landing points (the double-round-after-sum
identity is invisible while landing points are integers), and term kinds
beyond Constant/Lookup in `scoreNormalised` (the mirror refuses loudly — the
guards, not the data, are what's narrow).

## What could improve

1. **Hunt the still-open witnesses** — an active Speed/Distance fixture, a D6
   factory-default-threshold fixture, a merged/prelim comp. Each is a
   backlog-targetable gap-hunt, same pattern as previous growth stories.
2. **Binary64 raw-grain artefacts**: three f3k-june-2020 entries are ledgered
   representation diffs. A comparator that emulates GS's binary64 raw-sum
   arithmetic (as was done for the float32 persist cast) could compare those
   exactly and empty the last non-structural ledger — arguably truer than
   ledgering.
3. **Postgres leg**: confirm the acceptance suite was recently run under
   `SOARSCORE_TEST_STORE=postgres` too — backend parity is part of the claim.
4. **Mirror widening readiness** is built (resolved-task plumbing) but
   unexercised; the first fixture needing a `RateTerm`/`PiecewiseTerm` in
   `scoreNormalised` will exercise it — worth a deliberate story rather than
   opportunistic widening.
