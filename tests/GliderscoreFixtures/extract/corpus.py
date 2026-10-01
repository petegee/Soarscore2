#!/usr/bin/env python3
"""Regenerate the corpus coverage summaries from their authoritative inputs.

GS 04 Step 4 — the corpus index's oracle-coverage table and the
fixture-to-seed coverage view are GENERATED, never hand-edited. Every number
in them is measured at regen time from the file that owns it:

- registry (tests/GliderscoreFixtures/corpus-registry.json): manifest order,
  status, modes/seeds, comparison grains;
- scores-raw.json: archive rows + drawn rounds;
- expected-scores.json: compared-cell keys;
- expected-result.json: oracle source/lifecycle/window/excludedRounds plus
  the totals/preDropTotals/penalties/discards headers (counts and shapes
  only — expected VALUES stay in the oracle files);
- divergences.json: ledgered exclusions by grain;
- <slug>/parallel-run/*.json: seed-witness ledgers (triaged entries and
  structured cells, excluded oracle cells, scored-window assertions);
- tools/Soarscore.SeedData/json/*.json: the seed universe for coverage.

Usage:
    python3 corpus.py regen [--corpus DIR] [--seeds DIR]
    python3 corpus.py regen --check [...]
    python3 corpus.py --self-test

`regen` rewrites the marked sections of index.md and parallel-run-mapping.md
in place (hand prose outside the markers is untouched). `--check` recomputes
and exits 1 on any drift, printing a unified diff — the CI-ready gate (GS 05
wires it in). Stdlib-only, offline, deterministic: registry order for
fixtures, ordinal sort for seeds, no timestamps, UTF-8, trailing newline.

Offline contract: like extract.py/validate.py this is developer-run tooling.
Nothing in src/, test builds or CI imports it; only its output (the marked
sections) is consumed by readers.
"""

import argparse
import difflib
import io
import json
import sys
import tempfile
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

EXTRACT_DIR = Path(__file__).resolve().parent
DEFAULT_CORPUS_DIR = EXTRACT_DIR.parent
DEFAULT_SEED_DIR = EXTRACT_DIR.parent.parent.parent / "tools" / "Soarscore.SeedData" / "json"

REGISTRY_FILE = "corpus-registry.json"
INDEX_FILE = "index.md"
MAPPING_FILE = "parallel-run-mapping.md"

ORACLE_BLOCK = "oracle-coverage"
SEED_BLOCK = "seed-coverage"

BEGIN = "<!-- corpus-generated:begin:{name} -->"
END = "<!-- corpus-generated:end:{name} -->"

# Oracle sources that pin expectations against an external record versus a
# recomputation. The classification lives here (a reading of the source
# vocabulary), the per-fixture source value lives in expected-result.json.
EXTERNAL_SOURCES = {"gs-report-transcript", "server-persisted-progressive"}


def fail(message):
    raise SystemExit(f"corpus.py: {message}")


def load_json(path):
    try:
        return json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError, UnicodeDecodeError) as exc:
        fail(f"unreadable JSON: {path} ({exc})")


def registry_entries(corpus_dir):
    document = load_json(corpus_dir / REGISTRY_FILE)
    if not isinstance(document, dict) or not isinstance(document.get("entries"), list):
        fail(f"{REGISTRY_FILE} must hold an object with an entries array")
    return document["entries"]


def drawn_rounds(rows):
    rounds = sorted({row.get("RoundNo") for row in rows if isinstance(row, dict)})
    return [round_no for round_no in rounds if isinstance(round_no, int)]


def measure_fixture(corpus_dir, entry):
    """Measure one registry entry against the files that own each number."""
    slug = entry.get("slug")
    status = entry.get("status")
    modes = entry.get("modes") or []
    grains = ((entry.get("comparison") or {}).get("grains")) or []
    measured = {
        "slug": slug,
        "status": status,
        "modes": modes,
        "grains": list(grains),
    }
    fixture = corpus_dir / slug
    if status == "skipped" or not fixture.is_dir():
        return measured
    scores_raw = load_json(fixture / "scores-raw.json")
    rows = scores_raw.get("rows") or []
    expected_scores = load_json(fixture / "expected-scores.json")
    keys = expected_scores.get("scores") or {}
    oracle = load_json(fixture / "expected-result.json")
    window = oracle.get("scoredWindow") or {}
    first, last = window.get("firstRound"), window.get("lastRound")
    excluded = oracle.get("excludedRounds") or []
    key_rounds = {}
    for key in keys:
        try:
            key_rounds[key] = int(str(key).split("/")[1])
        except (IndexError, ValueError):
            continue
    compared = sum(1 for round_no in key_rounds.values() if first <= round_no <= last)
    excluded_cells = sum(
        1 for row in rows
        if isinstance(row, dict) and row.get("RoundNo") in set(excluded)
    )
    discards = oracle.get("discards") or []
    units = sorted({discard.get("unit") for discard in discards if discard.get("unit")})
    dropped = [discard for discard in discards if discard.get("droppedRounds")]
    penalties = [
        (penalty.get("pilotNo"), penalty.get("deduction"))
        for penalty in (oracle.get("penalties") or [])
        if penalty.get("deduction")
    ]
    divergences_path = fixture / "divergences.json"
    ledger = load_json(divergences_path) if divergences_path.is_file() else []
    ledger_grains = {}
    for item in ledger:
        grain = item.get("grain") if isinstance(item, dict) else None
        ledger_grains[grain] = ledger_grains.get(grain, 0) + 1
    teams = (fixture / "expected-teams.json").is_file()
    measured.update({
        "source": oracle.get("source"),
        "lifecycle": oracle.get("lifecycle"),
        "window": (first, last),
        "drawn": drawn_rounds(rows),
        "archiveRows": len(rows),
        "scoreKeys": len(keys),
        "comparedCells": compared,
        "excludedRounds": sorted(excluded),
        "excludedCells": excluded_cells,
        "discardUnits": units,
        "maxDrops": max([len(discard.get("droppedRounds") or []) for discard in discards] or [0]),
        "pilotsWithDrops": len(dropped),
        "rankedPilots": len(discards),
        "penalties": sorted(penalties),
        "ledgerEntries": len(ledger),
        "ledgerGrains": ledger_grains,
        "hasTeams": teams,
    })
    return measured


def measure_pair_ledger(corpus_dir, slug, seed):
    """Measure one registry parallel-run mode against its landed ledger."""
    ledger_path = corpus_dir / slug / "parallel-run" / f"{seed}.json"
    if not ledger_path.is_file():
        return {"landed": False}
    ledger = load_json(ledger_path)
    entries = ledger.get("triagedDifferences") or []
    cells = 0
    grains = {}
    kinds = {}
    for item in entries:
        count = len(item.get("cells") or []) if isinstance(item, dict) else 0
        cells += count
        grain = item.get("grain") if isinstance(item, dict) else None
        grains[grain] = grains.get(grain, 0) + count
        kind = item.get("triageKind") if isinstance(item, dict) else None
        kinds[kind] = kinds.get(kind, 0) + 1
    provenance = ledger.get("provenance") or {}
    return {
        "landed": True,
        "entries": len(entries),
        "cells": cells,
        "grainCells": grains,
        "kinds": kinds,
        "excludedCells": len(provenance.get("excludedOracleCells") or []),
        "scoredWindow": provenance.get("scoredWindowRounds"),
    }


def oracle_class(source):
    if source in EXTERNAL_SOURCES:
        return "external"
    return "reconstructed"


def render_window(measured):
    first, last = measured["window"]
    drawn = measured["drawn"]
    window = f"R{first}–R{last}"
    if drawn and (drawn[0] != first or drawn[-1] != last):
        window += f" of drawn R{drawn[0]}–R{drawn[-1]}"
    return f"{window}, {measured['lifecycle']}"


def render_exclusions(measured, pair_excluded):
    parts = []
    if measured["excludedRounds"]:
        cells = measured["excludedCells"]
        parts.append(
            f"excludedRounds={measured['excludedRounds']} "
            f"({cells} {'cell' if cells == 1 else 'cells'} uncompared)"
        )
    if measured["ledgerEntries"]:
        grains = ", ".join(
            f"{count} {grain}" for grain, count in sorted(measured["ledgerGrains"].items())
        )
        parts.append(f"ledger {measured['ledgerEntries']} ({grains})")
    if pair_excluded:
        parts.append(
            f"parallel-run excluded {pair_excluded} "
            f"{'cell' if pair_excluded == 1 else 'cells'}"
        )
    return "; ".join(parts) if parts else "none"


def render_drops(measured):
    units = "/".join(measured["discardUnits"]) if measured["discardUnits"] else "—"
    if not measured["maxDrops"]:
        return f"0 ({units}, proven empty)"
    return (
        f"{measured['maxDrops']} ({units}, "
        f"{measured['pilotsWithDrops']}/{measured['rankedPilots']} pilots)"
    )


def render_penalties(measured):
    if not measured["penalties"]:
        return "0"
    return ", ".join(f"pilot {pilot}:−{value}" for pilot, value in measured["penalties"])


def render_witnesses(measured, ledgers):
    parallel = [mode for mode in measured["modes"] if mode.get("mode") == "parallel-run"]
    if not parallel:
        return "parity only"
    parts = []
    for mode in parallel:
        seed = mode.get("seed")
        ledger = ledgers.get((measured["slug"], seed)) or {}
        if not ledger.get("landed"):
            parts.append(f"{seed}: no ledger")
            continue
        detail = f"{seed}: ledger {ledger['entries']} entries/{ledger['cells']} cells"
        if ledger.get("scoredWindow"):
            detail += f" (scored window R1–R{ledger['scoredWindow']})"
        if ledger.get("excludedCells"):
            detail += f", {ledger['excludedCells']} excluded"
        parts.append(detail)
    return "; ".join(parts)


def oracle_table(entries, measured_by_slug, ledgers):
    header = (
        "| fixture | oracle | window | archive rows | compared cells | "
        "exclusions | drops (unit) | penalties | seed witnesses |"
    )
    rule = "|---|---|---|---|---|---|---|---|---|"
    lines = [header, rule]
    for entry in entries:
        slug = entry.get("slug")
        if entry.get("status") == "skipped":
            lines.append(
                f"| {slug} | none (skipped) | — | n/a (no directory) | n/a | "
                f"n/a | n/a | n/a | skipped |"
            )
            continue
        measured = measured_by_slug[slug]
        source = measured["source"]
        lines.append(
            f"| {slug} | {source} ({oracle_class(source)}) | "
            f"{render_window(measured)} | {measured['archiveRows']} | "
            f"{measured['comparedCells']} | "
            f"{render_exclusions(measured, sum(ledgers.get((slug, m.get('seed')), {}).get('excludedCells', 0) for m in measured['modes'] if m.get('mode') == 'parallel-run'))} | "
            f"{render_drops(measured)} | {render_penalties(measured)} | "
            f"{render_witnesses(measured, ledgers)} |"
        )
    return "\n".join(lines)


def oracle_totals(entries, measured_by_slug, ledgers):
    active = [e for e in entries if e.get("status") == "active"]
    skipped = [e for e in entries if e.get("status") != "active"]
    archive = sum(measured_by_slug[e["slug"]]["archiveRows"] for e in active)
    compared = sum(measured_by_slug[e["slug"]]["comparedCells"] for e in active)
    excluded = sum(measured_by_slug[e["slug"]]["excludedCells"] for e in active)
    ledgered = sum(measured_by_slug[e["slug"]]["ledgerEntries"] for e in active)
    external = sum(
        1 for e in active
        if measured_by_slug[e["slug"]]["source"] in EXTERNAL_SOURCES
    )
    teams = sum(1 for e in active if measured_by_slug[e["slug"]]["hasTeams"])
    pairs = [
        (e["slug"], m.get("seed"))
        for e in active for m in (e.get("modes") or [])
        if m.get("mode") == "parallel-run"
    ]
    landed = sum(1 for pair in pairs if ledgers.get(pair, {}).get("landed"))
    witness_cells = sum(ledgers.get(pair, {}).get("cells", 0) for pair in pairs)
    return (
        f"Corpus: {len(entries)} competitions ({len(active)} active, "
        f"{len(skipped)} skipped); {archive} archive rows; {compared} compared "
        f"cells; {excluded} excluded-round cells uncompared; {ledgered} ledgered "
        f"divergence entries; {len(pairs)} seed-witness pairs "
        f"({landed} ledgers landed, {witness_cells} structured witness cells); "
        f"oracles {external} external / {len(active) - external} reconstructed"
        f"{f'; team ladders {teams}' if teams else ''}."
    )


def render_oracle_block(entries, measured_by_slug, ledgers):
    return (
        oracle_table(entries, measured_by_slug, ledgers)
        + "\n\n"
        + oracle_totals(entries, measured_by_slug, ledgers)
    )


def seed_universe(seed_dir):
    if not seed_dir.is_dir():
        fail(f"seed class directory missing: {seed_dir}")
    return sorted(
        path.name for path in seed_dir.glob("*.json") if path.is_file()
    )


def render_modes_block(entries, ledgers):
    lines = [
        "| fixture | status | parity | parallel-run seed | parallel ledger | comparison grains |",
        "|---|---|---|---|---|---|",
    ]
    for entry in entries:
        slug = entry.get("slug")
        status = entry.get("status")
        modes = entry.get("modes") or []
        parity = "yes" if any(m.get("mode") == "parity" for m in modes) else "—"
        seeds = [m.get("seed") for m in modes if m.get("mode") == "parallel-run"]
        if not seeds:
            lines.append(
                f"| {slug} | {status} | {parity} | — | — | "
                f"{', '.join((entry.get('comparison') or {}).get('grains') or [])} |"
            )
            continue
        for seed in seeds:
            ledger = ledgers.get((slug, seed)) or {}
            landed = (
                f"landed ({ledger['entries']} entries, {ledger['cells']} cells)"
                if ledger.get("landed") else "no ledger"
            )
            lines.append(
                f"| {slug} | {status} | {parity} | {seed} | {landed} | "
                f"{', '.join((entry.get('comparison') or {}).get('grains') or [])} |"
            )
    return "\n".join(lines)


def render_seed_block(entries, seed_dir, ledgers):
    pairs_by_seed = {}
    for entry in entries:
        for mode in entry.get("modes") or []:
            if mode.get("mode") == "parallel-run" and mode.get("seed"):
                pairs_by_seed.setdefault(mode["seed"], []).append(entry["slug"])
    lines = [
        "| seed | registry pairs | ledgers landed | coverage |",
        "|---|---|---|---|",
    ]
    covered = paired = uncovered = 0
    for seed in seed_universe(seed_dir):
        stem = seed[:-len(".json")] if seed.endswith(".json") else seed
        fixtures = pairs_by_seed.get(stem, [])
        landed = sum(1 for slug in fixtures if ledgers.get((slug, stem), {}).get("landed"))
        if fixtures and landed == len(fixtures):
            coverage = f"witnessed ({landed} {'ledger' if landed == 1 else 'ledgers'})"
            covered += 1
        elif fixtures:
            coverage = f"paired ({landed}/{len(fixtures)} ledgers)"
            paired += 1
        else:
            coverage = "uncovered (no registry pair)"
            uncovered += 1
        lines.append(
            f"| {seed} | {', '.join(fixtures) if fixtures else '—'} | "
            f"{landed}/{len(fixtures)} | {coverage} |"
        )
    lines.append("")
    lines.append(
        f"Seeds: {covered + paired + uncovered} files "
        f"({covered} witnessed, {paired} paired without a full ledger set, "
        f"{uncovered} uncovered)."
    )
    return "\n".join(lines)


def render_seed_coverage(entries, seed_dir, ledgers):
    return (
        "Executable modes (from the registry; ledgers measured at regen time):"
        "\n\n" + render_modes_block(entries, ledgers)
        + "\n\nSeed coverage (every seed file; pairs from the registry):"
        "\n\n" + render_seed_block(entries, seed_dir, ledgers)
    )


def splice(path, block_name, body):
    text = Path(path).read_text(encoding="utf-8")
    start, end = BEGIN.format(name=block_name), END.format(name=block_name)
    if text.count(start) != 1 or text.count(end) != 1:
        fail(f"{path} must carry exactly one {start} ... {end} pair")
    before, rest = text.split(start)
    _, after = rest.split(end)
    return f"{before}{start}\n{body}\n{end}{after}"


def generated_texts(corpus_dir, seed_dir):
    entries = registry_entries(corpus_dir)
    measured_by_slug = {}
    for entry in entries:
        slug = entry.get("slug")
        if not isinstance(slug, str) or not slug:
            fail(f"{REGISTRY_FILE} holds an entry with no slug")
        if entry.get("status") == "active":
            measured_by_slug[slug] = measure_fixture(corpus_dir, entry)
    ledgers = {}
    for entry in entries:
        for mode in entry.get("modes") or []:
            if mode.get("mode") == "parallel-run" and mode.get("seed"):
                ledgers[(entry["slug"], mode["seed"])] = measure_pair_ledger(
                    corpus_dir, entry["slug"], mode["seed"]
                )
    index_text = splice(
        corpus_dir / INDEX_FILE, ORACLE_BLOCK,
        render_oracle_block(entries, measured_by_slug, ledgers),
    )
    mapping_text = splice(
        corpus_dir / MAPPING_FILE, SEED_BLOCK,
        render_seed_coverage(entries, seed_dir, ledgers),
    )
    return {corpus_dir / INDEX_FILE: index_text, corpus_dir / MAPPING_FILE: mapping_text}


def regen(corpus_dir, seed_dir, check):
    wanted = generated_texts(corpus_dir, seed_dir)
    drifted = []
    for path, text in wanted.items():
        current = path.read_text(encoding="utf-8")
        if current != text:
            drifted.append(path)
    if check:
        if not drifted:
            print("corpus.py: coverage summaries fresh (index.md, parallel-run-mapping.md)")
            return 0
        for path in drifted:
            current = path.read_text(encoding="utf-8").splitlines()
            updated = wanted[path].splitlines()
            diff = "\n".join(difflib.unified_diff(
                current, updated, fromfile=f"a/{path.name}", tofile=f"b/{path.name}",
            ))
            print(diff)
        print(
            f"corpus.py: drift in {', '.join(p.name for p in drifted)} — "
            f"run `python3 extract/corpus.py regen` to refresh",
            file=sys.stderr,
        )
        return 1
    for path, text in wanted.items():
        path.write_text(text, encoding="utf-8")
    print("corpus.py: regenerated coverage summaries (index.md, parallel-run-mapping.md)")
    return 0


# ------------------------------------------------------------------ self-test

def write_json(path, document):
    Path(path).write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")


def build_mini_tree(root):
    corpus = root / "corpus"
    seeds = root / "seeds"
    corpus.mkdir(parents=True)
    seeds.mkdir(parents=True)
    registry = {
        "schemaVersion": 1,
        "entries": [
            {
                "slug": "mini-active",
                "status": "active",
                "modes": [{"mode": "parity"}, {"mode": "parallel-run", "seed": "mini-seed"}],
                "sourceRef": "self-test",
                "oracleRef": "self-test",
                "comparison": {"grains": ["raw"]},
                "evidenceLinks": [],
                "replay": {},
            },
            {
                "slug": "mini-skipped",
                "status": "skipped",
                "modes": [{"mode": "parity"}],
                "sourceRef": "self-test",
                "oracleRef": "none (self-test skip)",
                "comparison": {"grains": []},
                "evidenceLinks": [],
                "replay": {},
            },
        ],
    }
    write_json(corpus / REGISTRY_FILE, registry)
    fixture = corpus / "mini-active"
    (fixture / "parallel-run").mkdir(parents=True)
    rows = [
        {"CompNo": 1, "TaskNo": 1, "RoundNo": r, "GroupNo": 1,
         "ReFlightNo": 0, "PilotNo": p, "SeqNo": p}
        for r in (1, 2) for p in (1, 2)
    ]
    write_json(fixture / "scores-raw.json", {"schema": {}, "rows": rows})
    write_json(fixture / "expected-scores.json", {
        "keyFormat": "x",
        "scores": {f"1/{r['RoundNo']}/1/0/{r['PilotNo']}": {} for r in rows},
    })
    write_json(fixture / "expected-result.json", {
        "source": "reconstructed-ladder",
        "lifecycle": "finalised-full",
        "scoredWindow": {"firstRound": 1, "lastRound": 2},
        "excludedRounds": [],
        "totals": [{"pilotNo": 1, "total": 5}, {"pilotNo": 2, "total": 6}],
        "preDropTotals": [{"pilotNo": 1, "total": 5}, {"pilotNo": 2, "total": 6}],
        "penalties": [{"pilotNo": 1, "deduction": 0}, {"pilotNo": 2, "deduction": 0}],
        "discards": [
            {"pilotNo": 1, "unit": "round", "droppedRounds": [], "droppedValues": []},
            {"pilotNo": 2, "unit": "round", "droppedRounds": [], "droppedValues": []},
        ],
        "fieldAvailability": [],
    })
    write_json(fixture / "divergences.json", [
        {"grain": "raw", "round": 1, "group": 1, "pilotNo": 1, "reason": "D5: self-test"},
    ])
    write_json(fixture / "parallel-run" / "mini-seed.json", {
        "pair": {"fixture": "mini-active", "seed": "mini-seed"},
        "provenance": {},
        "triagedDifferences": [
            {"triageKind": 1, "grain": "raw", "cells": [
                {"round": 1, "group": 1, "pilotNo": 1},
                {"round": 1, "group": 1, "pilotNo": 2},
            ]},
        ],
    })
    write_json(seeds / "mini-seed.json", {"name": "mini-seed"})
    write_json(seeds / "other-seed.json", {"name": "other-seed"})
    (corpus / INDEX_FILE).write_text(
        "# self-test\n\n## Complete-result oracle coverage\n\n"
        f"{BEGIN.format(name=ORACLE_BLOCK)}\nplaceholder\n{END.format(name=ORACLE_BLOCK)}\n",
        encoding="utf-8",
    )
    (corpus / MAPPING_FILE).write_text(
        "# self-test\n\n"
        f"{BEGIN.format(name=SEED_BLOCK)}\nplaceholder\n{END.format(name=SEED_BLOCK)}\n",
        encoding="utf-8",
    )
    return corpus, seeds


def self_test():
    cases = []

    def check(name, condition, detail=""):
        cases.append((name, bool(condition), detail))

    with tempfile.TemporaryDirectory(prefix="corpus-selftest-") as tmp:
        corpus, seeds = build_mini_tree(Path(tmp))
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = regen(corpus, seeds, check=False)
        check("regen exits 0", code == 0, out.getvalue() + err.getvalue())
        index_text = (corpus / INDEX_FILE).read_text(encoding="utf-8")
        mapping_text = (corpus / MAPPING_FILE).read_text(encoding="utf-8")
        check("oracle table measures archive rows", "| mini-active | reconstructed-ladder (reconstructed)" in index_text, index_text)
        check("compared cells measured", "| 4 | 4 |" in index_text, index_text)
        check("ledgered exclusion referenced", "ledger 1 (1 raw)" in index_text, index_text)
        check("seed witness measured", "mini-seed: ledger 1 entries/2 cells" in index_text, index_text)
        check("totals line distinguishes counts",
              "2 competitions (1 active, 1 skipped); 4 archive rows; 4 compared cells" in index_text, index_text)
        check("external/reconstructed split shown",
              "oracles 0 external / 1 reconstructed" in index_text, index_text)
        check("skipped row has no measurements", "mini-skipped | none (skipped)" in index_text, index_text)
        check("seed coverage names the pair", "mini-seed.json | mini-active | 1/1 | witnessed" in mapping_text, mapping_text)
        check("unpaired seed uncovered",
              "other-seed.json | — | 0/0 | uncovered (no registry pair)" in mapping_text, mapping_text)

        first = {name: (corpus / name).read_bytes() for name in (INDEX_FILE, MAPPING_FILE)}
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            regen(corpus, seeds, check=False)
        second = {name: (corpus / name).read_bytes() for name in (INDEX_FILE, MAPPING_FILE)}
        check("regen is byte-deterministic", first == second)

        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            code = regen(corpus, seeds, check=True)
        check("fresh tree passes --check", code == 0)

        ledger_path = corpus / "mini-active" / "parallel-run" / "mini-seed.json"
        ledger = json.loads(ledger_path.read_text(encoding="utf-8"))
        ledger["triagedDifferences"][0]["cells"].append({"round": 2, "group": 1, "pilotNo": 1})
        write_json(ledger_path, ledger)
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = regen(corpus, seeds, check=True)
        check("ledger drift fails --check", code == 1, out.getvalue() + err.getvalue())
        check("--check names the drifted file", "parallel-run-mapping.md" in out.getvalue(), out.getvalue())
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            regen(corpus, seeds, check=False)
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            code = regen(corpus, seeds, check=True)
        check("regen after drift restores --check", code == 0)
        refreshed = (corpus / MAPPING_FILE).read_text(encoding="utf-8")
        check("refreshed witness count follows the ledger", "1 entries, 3 cells" in refreshed, refreshed)

    failed = 0
    for name, ok, detail in cases:
        if not ok:
            failed += 1
        print(f"{'OK  ' if ok else 'FAIL'} {name}")
        if detail and not ok:
            print(detail.replace("\n", "\n     ")[:2000])
    print(f"self-test: {len(cases) - failed}/{len(cases)} cases passed")
    return 1 if failed else 0


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Regenerate the corpus coverage summaries from their authoritative inputs."
    )
    parser.add_argument(
        "command", nargs="?", choices=["regen"],
        help="rewrite the marked sections of index.md and parallel-run-mapping.md",
    )
    parser.add_argument(
        "--check", action="store_true",
        help="with regen: verify the summaries are fresh, exit 1 on drift (CI gate)",
    )
    parser.add_argument(
        "--corpus", default=None,
        help="corpus directory (default: tests/GliderscoreFixtures beside extract/)",
    )
    parser.add_argument(
        "--seeds", default=None,
        help="seed class directory (default: tools/Soarscore.SeedData/json)",
    )
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args(argv)

    if args.self_test:
        return self_test()
    if args.command != "regen":
        parser.error("a command is required: regen (or --self-test)")
    corpus_dir = Path(args.corpus) if args.corpus else DEFAULT_CORPUS_DIR
    seed_dir = Path(args.seeds) if args.seeds else DEFAULT_SEED_DIR
    if not (corpus_dir / REGISTRY_FILE).is_file():
        fail(f"no {REGISTRY_FILE} under {corpus_dir}")
    return regen(corpus_dir, seed_dir, args.check)


if __name__ == "__main__":
    sys.exit(main())
