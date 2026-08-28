#!/usr/bin/env python3
"""Derivation of record for expected-result.json (f5j-christchurch-2019, CompNo 45).

Independently reconstructs GliderScore's Overall Results ladder for this comp from
this fixture directory's own committed files only (scores-raw.json,
competition.json, entries.json, expected-scores.json) using the documented GS
arithmetic (kanban/completed/resolve-gliderscore-scoring-arithmetic.md, DBVersion
6.78) and the pipeline story's ranking-oracle decision
(kanban/completed/gliderscore-golden-fixture-pipeline.md, "Ranking oracle -
decided 2026-08-25"): no GS report transcript exists for any of the five NZ
fixtures, so this oracle's source is "reconstructed-ladder". Re-running this
script verifies the committed expected-result.json against the reconstruction;
any divergence exits non-zero without writing anything (mismatch policy:
source-level triage before indicting the engine).

Rules applied (all cited to the arithmetic story):

Per-cell scores (both passes asserted against expected-scores.json):
  - Duration RawScore (`Update_RawScore` Case 1): packed-mmss decode
    Fix(v/100)*60 + (v - 100*Fix(v/100)); CalcTimeScore with 1 timekeeper =
    decode(Time1Mins); cap at target when varFltDednIdx=3 and class not in
    {DurALES,F3G} - F5J qualifies for the cap arm (600 s); x DurPointsPerSecond
    (1.0); + landing bonus (scheme-11 exact-match lookup, 0 -> short-circuit,
    miss -> 0 points; +0.005 nudge for Decs=1 then RoundNumber half-up);
    - height penalty two-rate linear (H<=durRefHeight: H*upTo, else
    refH*upTo + (H-refH)*over) with start height stored in the misleadingly
    named Scores column FlightScoreDeduction; Duration-only floor >= 0.
  - Normalisation (GroupScoreOption effective 1 = points basis): group key
    (TaskNo,RoundNo,GroupNo,ReFlightNo); max scanned over EVERY row of the
    group view including Updated='False' placeholders (their persisted raw,
    0.0 here); NS = RoundNumber(1000*Raw/MaxRaw, GroupScoreDecimals effective 1)
    half-up via Int(x+half*10^-d) op-order replicated in binary64, floored <0->0;
    zero-max guard writes 0. Landing sits inside Raw (points basis), so winners
    map to exactly 1000. varFltDednIdx=3 branch post-deduction (idx=4) not applicable.
    The F5JMotorReStarted flag takes no scoring effect (Dur.F5JMotorRestartOption null).

Aggregation (report rollup, `Rpt_Results_Overall_MOD.vb:2690-2712` semantics):
  - Window ends at TaskLastRound = MAX(RoundNo where Updated='True') = 11; the
    wholly-unflown placeholder rounds 12-18 sit outside the rollup. Mid-comp
    snapshot: the ladder covers exactly what exists, no backfilling.
  - Per pilot: sum of the best NormalisedScore per ORIGINAL round/task cell
    (re-flight de-dup keeping highest - none exist here, OriginalRoundNo == RoundNo
    on all 324 rows, ReFlightNo = 0 always), minus |Penalty| (all 0), floored >= 0.
  - NO drop-worst fires: Drop1AtRound/Drop2AtRound unset (null), Drop3-5 = 99
    (= never), F3QDrop6to10 = '99,99,99,99,99', DropScoreOption unset/effective-0
    and moot - recorded explicitly rather than assumed (competition.json
    configProvenance effective-drop-conclusion; story WI-3 anchor "#45").
    Therefore Score (post-drop) is identical to RawScore (pre-drop total).

Ranking (`dtCompResults_FillPilotRankAndPcnt`, THE LADDER):
  - Score and RawScore re-rounded to GroupScoreDecimals (effective 1 dp, half-up)
    BEFORE comparison; sort Score DESC, RawScore DESC; class F5J has no rung 3,
    the ladder provably ends at rung 2; duplicate ranks display "=n" with the
    group leader marked retroactively; Percent is display-only, never recorded.
  - Because no drop fired and no penalty exists, rung 2 cannot separate what
    rung 1 did not: every Score tie displays a shared "=n" rank.

DOCUMENTED CURATION DECISIONS (WI-3 "decide at curation" mandate):
  1. Ties: displayed per GS "=n" semantics above; INTRA-TIE ORDERING of the
     ranks list is PilotNo ascending. GS's HiddenRanking inside a fully-tied
     group is implementation-defined (DataView sort stability unresolved in the
     arithmetic story), so a deterministic choice is made here and recorded in
     expected-result.json notes. Observed ties: one group, the two zero scorers.
  2. Unset drop thresholds are honoured as "never" (per the fixture's provenance
     note [9]), matching the story-wide finding that drop config is unset DB-wide.

G4 / float32 persist-cast discipline: GS persists RawScore/NormalisedScore through
Single-typed OleDbParameters (binary32). This fixture carries that witness. Per the
curated contract (expected-scores.json valuesAsPersisted), STORED VALUES ARE CLEAN
EXACT-1DP DOUBLES and binary32 residue appears only under cast SIMULATION
(99/162 scored NS values flip under pack/unpack('<f')). This script therefore
ASSERTS PERSISTED VALUES numerically (absolute difference <= 1e-9 after both sides
are placed on the comp's 1 dp grid) and NEVER compares raw repr bits, nor
recompute-and-compare widened binary32 forms. Recomputation is done in binary64 in
GS's operation order, which reproduces every persisted cell exactly (164 updated
rows scored; placeholders persist 0.0; two rule-driven cancelled flights score 0).

Standalone, stdlib only, deterministic. Run: python3 ladder.py
"""

import json
import math
import sys
from collections import defaultdict
from pathlib import Path

FIXTURE_DIR = Path(__file__).resolve().parent
TOL = 1e-9


def load(name):
    return json.loads((FIXTURE_DIR / name).read_text(encoding="utf-8"))


def get_time_in_seconds(packed):
    """Scoring_MOD.vb:626-631 GetTimeInSeconds, Fix() truncates toward zero."""
    minutes = math.trunc(packed / 100)
    return minutes * 60 + (packed - 100 * minutes)


def round_number(nbr, decs):
    """GlobalFunctions_MOD.vb:3116-3134 RoundNumber, VB Int floors toward -inf."""
    if decs == 0:
        return float(math.floor(nbr + 0.5))
    half = {1: 0.05, 2: 0.005, 3: 0.0005}[decs]
    scale = 10 ** decs
    return math.floor((nbr + half) * scale) / scale


def main():
    competition = load("competition.json")
    scores_raw = load("scores-raw.json")["rows"]
    expected_scores = load("expected-scores.json")
    expected_result = load("expected-result.json")

    dur = competition["familyRows"]["Dur"]
    knobs = competition["configProvenance"]["knobs"]
    pp_second = dur["DurPointsPerSecond"]
    target_time = float(dur["durTargetTime"])
    ref_height = dur["durRefHeight"]
    rate_up_to = dur["durPenaltyUpToRefHeight"]
    rate_over = dur["durPenaltyOverRefHeight"]
    scheme_no = dur["durLndg"]
    schemes = {
        s["LndgNo"]: {p["Distance"]: p["Points"] for p in s["points"]}
        for s in competition["lookups"]["landingSchemes"]
    }
    landing_table = schemes[scheme_no]
    gs_class = competition["identity"]["GSCompClass"]

    # Effective knobs (stored Comps fields are empty on this master; see
    # competition.json configProvenance.knobs for the arithmetic derivation).
    decimals = knobs["GroupScoreDecimals"]["effective"]          # 1
    dedn_idx = dur["durFlightPenalty"]                           # 3 = height penalty
    cap_over_target = dedn_idx == 3 and gs_class not in ("DurALES", "F3G")

    def height_penalty(h):
        if h <= ref_height:
            return h * rate_up_to
        return ref_height * rate_up_to + (h - ref_height) * rate_over

    def landing_bonus(distance):
        if not distance:
            return 0.0
        points = landing_table.get(distance, 0.0)  # exact-match miss silently scores 0
        return round_number(points + 0.005, decimals)

    def computed_raw(row):
        ts = get_time_in_seconds(row["Time1Mins"])
        if ts > target_time and cap_over_target:
            ts = target_time
        ts *= pp_second
        raw = ts + landing_bonus(row["Landing"]) - height_penalty(row["FlightScoreDeduction"])
        return max(raw, 0.0)

    key_of = lambda r: "{}/{}/{}/{}/{}".format(
        r["TaskNo"], r["RoundNo"], r["GroupNo"], r["ReFlightNo"], r["PilotNo"])

    # ---- Pass 1: RawScore recomputed and asserted against persisted values ----
    groups = defaultdict(list)
    raw_mismatches = []
    for row in scores_raw:
        groups[(row["TaskNo"], row["RoundNo"], row["GroupNo"], row["ReFlightNo"])].append(row)
        k = key_of(row)
        mine, persisted = computed_raw(row), expected_scores["scores"][k]["RawScore"]
        if abs(mine - persisted) > TOL:
            raw_mismatches.append((k, mine, persisted))
    if raw_mismatches:
        print(f"FAIL: recomputed RawScore disagrees with expected-scores.json "
              f"on {len(raw_mismatches)} row(s):", file=sys.stderr)
        for k, mine, persisted in raw_mismatches[:20]:
            print(f"  {k}: computed {mine!r} persisted {persisted!r}", file=sys.stderr)
        return 1

    # ---- Pass 2: Normalisation (points basis) recomputed and asserted ----
    ns_by_key = {}
    ns_mismatches = []
    for gkey in sorted(groups):
        members = sorted(groups[gkey], key=key_of)
        group_max = max(computed_raw(r) for r in members)  # explicit scan, :283-289
        for row in members:
            k = key_of(row)
            if group_max <= 0:
                ns = 0.0                                   # zero-max guard :312-315
            else:
                ns = round_number(1000 * computed_raw(row) / group_max, decimals)
                if ns < 0:
                    ns = 0.0                               # floor :310
            ns_by_key[k] = ns
            persisted = expected_scores["scores"][k]["NormalisedScore"]
            if abs(ns - persisted) > TOL:
                ns_mismatches.append((k, ns, persisted))
    if ns_mismatches:
        print(f"FAIL: recomputed NormalisedScore disagrees with expected-scores.json "
              f"on {len(ns_mismatches)} cell(s):", file=sys.stderr)
        for k, mine, persisted in ns_mismatches[:20]:
            print(f"  {k}: computed {mine!r} persisted {persisted!r}", file=sys.stderr)
        return 1

    # ---- Pass 3: aggregation over the scored window, no drops ----
    last_updated_round = max(r["RoundNo"] for r in scores_raw if r["Updated"] == "True")
    pilots = sorted({r["PilotNo"] for r in scores_raw})
    members = {p["PilotNo"] for p in load("entries.json")["compPilots"]["rows"]}
    if set(pilots) != members:
        print("FAIL: scores-raw pilots differ from entries members", file=sys.stderr)
        return 1

    cell_ns = {}       # (round, pilot) -> list of candidate NS (re-flight dedup inputs)
    cell_penalty = {}  # (round, pilot) -> sum |Penalty|
    for row in scores_raw:
        ck = (row["OriginalRoundNo"], row["PilotNo"])
        cell_ns.setdefault(ck, []).append(ns_by_key[key_of(row)])
        cell_penalty[ck] = cell_penalty.get(ck, 0) + abs(row["Penalty"])

    totals = {}  # pilot -> (score, raw_score); both pre-round doubles
    for pilot in pilots:
        score = 0.0
        for rnd in range(1, last_updated_round + 1):
            candidates = cell_ns.get((rnd, pilot))
            if candidates:
                score += max(candidates)          # keep-highest re-flight de-dup
            score -= cell_penalty.get((rnd, pilot), 0)
        score = max(score, 0.0)                   # floor pre-drop :2712
        totals[pilot] = (score, score)            # no drops => Raw == Score

    # ---- Pass 4: THE LADDER (report-time re-round, Score DESC RawScore DESC) ----
    ladder = sorted(
        pilots,
        key=lambda p: (-round_number(totals[p][0], decimals),
                       -round_number(totals[p][1], decimals),
                       p),                            # documented intra-tie order
    )
    rounded = [(p, round_number(totals[p][0], decimals)) for p in ladder]
    ranks_computed = []
    i = 0
    while i < len(rounded):
        j = i
        while j + 1 < len(rounded) and rounded[j + 1][1] == rounded[i][1]:
            j += 1
        display = f"={i + 1}" if j > i else str(i + 1)
        for pos in range(i, j + 1):
            ranks_computed.append({"pilotNo": rounded[pos][0], "rank": display})
        i = j + 1

    # ---- Verify the committed oracle ----
    if expected_result.get("source") != "reconstructed-ladder":
        print(f"FAIL: expected-result.json source is "
              f"{expected_result.get('source')!r}, want 'reconstructed-ladder'",
              file=sys.stderr)
        return 1
    if expected_result.get("ranks") != ranks_computed:
        print("FAIL: expected-result.json ranks diverge from the reconstructed ladder",
              file=sys.stderr)
        for a, b in zip(expected_result.get("ranks", []), ranks_computed):
            if a != b:
                print(f"  first divergence: oracle {a} vs computed {b}", file=sys.stderr)
                break
        return 1

    # ---- Report ----
    updated_per_pilot = defaultdict(int)
    for row in scores_raw:
        if row["Updated"] == "True":
            updated_per_pilot[row["PilotNo"]] += 1
    by_pilot = {r["pilotNo"]: (r["rank"], round_number(totals[r["pilotNo"]][0], decimals))
                for r in ranks_computed}
    ties = sum(1 for r in ranks_computed if r["rank"].startswith("="))

    print(f"f5j-christchurch-2019 (CompNo 45, F5J) - reconstructed Overall Results ladder")
    print(f"window: rounds 1-{last_updated_round} scored (mid-comp snapshot; R12-R18 unflown, "
          f"excluded) | {len(scores_raw)} rows, {sum(updated_per_pilot.values())} updated")
    print(f"drops: NONE fire (D1/D2 unset, D3-D5=never, F3QDrop6to10 all 99) => Score == Raw")
    print(f"per-cell agreement vs expected-scores.json: RawScore {len(scores_raw)}/{len(scores_raw)}, "
          f"NormalisedScore {len(scores_raw)}/{len(scores_raw)} exact (asserted)")
    print(f"oracle check: expected-result.json == reconstruction ({len(ranks_computed)}/{len(ranks_computed)} rows, "
          f"{ties} pilots carry a '=n' shared rank)")
    print()
    print(f"{'Pos':>3} {'Rank':>4} {'Pilot':>5} {'Score':>10} {'Cells':>5}")
    for pos, (pilot, (display, score)) in enumerate(by_pilot.items(), 1):
        print(f"{pos:>3} {display:>4} {pilot:>5} {score:>10.1f} {updated_per_pilot[pilot]:>5}")
    print()
    top3 = [f"P{r['pilotNo']} {by_pilot[r['pilotNo']][1]:.1f}" for r in ranks_computed[:3]]
    print("top-3:", ", ".join(top3))
    print("ORACLE VERIFIED: ladder.py reconstruction agrees with committed expected-result.json")
    return 0


if __name__ == "__main__":
    sys.exit(main())
