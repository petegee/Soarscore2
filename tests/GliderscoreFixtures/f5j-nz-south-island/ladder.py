#!/usr/bin/env python3
"""WI-3 ranking oracle for f5j-nz-south-island (CompNo 121, F5J).

Standalone derivation-of-record for expected-result.json. Recomputes every
score cell from the raw inputs and comp config, cross-checks them against the
persisted expected-scores.json, aggregates the documented GliderScore ladder
(arithmetic story, THE LADDER) and prints the resulting ranking.

    python3 ladder.py            # verify everything, print the ranking
    python3 ladder.py --write    # additionally emit expected-result.json

Any mismatch stops with a non-zero exit (pipeline mismatch policy: needs
source-level triage before it can indict the Soarscore engine).

Derived scoring basis (faithful-null knobs — GroupScoreOption,
GroupScoreDecimals, RoundOrTruncate persist null; derivation mirrors the
sibling curators of comps 45/135): duration points-normalisation
best-in-group -> 1000 over the WHOLE raw (time cap + height penalty +
additive scheme-11 landing), rounded half-up to 1 dp; report-time re-round
of the totals to 1 dp half-up before comparison. Evidence: provenance
records an analysis-grade exact match of that model on all 133
Updated='True' NormalisedScore values, with group-best rows hitting exactly
1000.0 in all 33 scored (round, group) pairs; this script re-proves both on
every run.
"""

import argparse
import json
import math
import sys
from collections import defaultdict
from fractions import Fraction
from pathlib import Path

HERE = Path(__file__).resolve().parent

SLUG = "f5j-nz-south-island"
CLAMP_KEY = "1/3/3/0/99"          # R3/G3 SeqNo 2 PilotNo 99, launch height 1000 m
CLAMP_RAW_EXPECTED = Fraction(-2026)
VACANT_SEAT_SEQNOS = [1, 2, 4, 5]  # R7/G1 realised draw: seat 3 absent, occupant unknowable
MOTOR_RESTART_ROW = (6, 1, 2, 131)
MOTOR_RESTART_PAIR_KEY = "1/6/1/0/131"
ZERO_NOFLIGHT_CELLS = ["1/1/2/0/79", "1/2/1/0/79", "1/5/2/0/99", MOTOR_RESTART_PAIR_KEY]
EXPECTED_ROWS = 208
EXPECTED_UPDATED = 133
EXPECTED_PLACEHOLDERS = 75
EXPECTED_SCORED_GROUPS = 33
EXPECTED_OFF_LADDER_LANDINGS = 29


def load(name):
    return json.loads((HERE / name).read_text(encoding="utf-8"))


def frac(value):
    """JSON number -> exact Fraction via its decimal string repr."""
    return Fraction(str(value))


def half_up(value, decimals=1):
    """GS RoundNumber semantics: Int(Nbr + 0.5*10^-d) — arithmetic half-up.

    VB Int() floors toward -infinity, which is exactly math.floor here; the
    clamp-before-scale path feeds it only non-negative values where floor and
    truncation agree.
    """
    scaled = value * (10 ** decimals)
    return Fraction(math.floor(scaled + Fraction(1, 2)), 10 ** decimals)


def decode_packed_mmss(packed):
    """Scoring_MOD.vb packed-mmss decode via Fix truncation (minutes = whole hundreds)."""
    t = frac(packed)
    minutes = math.floor(t / 100)
    seconds = t - minutes * 100
    return minutes * 60 + seconds


def height_penalty(height, ref_height, rate_up_to, rate_over):
    h = frac(height)
    if h <= ref_height:
        return rate_up_to * h
    return rate_up_to * ref_height + rate_over * (h - ref_height)


def problems(label, list_):
    if list_:
        for p in list_[:20]:
            print(f"LADDER FAIL [{label}] {p}", file=sys.stderr)
        raise SystemExit(f"{SLUG}: {label} failed with {len(list_)} problem(s)")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--write", action="store_true",
                        help="emit expected-result.json after all checks pass")
    args = parser.parse_args()

    comp = load("competition.json")
    entries = load("entries.json")
    scores_raw = load("scores-raw.json")
    expected_scores = load("expected-scores.json")
    rows = scores_raw["rows"]
    persisted = expected_scores["scores"]

    dur = comp["familyRows"]["Dur"]
    ref_height = frac(dur["durRefHeight"])
    rate_up_to = frac(dur["durPenaltyUpToRefHeight"])
    rate_over = frac(dur["durPenaltyOverRefHeight"])
    target_time = frac(dur["durTargetTime"])
    scheme = {str(p["Distance"]): p["Points"]
              for p in comp["lookups"]["landingSchemes"][0]["points"]}
    members = {r["PilotNo"] for r in entries["compPilots"]["rows"]}

    # ---- structure witnesses -------------------------------------------------
    problems("shape", [
        *[f"row count {len(rows)} != expected {EXPECTED_ROWS}"
          for _ in () if len(rows) != EXPECTED_ROWS],
        *[f"ReFlightNo={r['ReFlightNo']} non-zero at "
          f"{r['TaskNo']}/{r['RoundNo']}/{r['GroupNo']}/{r['SeqNo']}/P{r['PilotNo']}"
          for r in rows if r['ReFlightNo'] != 0],
        *[f"OriginalRoundNo {r['OriginalRoundNo']} != RoundNo {r['RoundNo']} at "
          f"{r['TaskNo']}/{r['RoundNo']}/{r['GroupNo']}/{r['SeqNo']}/P{r['PilotNo']}"
          for r in rows if r['OriginalRoundNo'] != r['RoundNo']],
    ])
    drawn_rounds = sorted({r["RoundNo"] for r in rows})
    problems("draw-shape", [
        *[f"TaskNo={r['TaskNo']} not 1 on {r['RoundNo']}/G{r['GroupNo']}"
          for r in rows if r["TaskNo"] != 1],
        *[f"PilotNo {r['PilotNo']} not among entries members"
          for r in rows if r["PilotNo"] not in members],
    ])

    r7g1 = sorted(r["SeqNo"] for r in rows if r["RoundNo"] == 7 and r["GroupNo"] == 1)
    problems("vacant-seat", [] if r7g1 == VACANT_SEAT_SEQNOS else
             [f"R7/G1 SeqNos {r7g1} != expected vacant-seat set {VACANT_SEAT_SEQNOS}"])

    updated = [r for r in rows if r["Updated"] == "True"]
    placeholders = [r for r in rows if r["Updated"] != "True"]
    problems("updated-count", [] if len(updated) == EXPECTED_UPDATED else
             [f"{len(updated)} Updated='True' rows != {EXPECTED_UPDATED}"])
    problems("placeholder-count", [] if len(placeholders) == EXPECTED_PLACEHOLDERS else
             [f"{len(placeholders)} placeholder rows != {EXPECTED_PLACEHOLDERS}"])
    scored_content_rounds = sorted({r["RoundNo"] for r in updated})

    motor_rows = [r for r in rows if r["F5JMotorReStarted"]]
    problems("motor-restart", [
        *([] if len(motor_rows) == 1 else
          [f"expected exactly one F5JMotorReStarted row, found {len(motor_rows)}"]),
        *([] if [(r["RoundNo"], r["GroupNo"], r["SeqNo"], r["PilotNo"]) for r in motor_rows]
          == [MOTOR_RESTART_ROW] else
          ["motor-restart row identity mismatch"]),
        *([] if dur.get("F5JMotorRestartOption") == 1 else
          [f"F5JMotorRestartOption={dur.get('F5JMotorRestartOption')} != 1"]),
        *[f"motor-restart row carries score field "
          f"{name}={frac(r[name])} != 0"
          for name in ("Time1Mins", "Time1Secs", "FlightScoreDeduction", "Landing")
          for r in motor_rows if frac(r[name]) != 0],
    ])

    # Drop thresholds cannot fire: stored DropScoreOption=0 sits beside
    # unset-or-'99' (=never) sentinels everywhere, and only 11 of the 16 drawn
    # rounds carry any score content besides.
    scoring = comp["scoring"]
    thresholds = [scoring.get(f"Drop{i}AtRound") for i in range(1, 6)]
    f3q = [int(x) for x in str(scoring.get("F3QDrop6to10", "")).split(",")]
    problems("drops-cannot-fire", [
        *([] if scoring.get("DropScoreOption") == 0 else
          [f"DropScoreOption={scoring.get('DropScoreOption')} != 0"]),
        *[f"Drop{i}AtRound={v} must be null or 99 (=never)"
          for i, v in enumerate(thresholds, start=1)
          if v is not None and int(v) != 99],
        *[f"F3QDrop6to10 contains non-'99' entry {f3q}"
          for v in f3q if v != 99][:1],
        *([] if all(int(v) >= len(drawn_rounds) for v in thresholds if v is not None) else
          ["a threshold could fire within the drawn round count"]),
    ])

    # ---- per-flight recompute -----------------------------------------------
    def raw_score(row):
        time_s = min(decode_packed_mmss(row["Time1Mins"]), target_time)
        landing_pts = scheme.get(str(row["Landing"]), 0)  # off-table lands SILENTLY as 0
        return (time_s - height_penalty(row["FlightScoreDeduction"], ref_height,
                                        rate_up_to, rate_over) + landing_pts)

    groups = defaultdict(list)
    for r in updated:
        groups[(r["TaskNo"], r["RoundNo"], r["GroupNo"], r["ReFlightNo"])].append(r)

    computed_raw = {}
    computed_ns_clamp_first = {}
    computed_ns_clamp_last = {}
    guard_fired = []
    off_ladder = []
    for gkey, group_rows in groups.items():
        best = max(max(Fraction(0), raw_score(r)) for r in group_rows)
        if best <= 0:
            guard_fired.append(gkey)  # zero-max guard: whole group writes 0
        for r in group_rows:
            k = f"{r['TaskNo']}/{r['RoundNo']}/{r['GroupNo']}/{r['ReFlightNo']}/{r['PilotNo']}"
            raw = raw_score(r)
            computed_raw[k] = raw
            if best > 0:
                # Flooring encoded BEFORE scaling (documented GS 'floored <0->0'
                # inside normalisation). Variant B clamps only AFTER rounding;
                # the two are compared below to pin down whether this data can
                # even distinguish the locations.
                computed_ns_clamp_first[k] = (
                    half_up(Fraction(1000) * max(Fraction(0), raw) / best))
                computed_ns_clamp_last[k] = max(
                    Fraction(0), half_up(Fraction(1000) * raw / best))
            else:
                computed_ns_clamp_first[k] = Fraction(0)
                computed_ns_clamp_last[k] = Fraction(0)
            if str(r["Landing"]) not in scheme:
                off_ladder.append(k)

    problems("off-ladder-landing-count",
             [] if len(off_ladder) == EXPECTED_OFF_LADDER_LANDINGS else
             [f"{len(off_ladder)} updated rows carry off-table (silently-zero) landings, "
              f"expected {EXPECTED_OFF_LADDER_LANDINGS}: {off_ladder}"])

    flooring_distinctions = [k for k in computed_ns_clamp_first
                             if computed_ns_clamp_first[k] != computed_ns_clamp_last[k]]
    if flooring_distinctions:
        print(f"LADDER NOTE: clamp-before-scale vs clamp-after-round DIFFER on "
              f"{len(flooring_distinctions)} rows: {sorted(flooring_distinctions)[:5]}",
              file=sys.stderr)

    # ---- cells vs persisted ground truth (ALL 208 rows) ----------------------
    mismatches = []
    negative_kept = []
    for k, cell in persisted.items():
        if k not in computed_raw:  # placeholder row: persisted verbatim zeros
            e_raw, e_ns = frac(cell["RawScore"]), frac(cell["NormalisedScore"])
            if e_raw != 0 or e_ns != 0:
                mismatches.append(f"{k}: placeholder expected {(e_raw, e_ns)} not zeros")
            continue
        if frac(cell["RawScore"]) != computed_raw[k]:
            mismatches.append(
                f"{k}: recomputed RawScore {float(computed_raw[k])} != "
                f"persisted {cell['RawScore']}")
        if frac(cell["NormalisedScore"]) != computed_ns_clamp_first[k]:
            mismatches.append(
                f"{k}: recomputed NS {float(computed_ns_clamp_first[k])} != "
                f"persisted {cell['NormalisedScore']}")
    missing_persisted = sorted(set(computed_raw) - set(persisted))
    if missing_persisted:
        mismatches.append(f"computed rows absent from expected-scores: {missing_persisted}")
    problems("cells-vs-persisted", mismatches)

    # CLAMP WITNESS — two separate behaviours (mirrors provenance):
    # (a) RAW persists NEGATIVE unfloored;
    # (b) normalisation clamps it to 0.0, contributing 0.0 to its ladder cell.
    if persisted.get(CLAMP_KEY) is None:
        problems("clamp-witness", [f"expected-scores lacks the clamp row {CLAMP_KEY}"])
    else:
        negatives = [k for k, v in computed_raw.items() if v < 0]
        problems("clamp-witness", [
            *([] if frac(persisted[CLAMP_KEY]["RawScore"]) == CLAMP_RAW_EXPECTED else
              [f"persisted clamp RAW {persisted[CLAMP_KEY]['RawScore']} != "
               f"{float(CLAMP_RAW_EXPECTED)}"]),
            *([] if computed_raw[CLAMP_KEY] == CLAMP_RAW_EXPECTED else
              [f"recomputed clamp RAW {float(computed_raw[CLAMP_KEY])} != "
               f"{float(CLAMP_RAW_EXPECTED)}"]),
            *([] if computed_raw[CLAMP_KEY] < 0 else
              ["recomputed clamp RAW is not negative — no unfloored raw to assert"]),
            *([] if persisted[CLAMP_KEY]["NormalisedScore"] == 0.0 else
              [f"persisted clamp NS {persisted[CLAMP_KEY]['NormalisedScore']} != 0.0"]),
            *([] if computed_ns_clamp_first[CLAMP_KEY] == 0 else
              [f"recomputed clamp NS {float(computed_ns_clamp_first[CLAMP_KEY])} != 0"]),
            *([] if negatives == [CLAMP_KEY] else
              [f"negative raw rows {negatives} != exactly the one clamp row {CLAMP_KEY}"]),
        ])

    zero_updated_cells = [k for k, v in computed_raw.items() if v == 0]
    problems("zero-noflight-set", [] if sorted(zero_updated_cells) ==
             ZERO_NOFLIGHT_CELLS else
             [f"zero-valued updated rows {sorted(zero_updated_cells)} != the four "
              f"zero-time no-flights recorded in provenance"])

    if guard_fired:
        problems("zero-max-guard",
                 [f"zero-max normalisation guard unexpectedly fired for groups "
                  f"{guard_fired} (all 33 scored groups hold a positive best)"])

    gbest_exact = sum(1 for k in computed_ns_clamp_first
                      if computed_ns_clamp_first[k] == Fraction(1000))
    problems("group-best-1000", [] if gbest_exact == EXPECTED_SCORED_GROUPS else
             [f"{gbest_exact} rows at exactly 1000.0 != one-per-group over "
              f"{EXPECTED_SCORED_GROUPS} scored groups"])

    # ---- THE LADDER ----------------------------------------------------------
    # Cells keyed by OriginalRoundNo, dedup keeping the highest normalised score
    # (trivial here: no re-flights exist). Best-per-(orig-round, task) NS minus
    # penalty totals, floored at 0 pre-drop, re-rounded to GroupScoreDecimals
    # (=1 dp effective) before comparison. Placeholder rows contribute nothing.
    penalties = defaultdict(int)
    for r in updated:
        penalties[r["PilotNo"]] += abs(r["Penalty"])

    cells = {}
    per_pilot_cells = defaultdict(dict)
    for k, ns in computed_ns_clamp_first.items():
        task_no, rnd, grp, refl, pilot_no = (int(x) for x in k.split("/"))
        orig = next(r["OriginalRoundNo"] for r in updated
                    if r["TaskNo"] == task_no and r["RoundNo"] == rnd
                    and r["GroupNo"] == grp and r["ReFlightNo"] == refl
                    and r["PilotNo"] == pilot_no)
        slot = per_pilot_cells[pilot_no].setdefault(orig, Fraction(0))
        if ns > slot:
            per_pilot_cells[pilot_no][orig] = ns

    def total_for(pilot_no):
        base = sum(per_pilot_cells[pilot_no].values(), Fraction(0)) \
            - Fraction(penalties[pilot_no])
        return max(Fraction(0), base)  # floored at 0 pre-drop (Rpt :2712)

    totals = {p: half_up(total_for(p)) for p in members}
    # Re-rounding to 1 dp must be a no-op on the 1-dp NS grid.
    drift = [p for p in members if half_up(total_for(p)) != total_for(p)]
    if drift:
        print(f"LADDER NOTE: report re-round moved totals for {drift}", file=sys.stderr)

    ranked = sorted(members, key=lambda p: (-totals[p], -total_for(p)))
    ranks, position = [], 0
    while position < len(ranked):
        end = position
        while end < len(ranked) and totals[ranked[end]] == totals[ranked[position]]:
            end += 1
        text = str(position + 1) if end - position == 1 else f"={position + 1}"
        ranks.extend({"pilotNo": ranked[i], "rank": text}
                     for i in range(position, end))
        position = end

    print(f"LADDER RECONSTRUCTION {SLUG}")
    print(f"  drawn rounds: {len(drawn_rounds)} ({drawn_rounds[0]}..{drawn_rounds[-1]}); "
          f"scored-content rounds: {len(scored_content_rounds)} "
          f"({scored_content_rounds}); updated rows {len(updated)}; "
          f"placeholders {len(placeholders)} (all-zeros, excluded)")
    print(f"  recomputed cells: raw {len(computed_raw)}, normalised "
          f"{len(computed_ns_clamp_first)}; clamp-before-scale vs clamp-after-round "
          f"differences: {len(flooring_distinctions)}")
    for item in ranks:
        t = totals[item["pilotNo"]]
        mark = "" if "=" not in item["rank"] else "  (shared rank)"
        print(f"  {item['rank']:>4}  pilot {item['pilotNo']:<4} score/rawscore {float(t):>9}")

    if args.write:
        document = {
            "source": "reconstructed-ladder",
            "notes": build_notes(len(drawn_rounds), scored_content_rounds,
                                 flooring_distinctions),
            "ranks": ranks,
        }
        (HERE / "expected-result.json").write_text(
            json.dumps(document, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8")
        print(f"  wrote {HERE / 'expected-result.json'}")
    print("LADDER PASS")


def build_notes(n_drawn_rounds, scored_rounds, flooring_distinctions):
    flooring_note = (
        "Flooring location pinned empirically: clamp-before-scale and "
        "clamp-after-round agree on all 133 updated rows (the lone negative row "
        "is deeply negative), so this dataset cannot distinguish the two; the "
        "script encodes clamp-before-scale (documented GS floor '<0->0' inside "
        "normalisation) and asserts the equivalence on every run."
        if not flooring_distinctions else
        f"Flooring location observable: {len(flooring_distinctions)} rows differ "
        "between clamp-before-scale and clamp-after-round.")
    return [
        "No GliderScore report transcript exists for this comp, so ranks come "
        "from the documented ladder (arithmetic story, THE LADDER): sum of "
        "best-per-original-round NormalisedScore minus penalty totals, floored "
        "at 0 pre-drop, report-time re-round to 1 dp half-up, sort Score DESC "
        "then RawScore DESC; class F5J has no rescue chain, so the ladder ends "
        "at rung 2; tied pilots share the displayed rank '=n'. A mismatch "
        "against this oracle needs source-level triage before it can indict "
        "the Soarscore engine.",
        "Per-cell ground truth is strong and machine-checked: ladder.py "
        "(beside this file, derivation-of-record) recomputes raw AND "
        "normalised scores from raw inputs and proves them equal to the "
        f"persisted values on every run — 133 Updated='True' rows plus 75 "
        "all-zero placeholder rows (208 total) — before touching the ladder.",
        "Derived scoring basis (stored knobs faithful-null; derivation "
        "mirrors sibling curators of comps 45/135): duration points-basis "
        "normalisation best-in-group -> 1000 rounded half-up to 1 dp, applied "
        "to the WHOLE raw (min(packed-mmss time, 600 s) - two-rate height "
        "penalty 0.5/m <=200 m then 100+3.0/m above + ADDITIVE scheme-11 "
        "'F5J Enter Landing' lookup). RawScore in report terms is the PRE-DROP "
        "normalised total — unrelated to the per-flight Scores.RawScore column.",
        "Drops explicitly CANNOT fire: stored DropScoreOption=0 beside "
        "null-or-'99' thresholds throughout (Drop1/2/5 unset, Drop3/Drop4=99, "
        "F3QDrop6to10 all '99'), and only 11 of 16 drawn rounds carry score "
        "content anyway — so Score equals the pre-drop RawReportScore for "
        "every pilot.",
        "Clamp witness behaviours asserted separately: the extreme-height row "
        "R3/G3 s2 P99 (launch height 1000 m) keeps its computed raw PERSISTED "
        "UNFLOORED at exactly -2026.0 in the cell ground truth, while "
        "normalisation clamps it to 0.0 — its contribution to the final "
        f"ladder is 0.0 ({flooring_note})",
        "Aggregation scope honesty: 16 drawn rounds x 13 pilots = 208 rows; "
        "scored content lives on rounds 1-11 only (R12-R16 are wholly "
        "placeholder no-show rows, kept verbatim at zeros and excluded from "
        "the ladder). No re-flights exist anywhere (ReFlightNo=0 and "
        "OriginalRoundNo==RoundNo on all 208 rows), so orig-round cells are "
        "plain round numbers. The vacant seat R7/G1 SeqNos {1,2,4,5} stays "
        "uninvented — a draw-realised gap only; aggregations are unaffected. "
        "Off-ladder landings stay silent: 29 scored rows carry Landing=0.0, "
        "not on the scheme-11 table, contributing 0 without error (asserted "
        "count). Motor-restart row R6/G1 s2 P131 (F5JMotorRestartOption=1) is "
        "a zeroed no-flight contributing 0; its non-droppability is moot "
        "because no drop can fire.",
        "Intra-tie ordering of '=n' groups would be insignificant (identical "
        "displayed rank; HiddenRanking within ties is implementation-defined). "
        "Percent is display-only and deliberately not recorded.",
    ]


if __name__ == "__main__":
    main()
