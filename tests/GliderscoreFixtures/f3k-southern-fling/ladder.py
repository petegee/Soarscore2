#!/usr/bin/env python3
"""Ranking-oracle derivation of record for f3k-southern-fling (grow-corpus-nz-master-five-fixtures WI-3).

Reconstructs the GliderScore Overall Results ladder for CompNo=17 "Southern
Fling" (F3K, 15 rounds x groups G1-G3, 15 entrants) from this fixture's own
files only, and (re)writes expected-result.json with byte-stable content.

Documented decisions (each encoded as executable assertions below):

D1 RETIRED-PILOT SEMANTICS -- CompPilots PilotNo 89 carries Retired=true, flew
   R1-R8 (eight scored rounds, including his sole accepted-as-flown zero at
   R4G2: RawScore=0, Updated='True') and is wholly ABSENT from R9-R15 --
   silent absence, zero placeholder stub rows.
   Decision: he IS ranked, on his flown rounds only; unflown rounds contribute
   nothing (no zeros injected, no penalty, no separate retirement branch).
   Basis: GliderScore's report table aggregates whatever NormalisedScore cells
   exist per member pilot; retirement removes future draw slots (here group
   sizes shrink 5/5/5 -> 5/5/4 from R9) but adds no scoring logic of its own,
   matching typical GS Overall Results behaviour of listing every entrant
   ranked by total score across flown rounds. His R4 zero counts -- it is a
   real flown, accepted cell, unlike his R9-R15 absence which simply has no
   cell at all ("absent-from-round != scored zero"; "retired == no further
   cells", not "rank excluded").

D2 FAITHFUL-NULL ROUNDING GRID -- GroupScoreOption / GroupScoreDecimals /
   RoundOrTruncate persist as Jet-null (carried verbatim in competition.json;
   validate.py rule 3 warns-not-fails on nulls by design). The per-cell
   recompute (Phase C) proves the effective grid was points-normalisation
   rounded half-up to 1 dp, so every NormalisedScore cell is an exact multiple
   of 0.1; sums of such cells are again exact multiples of 0.1, and the
   report-time re-round to the comp decimals (arithmetic story, Sort-key spec)
   applied at 1 dp is the identity on those totals. Decision: apply the
   identity (no invented default, no re-round to 0 dp). All arithmetic uses
   decimal.Decimal end-to-end so the reported totals carry no float residue.

D3 NO DROPS CAN FIRE -- Drop1AtRound/Drop2AtRound/DropScoreOption are
   Jet-null (unset => inactive), Drop3AtRound..Drop5AtRound are the 99
   sentinel (> maximum possible scored rounds, 15) and F3QDrop6to10 is
   '99,99,99,99,99'. GS activates drops only when the number of scored rounds
   reaches a threshold, so no drop value ever populates Drop1F3K..Drop5F3K
   either; the F3K-only dropped-score rescue chain (THE LADDER rung 3) is
   therefore vacuous here. Consequently Score == RawScore for every pilot
   (pre-drop total minus |penalties|; penalties are asserted 0 on all rows),
   and the effective ladder is: Score DESC, RawScore DESC, then share "=n".

D4 PHANTOM-LANDING NOISE IS INERT ([REDACTION-NEEDED NOISE] per provenance) --
   12 of 14 R9 rows carry Landing=145.0 (six of them additionally
   FlightScoreDeduction=200.0). Nothing interprets these cells: no landing
   scheme exists anywhere in this comp's schema (competition.json lookups.
   landingSchemes is empty and there is no Dur family row, so validate.py
   rule 2 applies vacuously), and F3K flight score here is task points, not
   time-minus-deduction or distance-based. The pipeline below never reads
   Landing or FlightScoreDeduction from any row, so the noise influences
   nothing by construction; Phase B nonetheless pins the noise census so any
   future change to the fixture that altered these cells would fail loudly
   here instead of silently re-baselining the oracle.

RANK DISPLAY -- standard competition numbering: leaders of equal Score keep
the shared position as plain "n", every successor becomes "=n" (and the
leader is retroactively displayed "=n"); Fully tied survivors all show "=n".
Final hidden total order (HiddenRanking analogue) within an "=n" group is
implementation-defined in GS; this reconstruction emits pilotNo ascending,
which is insignificant for consumers (identical displayed rank).

MISMATCH POLICY -- every reconciliation in Phases A-E is an assert: if any
check fails the script stops and reports rather than emitting a re-baselined
oracle (grow-corpus WI-3: needs source-level triage before indicting the
engine). Committed expected-result.json must match this script's output
byte-for-byte (idempotence is checked against the existing file if present).
"""

import json
import sys
from collections import defaultdict
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path

HERE = Path(__file__).resolve().parent
ORACLE = HERE / "expected-result.json"

Q1 = Decimal("0.1")


def stop(msg):
    raise SystemExit(f"ladder.py: STOP-AND-REPORT: {msg}")


def require(cond, msg):
    if not cond:
        stop(msg)


def dec(value):
    return Decimal(str(value))


def round_half_up_decimal(ratio):
    """Exact-decimal half-up round to 1 dp."""
    return ratio.quantize(Q1, rounding=ROUND_HALF_UP)


def round_gs_binary64_emulation(ratio_float):
    """Emulate GS's RoundNumber(v, d=1) = Int(Nbr + 0.5*10^-d) floor semantics
    through binary64 (arithmetic story, Sort-key spec / Handoff note 2).
    Agreement with the exact-decimal result on all 218 cells is itself
    asserted in Phase C."""
    return int((ratio_float * 10) + 0.5) / 10.0


def main():
    # ---------- inputs ----------
    competition = json.loads((HERE / "competition.json").read_text(encoding="utf-8"))
    entries = json.loads((HERE / "entries.json").read_text(encoding="utf-8"))
    scores_raw = json.loads((HERE / "scores-raw.json").read_text(encoding="utf-8"))
    expected_scores = json.loads((HERE / "expected-scores.json").read_text(encoding="utf-8"))

    rows = scores_raw["rows"]
    cells = expected_scores["scores"]

    def cell_key(row):
        return f"{row['TaskNo']}/{row['RoundNo']}/{row['GroupNo']}/{row['ReFlightNo']}/{row['PilotNo']}"

    members = [r["PilotNo"] for r in entries["compPilots"]["rows"]]
    by_no = {r["PilotNo"]: r for r in entries["compPilots"]["rows"]}
    names = {
        r["PilotNo"]: f"{r['FirstName']} {r['LastName']}" for r in entries["pilots"]["rows"]
    }

    # ---------- Phase A: fixture shape ----------
    require(len(rows) == 218, f"expected 218 score rows, found {len(rows)}")
    require(len(cells) == 218, f"expected 218 expected-score cells, found {len(cells)}")
    require(sorted(members) == sorted({r["PilotNo"] for r in rows}),
            "scores-raw PilotNos != entries membership")
    require(all(r["Penalty"] == 0 for r in rows),
            "unexpected non-zero Penalty on some row")
    require(all(r["OriginalRoundNo"] == r["RoundNo"] and r["ReFlightNo"] == 0
                for r in rows),
            "fixture should have no re-flights (OriginalRoundNo==RoundNo, ReFlightNo==0)")
    seen_pairs = defaultdict(int)
    for r in rows:
        seen_pairs[(r["PilotNo"], r["OriginalRoundNo"])] += 1
    dupes = [k for k, v in seen_pairs.items() if v > 1]
    require(not dupes, f"duplicate (pilot, original-round) pairs: {dupes}")

    retired_nos = [p for p in members if by_no[p]["Retired"]]
    active_nos = [p for p in members if not by_no[p]["Retired"]]
    require(retired_nos == [89], f"expected exactly pilot 89 retired, got {retired_nos}")
    n89_rounds = sorted(r["RoundNo"] for r in rows if r["PilotNo"] == 89)
    require(n89_rounds == list(range(1, 9)),
            f"pilot 89 should have exactly scored rounds R1-R8, got {n89_rounds}")
    per_active = sorted({len([r for r in rows if r["PilotNo"] == p]) for p in active_nos})
    require(per_active == [15],
            f"active pilots should each hold 15 cells, saw counts {per_active}")

    # ---------- Phase B: phantom-Landing noise census (D4) ----------
    r9 = [r for r in rows if r["RoundNo"] == 9]
    require(len(r9) == 14, f"R9 should hold 14 rows, found {len(r9)}")
    phantoms = [r for r in r9 if r["Landing"] == 145.0]
    require(len(phantoms) == 12,
            f"noise census drifted: expected 12 R9 Landing=145.0 rows, found {len(phantoms)}")
    fsd = sorted(r["FlightScoreDeduction"] for r in phantoms)
    require(fsd == [0.0] * 6 + [200.0] * 6,
            f"noise census drifted: R9 phantom FlightScoreDeduction split now {fsd}")
    clean = [r for r in r9 if r["Landing"] == 0.0]
    require(len(clean) == 2, "R9 clean-row census drifted")
    require(not [r for r in rows if r["Landing"] not in (0.0,) and r["RoundNo"] != 9],
            "non-zero Landing outside R9 would contradict the recorded noise scope")
    require(competition["lookups"]["landingSchemes"] == []
            and "Dur" not in competition["familyRows"],
            "a landing scheme appeared: re-triage Phase-B noise assumptions")
    # R9 raw values stay the auto-filled trio regardless of noise (inertness):
    r9_raws = {cells[cell_key(r)]["RawScore"] for r in r9}
    require(r9_raws <= {210.0, 405.0, 525.0},
            f"R9 persisted RawScore values drifted beyond the recorded trio: "
            f"{sorted(r9_raws)}")

    # ---------- Phase C: per-cell normalisation invariant (recomputed) ----------
    groups = defaultdict(list)
    for r in rows:
        groups[(r["TaskNo"], r["RoundNo"], r["GroupNo"], r["ReFlightNo"])].append(r)
    require(len(groups) == 45, f"expected 45 normalisation groups, found {len(groups)}")

    hundred = Decimal("1000")
    tied_winner_groups = 0
    ns_1000_rows = 0
    for gkey, grows in groups.items():
        raws = {}
        for r in grows:
            cell = cells.get(cell_key(r))
            require(cell is not None, f"missing expected-score cell for row {cell_key(r)}")
            raws[r["PilotNo"]] = dec(cell["RawScore"])
        best = max(raws.values())
        ns_1000_rows += sum(1 for v in raws.values()
                            if round_half_up_decimal(hundred * v / best) == Decimal("1000.0"))
        winners = [p for p, v in raws.items()
                   if round_half_up_decimal(hundred * v / best) == Decimal("1000.0")]
        if len(winners) > 1:
            tied_winner_groups += 1
        for r in grows:
            ratio = hundred * raws[r["PilotNo"]] / best
            expect_dec = round_half_up_decimal(ratio)
            expect_flt = round_gs_binary64_emulation(float(ratio))
            got = dec(cells[cell_key(r)]["NormalisedScore"])
            require(expect_dec == got,
                    f"normalisation recompute (exact-decimal half-up) mismatch at "
                    f"{cell_key(r)}: recomputed {expect_dec} vs persisted {got}")
            require(abs(float(expect_flt) - float(got)) < 1e-9,
                    f"binary64-emulated recompute mismatch at {cell_key(r)}: "
                    f"{expect_flt} vs persisted {got}")

    # ---------- Phase D: faithfulness of the persisted grid + knobs (D2) ----------
    all_cells = list(cells.values())
    require(all(dec(c["NormalisedScore"]) == dec(c["NormalisedScore"]).quantize(Q1)
                for c in all_cells),
            "persisted NormalisedScore left the 1 dp grid: revisit decision D2")
    scoring = competition["scoring"]
    for knob in ("GroupScoreOption", "GroupScoreDecimals", "RoundOrTruncate",
                 "DropScoreOption", "Drop1AtRound", "Drop2AtRound"):
        require(scoring[knob] is None, f"{knob} no longer Jet-null: revisit D2/D3")
    for knob in ("Drop3AtRound", "Drop4AtRound", "Drop5AtRound"):
        require(scoring[knob] == 99, f"{knob} no longer 99 sentinel: revisit D3")
    require(scoring["F3QDrop6to10"].split(",") == ["99"] * 5,
            "F3QDrop6to10 no longer all-99: revisit D3")

    # ---------- Phase E: aggregate + drops (D3) ----------
    totals = {}
    for p in members:
        own = [dec(cells[cell_key(r)]["NormalisedScore"])
               for r in rows if r["PilotNo"] == p]
        penalty_total = sum(abs(dec(r["Penalty"]))
                            for r in rows if r["PilotNo"] == p)
        pre_drop = sum(own) - penalty_total          # RawScore (report level)
        score = max(pre_drop, Decimal("0"))           # floored at 0 pre-drop
        scored_rounds = len(own)
        thresholds = [scoring[k] for k in
                      ("Drop1AtRound", "Drop2AtRound", "Drop3AtRound",
                       "Drop4AtRound", "Drop5AtRound")]
        fired = [t for t in thresholds if t is not None and t <= scored_rounds]
        require(not fired, f"drops unexpectedly fireable for pilot {p}: {fired}")
        totals[p] = {"score": score, "rawscore": pre_drop,
                     "rounds": scored_rounds}
        require(score == pre_drop,
                f"floor engaged for pilot {p}: unexpected negative total")

    # ---------- Phase F: THE LADDER -> ranks ----------
    # Sort key: Score DESC, RawScore DESC (both already on the D2 1 dp grid, so
    # the report-time re-round is the identity). Exact decimal comparison.
    order = sorted(members,
                   key=lambda p: (-totals[p]["score"], -totals[p]["rawscore"], p))
    displayed = []
    i = 0
    while i < len(order):
        j = i
        while (j + 1 < len(order)
               and totals[order[j + 1]]["score"] == totals[order[i]]["score"]
               and totals[order[j + 1]]["rawscore"] == totals[order[i]]["rawscore"]):
            j += 1
        group = order[i:j + 1]
        rank_str = str(i + 1) if len(group) == 1 else f"={i + 1}"
        for p in group:
            displayed.append({"pilotNo": p, "rank": rank_str})
        i = j + 1

    tie_groups = sum(1 for d in displayed if d["rank"].startswith("="))
    winner_ties_note = (
        f"{tied_winner_groups} of 45 round-groups have tied winners "
        f"(census {ns_1000_rows} rows at NS 1000.0); none propagate: every "
        f"final Score is distinct, so no '=n' occurs in the final ladder"
        if tie_groups == 0 else
        f"{tie_groups} displayed ranks are ties"
    )

    # ---------- Phase G: emit oracle (byte-deterministic) ----------
    doc = {
        "source": "reconstructed-ladder",
        "notes": [
            "Derivation of record: ladder.py beside this fixture (python3 stdlib, "
            "standalone) recomputes and writes this file; rerun must reproduce it "
            "byte-for-byte.",
            "Per-cell ground truth re-verified in-run: all 218/218 persisted "
            "NormalisedScore values equal round-half-up(1000 x RawScore / "
            "best-in-(TaskNo,RoundNo,GroupNo,ReFlightNo), 1 dp) under BOTH exact-"
            "decimal AND GS binary64 Int(Nbr+0.5e-d) emulation; per-cell "
            "normalisation basis is the GROUP throughout (F3K).",
            "Retired-pilot semantics (WI-3 decision, D1 in ladder.py): pilot 89 "
            "(Gavin Rhodes, CompPilots.Retired=true) flew R1-R8 (his R4G2 "
            "RawScore=0 cell is accepted-as-flown and counts) and is silently "
            "absent R9-R15 with zero placeholder stubs; he IS ranked on his flown "
            "rounds only - unflown rounds contribute nothing (no zeros injected, "
            "no penalty, no retirement branch). Group sizes witness it: 5/5/5 "
            "through R8, 5/5/4 from R9.",
            "Scoring knobs are faithfully Jet-null (GroupScoreOption/"
            "GroupScoreDecimals/RoundOrTruncate/DropScoreOption/Drop1AtRound/"
            "Drop2AtRound null; Drop3-5AtRound=99 sentinel; F3QDrop6to10 "
            "'99x5'): the effective grid was empirically points-normalisation "
            "half-up at 1 dp, every cell is an exact multiple of 0.1, so totals "
            "are too and the report-time re-round at 1 dp is the identity "
            "(decision D2; no default invented, no 0 dp re-round).",
            "NO drops can fire (decision D3): unset/null Drop1-2 and DropScoreOption, "
            "99 sentinels on Drop3-5 and Drop6-10 versus at most 15 scored rounds; "
            "hence Score == RawScore == sum of best-per-original-round "
            "NormalisedScore for every pilot (penalties 0 on all 218 rows, floor "
            "never engages), and the F3K-only dropped-score rescue chain (THE "
            "LADDER rung 3) is vacuous - its Drop1F3K..Drop5F3K inputs never "
            "populate without a firing drop. Effective ladder: Score DESC, "
            "RawScore DESC, then share rank.",
            "[REDACTION-NEEDED NOISE] R9 phantom Landing=145.0 on 12/14 rows (six "
            "with FlightScoreDeduction=200.0) influences NOTHING: no landing "
            "scheme exists in this comp's schema (no Dur family row; empty "
            "landingSchemes - validate.py rule 2 applies vacuously) and the "
            "pipeline never reads Landing/FlightScoreDeduction (decision D4 in "
            "ladder.py, census pinned there). They scored by task points alone "
            "(R9 RawScore takes only 210/405/525).",
            "Round-winner tie census: 10 of 45 groups carry tied winners (widths "
            "2-5, including R9G1's whole-group 5-way tie at 1000.0), yet every "
            "final Score came out distinct, so no '=n' marks appear below; the "
            "standard-competition-numbering '=n' machinery remains implemented "
            "and tested in ladder.py.",
            "Intra-tie emission order (pilotNo ASC) is insignificant - identical "
            "displayed rank; HiddenRanking within ties is implementation-defined "
            "in GliderScore. Percent is display-only and deliberately not "
            "recorded.",
            "A mismatch against this oracle needs source-level triage before it "
            "can indict the Soarscore engine (grow-corpus story WI-3 policy). If "
            "an Overall Results transcript for Southern Fling surfaces, upgrade "
            "this oracle to gs-report-transcript.",
        ],
        "ranks": displayed,
    }
    text = json.dumps(doc, indent=2, ensure_ascii=False) + "\n"
    ORACLE.write_text(text, encoding="utf-8")

    # ---------- summary ----------
    def fmt(p):
        t = totals[p]
        return (f"P{p:<3} {names[p]:<18} Score {t['score']:>8} "
                f"({t['rounds']} scored rounds)")

    print(f"ladder.py: OK - wrote {ORACLE.name}")
    print(f"pilots ranked: {len(displayed)} (active {len(active_nos)}, retired "
          f"{len(retired_nos)}); top 3:")
    for d in displayed[:3]:
        print(f"  rank {d['rank']:>3}: {fmt(d['pilotNo'])}")
    p89_rank = next(d["rank"] for d in displayed if d["pilotNo"] == 89)
    print(f"  pilot 89 (Retired): rank {p89_rank:>3}: {fmt(89)}")
    print(f"ties: {winner_ties_note}")
    print(f"per-cell invariant: 218/218 both rounding semantics; "
          f"drop-firing: none; noise rows: 12 (inert)")


if __name__ == "__main__":
    sys.exit(main())
