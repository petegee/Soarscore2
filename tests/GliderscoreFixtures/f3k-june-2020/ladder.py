#!/usr/bin/env python3
"""Derivation of record for f3k-june-2020/expected-result.json (WI-3 ranking oracle).

Reconstructs GliderScore's Overall Results ladder for CompNo=54 "2020 June F3K"
from the fixture's own persisted values:

  - per-cell ground truth is consumed AS PERSISTED from expected-scores.json
    (RawScore / NormalisedScore by key TaskNo/RoundNo/GroupNo/ReFlightNo/PilotNo);
    slot columns are never read and RawScore is never re-derived, so the five
    round-4 H-task decode deviants flow through untouched (flagged, not fixed);
  - round cells aggregate per ORIGINAL round keeping each pilot's highest
    NormalisedScore (GS report rollup, arithmetic story section 7,
    Rpt_Results_Overall_MOD.vb:2533-2556) - which is what excludes the
    round-7 cancelled zeros / superseded placeholder and admits each affected
    pilot once from his replacement G4 cell;
  - no drops fire (DropScoreOption=0 with unset thresholds) so Score == RawScore;
  - both keys re-rounded to the effective grid (Decs=1, per competition.json
    scoringCaveats) then ranked Score DESC, RawScore DESC (THE LADDER rungs 1-2);
    the F3K dropped-score rescue chain (rung 3) is provably inert with no drops.

Python 3 stdlib only. Running this file regenerates and validates
expected-result.json beside it; any violated expectation stops with a report.
"""

import json
import math
import sys
from collections import Counter
from pathlib import Path

FIXTURE = Path(__file__).resolve().parent

KEY_FORMAT = "{TaskNo}/{RoundNo}/{GroupNo}/{ReFlightNo}/{PilotNo}"
DECS = 1  # effective GroupScoreDecimals per competition.json scoringCaveats


def fail(message):
    sys.exit(f"STOP AND REPORT - ladder.py: {message}")


def check(condition, message):
    if not condition:
        fail(message)


def round_number(nbr, decs):
    """GladerScore GlobalFunctions_MOD.vb:3116-3134 - Int(Nbr*Scale + 0.5)/Scale."""
    scale = float(10 ** decs)
    return math.floor(nbr * scale + 0.5) / scale


def load(name):
    return json.loads((FIXTURE / name).read_text(encoding="utf-8"))


def main():
    comp = load("competition.json")
    entries = load("entries.json")
    raw = load("scores-raw.json")["rows"]
    expected = load("expected-scores.json")["scores"]

    scoring = comp["scoring"]
    pilots = sorted(r["PilotNo"] for r in entries["compPilots"]["rows"])
    check(len(pilots) == len(set(pilots)), "duplicate PilotNo in entries.json")
    keys = lambda r: KEY_FORMAT.format(
        TaskNo=r["TaskNo"], RoundNo=r["RoundNo"], GroupNo=r["GroupNo"],
        ReFlightNo=r["ReFlightNo"], PilotNo=r["PilotNo"])

    # --- consistency of the three score surfaces -----------------------------
    check(len(raw) == len(expected) == 199,
          f"expected 199 rows/cells, got {len(raw)}/{len(expected)}")
    for row in raw:
        k = keys(row)
        if k not in expected:
            fail(f"scores-raw row {k} has no expected-scores entry")
        if row.get("Penalty", 0) != 0 or row.get("FlightScoreDeduction", 0.0) != 0.0 \
                or row.get("Landing", 0.0) != 0.0:
            fail(f"{k}: unexpected non-inert penalty surface")

    # Persisted cell values are taken ONLY from expected-scores.json (keyed off
    # scores-raw identities). Slot fields (Laps/Times/Landing/Deduction) are
    # deliberately never consulted: the five round-4 H-task decode deviants
    # (R4/G1 P89, R4/G1 P83, R4/G1 P140, R4/G2 P101, R4/G3 P78 per provenance)
    # contribute AS PERSISTED - there is no decode re-derivation anywhere in
    # this file to second-guess them.
    DEVIANT_KEYS = {
        "5/4/1/0/89", "5/4/1/0/83", "5/4/1/0/140", "5/4/2/0/101", "5/4/3/0/78",
    }
    check(DEVIANT_KEYS <= set(expected),
          "round-4 H-task decode-deviant keys missing from expected-scores.json")

    # --- normalisation invariant (recomputed here, not trusted) --------------
    groups = {}
    for row in raw:
        groups.setdefault((row["TaskNo"], row["RoundNo"], row["GroupNo"],
                           row["ReFlightNo"]), []).append(row | expected[keys(row)])
    check(len(groups) == 40, f"expected 40 groups, got {len(groups)}")
    ns_checked = anchors = 0
    for gkey, rows in sorted(groups.items()):
        gmax = max(r["RawScore"] for r in rows)
        for r in rows:
            want = round_number(r["RawScore"] / gmax * 1000.0, DECS)
            if abs(want - r["NormalisedScore"]) > 1e-9:
                fail(f"normalisation invariant broken at {gkey} P{r['PilotNo']}: "
                     f"recomputed {want} != persisted {r['NormalisedScore']}")
            ns_checked += 1
        if any(abs(r["NormalisedScore"] - 1000.0) < 1e-9 for r in rows):
            anchors += 1
    check(ns_checked == 199, f"normalisation checked {ns_checked}/199 rows")
    check(anchors == 40, f"anchor groups {anchors}/40")

    # --- zero-row inventory ---------------------------------------------------
    zeros = [r | expected[keys(r)] for r in raw if expected[keys(r)]["RawScore"] == 0.0]
    genuine = Counter(r["PilotNo"] for r in zeros if r["Updated"] == "True")
    placeholders = Counter(r["PilotNo"] for r in zeros if r["Updated"] == "False")
    check(sum(genuine.values()) == 16 and sum(placeholders.values()) == 11,
          f"zero inventory drifted: {dict(genuine)} / {dict(placeholders)}")
    check(dict(genuine) == {81: 6, 84: 6, 101: 1, 102: 1, 128: 2},
          f"genuine-zero pilots drifted: {dict(genuine)}")
    check(set(placeholders) == {84, 85, 90},
          f"placeholder pilots drifted: {dict(placeholders)}")
    check(all(r["NormalisedScore"] == 0.0 for r in zeros),
          "a RawScore=0 row carries non-zero NormalisedScore")

    # --- aggregation over original rounds ------------------------------------
    rounds = sorted({r["RoundNo"] for r in raw})
    by_orig = {}
    for row in raw:
        by_orig.setdefault((row["PilotNo"], row["OriginalRoundNo"]), []).append(row)
    for orig in rounds:
        seated = sorted(p for (p, o) in by_orig if o == orig)
        check(seated == pilots, f"round {orig} does not seat all 15 pilots: {seated}")

    def pick(rows):
        """Cell selection: live (Updated='True') rows win over placeholders;
        among equal standing, GS's own de-dup keeps the highest NormalisedScore."""
        live = [r for r in rows if r["Updated"] == "True"]
        pool = live or rows
        best = max(pool, key=lambda r: expected[keys(r)]["NormalisedScore"])
        top = [r for r in pool
               if abs(expected[keys(r)]["NormalisedScore"]
                      - expected[keys(best)]["NormalisedScore"]) < 1e-9]
        check(len(top) == 1,
              f"ambiguous cell for P{rows[0]['PilotNo']} OR{rows[0]['OriginalRoundNo']}")
        return best

    cells = {(p, o): expected[keys(pick(rows))]["NormalisedScore"]
             for (p, o), rows in sorted(by_orig.items())}

    # Cross-check against GS's flag-blind dedup (keep max NormalisedScore over
    # ALL candidate rows of the original round) - the domain reason the
    # cancelled zeros lose to their replacements without special-casing.
    generic = {(p, o): max(expected[keys(r)]["NormalisedScore"] for r in rows)
               for (p, o), rows in by_orig.items()}
    for k in sorted(cells):
        if abs(cells[k] - generic[k]) > 1e-9:
            fail(f"selection disagrees with GS keep-max dedup at {k}: "
                 f"{cells[k]} vs {generic[k]}")

    # Round 7 re-draw witness: exactly four multi-seat pilot-rounds exist in the
    # whole comp, all in round 7.
    multi = sorted((p, o) for (p, o), rows in by_orig.items() if len(rows) > 1)
    check(multi == [(85, 7), (101, 7), (102, 7), (128, 7)],
          f"multi-seat pilot-rounds drifted: {multi}")
    g4 = {(r["SeqNo"], r["PilotNo"]): r for r in raw
          if r["RoundNo"] == 7 and r["GroupNo"] == 4}
    check(sorted(s for s, _ in g4) == [1, 2, 3, 4],
          "replacement G4 seats drifted")
    REDRAW = {  # excluded superseded G3 seat -> included-once G4 cell (ns)
        101: (("GroupNo", 3), ("SeqNo", 2), ("Updated", "True"), 723.7),
        102: (("GroupNo", 3), ("SeqNo", 3), ("Updated", "True"), 276.3),
        128: (("GroupNo", 3), ("SeqNo", 4), ("Updated", "True"), 1000.0),
        85:  (("GroupNo", 3), ("SeqNo", 7), ("Updated", "False"), 157.9),
    }
    for pilot, ((_, g), (_, s), (_, upd), want_ns) in REDRAW.items():
        stale = [r for r in raw if r["RoundNo"] == 7 and r["OriginalRoundNo"] == 7
                 and r["ReFlightNo"] == 0 and r["GroupNo"] == g
                 and r["PilotNo"] == pilot and r["Updated"] == upd]
        if len(stale) != 1 or stale[0]["SeqNo"] != s \
                or expected[keys(stale[0])]["RawScore"] != 0.0:
            fail(f"round-7 superseded row for P{pilot} not found as documented")
        fresh = [r for r in g4.values() if r["PilotNo"] == pilot]
        if len(fresh) != 1 or abs(cells[(pilot, 7)] - want_ns) > 1e-9:
            fail(f"P{pilot} must enter once from his G4 cell at ns={want_ns}, "
                 f"got cells {[expected[keys(r)]['NormalisedScore'] for r in fresh]} "
                 f"/ aggregated {cells[(pilot, 7)]}")
    # Unaffected live G3 seats stand; empty drawn seats stay empty and inert.
    live_g3 = {r["PilotNo"]: expected[keys(r)]["NormalisedScore"]
               for r in raw if r["RoundNo"] == 7 and r["GroupNo"] == 3}
    for p, want in {78: 1000.0, 140: 181.8, 84: 636.4, 81: 909.1}.items():
        check(abs(live_g3[p] - want) < 1e-9,
              f"unaffected G3 seat P{p} drifted: {live_g3[p]} != {want}")
    for grp, seqs in {1: [1], 2: [2, 3]}.items():
        have = {r["SeqNo"] for r in raw if r["RoundNo"] == 7 and r["GroupNo"] == grp}
        check(not (set(seqs) & have),
              f"documented-empty R7/G{grp} seats {seqs} unexpectedly occupied")

    # --- drop configuration cannot fire --------------------------------------
    check(scoring["DropScoreOption"] == 0,
          "DropScoreOption drifted from 0")
    check(scoring["Drop1AtRound"] is None and scoring["Drop2AtRound"] is None,
          "Drop1/Drop2AtRound drifted from NULL")
    check(all(scoring[k] == 99 for k in ("Drop3AtRound", "Drop4AtRound",
                                         "Drop5AtRound")),
          "Drop3-5AtRound drifted from 99")
    check(scoring["F3QDrop6to10"].split(",") == ["99"] * 5,
          "F3QDrop6to10 drifted from all-99")
    # Unset thresholds => GS's DropScores activation flag stays False =>
    # dtCompResults_ApplyDropScores is never called (arithmetic story, drop-worst
    # section): Score stays identical to RawScore and no Drop1F3K..Drop5F3K
    # value exists, so THE LADDER rung 3 (F3K rescue chain) is provably inert -
    # verified independently below: no Score ties survive rung 2 at all.

    # --- totals, rounding, THE LADDER ----------------------------------------
    totals = {}
    for p in pilots:
        pre_round = sum(cells[(p, o)] for o in rounds)
        floored = max(pre_round, 0.0)  # floored pre-drop (:2712); unreachable here
        totals[p] = {"raw": round_number(floored, DECS),
                     "score": round_number(floored, DECS)}
    order = sorted(pilots, key=lambda p: (-totals[p]["score"], -totals[p]["raw"]))
    adjacent = min(b - a for a, b in zip(
        sorted({t["score"] for t in totals.values()}),
        sorted({t["score"] for t in totals.values()})[1:]))
    tied_groups = [c for c in Counter(t["score"] for t in totals.values()).values()
                   if c > 1]
    if tied_groups:
        fail(f"F3K rescue chain assumed inert but {len(tied_groups)} tie group(s)")
    check(adjacent >= 1.0,
          f"rank-deciding gap {adjacent} inside the 1dp grid")

    ranks = []
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and totals[order[j + 1]]["score"] == totals[order[i]]["score"]:
            j += 1
        shown = f"={i + 1}" if j > i else str(i + 1)
        ranks.extend({"pilotNo": order[k], "rank": shown} for k in range(i, j + 1))
        i = j + 1

    doc = {
        "source": "reconstructed-ladder",
        "notes": [
            "Oracle reconstructed per WI-3 (grow-corpus-nz-master-five-fixtures.md): no GliderScore "
            "report transcript exists for this comp, so ranks come from the documented ladder "
            "(resolve-gliderscore-scoring-arithmetic.md, THE LADDER), computed by ladder.py beside "
            "this file as the derivation of record; derivation was applied to persisted cell values "
            "only (expected-scores.json), so a mismatch here needs source-level triage before it "
            "can indict the Soarscore engine.",
            "Aggregation: single task (TaskNo=5 on all 199 rows; task identity lives in "
            "F3KTaskByRound, E(1)=task letter on schedule round 7), ReFlightNo=0 and "
            "OriginalRoundNo==RoundNo everywhere, so report cells key on original round per pilot "
            "(arithmetic story section 7). Every pilot holds at least one row in all 13 rounds "
            "(verified); the multi-row pilot-rounds are EXACTLY the four round-7 re-draw pairs.",
            "MID-COMP GROUP RE-DRAW, round 7 (the anchor property this oracle pins): three "
            "standing cancelled-zero rows R7/G3 seq2=P101, seq3=P102, seq4=P128 (Updated='True', "
            "RawScore=0, contributing nothing) and the superseded appended-seq7 placeholder P85 "
            "(Updated='False') are EXCLUDED from round-7 contributions; each affected pilot enters "
            "ONCE from his replacement G4 cell (keys 5/7/4/0/<PilotNo>): P101 723.7, P128 1000.0, "
            "P102 276.3, P85 157.9. The unaffected live G3 seats stand (P78 1000.0 anchor, P81 "
            "909.1, P84 636.4, P140 181.8); empty drawn seats R7/G1 seq1 and R7/G2 seq2-3 are "
            "inert. Exclusion needs no cancellation-flag heuristic: GS's own rollup de-duplication "
            "(keep the highest NormalisedScore per original round) discards the zeros/lower "
            "duplicates; ladder.py encodes both the explicit per-pilot map above and that generic "
            "rule, and asserts they agree on every cell of the comp.",
            "Zero-cell policy: genuine Updated='True' zeros {P81x6, P84x6, P101x1 (superseded R7 "
            "row), P102x1 (same), P128x2 (R7 superseded + a real flown zero R12/G2)} contribute 0 "
            "cells; Updated='False' placeholder rows (P84 x2, P85 x3, P90 x6) form 0-valued cells "
            "only where they are the pilot's sole row for that round (P84 R12/R13, P90 R9-R13) and "
            "are excluded wherever any live row exists. Inventory asserted 16 genuine / 11 "
            "placeholder against provenance.json.",
            "No drops fire - recorded explicitly: DropScoreOption=0 with Drop1/Drop2AtRound stored "
            "NULL and Drop3-5=F3QDrop6to10 all 99 means GS's DropScores activation flag never "
            "becomes true and dtCompResults_ApplyDropScores is never called; Score equals the "
            "pre-drop total. Consequently no Drop1F3K..Drop5F3K dropper exists and THE LADDER's "
            "F3K rescue chain (rung 3) is provably inert; independently, no Score ties survive "
            "rung 2 at all (smallest rank-deciding gap 9.8 between P140 and P89), so all ranks "
            "display plainly 1..15.",
            "Effective ranking grid Decs=1: GroupScoreOption/GroupScoreDecimals are stored NULL "
            "(see competition.json scoringCaveats); the observed effective grid pinned by the "
            "199/199 normalisation check is used, both ladder keys re-rounded half-up "
            "(Int(x*10+0.5)/10) before comparison. Percent is display-only and deliberately not "
            "recorded.",
            "Deviance discipline: the five round-4 H-task decode-deviant cells (R4/G1 P89, R4/G1 "
            "P83, R4/G1 P140, R4/G2 P101, R4/G3 P78) feed the ladder AS PERSISTED - ladder.py "
            "never reads slot columns nor re-derives RawScore, so there is no code path that could "
            "'repair' them (flagged, not fixed). The 199/199 normalisation invariant (NS == "
            "round(Raw/groupMax*1000, 1 dp), anchors 1000.0 in all 40 groups) was RECOMPUTED by "
            "this derivation, including those five cells' persisted values.",
        ],
        "ranks": ranks,
    }

    out = FIXTURE / "expected-result.json"
    out.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n",
                   encoding="utf-8")

    names = {r["PilotNo"]: f"{r['FirstName']} {r['LastName']}".strip()
             for r in entries["pilots"]["rows"]}
    print(f"f3k-june-2020 reconstructed ladder: {ns_checked}/199 NS invariant, "
          f"{anchors}/40 anchors, {len(ranks)} pilots ranked")
    print("excluded R7 rows: G3 s2 P101, s3 P102, s4 P128 (cancelled zeros) + "
          "s7 P85 (superseded placeholder); admitted once each from G4:")
    for p in (101, 128, 102, 85):
        print(f"  P{p:<4} cell 5/7/4/0/{p} -> {cells[(p, 7)]}")
    for entry in ranks[:3]:
        print(f"top-3: rank {entry['rank']} P{entry['pilotNo']} "
              f"{names[entry['pilotNo']]} score {totals[entry['pilotNo']]['score']}")
    print(f"wrote {out.name} (source=reconstructed-ladder)")


if __name__ == "__main__":
    main()
