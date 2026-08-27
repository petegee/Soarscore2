#!/usr/bin/env python3
"""Ranking-oracle derivation of record for fixture f5j-hawkes-bay-trials (CompNo 135).

Story: kanban/in-progress/grow-corpus-nz-master-five-fixtures.md WI-3 (ranking
oracle, default `reconstructed-ladder` per the hybrid decision recorded in
kanban/completed/gliderscore-golden-fixture-pipeline.md "Ranking oracle —
decided 2026-08-25 (hybrid)"). No GliderScore report transcript exists for this
comp, so the final ladder is RECONSTRUCTED here by the documented GS algorithm
(kanban/completed/resolve-gliderscore-scoring-arithmetic.md, section
"Ranking & tie-breaks", THE LADDER). A mismatch against the produced
expected-result.json therefore needs source-level triage before it can indict
the Soarscore engine.

How it works (deterministic, pure stdlib, runs beside the fixture):

1. Cell values are taken verbatim from expected-scores.json (the curated,
   independently-reverified persisted GliderScore NormalisedScore layer;
   integrity against scores-raw.json key space is asserted below).
2. Aggregation keys EVERY row to its ORIGINAL round cell via OriginalRoundNo
   alone (Rpt_Results_Overall_MOD.vb:2698-2706) — never the entry's own
   RoundNo/group. This fixture has exactly four re-flight cells, all PilotNo
   128, each flown inside a NEW group's normalisation basis in R5/R6 yet
   aggregating back into its ORIGINAL round (their originals were deleted on
   re-flight, so those slots contribute nothing):
       orig R1 -> flew R5/G2   orig R2 -> flew R5/G3
       orig R4 -> flew R6/G1   orig R3 -> flew R6/G3
   Note group-continuity does NOT hold: R2's vacated seat sat in G1 yet the
   re-flight went to R5/G3, and R3's sat in G2 yet went to R6/G3 — hence the
   keying discipline. Detection uses RoundNo != OriginalRoundNo because
   ReFlightNo is 0 throughout (a known detection trap).
3. Per pilot, Score = report-time-rounded (best-per-(original-round, task)
   NormalisedScore sum minus |penalties|, floored at 0); RawScore = the same
   sum pre-drop. Both compared as "Score DESC, RawScore DESC".
4. DROPS NOT FIREABLE — asserted and printed: competition.json records
   DropScoreOption=0 with Drop1AtRound/Drop2AtRound/Drop5AtRound NULL and
   Drop3AtRound/Drop4AtRound = 99 (GS "never" sentinels) plus
   F3QDrop6To10="99,.."; no configured threshold can be reached within the
   10 scored rounds, so no drop activates, Score == RawScore for every pilot,
   and any Score tie is automatically a full Score+RawScore tie (shared "=n").
5. Ties display standard-competition ranks: the whole equal group — leader
   included, retroactively — gets "=n" (HiddenRanking within ties is
   implementation-defined and not recorded). Class F5J ends the ladder at
   rung 2 (no F3K rescue chain). Percent is display-only, never recorded.

Outputs: prints the full ladder + assertion evidence, and (re)writes
expected-result.json {"source": "reconstructed-ladder", ..., "ranks":[...]}
in sibling shape. Byte-deterministic: rerunning reproduces the committed
oracle exactly (`python3 ladder.py --check` verifies without writing).

Usage: python3 ladder.py [--check]
"""

from __future__ import annotations

import argparse
import json
import sys
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path

HERE = Path(__file__).resolve().parent
SCORED_ROUNDS_MAX = 16  # draw extent upper bound; actual scored set derived from data


def die(msg: str) -> None:
    raise SystemExit(f"ladder.py: {msg}")


def load(name: str):
    return json.loads((HERE / name).read_text(encoding="utf-8"))


def cell_key(row) -> str:
    return "{}/{}/{}/{}/{}".format(
        row["TaskNo"], row["RoundNo"], row["GroupNo"], row["ReFlightNo"], row["PilotNo"]
    )


def dec(value) -> Decimal:
    return Decimal(str(value))


def gs_round(value: Decimal, decimals: int) -> Decimal:
    """GS report-time re-round: RoundNumber = Int(Nbr + 0.5*10^-d), half-up."""
    q = Decimal(1).scaleb(-decimals)
    return value.quantize(q, rounding=ROUND_HALF_UP)


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument(
        "--check",
        action="store_true",
        help="verify the committed expected-result.json matches this derivation instead of writing it",
    )
    args = parser.parse_args(argv)

    competition = load("competition.json")
    entries = load("entries.json")
    scores_raw = load("scores-raw.json")
    expected_scores = load("expected-scores.json")

    # --- configuration (effective knobs, stored-null faithful) -----------------
    scoring = competition["scoring"]
    knobs = competition["configProvenance"]["knobs"]
    decimals = knobs["GroupScoreDecimals"]["effective"]
    assert knobs["GroupScoreDecimals"]["stored"] is None
    assert knobs["GroupScoreOption"]["effective"] == 1  # points-based normalisation
    assert knobs["RoundOrTruncate"]["effective"] == 0  # rounded
    assert scoring["RoundOrTruncate"] is None and scoring["GroupScoreOption"] is None
    cls = competition["identity"]["GSCompClass"]
    assert cls == "F5J", f"ladder documented for F5J (rung-2 end); got {cls}"

    rows = scores_raw["rows"]
    score_map = expected_scores["scores"]

    # Cell-integrity precondition: every scores-raw row has exactly one
    # curated persisted value, and it sits on the effective <=1-dp grid.
    for row in rows:
        key = cell_key(row)
        if key not in score_map:
            die(f"scores-raw key {key} missing from expected-scores.json")
        for field in ("RawScore", "NormalisedScore"):
            d = dec(score_map[key][field])
            if d != d.quantize(Decimal(1).scaleb(-decimals)):
                die(f"cell {key} {field}={d} off the {decimals}-dp persist grid")

    # --- re-flight detection (OriginalRoundNo != RoundNo; ReFlightNo trap) -----
    reflight_values = {row["ReFlightNo"] for row in rows}
    assert reflight_values == {0}, "ReFlightNo is 0 on every row: detection must use OriginalRoundNo"
    reflows = [r for r in rows if r["OriginalRoundNo"] != r["RoundNo"]]
    assert {r["PilotNo"] for r in reflows} == {128}, reflows
    vacated = sorted({r["OriginalRoundNo"] for r in reflows})
    print("RE-FLIGHT CELLS (keyed to orig-round by OriginalRoundNo alone):")
    seen_flight_cells = set()
    for r in sorted(reflows, key=lambda x: x["OriginalRoundNo"]):
        key = cell_key(r)
        cell = score_map[key]
        print(
            "  orig R{} -> flew R{}/G{} : pilot {}  RawScore {} -> NormalisedScore {}".format(
                r["OriginalRoundNo"], r["RoundNo"], r["GroupNo"],
                r["PilotNo"], cell["RawScore"], cell["NormalisedScore"],
            )
        )
        seen_flight_cells.add((r["OriginalRoundNo"], r["PilotNo"]))
    assert len(seen_flight_cells) == len(reflows) == len(vacated), (
        "each re-flight must map to a distinct original round"
    )
    # Delete-on-reflight evidence: in the vacated rounds themselves, pilot 128
    # owns NO original cells for R1-R4 (he does keep normal originals R5-R16).
    p128_originals_in_vacated = [
        r for r in rows
        if r["PilotNo"] == 128 and r["OriginalRoundNo"] == r["RoundNo"]
        and r["RoundNo"] in vacated
    ]
    assert vacated == [1, 2, 3, 4] and not p128_originals_in_vacated, (
        "pilot 128 must hold no original R1-R4 slots (delete-on-reflight)"
    )

    # --- scored-round window ---------------------------------------------------
    updated_rounds = sorted({r["RoundNo"] for r in rows if r["Updated"] == "True"})
    n_scored = max(updated_rounds)
    placeholders_late = [r for r in rows if r["RoundNo"] > n_scored]
    assert all(r["Updated"] == "False" for r in placeholders_late)
    assert all(
        dec(score_map[cell_key(r)]["NormalisedScore"]) == 0 for r in placeholders_late
    ), "rounds above the scored window must be untouched placeholder zeros"
    print(f"SCORED ROUNDS: {updated_rounds[0]}-{n_scored} "
          f"(rounds {n_scored + 1}-{max(r['RoundNo'] for r in rows)} excluded: placeholder zeros)")

    # --- DROPS NOT FIREABLE ----------------------------------------------------
    drop_fields = ["Drop1AtRound", "Drop2AtRound", "Drop3AtRound",
                   "Drop4AtRound", "Drop5AtRound"]
    configured = [(f, scoring[f]) for f in drop_fields]
    extra = [int(x) for x in scoring.get("F3QDrop6to10", "").split(",") if x.strip()]
    reachable = []
    for name, threshold in configured:
        if threshold is None:
            continue  # never configured: no drop exists to fire
        if threshold <= n_scored:
            reachable.append((name, threshold))
    for i, threshold in enumerate(extra, start=6):
        if threshold <= n_scored:
            reachable.append((f"F3QDrop{i}", threshold))
    if reachable:
        die(f"DROPS FIRE under this data but the story asserts they cannot: {reachable}")
    print(
        "DROPS NOT FIREABLE (explicit): DropScoreOption={} literal; {} ; "
        "F3QDrop6to10={} — no configured or sentinel-free threshold <= {} scored "
        "rounds, so no drop activates: Score == RawScore for every pilot.".format(
            scoring["DropScoreOption"],
            ", ".join(f"{n}={'NULL(unset)' if t is None else t}" for n, t in configured),
            scoring.get("F3QDrop6to10"), n_scored,
        )
    )

    # --- aggregate into ORIGINAL-round cells ------------------------------------
    penalties_all_zero = all(r["Penalty"] == 0 for r in rows)
    assert penalties_all_zero, "this ladder assumes Penalty=0 (asserted against the data)"
    cells: dict[tuple[int, int, int], Decimal] = {}
    tasks = set()
    for r in rows:
        tasks.add(r["TaskNo"])
        slot = (r["PilotNo"], r["OriginalRoundNo"], r["TaskNo"])
        ns = dec(score_map[cell_key(r)]["NormalisedScore"])
        if r["Updated"] == "False":
            assert ns == 0, "placeholder rows carry a non-zero NormalisedScore"
        # best-per-cell (higher-is-better duration); degenerate here: one live row per cell
        prev = cells.get(slot)
        cells[slot] = ns if prev is None else max(prev, ns)
    assert tasks == {1}, f"single-task comp expected, saw tasks {sorted(tasks)}"
    p128_slots_multi = [k for k in cells if k[0] == 128 and k[1] in vacated]
    assert len(p128_slots_multi) == len(vacated)

    members = sorted({r["PilotNo"] for r in entries["compPilots"]["rows"]})
    results: dict[int, tuple[Decimal, Decimal]] = {}
    for pilot in members:
        raw_total = sum(
            (ns for (p, _, _), ns in cells.items() if p == pilot), Decimal(0)
        )
        penalty = sum(
            (Decimal(str(r["Penalty"])) for r in rows if r["PilotNo"] == pilot), Decimal(0)
        )
        raw = gs_round(raw_total - penalty, decimals)
        score = gs_round(max(Decimal(0), raw), decimals)  # no drops ever subtract
        results[pilot] = (score, raw)

    # --- THE LADDER: Score DESC, RawScore DESC; F5J ends at rung 2 --------------
    order = sorted(members, key=lambda p: (-results[p][0], -results[p][1], p))
    ranks = []
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and results[order[j + 1]] == results[order[i]]:
            j += 1
        display = f"={i + 1}" if j > i else str(i + 1)
        ranks.extend({"pilotNo": pn, "rank": display} for pn in order[i:j + 1])
        i = j + 1

    high = results[order[0]][0]
    print(f"\nLADDER ({len(members)} pilots, {n_scored} scored rounds, no drops; "
          "Score DESC, RawScore DESC; ties share '=n'):")
    for entry in ranks:
        score, raw = results[entry["pilotNo"]]
        pct = "" if high == 0 else f"{100 * score / high:.2f}%"
        print(f"  {entry['rank']:>4}  pilot {entry['pilotNo']:>4}  "
              f"Final Score {format(score, f'.{decimals}f'):>8}  Raw {format(raw, f'.{decimals}f'):>8}")

    p128 = results[128]
    p128_cells = {
        rnd: next(ns for (p, o, _), ns in cells.items() if p == 128 and o == rnd)
        for rnd in vacated
    }
    print(
        "\npilot 128 total {}: re-aggregated orig-round cells R1-R4 = {}; "
        "(normalisation happened inside the NEW groups R5/R6; the aggregate "
        "rides OriginalRoundNo).".format(p128[0], [str(p128_cells[r]) for r in vacated])
    )

    notes = [
        "No GliderScore report transcript exists for this comp, so ranks come from the documented ladder "
        "(arithmetic story, THE LADDER: Score DESC, RawScore DESC after report-time re-round to the "
        "effective GroupScoreDecimals=1); class F5J ends the ladder at rung 2, so tied pilots share the "
        "displayed rank '=n'. Derivation of record: ladder.py in this directory (python3, stdlib-only, "
        "byte-deterministic; rerunning regenerates this file exactly, --check verifies).",
        "Per-cell ground truth is the curated persisted-NormalisedScore layer (expected-scores.json, "
        "grid-checked); the per-cell values were already independently bit-verified by the curator "
        "(see provenance.json notes). What remains unverified against GliderScore itself is the "
        "report-level aggregation+ladder step.",
        "Aggregation keys every row to its ORIGINAL-round cell via OriginalRoundNo alone "
        "(Rpt_Results_Overall_MOD.vb:2698-2706): the four re-flights (all pilot 128) normalise inside "
        "their NEW groups yet land in orig-round cells orig R1->flew R5/G2, R2->R5/G3, R3->R6/G3, "
        "R4->R6/G1. Their originals were deleted on re-flight (pilot 128 has no R1-R4 original cells), "
        "so the vacated slots contribute nothing. Group continuity does NOT hold (R2's vacated seat was "
        "in G1 yet the re-flight went to R5/G3; R3's was in G2 yet went to R6/G3) — hence keying on "
        "OriginalRoundNo alone. Detection uses RoundNo != OriginalRoundNo because ReFlightNo=0 "
        "throughout.",
        "DROPS NOT FIREABLE, stated explicitly: DropScoreOption=0 with Drop1AtRound/Drop2AtRound/"
        "Drop5AtRound NULL (never configured), Drop3AtRound/Drop4AtRound=99 ('never' sentinels) and "
        "F3QDrop6to10='99,99,99,99,99'; no threshold can be reached within the 10 scored rounds, so no "
        "drop activates and Score == RawScore for every pilot (any Score tie is therefore a full "
        "Score+RawScore tie and genuinely shares a rank; rung 2 can never separate distinct raws).",
        "Rounds 11-16 are wholly-unflown placeholder zeros and are excluded; penalties are 0 on all 288 "
        "rows (asserted by ladder.py).",
        "Intra-tie ordering of '=n' groups in the ranks array is insignificant (it is pilotNo ascending "
        "here purely for determinism; HiddenRanking within ties is implementation-defined). Percent is "
        "display-only (100 x Score / winner Score) and deliberately not recorded. A mismatch against "
        "this oracle needs source-level triage before it can indict the Soarscore engine.",
    ]

    document = {
        "source": "reconstructed-ladder",
        "notes": notes,
        "ranks": ranks,
    }

    out_path = HERE / "expected-result.json"
    rendered = json.dumps(document, indent=2) + "\n"
    if args.check:
        current = out_path.read_text(encoding="utf-8") if out_path.is_file() else ""
        if current != rendered:
            die("--check: committed expected-result.json differs from this derivation")
        print("\n--check OK: committed expected-result.json matches this derivation byte-for-byte")
        return 0
    out_path.write_text(rendered, encoding="utf-8")
    print(f"\nwrote {out_path.relative_to(HERE.parent.parent.parent)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
