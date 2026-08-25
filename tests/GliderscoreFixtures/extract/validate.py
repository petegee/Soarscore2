#!/usr/bin/env python3
"""Validate a curated GliderScore fixture directory against the WI-2 schema v1 rules.

Usage:
    python3 validate.py <fixture-dir> [--index PATH]

See README.md (same directory) for the enforced rules and index contract.
"""

import argparse
import json
import sys
from pathlib import Path

REQUIRED_FILES = [
    "provenance.json",
    "competition.json",
    "entries.json",
    "scores-raw.json",
    "expected-scores.json",
    "expected-result.json",
]
CANONICAL_KEY_FORMAT = "{TaskNo}/{RoundNo}/{GroupNo}/{ReFlightNo}/{PilotNo}"
SERIES_OFF = {"", "0"}
PRELIM_OFF = {-1, 0}


def fail(errors, message):
    errors.append(message)


def load_json(fixture_dir, name, errors):
    path = fixture_dir / name
    if not path.is_file():
        fail(errors, f"missing file: {name}")
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        fail(errors, f"{name} is not valid JSON: {exc}")
        return None


def member_pilot_nos(entries, errors):
    comp_pilots = entries.get("compPilots") or {}
    rows = comp_pilots.get("rows") or []
    nos = set()
    for row in rows:
        pilot_no = row.get("PilotNo")
        if pilot_no is None:
            fail(errors, "entries.json compPilots row without PilotNo")
            continue
        nos.add(pilot_no)
    return nos


def check_rule_1(scores_raw, entries, errors):
    members = member_pilot_nos(entries, errors)
    for i, row in enumerate(scores_raw.get("rows") or []):
        pilot_no = row.get("PilotNo")
        if pilot_no not in members:
            fail(errors, f"rule 1: scores-raw row {i} PilotNo {pilot_no!r} not among entries members")
        for column in ("TaskNo", "RoundNo", "GroupNo", "SeqNo"):
            if row.get(column) is None:
                fail(errors, f"rule 1: scores-raw row {i} has null {column}")


def check_rule_2(competition, scores_raw, errors):
    family_rows = competition.get("familyRows") or {}
    dur = family_rows.get("Dur")
    if not isinstance(dur, dict):
        fail(errors, "rule 2: competition.json has no Dur family row to reference a landing scheme")
        return
    scheme_no = dur.get("durLndg")
    schemes = {
        scheme.get("LndgNo"): {p.get("Distance") for p in (scheme.get("points") or [])}
        for scheme in competition.get("lookups", {}).get("landingSchemes", [])
    }
    if scheme_no not in schemes:
        fail(errors, f"rule 2: referenced landing scheme LndgNo={scheme_no!r} absent from competition.json")
        return
    distances = schemes[scheme_no]
    for i, row in enumerate(scores_raw.get("rows") or []):
        landing = row.get("Landing")
        if landing is None or landing == 0:
            continue
        if landing not in distances:
            fail(
                errors,
                f"rule 2: scores-raw row {i} Landing={landing!r} is off-table for scheme "
                f"LndgNo={scheme_no} (would silently score 0 in GliderScore)",
            )


def check_rule_3(competition, errors):
    scoring = competition.get("scoring") or {}
    decs = scoring.get("GroupScoreDecimals")
    rot = scoring.get("RoundOrTruncate")
    if decs not in (0, 1, 2, 3):
        fail(errors, f"rule 3: GroupScoreDecimals={decs!r} outside {{0,1,2,3}} (GS zeroes/stales scores)")
    if rot not in (0, 1):
        fail(errors, f"rule 3: RoundOrTruncate={rot!r} outside {{0,1}} (GS stales NormalisedScore)")


def check_rule_4(competition, entries, scores_raw, errors):
    expected = (competition.get("identity") or {}).get("CompNo")
    found = {expected}
    label = {"competition.json identity"}
    for row in (entries.get("compPilots") or {}).get("rows") or []:
        found.add(row.get("CompNo"))
        label.add("entries.json compPilots")
    for row in scores_raw.get("rows") or []:
        found.add(row.get("CompNo"))
        label.add("scores-raw.json")
    if len(found) != 1 or expected is None:
        detail = ", ".join(f"{v!r}" for v in sorted(found, key=lambda v: (v is None, v)))
        fail(errors, f"rule 4: more than one CompNo across fixture ({detail}; sections seen: {sorted(label)})")


def gap_flags(competition):
    triage = competition.get("triage") or {}
    flags = []
    if triage.get("UseTeams") is True:
        flags.append("UseTeams=true (team scoring concept gap)")
    series = triage.get("CompSeriesNo")
    if isinstance(series, str) and series.strip() not in SERIES_OFF:
        flags.append(f"CompSeriesNo={series!r} (comp-series concept gap)")
    elif isinstance(series, int) and series not in PRELIM_OFF:
        flags.append(f"CompSeriesNo={series!r} (comp-series concept gap)")
    prelim = triage.get("PrelimCompNo")
    if prelim not in (None,) and prelim not in PRELIM_OFF:
        flags.append(f"PrelimCompNo={prelim!r} (preliminary/fly-off concept gap)")
    merged = triage.get("MergedComps")
    if isinstance(merged, str) and merged.strip():
        flags.append(f"MergedComps={merged!r} (merged-comp concept gap)")
    return flags


def index_skips(index_path, slug, errors):
    try:
        lines = index_path.read_text(encoding="utf-8").splitlines()
    except OSError as exc:
        fail(errors, f"index file unreadable: {index_path} ({exc})")
        return False
    for line in lines:
        token = line.strip().lstrip("-*").strip().split()
        if token and token[0] == slug and "skipped" in line.lower():
            return True
    return False


def check_rule_5(competition, slug, index_path, warnings, errors):
    flags = gap_flags(competition)
    if not flags:
        return
    reason = "; ".join(flags)
    if index_path is None:
        warnings.append(
            f"rule 5 WARNING: {slug} trips a concept-gap flag ({reason}); "
            f"it MUST be skip-listed in tests/GliderscoreFixtures/index.md before activation"
        )
        return
    if not index_skips(index_path, slug, errors):
        fail(errors, f"rule 5: {slug} trips a concept-gap flag ({reason}) but is not skip-listed in {index_path}")


def composite_key(row):
    return CANONICAL_KEY_FORMAT.format(
        TaskNo=row["TaskNo"], RoundNo=row["RoundNo"], GroupNo=row["GroupNo"],
        ReFlightNo=row["ReFlightNo"], PilotNo=row["PilotNo"],
    )


def check_integrity(expected_scores, scores_raw, entries, errors):
    declared = expected_scores.get("keyFormat")
    if declared != CANONICAL_KEY_FORMAT:
        fail(errors, f"integrity: unexpected keyFormat {declared!r}, expected {CANONICAL_KEY_FORMAT!r}")
        return
    raw_keys = {}
    for i, row in enumerate(scores_raw.get("rows") or []):
        try:
            raw_keys[composite_key(row)] = i
        except TypeError:
            fail(errors, f"integrity: scores-raw row {i} lacks composite-key components")
    score_map = expected_scores.get("scores") or {}
    expected_keys = set(score_map)
    missing = sorted(raw_keys.keys() - expected_keys)
    extra = sorted(expected_keys - raw_keys.keys())
    if missing:
        fail(errors, f"integrity: scores-raw rows without an expected-scores key: {missing}")
    if extra:
        fail(errors, f"integrity: expected-scores keys without a scores-raw row: {extra}")
    members = member_pilot_nos(entries, errors)
    for key in expected_keys:
        pilot_no = key.rsplit("/", 1)[-1]
        if pilot_no.isdigit() and int(pilot_no) not in members:
            fail(errors, f"integrity: expected-scores key {key} pilot not among entries members")
    duplicates = len(raw_keys) != len(scores_raw.get("rows") or [])
    if duplicates:
        fail(errors, "integrity: duplicate composite keys across scores-raw rows")


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Validate a curated GliderScore fixture directory (schema v1)."
    )
    parser.add_argument("fixture_dir", help="fixture directory containing the curated JSON files")
    parser.add_argument(
        "--index", default=None,
        help="path to tests/GliderscoreFixtures/index.md; required to prove rule-5 skip-listing",
    )
    args = parser.parse_args(argv)

    fixture_dir = Path(args.fixture_dir)
    if not fixture_dir.is_dir():
        raise SystemExit(f"validate.py: no such fixture directory: {fixture_dir}")

    errors = []
    warnings = []
    documents = {
        name: load_json(fixture_dir, name, errors)
        for name in REQUIRED_FILES
    }
    if any(document is None for document in documents.values()):
        for message in errors:
            print(f"FAIL {message}", file=sys.stderr)
        print(f"validate.py: {len(errors)} error(s); aborting before rule checks", file=sys.stderr)
        return 1

    competition = documents["competition.json"]
    entries = documents["entries.json"]
    scores_raw = documents["scores-raw.json"]
    expected_scores = documents["expected-scores.json"]

    slug = fixture_dir.name
    index_path = Path(args.index) if args.index else None

    check_rule_1(scores_raw, entries, errors)
    check_rule_2(competition, scores_raw, errors)
    check_rule_3(competition, errors)
    check_rule_4(competition, entries, scores_raw, errors)
    check_rule_5(competition, slug, index_path, warnings, errors)
    check_integrity(expected_scores, scores_raw, entries, errors)

    for warning in warnings:
        print(warning, file=sys.stderr)
    if errors:
        for message in errors:
            print(f"FAIL {message}", file=sys.stderr)
        print(f"validate.py: {slug}: {len(errors)} error(s)", file=sys.stderr)
        return 1

    row_count = len(scores_raw.get("rows") or [])
    key_count = len((expected_scores.get("scores") or {}))
    print(
        f"validate.py: {slug}: PASS "
        f"(rules 1-5, integrity; scores-raw rows={row_count}, expected-score keys={key_count})"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
