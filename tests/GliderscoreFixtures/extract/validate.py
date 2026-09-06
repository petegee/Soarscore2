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
]
# Final-ranking oracle: absent while a fixture is at the scores stage of the
# pipeline AND its provenance declares the deferral; otherwise required.
ORACLE_FILE = "expected-result.json"
DEFERRAL_FLAG = "expectedResultDeferred"
CANONICAL_KEY_FORMAT = "{TaskNo}/{RoundNo}/{GroupNo}/{ReFlightNo}/{PilotNo}"
SERIES_OFF = {"", "0"}
PRELIM_OFF = {-1, 0}
JUSTIFIABLE_CONCEPTS = {"series"}


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


def check_rule_3(competition, errors, warnings):
    scoring = competition.get("scoring") or {}
    decs = scoring.get("GroupScoreDecimals")
    rot = scoring.get("RoundOrTruncate")
    for field, value, allowed in (
        ("GroupScoreDecimals", decs, (0, 1, 2, 3)),
        ("RoundOrTruncate", rot, (0, 1)),
    ):
        if value is None:
            # Persisted Jet null (unset): scores demonstrably survive it (DB-wide
            # pattern), unlike an out-of-range VALUE which zeroes/stales them.
            # Record-and-warn; coercing to a default would falsify curation.
            warnings.append(
                f"rule 3 note: {field} unset (null) \u2014 recorded faithfully; "
                f"the out-of-range guard concerns actual values only"
            )
        elif value not in allowed:
            fail(
                errors,
                f"rule 3: {field}={value!r} outside {set(allowed)} (GS zeroes/stales scores)",
            )


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


def justification_problem(concept, competition):
    just = (competition.get("triage") or {}).get("triageJustification")
    entry = just.get(concept) if isinstance(just, dict) else None
    if not isinstance(entry, dict):
        return f"missing triageJustification.{concept}"
    evidence = entry.get("evidence")
    if not isinstance(evidence, str) or not evidence.strip():
        return f"triageJustification.{concept}.evidence must be a non-empty string"
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


def check_rule_5(competition, slug, index_path, warnings, errors):
    flags = gap_flags(competition)
    if not flags:
        return
    open_flags = []
    for concept, label in flags:
        if concept not in JUSTIFIABLE_CONCEPTS:
            open_flags.append(label)
            continue
        problem = justification_problem(concept, competition)
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
            f"(only a series flag can be excused by a sound competition.json triageJustification)"
        )
        return
    if not index_skips(index_path, slug, errors):
        fail(errors, f"rule 5: {slug} trips a concept-gap flag ({reason}) but is not skip-listed in {index_path}")


def _as_int(value):
    try:
        return int(str(value).strip())
    except (TypeError, ValueError):
        return None


def check_team_expectations(competition, entries, fixture_dir, errors):
    """Rule 5's team framing (grow-corpus-team-parity-fixtures.md Move 3).

    Team scoring is not a concept gap — teams-mvp landed it — so UseTeams=true
    no longer forces skip-listing nor a no-effect triageJustification. What a
    team-bearing fixture must carry instead is its DECLARED TEAM-GRAIN
    EXPECTATION, mirroring the harness (Comparator.TeamGrainOverlap and the
    ladder grain's guard, teams-mvp.md decision 8):

    - UseTeams=true with populated CompPilots.Team (any Team > 0) and
      NbrForTeamScore == 3: expected-teams.json must exist (the GS team-ladder
      oracle the harness guard throws without);
    - UseTeams=true with populated teams and any other NbrForTeamScore: a
      documentary T1 entry (grain "team") must exist in divergences.json —
      the MVP classification method is fixed at three and is never emulated,
      so the team grain will not run and the incomparability is pinned;
    - team knobs without populated teams stay unflagged (no team grain can
      run), as does UseTeams=false (GS computes no team scores either).
    """
    triage = competition.get("triage") or {}
    if triage.get("UseTeams") is not True:
        return
    populated = False
    for row in (entries.get("compPilots") or {}).get("rows") or []:
        team = _as_int(row.get("Team"))
        if team is not None and team > 0:
            populated = True
            break
    if not populated:
        return
    if _as_int(triage.get("NbrForTeamScore")) == 3:
        if not (fixture_dir / "expected-teams.json").is_file():
            fail(
                errors,
                f"rule 5: team-bearing overlap fixture (UseTeams=true, NbrForTeamScore=3, "
                f"populated CompPilots.Team) requires expected-teams.json \u2014 author the GS "
                f"team-ladder oracle (grow-corpus-team-parity-fixtures.md WI-1C); the harness "
                f"ladder grain throws without it",
            )
        return
    path = fixture_dir / "divergences.json"
    cited = False
    if path.is_file():
        try:
            ledger = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, UnicodeDecodeError) as exc:
            fail(errors, f"rule 5: divergences.json is not valid JSON: {exc}")
            return
        if not isinstance(ledger, list):
            fail(errors, "rule 5: divergences.json must hold a JSON array of ledger entries")
            return
        cited = any(
            isinstance(entry, dict)
            and entry.get("grain") == "team"
            and "T1" in str(entry.get("reason") or "")
            for entry in ledger
        )
    if not cited:
        fail(
            errors,
            f"rule 5: team-bearing fixture (UseTeams=true, populated CompPilots.Team, "
            f"NbrForTeamScore={triage.get('NbrForTeamScore')!r}) declares a classification "
            f"method the MVP never emulates \u2014 pin the incomparability with a documentary "
            f"T1 entry (grain \u201cteam\u201d) in divergences.json (teams-mvp.md decision 8); "
            f"the harness team grain does not run for it",
        )


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
SOUND_SERIES = {"series": {"deadLinkCount": 0, "evidence": "CompSeries table is empty; every series link is dead"}}


def base_competition(triage):
    return {
        "identity": {"CompNo": 1, "CompName": "self-test comp"},
        "scoring": {"GroupScoreDecimals": 0, "RoundOrTruncate": 0},
        "familyRows": {"Dur": {"CompNo": 1, "durLndg": 1}},
        "lookups": {"landingSchemes": [{"LndgNo": 1, "points": []}]},
        "triage": triage,
    }


def write_fixture(root, slug, triage, justification=None, extra_score_columns=None,
                  omit_oracle=False, provenance_extra=None, pilot_teams=(),
                  divergences=None, with_expected_teams=False):
    fixture = root / slug
    fixture.mkdir(parents=True)
    if justification is not None:
        triage = {**triage, "triageJustification": justification}
    schema = {"CompNo": "Long", "TaskNo": "Integer", "RoundNo": "Long"}
    schema.update(extra_score_columns or {})
    documents = {
        "provenance.json": {**(provenance_extra or {})},
        "competition.json": base_competition(triage),
        "entries.json": {
            "compPilots": {
                "rows": [
                    {"CompNo": 1, "PilotNo": pilot_no, "Team": team}
                    for pilot_no, team in enumerate(pilot_teams, start=1)
                ]
            }
        },
        "scores-raw.json": {"schema": schema, "rows": []},
        "expected-scores.json": {"keyFormat": CANONICAL_KEY_FORMAT, "scores": {}},
    }
    if not omit_oracle:
        documents["expected-result.json"] = {}
    if with_expected_teams:
        documents["expected-teams.json"] = {"source": "reconstructed-gs-team-ladder", "standings": []}
    if divergences is not None:
        documents["divergences.json"] = divergences
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

        teamless_knobs = write_fixture(root, "teamless-knobs", {**TRIAGE_OFF, "UseTeams": True})
        run(
            "team knobs without populated teams need no expectation and no skip-listing",
            [str(teamless_knobs), "--index", str(index_path)], 0, ("PASS",), ("rule 5",),
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

        fully_sound = write_fixture(
            root, "fully-sound", {**TRIAGE_OFF, "CompSeriesNo": "1"},
            justification=dict(SOUND_SERIES),
        )
        run(
            "sound series justification activates without skip-listing",
            [str(fully_sound), "--index", str(index_path)], 0, ("PASS",), ("rule 5",),
        )

        stale_teams_excuse = write_fixture(
            root, "stale-teams-excuse", {**TRIAGE_OFF, "CompSeriesNo": "1"},
            justification={"teams": {"evidence": "legacy no-effect excuse, no longer a flag"}},
        )
        run(
            "a stale teams justification does not excuse flagged series",
            [str(stale_teams_excuse), "--index", str(index_path)], 1,
            ("triageJustification.series",),
        )

        series_warn = write_fixture(root, "series-warn", {**TRIAGE_OFF, "CompSeriesNo": "1"})
        run(
            "flagged series without --index warns instead of failing",
            [str(series_warn)], 0, ("rule 5 WARNING",), ("FAIL",),
        )

        overlap = {**TRIAGE_OFF, "UseTeams": True, "NbrForTeamScore": 3}
        overlap_ok = write_fixture(
            root, "team-overlap-ok", overlap, pilot_teams=(4, 4, 4), with_expected_teams=True,
        )
        run(
            "team-bearing overlap fixture with the ladder oracle activates",
            [str(overlap_ok), "--index", str(index_path)], 0, ("PASS",), ("rule 5",),
        )

        overlap_bare = write_fixture(root, "team-overlap-bare", overlap, pilot_teams=(4, 4, 4))
        run(
            "team-bearing overlap fixture without expected-teams.json fails",
            [str(overlap_bare), "--index", str(index_path)], 1,
            ("expected-teams.json",),
        )

        nbr_two = {**TRIAGE_OFF, "UseTeams": True, "NbrForTeamScore": 2}
        t1_ledger = [{
            "grain": "team", "round": None, "group": None, "pilotNo": None,
            "reason": "T1: NbrForTeamScore=2 declares a method the MVP never emulates",
        }]
        t1_ok = write_fixture(
            root, "team-t1-ok", nbr_two, pilot_teams=(4, 4, 4), divergences=t1_ledger,
        )
        run(
            "Nbr\u22603 team-bearing fixture with a T1 ledger entry activates",
            [str(t1_ok), "--index", str(index_path)], 0, ("PASS",), ("rule 5",),
        )

        t1_missing = write_fixture(root, "team-t1-missing", nbr_two, pilot_teams=(4, 4, 4))
        run(
            "Nbr\u22603 team-bearing fixture without a T1 ledger entry fails",
            [str(t1_missing), "--index", str(index_path)], 1,
            ("T1",),
        )

        inert_by_knob = write_fixture(
            root, "team-inert-by-knob", {**TRIAGE_OFF, "NbrForTeamScore": 3},
            pilot_teams=(4, 4, 4),
        )
        run(
            "UseTeams=false with populated teams stays inert (no expectation demanded)",
            [str(inert_by_knob), "--index", str(index_path)], 0, ("PASS",), ("rule 5",),
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

        staged = write_fixture(
            root, "wi2-staged", TRIAGE_OFF,
            omit_oracle=True, provenance_extra={DEFERRAL_FLAG: True},
        )
        run(
            "declared oracle deferral passes without expected-result.json",
            [str(staged)], 0, ("PASS", DEFERRAL_FLAG), ("missing file",),
        )

        undeclared = write_fixture(root, "oracle-undeclared", TRIAGE_OFF)
        (undeclared / ORACLE_FILE).unlink()
        run(
            "undeclared missing oracle stays a hard failure",
            [str(undeclared)], 1, ("missing file",),
        )

        contradicted = write_fixture(
            root, "oracle-contradiction", TRIAGE_OFF,
            provenance_extra={DEFERRAL_FLAG: True},
        )
        run(
            "deferral declared while oracle present fails",
            [str(contradicted)], 1, ("contradict",),
        )

        def null_knobs(root_slug, **overrides):
            fixture = write_fixture(root, root_slug, TRIAGE_OFF)
            competition_doc = json.loads((fixture / "competition.json").read_text(encoding="utf-8"))
            competition_doc["scoring"].update(overrides)
            (fixture / "competition.json").write_text(
                json.dumps(competition_doc, indent=2) + "\n", encoding="utf-8"
            )
            return fixture

        run(
            "unset (null) scoring knobs warn but pass",
            [str(null_knobs("null-knobs", GroupScoreDecimals=None, RoundOrTruncate=None))],
            0, ("rule 3 note",), ("FAIL",),
        )
        run(
            "out-of-range decimals keep failing under faithful-null rule 3",
            [str(null_knobs("decimals-nine", GroupScoreDecimals=9))], 1,
            ("outside",),
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

    provenance = load_json(fixture_dir, "provenance.json", errors)
    deferred = isinstance(provenance, dict) and provenance.get(DEFERRAL_FLAG) is True
    oracle_path = fixture_dir / ORACLE_FILE
    if not deferred:
        load_json(fixture_dir, ORACLE_FILE, errors)
    elif oracle_path.is_file():
        fail(
            errors,
            f"{ORACLE_FILE} present but provenance declares {DEFERRAL_FLAG}=true "
            f"(clear the flag or the file \u2014 they contradict)",
        )
    else:
        warnings.append(
            f"deferral WARNING: {fixture_dir.name} declares {DEFERRAL_FLAG}=true with no {ORACLE_FILE}; "
            f"the ranking oracle must land before corpus activation"
        )

    documents = {
        "provenance.json": provenance,
        **{
            name: load_json(fixture_dir, name, errors)
            for name in REQUIRED_FILES
            if name != "provenance.json"
        },
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
    check_rule_3(competition, errors, warnings)
    check_rule_4(competition, entries, scores_raw, errors)
    check_rule_5(competition, slug, index_path, warnings, errors)
    check_team_expectations(competition, entries, fixture_dir, errors)
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
