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
import re
import shutil
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


# ---------------------------------------------------------------------------
# GS 04 Step 3 — corpus-registry awareness. The registry
# (tests/GliderscoreFixtures/corpus-registry.json, schema v1) is the
# authoritative fixture manifest; index.md stays the human-readable manifest
# and the two must agree. These checks are stdlib-only like the rest of this
# tool: the schema below is validated structurally here, mirroring
# corpus-registry.schema.json field for field (no jsonschema dependency, no
# network). Every failure names the fixture slug and the field.
# ---------------------------------------------------------------------------

REGISTRY_FILE = "corpus-registry.json"
REGISTRY_SCHEMA_VERSION = 1
REGISTRY_STATUSES = {"active", "skipped"}
REGISTRY_MODES = {"parity", "parallel-run"}

REPLAY_SCENARIO = re.compile(r'replays the GliderScore fixture "([^"]+)"')
PARALLEL_SCENARIO = re.compile(
    r'parallel-runs the GliderScore fixture "([^"]+)" under the seed class "([^"]+)"'
)


def _is_int(value):
    return isinstance(value, int) and not isinstance(value, bool)


def _is_number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def check_registry_schema(document, errors):
    """Structural validation mirroring corpus-registry.schema.json (v1).

    Returns the entries list on success-shaped documents, None otherwise.
    Per-entry shape problems are recorded but do not stop the scan, so one
    run names every malformed entry.
    """
    if not isinstance(document, dict):
        fail(errors, "registry: corpus-registry.json must hold a JSON object (schema v1)")
        return None
    extra = sorted(set(document) - {"schemaVersion", "entries"})
    if extra:
        fail(
            errors,
            f"registry: unknown top-level field(s) {extra} "
            f"(schema v1 allows schemaVersion, entries)",
        )
    if document.get("schemaVersion") != REGISTRY_SCHEMA_VERSION:
        fail(
            errors,
            f"registry: field schemaVersion is {document.get('schemaVersion')!r}, "
            f"this validator reads {REGISTRY_SCHEMA_VERSION}",
        )
    entries = document.get("entries")
    if not isinstance(entries, list):
        fail(errors, "registry: field entries must hold a JSON array")
        return None
    for position, entry in enumerate(entries):
        check_registry_entry(entry, position, errors)
    return entries


def check_registry_entry(entry, position, errors):
    label = f"registry: entries[{position}]"
    if not isinstance(entry, dict):
        fail(errors, f"{label} must hold a JSON object")
        return
    slug = entry.get("slug")
    if isinstance(slug, str) and slug:
        label = f"registry: fixture {slug!r}"
    else:
        fail(errors, f"{label} is missing its slug (field slug)")
    extra = sorted(set(entry) - {
        "slug", "status", "modes", "sourceRef", "oracleRef",
        "comparison", "evidenceLinks", "replay",
    })
    if extra:
        fail(errors, f"{label} carries unknown field(s) {extra}")
    for field in ("slug", "status", "modes", "sourceRef", "oracleRef",
                  "comparison", "evidenceLinks", "replay"):
        if field not in entry:
            fail(errors, f"{label} is missing its declaration (field {field})")
    if "status" in entry and entry["status"] not in REGISTRY_STATUSES:
        fail(
            errors,
            f"{label} declares status {entry['status']!r} "
            f"(field status; expected 'active' or 'skipped')",
        )
    for field in ("sourceRef", "oracleRef"):
        if field in entry and (
            not isinstance(entry[field], str) or not entry[field].strip()
        ):
            fail(errors, f"{label} declares an empty reference (field {field})")
    check_registry_modes_shape(entry.get("modes"), label, errors)
    check_registry_comparison_shape(entry.get("comparison"), label, errors)
    if "evidenceLinks" in entry and (
        not isinstance(entry["evidenceLinks"], list)
        or any(not isinstance(link, str) for link in entry["evidenceLinks"])
    ):
        fail(errors, f"{label} must list string evidence pointers (field evidenceLinks)")
    check_registry_replay_shape(entry.get("replay"), label, errors)


def check_registry_modes_shape(modes, label, errors):
    if not isinstance(modes, list) or not modes:
        fail(errors, f"{label} must declare at least one executable mode (field modes)")
        return
    seen = set()
    for position, mode in enumerate(modes):
        field = f"{label} field modes[{position}]"
        if not isinstance(mode, dict):
            fail(errors, f"{field} must hold a JSON object")
            continue
        extra = sorted(set(mode) - {"mode", "seed"})
        if extra:
            fail(errors, f"{field} carries unknown field(s) {extra}")
        name = mode.get("mode")
        if name not in REGISTRY_MODES:
            fail(
                errors,
                f"{field} names mode {name!r} (field mode; expected 'parity' or 'parallel-run')",
            )
            continue
        seed = mode.get("seed")
        if name == "parallel-run" and (
            not isinstance(seed, str) or not seed.strip()
        ):
            fail(errors, f"{field} is a parallel-run mode with no seed (field seed)")
        key = (name, seed if isinstance(seed, str) else None)
        if key in seen:
            fail(errors, f"{label} declares the same mode twice (field modes: {name!r})")
        seen.add(key)


def check_registry_comparison_shape(comparison, label, errors):
    if not isinstance(comparison, dict):
        fail(errors, f"{label} must declare its comparison grains (field comparison)")
        return
    extra = sorted(set(comparison) - {"grains"})
    if extra:
        fail(errors, f"{label} field comparison carries unknown field(s) {extra}")
    grains = comparison.get("grains")
    if not isinstance(grains, list) or any(not isinstance(g, str) for g in grains):
        fail(errors, f"{label} must list string comparison grains (field comparison.grains)")


def check_registry_replay_shape(replay, label, errors):
    if not isinstance(replay, dict):
        fail(errors, f"{label} must declare its replay scope (field replay)")
        return
    extra = sorted(set(replay) - {
        "roundParameterBinds", "syntheticSlots", "parallelRun",
    })
    if extra:
        fail(errors, f"{label} field replay carries unknown field(s) {extra}")
    for position, bind in enumerate(replay.get("roundParameterBinds") or []):
        field = f"{label} field replay.roundParameterBinds[{position}]"
        if not isinstance(bind, dict):
            fail(errors, f"{field} must hold a JSON object")
            continue
        if sorted(bind) != ["basisRef", "parameter", "roundNo", "value"]:
            fail(errors, f"{field} must declare parameter, roundNo, value, basisRef")
            continue
        if not isinstance(bind["parameter"], str) or not bind["parameter"].strip():
            fail(errors, f"{field} names no parameter (field parameter)")
        if not _is_int(bind["roundNo"]) or bind["roundNo"] < 1:
            fail(errors, f"{field} declares roundNo {bind['roundNo']!r} (field roundNo)")
        if not _is_number(bind["value"]):
            fail(errors, f"{field} declares value {bind['value']!r} (field value)")
        if not isinstance(bind["basisRef"], str) or not bind["basisRef"].strip():
            fail(errors, f"{field} cites no basis (field basisRef)")
    slots = replay.get("syntheticSlots")
    if slots is not None:
        if not isinstance(slots, dict):
            fail(errors, f"{label} field replay.syntheticSlots must hold a JSON object")
        else:
            extra_slots = sorted(set(slots) - {"prescriptionOnly", "flightLess"})
            if extra_slots:
                fail(
                    errors,
                    f"{label} field replay.syntheticSlots carries unknown kind(s) {extra_slots}",
                )
            for kind in ("prescriptionOnly", "flightLess"):
                for position, slot in enumerate(slots.get(kind) or []):
                    field = f"{label} field replay.syntheticSlots.{kind}[{position}]"
                    if not isinstance(slot, dict):
                        fail(errors, f"{field} must hold a JSON object")
                        continue
                    if sorted(slot) != ["basisRef", "groupNo", "pilotNo", "roundNo"]:
                        fail(errors, f"{field} must declare roundNo, groupNo, pilotNo, basisRef")
                        continue
                    if not _is_int(slot["roundNo"]) or slot["roundNo"] < 1:
                        fail(errors, f"{field} declares roundNo {slot['roundNo']!r}")
                    if not _is_int(slot["groupNo"]) or slot["groupNo"] < 1:
                        fail(errors, f"{field} declares groupNo {slot['groupNo']!r}")
                    if not _is_int(slot["pilotNo"]):
                        fail(errors, f"{field} declares pilotNo {slot['pilotNo']!r}")
                    if not isinstance(slot["basisRef"], str) or not slot["basisRef"].strip():
                        fail(errors, f"{field} cites no basis (field basisRef)")
    parallel = replay.get("parallelRun")
    if parallel is not None:
        if not isinstance(parallel, dict):
            fail(errors, f"{label} field replay.parallelRun must hold a JSON object")
        else:
            extra_parallel = sorted(
                set(parallel)
                - {"scoredWindowAssertion", "landingInstrument", "skipParityRoundBinds"}
            )
            if extra_parallel:
                fail(
                    errors,
                    f"{label} field replay.parallelRun carries unknown field(s) {extra_parallel}",
                )
            window = parallel.get("scoredWindowAssertion")
            if window is not None and (
                not isinstance(window, dict)
                or sorted(window) != ["basisRef", "rounds"]
                or not _is_int(window["rounds"])
                or window["rounds"] < 1
                or not isinstance(window["basisRef"], str)
                or not window["basisRef"].strip()
            ):
                fail(
                    errors,
                    f"{label} field replay.parallelRun.scoredWindowAssertion "
                    f"must declare rounds (>= 1) and basisRef",
                )
            tape = parallel.get("landingInstrument")
            if tape is not None and (
                not isinstance(tape, dict)
                or sorted(tape) != ["basisRef", "instrument", "tapeFile"]
                or not isinstance(tape["instrument"], str)
                or not tape["instrument"].strip()
                or not isinstance(tape["tapeFile"], str)
                or not tape["tapeFile"].strip()
                or not isinstance(tape["basisRef"], str)
                or not tape["basisRef"].strip()
            ):
                fail(
                    errors,
                    f"{label} field replay.parallelRun.landingInstrument "
                    f"must declare instrument, tapeFile and basisRef",
                )
            skip = parallel.get("skipParityRoundBinds")
            if skip is not None and (
                not isinstance(skip, dict)
                or sorted(skip) != ["basisRef", "value"]
                or not isinstance(skip["value"], bool)
                or not isinstance(skip["basisRef"], str)
                or not skip["basisRef"].strip()
            ):
                fail(
                    errors,
                    f"{label} field replay.parallelRun.skipParityRoundBinds "
                    f"must declare value (boolean) and basisRef",
                )


def parse_index_manifest(index_path, errors):
    """Competitions-section slugs with their skip-listed state.

    Same tokenisation the harness loader uses: one `- <slug> — …` bullet per
    competition under `## Competitions`; a slug counts as skip-listed when its
    line contains "skipped". Dashes elsewhere (skip rules, diversity notes)
    are prose, never manifest entries.
    """
    try:
        lines = index_path.read_text(encoding="utf-8").splitlines()
    except OSError as exc:
        fail(errors, f"registry: index file unreadable: {index_path} ({exc})")
        return []
    try:
        start = next(
            i for i, line in enumerate(lines) if line.strip() == "## Competitions"
        )
    except StopIteration:
        fail(errors, f"registry: {index_path} carries no '## Competitions' section")
        return []
    section = []
    for line in lines[start + 1:]:
        if line.startswith("## "):
            break
        section.append(line)
    rows = []
    for line in section:
        if not line.startswith("- "):
            continue
        slug = line[2:].split(" ")[0]
        if slug:
            rows.append((slug, "skipped" in line.lower()))
    return rows


def corpus_repo_root(corpus_dir, errors):
    if corpus_dir.name == "GliderscoreFixtures" and corpus_dir.parent.name == "tests":
        return corpus_dir.parent.parent
    fail(
        errors,
        f"registry: cannot derive the repo root from corpus dir {corpus_dir} "
        f"(expected <root>/tests/GliderscoreFixtures)",
    )
    return None


def check_registry_snapshot(corpus_dir, slug, errors):
    """The oracle's declared snapshot scope must describe the drawn rounds.

    expected-result.json declares scoredWindow + lifecycle + excludedRounds
    (the GS 02 contract); the drawn rounds come from scores-raw.json. An
    excluded-round list that contradicts the window, or a window outside the
    drawn range, fails naming the fixture and the field. Numerical
    expectations keep their one authoritative location — this check compares
    declarations, never values.
    """
    fixture = corpus_dir / slug
    try:
        oracle = json.loads((fixture / "expected-result.json").read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError, UnicodeDecodeError) as exc:
        fail(errors, f"registry: fixture {slug!r}: field expected-result.json is unreadable ({exc})")
        return
    if not isinstance(oracle, dict):
        fail(errors, f"registry: fixture {slug!r}: field expected-result.json must hold a JSON object")
        return
    window = oracle.get("scoredWindow")
    if (
        not isinstance(window, dict)
        or not _is_int(window.get("firstRound"))
        or not _is_int(window.get("lastRound"))
    ):
        fail(
            errors,
            f"registry: fixture {slug!r}: field expected-result.json.scoredWindow "
            f"must declare integer firstRound/lastRound (the snapshot scope)",
        )
        return
    lifecycle = oracle.get("lifecycle")
    if not isinstance(lifecycle, str) or not lifecycle.strip():
        fail(
            errors,
            f"registry: fixture {slug!r}: field expected-result.json.lifecycle "
            f"must be a non-empty string (how the comparison reads the oracle)",
        )
    excluded = oracle.get("excludedRounds")
    if not isinstance(excluded, list) or any(not _is_int(round_no) for round_no in excluded):
        fail(
            errors,
            f"registry: fixture {slug!r}: field expected-result.json.excludedRounds "
            f"must list integer round numbers",
        )
        return
    try:
        scores_raw = json.loads((fixture / "scores-raw.json").read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError, UnicodeDecodeError) as exc:
        fail(errors, f"registry: fixture {slug!r}: field scores-raw.json is unreadable ({exc})")
        return
    drawn = sorted({
        row.get("RoundNo")
        for row in (scores_raw.get("rows") or [])
        if isinstance(row, dict) and _is_int(row.get("RoundNo"))
    })
    if not drawn:
        return
    first, last = window["firstRound"], window["lastRound"]
    if first > last or first not in drawn or last not in drawn:
        fail(
            errors,
            f"registry: fixture {slug!r}: field expected-result.json.scoredWindow "
            f"[{first},{last}] is incompatible with the drawn rounds "
            f"{drawn[0]}-{drawn[-1]} (bounds must be drawn rounds)",
        )
    want_excluded = sorted(round_no for round_no in drawn if round_no < first or round_no > last)
    if sorted(excluded) != want_excluded:
        fail(
            errors,
            f"registry: fixture {slug!r}: field expected-result.json.excludedRounds "
            f"{sorted(excluded)} contradicts the drawn rounds outside the declared "
            f"window (expected {want_excluded})",
        )


def check_registry_entry_refs(corpus_dir, seed_dir, slug, entry, errors):
    """Cross-file references an entry declares must resolve.

    Parallel-run seeds resolve to a seed class AND the fixture's own
    parallel-run ledger; parallelRun replay declarations require a
    parallel-run mode to own them; the teamLadder grain requires its team
    oracle. Every failure names the fixture and the field.
    """
    modes = entry.get("modes") if isinstance(entry, dict) else None
    has_parallel = False
    if isinstance(modes, list):
        for position, mode in enumerate(modes):
            if not isinstance(mode, dict) or mode.get("mode") != "parallel-run":
                continue
            has_parallel = True
            seed = mode.get("seed")
            if not isinstance(seed, str) or not seed.strip():
                continue
            if not (seed_dir / f"{seed}.json").is_file():
                fail(
                    errors,
                    f"registry: fixture {slug!r}: field modes[{position}].seed {seed!r} "
                    f"names no seed class (missing {seed}.json under tools/Soarscore.SeedData/json)",
                )
            if not (corpus_dir / slug / "parallel-run" / f"{seed}.json").is_file():
                fail(
                    errors,
                    f"registry: fixture {slug!r}: field modes[{position}].seed {seed!r} "
                    f"has no parallel-run ledger ({slug}/parallel-run/{seed}.json)",
                )
    replay = entry.get("replay") if isinstance(entry, dict) else None
    parallel = replay.get("parallelRun") if isinstance(replay, dict) else None
    if isinstance(parallel, dict) and parallel and not has_parallel:
        fail(
            errors,
            f"registry: fixture {slug!r}: field replay.parallelRun declares "
            f"{sorted(parallel)} but no parallel-run mode owns them (orphaned replay declaration)",
        )
    comparison = entry.get("comparison") if isinstance(entry, dict) else None
    grains = comparison.get("grains") if isinstance(comparison, dict) else None
    # gs_10 — the parallel "teams" grain reads the same GS team-ladder oracle
    # as the parity "teamLadder" grain, so both tokens require it.
    team_grains = (
        {g for g in grains if g in ("teamLadder", "teams")}
        if isinstance(grains, list)
        else set()
    )
    if team_grains and not (corpus_dir / slug / "expected-teams.json").is_file():
        fail(
            errors,
            f"registry: fixture {slug!r}: field comparison.grains claims "
            f"{sorted(team_grains)} but {slug}/expected-teams.json is absent (the team grains' oracle)",
        )


def check_registry_scenarios(features_dir, slugs_active, parallel_pairs, errors):
    """Every executable declaration needs its literal-record scenario.

    Active slugs need a replay scenario; parallel-run modes need a
    parallel-run scenario under their seed; scenarios naming unlisted slugs
    are orphaned references. Failures name the fixture.
    """
    replay_path = features_dir / "ReplayingAGliderscoreFixture.feature"
    parallel_path = features_dir / "ParallelRunningAGliderscoreFixture.feature"
    try:
        replay_text = replay_path.read_text(encoding="utf-8")
        parallel_text = parallel_path.read_text(encoding="utf-8")
    except OSError as exc:
        fail(errors, f"registry: scenario files unreadable under {features_dir} ({exc})")
        return set(), set()
    replayed = set(REPLAY_SCENARIO.findall(replay_text))
    paired = set(PARALLEL_SCENARIO.findall(parallel_text))
    for slug in sorted(slugs_active):
        if slug not in replayed:
            fail(
                errors,
                f"registry: active fixture {slug!r} has no executable replay scenario "
                f"(field modes: add the ReplayingAGliderscoreFixture scenario)",
            )
    for slug, seed in sorted(parallel_pairs):
        if (slug, seed) not in paired:
            fail(
                errors,
                f"registry: fixture {slug!r} declares parallel-run seed {seed!r} "
                f"(field modes) with no executable parallel-run scenario",
            )
    known = set(slugs_active) | {slug for slug, _ in parallel_pairs}
    for slug in sorted((replayed | {slug for slug, _ in paired}) - known):
        fail(
            errors,
            f"registry: scenario names {slug!r} but the registry lists no such fixture "
            f"(orphaned scenario reference)",
        )
    return replayed, paired


def validate_corpus(corpus_dir, index_path, errors, warnings):
    """Corpus gate: registry schema, duplicates, index agreement, per-active
    entry references + snapshot scope, and scenario coverage. Returns
    (active_count, skipped_count) for the PASS summary.
    """
    registry_path = corpus_dir / REGISTRY_FILE
    if not registry_path.is_file():
        fail(errors, f"registry: missing file: {REGISTRY_FILE} beside {index_path.name}")
        return 0, 0
    try:
        document = json.loads(registry_path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        fail(errors, f"registry: {REGISTRY_FILE} is not valid JSON: {exc}")
        return 0, 0
    entries = check_registry_schema(document, errors)
    if entries is None:
        return 0, 0
    by_slug = {}
    for entry in entries:
        if not isinstance(entry, dict):
            continue
        slug = entry.get("slug")
        if not isinstance(slug, str) or not slug:
            continue
        if slug in by_slug:
            fail(errors, f"registry: duplicate slug {slug!r} (field entries.slug)")
        else:
            by_slug[slug] = entry
    index_rows = parse_index_manifest(index_path, errors)
    index_by_slug = dict(index_rows)
    if len(index_by_slug) != len(index_rows):
        fail(errors, "registry: index.md lists the same slug twice under ## Competitions")
    for slug in sorted(set(by_slug) | set(index_by_slug)):
        in_registry = slug in by_slug
        in_index = slug in index_by_slug
        if not in_registry:
            fail(
                errors,
                f"registry: fixture {slug!r} is listed in index.md but has no "
                f"{REGISTRY_FILE} entry (field entries)",
            )
        elif not in_index:
            fail(
                errors,
                f"registry: fixture {slug!r} has a {REGISTRY_FILE} entry but no "
                f"index.md bullet (the index stays the human-readable manifest)",
            )
        elif (by_slug[slug].get("status") == "active") == index_by_slug[slug]:
            fail(
                errors,
                f"registry: fixture {slug!r} status disagrees: index.md says "
                f"{'skipped' if index_by_slug[slug] else 'active'} but "
                f"{REGISTRY_FILE} status is {by_slug[slug].get('status')!r} (field status)",
            )
    repo_root = corpus_repo_root(corpus_dir, errors)
    seed_dir = repo_root / "tools" / "Soarscore.SeedData" / "json" if repo_root else None
    features_dir = (
        repo_root / "tests" / "Soarscore.Acceptance.Tests" / "Features"
        if repo_root else None
    )
    if seed_dir is not None and not seed_dir.is_dir():
        fail(errors, f"registry: seed class directory missing: {seed_dir}")
        seed_dir = None
    if features_dir is not None and not features_dir.is_dir():
        fail(errors, f"registry: scenario directory missing: {features_dir}")
        features_dir = None
    active = sorted(
        slug for slug, entry in by_slug.items() if entry.get("status") == "active"
    )
    for slug in active:
        fixture = corpus_dir / slug
        if not fixture.is_dir():
            fail(
                errors,
                f"registry: active fixture {slug!r} has no directory under {corpus_dir.name} "
                f"(field slug)",
            )
            continue
        if not (fixture / "class-definition.json").is_file():
            fail(
                errors,
                f"registry: active fixture {slug!r} carries no class-definition.json "
                f"(nothing executable to replay)",
            )
        check_registry_snapshot(corpus_dir, slug, errors)
        if seed_dir is not None:
            check_registry_entry_refs(corpus_dir, seed_dir, slug, by_slug[slug], errors)
    parallel_pairs = sorted({
        (slug, mode.get("seed"))
        for slug, entry in by_slug.items()
        if isinstance(entry.get("modes"), list)
        for mode in entry["modes"]
        if isinstance(mode, dict)
        and mode.get("mode") == "parallel-run"
        and isinstance(mode.get("seed"), str)
        and mode["seed"].strip()
    })
    if features_dir is not None:
        check_registry_scenarios(features_dir, active, parallel_pairs, errors)
    skipped = sorted(
        slug for slug, entry in by_slug.items() if entry.get("status") == "skipped"
    )
    return len(active), len(skipped)


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


def build_mini_corpus(root):
    """A complete miniature corpus: registry + index + one active fixture
    (parity and parallel-run modes) + one skipped slug without a directory +
    the scenario and seed files the cross-references resolve against."""
    corpus = root / "tests" / "GliderscoreFixtures"
    features = root / "tests" / "Soarscore.Acceptance.Tests" / "Features"
    seeds = root / "tools" / "Soarscore.SeedData" / "json"
    features.mkdir(parents=True)
    seeds.mkdir(parents=True)

    write_fixture(corpus, "mini-active", TRIAGE_OFF, pilot_teams=(0, 0))
    active = corpus / "mini-active"
    rows = [
        {
            "CompNo": 1, "TaskNo": 1, "RoundNo": round_no, "GroupNo": 1,
            "ReFlightNo": 0, "PilotNo": pilot_no, "SeqNo": pilot_no, "Landing": 0,
        }
        for round_no in (1, 2)
        for pilot_no in (1, 2)
    ]
    scores_doc = json.loads((active / "scores-raw.json").read_text(encoding="utf-8"))
    scores_doc["rows"] = rows
    (active / "scores-raw.json").write_text(json.dumps(scores_doc, indent=2) + "\n", encoding="utf-8")
    expected_doc = json.loads((active / "expected-scores.json").read_text(encoding="utf-8"))
    expected_doc["scores"] = {
        f"1/{row['RoundNo']}/1/0/{row['PilotNo']}": {"RawScore": 0.0, "NormalisedScore": 0.0}
        for row in rows
    }
    (active / "expected-scores.json").write_text(
        json.dumps(expected_doc, indent=2) + "\n", encoding="utf-8"
    )
    (active / "expected-result.json").write_text(json.dumps({
        "scoredWindow": {"firstRound": 1, "lastRound": 2},
        "lifecycle": "finalised-full",
        "excludedRounds": [],
    }, indent=2) + "\n", encoding="utf-8")
    (active / "class-definition.json").write_text("{}\n", encoding="utf-8")
    (active / "parallel-run").mkdir()
    (active / "parallel-run" / "mini-seed.json").write_text("{}\n", encoding="utf-8")

    (seeds / "mini-seed.json").write_text(
        json.dumps({"name": "mini-seed"}, indent=2) + "\n", encoding="utf-8"
    )
    (features / "ReplayingAGliderscoreFixture.feature").write_text(
        "Feature: self-test\n"
        "  Scenario: mini\n"
        '    When the harness replays the GliderScore fixture "mini-active"\n',
        encoding="utf-8",
    )
    (features / "ParallelRunningAGliderscoreFixture.feature").write_text(
        "Feature: self-test\n"
        "  Scenario: mini pair\n"
        '    When the harness parallel-runs the GliderScore fixture "mini-active"'
        ' under the seed class "mini-seed"\n',
        encoding="utf-8",
    )
    write_mini_registry(corpus, [
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
    ])
    (corpus / "index.md").write_text(
        "# self-test index\n\n## Competitions\n\n"
        "- mini-active \u2014 active \u2014 self-test\n"
        "- mini-skipped \u2014 skipped \u2014 self-test concept gap\n",
        encoding="utf-8",
    )
    return corpus


def write_mini_registry(corpus, entries):
    (corpus / REGISTRY_FILE).write_text(
        json.dumps({"schemaVersion": 1, "entries": entries}, indent=2) + "\n",
        encoding="utf-8",
    )


def rewrite_json(path, mutate):
    document = json.loads(path.read_text(encoding="utf-8"))
    mutate(document)
    path.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")


def self_test_corpus(run, root):
    """GS 04 Step 3 — deliberate incomplete/contradictory registries: each
    must fail naming the fixture and the field."""
    base = root / "mini-base"
    corpus = build_mini_corpus(base)
    run(
        "miniature corpus passes the registry gate",
        ["--index", str(corpus / "index.md")], 0, ("corpus: PASS",), ("FAIL",),
    )

    def fork(name):
        case = root / name
        if case.exists():
            shutil.rmtree(case)
        shutil.copytree(base, case)
        return case / "tests" / "GliderscoreFixtures"

    forked = fork("case-incomplete")
    rewrite_json(forked / REGISTRY_FILE, lambda doc: doc["entries"][0].pop("modes"))
    run(
        "registry entry missing its modes fails naming fixture and field",
        ["--index", str(forked / "index.md")], 1,
        ("mini-active", "modes"),
    )

    forked = fork("case-duplicate")
    rewrite_json(
        forked / REGISTRY_FILE,
        lambda doc: doc["entries"].append(dict(doc["entries"][0])),
    )
    run(
        "duplicate registry slug fails naming the slug",
        ["--index", str(forked / "index.md")], 1,
        ("duplicate", "mini-active"),
    )

    forked = fork("case-orphan-seed")
    rewrite_json(
        forked / REGISTRY_FILE,
        lambda doc: doc["entries"][0]["modes"][1].update(seed="ghost-seed"),
    )
    run(
        "parallel-run mode with no seed class or ledger fails naming fixture and field",
        ["--index", str(forked / "index.md")], 1,
        ("mini-active", "ghost-seed", "seed"),
    )

    forked = fork("case-orphan-replay")
    def strip_mode_add_replay(doc):
        doc["entries"][0]["modes"] = [{"mode": "parity"}]
        doc["entries"][0]["replay"] = {
            "parallelRun": {
                "skipParityRoundBinds": {"value": True, "basisRef": "self-test"}
            }
        }
    rewrite_json(forked / REGISTRY_FILE, strip_mode_add_replay)
    run(
        "parallelRun replay without a parallel-run mode fails naming fixture and field",
        ["--index", str(forked / "index.md")], 1,
        ("mini-active", "replay.parallelRun"),
    )

    forked = fork("case-snapshot")
    rewrite_json(
        forked / "mini-active" / "expected-result.json",
        lambda doc: doc.update(excludedRounds=[2]),
    )
    run(
        "snapshot scope contradicting the drawn rounds fails naming fixture and field",
        ["--index", str(forked / "index.md")], 1,
        ("mini-active", "excludedRounds"),
    )

    forked = fork("case-no-scenario")
    features = forked.parent / "Soarscore.Acceptance.Tests" / "Features"
    (features / "ReplayingAGliderscoreFixture.feature").write_text(
        "Feature: self-test\n", encoding="utf-8"
    )
    run(
        "active fixture without its replay scenario fails naming the fixture",
        ["--index", str(forked / "index.md")], 1,
        ("mini-active", "scenario"),
    )

    forked = fork("case-index-drift")
    (forked / "index.md").write_text(
        "# self-test index\n\n## Competitions\n\n"
        "- mini-active \u2014 skipped \u2014 drifted\n"
        "- mini-skipped \u2014 skipped \u2014 self-test concept gap\n",
        encoding="utf-8",
    )
    run(
        "index status disagreeing with the registry fails naming the slug",
        ["--index", str(forked / "index.md")], 1,
        ("mini-active", "status"),
    )

    forked = fork("case-missing-dir")
    def add_ghost(doc):
        doc["entries"].append({
            "slug": "ghost-fixture",
            "status": "active",
            "modes": [{"mode": "parity"}],
            "sourceRef": "self-test",
            "oracleRef": "self-test",
            "comparison": {"grains": []},
            "evidenceLinks": [],
            "replay": {},
        })
    rewrite_json(forked / REGISTRY_FILE, add_ghost)
    with (forked / "index.md").open("a", encoding="utf-8") as handle:
        handle.write("- ghost-fixture \u2014 active \u2014 self-test\n")
    run(
        "active registry entry without a fixture directory fails naming the slug",
        ["--index", str(forked / "index.md")], 1,
        ("ghost-fixture",),
    )


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

        self_test_corpus(run, root)

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


def validate_fixture(fixture_dir, index_path):
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


def run_corpus(index_path):
    """Corpus gate (GS 04 Step 3): the registry checks plus rules 1-6 for
    every registry-listed fixture that carries a directory, in registry
    order. Deterministic: registry order throughout, sorted sets elsewhere.
    """
    corpus_dir = index_path.resolve().parent
    errors: list = []
    warnings: list = []
    active_count, skipped_count = validate_corpus(corpus_dir, index_path, errors, warnings)
    fixture_code = 0
    try:
        registry = json.loads((corpus_dir / REGISTRY_FILE).read_text(encoding="utf-8"))
        slugs = [
            entry.get("slug")
            for entry in (registry.get("entries") or [])
            if isinstance(entry, dict) and isinstance(entry.get("slug"), str)
        ]
    except (OSError, json.JSONDecodeError, UnicodeDecodeError):
        slugs = []
    for slug in slugs:
        if not (corpus_dir / slug).is_dir():
            continue
        if validate_fixture(corpus_dir / slug, index_path) != 0:
            fixture_code = 1
    for warning in warnings:
        print(warning, file=sys.stderr)
    if errors:
        for message in errors:
            print(f"FAIL {message}", file=sys.stderr)
        print(f"validate.py: corpus: {len(errors)} error(s)", file=sys.stderr)
        return 1
    if fixture_code != 0:
        print("validate.py: corpus: fixture rule checks failed (see above)", file=sys.stderr)
        return 1
    print(
        f"validate.py: corpus: PASS "
        f"(registry schema v1; {active_count} active, {skipped_count} skipped; "
        f"index, snapshot scopes, references and scenarios agree)"
    )
    return 0


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Validate a curated GliderScore fixture directory (schema v1)."
    )
    parser.add_argument(
        "fixture_dir", nargs="?", default=None,
        help="fixture directory containing the curated JSON files; "
        "omit with --index to validate the whole corpus via the registry",
    )
    parser.add_argument(
        "--index", default=None,
        help="path to tests/GliderscoreFixtures/index.md; required to prove rule-5 skip-listing, "
        "or alone to run the whole-corpus registry gate",
    )
    parser.add_argument(
        "--self-test", action="store_true",
        help="build throwaway fixtures in a temp directory and prove rule 5 both directions",
    )
    args = parser.parse_args(argv)

    if args.self_test:
        return self_test()
    if not args.fixture_dir:
        if not args.index:
            parser.error("a fixture directory is required (or pass --index for the corpus gate, --self-test)")
        return run_corpus(Path(args.index))

    fixture_dir = Path(args.fixture_dir)
    if not fixture_dir.is_dir():
        raise SystemExit(f"validate.py: no such fixture directory: {fixture_dir}")

    index_path = Path(args.index) if args.index else None
    return validate_fixture(fixture_dir, index_path)


if __name__ == "__main__":
    sys.exit(main())
