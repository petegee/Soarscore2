#!/usr/bin/env python3
"""Validate a curated GliderScore fixture directory against the WI-2 schema v1 rules.

Usage:
    python3 validate.py <fixture-dir> [--index PATH]
    python3 validate.py --self-test

See README.md (same directory) for the enforced rules and index contract.
"""

import argparse
import contextlib
import io
import json
import sys
import tempfile
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
JUSTIFIABLE_CONCEPTS = {"teams", "series"}


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
        flags.append(("teams", "UseTeams=true (team scoring concept gap)"))
    series = triage.get("CompSeriesNo")
    if isinstance(series, str) and series.strip() not in SERIES_OFF:
        flags.append(("series", f"CompSeriesNo={series!r} (comp-series concept gap)"))
    elif isinstance(series, int) and series not in PRELIM_OFF:
        flags.append(("series", f"CompSeriesNo={series!r} (comp-series concept gap)"))
    prelim = triage.get("PrelimCompNo")
    if prelim not in (None,) and prelim not in PRELIM_OFF:
        flags.append(("prelim", f"PrelimCompNo={prelim!r} (preliminary/fly-off concept gap)"))
    merged = triage.get("MergedComps")
    if isinstance(merged, str) and merged.strip():
        flags.append(("merged", f"MergedComps={merged!r} (merged-comp concept gap)"))
    return flags


def justification_problem(concept, competition, scores_raw):
    just = (competition.get("triage") or {}).get("triageJustification")
    entry = just.get(concept) if isinstance(just, dict) else None
    if not isinstance(entry, dict):
        return f"missing triageJustification.{concept}"
    evidence = entry.get("evidence")
    if not isinstance(evidence, str) or not evidence.strip():
        return f"triageJustification.{concept}.evidence must be a non-empty string"
    if concept == "teams":
        columns = [name for name in (scores_raw.get("schema") or {}) if "team" in str(name).lower()]
        if columns:
            return (
                f"triageJustification.teams claims no team data but scores-raw.json "
                f"declares column(s) {columns}"
            )
        return None
    count = entry.get("deadLinkCount")
    if isinstance(count, bool) or not isinstance(count, int):
        return "triageJustification.series.deadLinkCount must be an integer"
    if count != 0:
        return (
            f"triageJustification.series.deadLinkCount={count!r} is non-zero "
            f"(a live series link plausibly alters the oracle)"
        )
    return None


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


def check_rule_5(competition, scores_raw, slug, index_path, warnings, errors):
    flags = gap_flags(competition)
    if not flags:
        return
    open_flags = []
    for concept, label in flags:
        if concept not in JUSTIFIABLE_CONCEPTS:
            open_flags.append(label)
            continue
        problem = justification_problem(concept, competition, scores_raw)
        if problem is None:
            continue
        open_flags.append(f"{label}; justification unsound ({problem})")
    if not open_flags:
        return
    reason = "; ".join(open_flags)
    if index_path is None:
        warnings.append(
            f"rule 5 WARNING: {slug} trips a concept-gap flag ({reason}); "
            f"it MUST be skip-listed in tests/GliderscoreFixtures/index.md before activation "
            f"(only team/series flags can be excused by a sound competition.json triageJustification)"
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


TRIAGE_OFF = {"UseTeams": False, "CompSeriesNo": "0", "PrelimCompNo": -1, "MergedComps": ""}
SOUND_TEAMS = {"teams": {"evidence": "Scores carries no team columns; team standings are report-time aggregations"}}
SOUND_SERIES = {"series": {"deadLinkCount": 0, "evidence": "CompSeries table is empty; every series link is dead"}}


def base_competition(triage):
    return {
        "identity": {"CompNo": 1, "CompName": "self-test comp"},
        "scoring": {"GroupScoreDecimals": 0, "RoundOrTruncate": 0},
        "familyRows": {"Dur": {"CompNo": 1, "durLndg": 1}},
        "lookups": {"landingSchemes": [{"LndgNo": 1, "points": []}]},
        "triage": triage,
    }


def write_fixture(root, slug, triage, justification=None, extra_score_columns=None):
    fixture = root / slug
    fixture.mkdir(parents=True)
    if justification is not None:
        triage = {**triage, "triageJustification": justification}
    schema = {"CompNo": "Long", "TaskNo": "Integer", "RoundNo": "Long"}
    schema.update(extra_score_columns or {})
    documents = {
        "provenance.json": {},
        "competition.json": base_competition(triage),
        "entries.json": {"compPilots": {"rows": []}},
        "scores-raw.json": {"schema": schema, "rows": []},
        "expected-scores.json": {"keyFormat": CANONICAL_KEY_FORMAT, "scores": {}},
        "expected-result.json": {},
    }
    for name, document in documents.items():
        (fixture / name).write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
    return fixture


def self_test():
    corpus_dir = Path(__file__).resolve().parent.parent
    index_path = corpus_dir / "index.md"
    cases = []

    def run(name, argv, want_code, want_parts=(), forbid_parts=()):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = main(argv)
        produced = out.getvalue() + err.getvalue()
        ok = (
            code == want_code
            and all(part in produced for part in want_parts)
            and all(part not in produced for part in forbid_parts)
        )
        detail = "" if ok else f"exit={code} (wanted {want_code}); output:\n{produced.strip()}"
        cases.append((name, ok, detail))

    with tempfile.TemporaryDirectory(prefix="validate-selftest-") as tmp:
        root = Path(tmp)

        unflagged = write_fixture(root, "unflagged", TRIAGE_OFF)
        run("unflagged fixture passes untouched", [str(unflagged)], 0, ("PASS",), ("rule 5",))

        bare_teams = write_fixture(root, "bare-teams", {**TRIAGE_OFF, "UseTeams": True})
        run(
            "flagged without justification warns when --index absent",
            [str(bare_teams)], 0, ("rule 5 WARNING", "triageJustification"),
        )
        run(
            "flagged without justification fails under --index",
            [str(bare_teams), "--index", str(index_path)], 1,
            ("rule 5", "triageJustification"),
        )

        series_no_count = write_fixture(
            root, "series-no-count", {**TRIAGE_OFF, "CompSeriesNo": "1"},
            justification={"series": {"evidence": "empty CompSeries table"}},
        )
        run(
            "series justification missing deadLinkCount fails",
            [str(series_no_count), "--index", str(index_path)], 1,
            ("deadLinkCount",),
        )

        series_live = write_fixture(
            root, "series-live", {**TRIAGE_OFF, "CompSeriesNo": "1"},
            justification={"series": {"deadLinkCount": 3, "evidence": "three live links"}},
        )
        run(
            "series justification non-zero deadLinkCount fails",
            [str(series_live), "--index", str(index_path)], 1,
            ("non-zero",),
        )

        teams_empty_evidence = write_fixture(
            root, "teams-empty-evidence", {**TRIAGE_OFF, "UseTeams": True},
            justification={"teams": {"evidence": "   "}},
        )
        run(
            "teams justification empty evidence fails",
            [str(teams_empty_evidence), "--index", str(index_path)], 1,
            ("evidence must be a non-empty string",),
        )

        teams_column = write_fixture(
            root, "teams-column", {**TRIAGE_OFF, "UseTeams": True},
            justification=SOUND_TEAMS, extra_score_columns={"TeamNo": "Long"},
        )
        run(
            "team column in scores-raw defeats clean-teams claim",
            [str(teams_column), "--index", str(index_path)], 1,
            ("TeamNo",),
        )

        comp_one_shape = {**TRIAGE_OFF, "UseTeams": True, "CompSeriesNo": "1"}
        fully_sound = write_fixture(
            root, "fully-sound", comp_one_shape,
            justification={**SOUND_TEAMS, **SOUND_SERIES},
        )
        run(
            "sound justifications for every flag activate without skip-listing",
            [str(fully_sound), "--index", str(index_path)], 0, ("PASS",), ("rule 5",),
        )

        half_sound = write_fixture(root, "half-sound", comp_one_shape, justification=dict(SOUND_TEAMS))
        run(
            "sound teams does not excuse flagged series",
            [str(half_sound), "--index", str(index_path)], 1,
            ("triageJustification.series",),
        )

        dur_less_landing = write_fixture(root, "dur-less-landing", TRIAGE_OFF)
        competition_doc = json.loads((dur_less_landing / "competition.json").read_text(encoding="utf-8"))
        competition_doc["familyRows"] = {}
        competition_doc["lookups"]["landingSchemes"] = []
        (dur_less_landing / "competition.json").write_text(
            json.dumps(competition_doc, indent=2) + "\n", encoding="utf-8"
        )
        entries_doc = json.loads((dur_less_landing / "entries.json").read_text(encoding="utf-8"))
        entries_doc["compPilots"]["rows"] = [{"CompNo": 1, "PilotNo": 13}]
        (dur_less_landing / "entries.json").write_text(
            json.dumps(entries_doc, indent=2) + "\n", encoding="utf-8"
        )
        scores_doc = json.loads((dur_less_landing / "scores-raw.json").read_text(encoding="utf-8"))
        scores_doc["schema"]["Landing"] = "Double"
        scores_doc["rows"] = [{
            "CompNo": 1, "TaskNo": 5, "RoundNo": 4, "GroupNo": 1,
            "ReFlightNo": 0, "PilotNo": 13, "SeqNo": 2, "Landing": 145.0,
        }]
        (dur_less_landing / "scores-raw.json").write_text(
            json.dumps(scores_doc, indent=2) + "\n", encoding="utf-8"
        )
        expected_doc = json.loads((dur_less_landing / "expected-scores.json").read_text(encoding="utf-8"))
        expected_doc["scores"]["5/4/1/0/13"] = {"RawScore": 0.0, "NormalisedScore": 0.0}
        (dur_less_landing / "expected-scores.json").write_text(
            json.dumps(expected_doc, indent=2) + "\n", encoding="utf-8"
        )
        run(
            "Dur-less fixture passes rule 2 despite non-zero Landing values",
            [str(dur_less_landing)], 0, ("PASS",), ("rule 2",),
        )

        prelim = write_fixture(root, "prelim-flagged", {**TRIAGE_OFF, "PrelimCompNo": 2})
        run(
            "prelim flag stays an unconditional skip",
            [str(prelim), "--index", str(index_path)], 1, ("PrelimCompNo",),
        )

    run(
        "ales-sample-comp regression",
        [str(corpus_dir / "ales-sample-comp"), "--index", str(index_path)],
        0, ("PASS",), ("FAIL", "WARNING"),
    )

    failed = 0
    for name, ok, detail in cases:
        if not ok:
            failed += 1
        print(f"{'OK  ' if ok else 'FAIL'} {name}")
        if detail:
            print(detail.replace("\n", "\n     "))
    print(f"self-test: {len(cases) - failed}/{len(cases)} cases passed")
    return 1 if failed else 0


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Validate a curated GliderScore fixture directory (schema v1)."
    )
    parser.add_argument(
        "fixture_dir", nargs="?", default=None,
        help="fixture directory containing the curated JSON files",
    )
    parser.add_argument(
        "--index", default=None,
        help="path to tests/GliderscoreFixtures/index.md; required to prove rule-5 skip-listing",
    )
    parser.add_argument(
        "--self-test", action="store_true",
        help="build throwaway fixtures in a temp directory and prove rule 5 both directions",
    )
    args = parser.parse_args(argv)

    if args.self_test:
        return self_test()
    if not args.fixture_dir:
        parser.error("a fixture directory is required (or pass --self-test)")

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
    check_rule_5(competition, scores_raw, slug, index_path, warnings, errors)
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
