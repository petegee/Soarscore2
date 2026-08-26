#!/usr/bin/env python3
r"""Converter: download-CSV records -> human-inspection triage JSON (WI-4).

Pure conversion + draw-completeness checking + best-effort task scraping;
no network, no filesystem. Input is one competition's list of
csvparse.DownloadRecord. Semantics are authoritative per
kanban/in-progress/gliderscore-webmine-tool.md §"Validation of the mining
approach" and gliderscore-online-data-mining.md §2.4:

- Duration classes (F3J/F5J/ALES/Thml) pack Time1Mins/Time1Secs/Time2Mins/
  Time2Secs splits plus FlightScoreDeduction + Landing into Data2..Data7,
  Data1=Laps unused; decoded here as exact integer seconds (mins*60+secs).
- F3K consumes Data1..Data7 as up-to-seven flight times IN SECONDS per task
  letter; the task-letter mapping itself is out of scope for this module.
- F5K/F5K2024 ride Flight1..4; launch heights / bonus tables are NOT in the
  download CSV — recorded as a limitation, never reconstructed.
- Anything else passes through raw only (semantics never guessed).

Nonzero-only decoding rule: zero-valued time/flight slots stay out of
decoded fields/lists but REMAIN in `raw` (mirrors the replay-harness capture
rule: absent vs recorded-zero ambiguity avoided). Every assignment keeps
raw:[data1..data7] as evidence — triage is human-inspection material.

Ordering is a deterministic normalisation: rounds ascend by
(round, group, reflight); assignments within a bucket ascend by a full-field
canonical key (pilot number first), so permuted inputs converge to the same
document. The wire file order stays preserved verbatim in <CompID>_records.json.

check_draw_completeness is informational-plus-hard: duplicate base slots
(reflight 0) sharing (round, group, pilot) and domain-bound breaches
(round/group/pilot < 1, reflight < 0) are HARD violations; per-pilot base-slot
summaries and below-max round counts are SOFT gaps that never fail a run.
Groups vary per round in real data (documented jerilderie phantom-group
case), so absence of a pilot from a specific group is deliberately NOT
flagged — only whole-round shortfalls are.

scrape_tasks parses one pilot's eScoring.aspx screen tolerant-of-noise:
strip script/style, tags -> newlines, find (?i)\bround\s*(\d+)\b markers,
take each marker's nearest following non-empty text line (until the next
marker/EOF) as the task, leading dash/colon separators stripped, last-wins
on duplicated round numbers. It NEVER raises on weird html: unparsable
structure returns {} plus a single-line stderr warning.
"""

import re

import csvparse

__all__ = [
    "DURATION_TYPES",
    "F3K_TYPE",
    "F5K_TYPES",
    "TASK_SCRAPE_EMPTY_LIMITATION",
    "convert_records",
    "check_draw_completeness",
    "scrape_tasks",
]

# Family routing is exact-match on the wire's CompType vocabulary (same
# case-sensitivity discipline as the safety kernel): an unknown spelling
# degrades to passthrough, never to a guessed decode.
DURATION_TYPES = frozenset({"F3J", "F5J", "ALES", "Thml"})
F3K_TYPE = "F3K"
F5K_TYPES = frozenset({"F5K", "F5K2024"})

TASK_SCRAPE_EMPTY_LIMITATION = "task scrape found nothing recognisable"

_LIMIT_DURATION = (
    "duration-family decode: Data2/Data3 and Data4/Data5 read as packed "
    "minute/second splits into time1Seconds/time2Seconds (exact ints), "
    "Data6=FlightScoreDeduction kept unchanged (sign untouched), "
    "Data7=Landing, Data1=Laps passthrough"
)
_LIMIT_F3K = (
    "F3K decode: Data1..7 read as up-to-seven flight times in seconds in "
    "slot order (zeros dropped); task-letter mapping NOT resolved here — "
    "read it from scraped tasksByRound or contest notes"
)
_LIMIT_F5K_TEMPLATE = (
    "{comp} decode: Flight1..4 read as flight-time seconds; launch heights "
    "not in download CSV, so launch-height and bonus score inputs are absent"
)
_LIMIT_UNKNOWN_TEMPLATE = (
    "{comp}: unconverted passthrough — Data1..7 semantics unmapped for this "
    "CompType, values left raw (never guessed)"
)

_EMPTY_SCRAPE_WARNING = (
    "task scrape: no recognisable round/task structure in the pilot screen "
    "html; returning no tasks"
)

_SCRIPT_OR_STYLE_RE = re.compile(r"(?is)<(script|style)[^>]*>.*?</\1\s*>")
_TAG_RE = re.compile(r"<[^>]+>")
_ROUND_MARKER_RE = re.compile(r"(?i)\bround\s*(\d+)\b")
_ENTITIES = (("&nbsp;", " "), ("&amp;", "&"), ("&quot;", '"'), ("&#39;", "'"))
_TASK_LEADING_JUNK = " \t-\u2013\u2014:"


def _raw_slots(record):
    return [record.data1, record.data2, record.data3, record.data4,
            record.data5, record.data6, record.data7]


def _decode_duration_slots(record):
    fields = {}

    def put(key, value):
        if value != 0:
            fields[key] = value

    put("laps", record.data1)
    put("time1Seconds", round(record.data2) * 60 + round(record.data3))
    put("time2Seconds", round(record.data4) * 60 + round(record.data5))
    put("deduction", record.data6)
    put("landing", record.data7)
    fields["raw"] = _raw_slots(record)
    return fields


def _decode_f3k_slots(record):
    return {"flights": [value for value in _raw_slots(record) if value != 0],
            "raw": _raw_slots(record)}


def _decode_f5k_flights(record):
    flights = [value for value in (record.flight1, record.flight2,
                                   record.flight3, record.flight4)
               if value is not None and value != 0]
    return {"flights": flights, "raw": _raw_slots(record)}


def _decode_passthrough(record):
    return {"raw": _raw_slots(record)}


def _family_router(comp_type):
    """Return (decoder, limitation line) for one CompType."""
    if comp_type in DURATION_TYPES:
        return _decode_duration_slots, _LIMIT_DURATION
    if comp_type == F3K_TYPE:
        return _decode_f3k_slots, _LIMIT_F3K
    if comp_type in F5K_TYPES:
        decoder = _decode_f5k_flights
        limitation = _LIMIT_F5K_TEMPLATE.format(comp=comp_type)
        return decoder, limitation
    return _decode_passthrough, _LIMIT_UNKNOWN_TEMPLATE.format(comp=comp_type)


def _common_fields(record):
    return {
        "pilotNo": record.pilot_no,
        "pilotName": record.pilot_name,
        "penalty": record.penalty,
        "landingOver75m": record.landing_over_75m,
        "f5jMotorReStarted": record.f5j_motor_re_started,
    }


def _sort_float(value):
    """Total-order wrapper for possibly-absent (None) flight slots: present
    slots compare by value and all precede absent ones. Booleans in the flag
    fields need no wrapper — bool is an int subclass, so False/True sort
    against numeric floats natively."""
    return (0, value) if value is not None else (1,)


def _assignment_sort_key(record):
    # Canonical, permutation-proof ordering inside one bucket: pilot first,
    # then every compared field, so equal-key rows converge deterministically.
    return (
        record.pilot_no, record.pilot_name, record.penalty,
        record.landing_over_75m, record.f5j_motor_re_started,
        record.model_id,
        record.data1, record.data2, record.data3, record.data4,
        record.data5, record.data6, record.data7,
        _sort_float(record.flight1), _sort_float(record.flight2),
        _sort_float(record.flight3), _sort_float(record.flight4),
    )


def convert_records(records):
    """Convert one competition's DownloadRecords into the triage document.

    Raises SystemExit on an empty record set (loudness per WI-3 escalation)
    and csvparse.CsvParseError when records span multiple comps/types.
    """
    records = list(records)
    if not records:
        raise SystemExit("no records — nothing to triage")
    comp_ids = sorted({record.comp_id for record in records})
    if len(comp_ids) > 1:
        raise csvparse.CsvParseError(
            "records span multiple comps: " + ", ".join(comp_ids)
        )
    comp_id = comp_ids[0]
    comp_type = csvparse.uniform_comp_type(records)

    decode, limitation = _family_router(comp_type)

    pilot_names = {}
    buckets = {}
    for record in records:
        pilot_names.setdefault(record.pilot_no, record.pilot_name)
        key = (record.round_no, record.group_no, record.reflight_no)
        buckets.setdefault(key, []).append(record)

    rounds = []
    for key in sorted(buckets):
        round_no, group_no, reflight_no = key
        assignments = [
            {**_common_fields(record), **decode(record),
             "raw": _raw_slots(record)}
            for record in sorted(buckets[key], key=_assignment_sort_key)
        ]
        rounds.append({
            "round": round_no,
            "group": group_no,
            "reflight": reflight_no,
            "assignments": assignments,
        })

    return {
        "compId": comp_id,
        "compType": comp_type,
        "name": None,
        "pilots": [{"pilotNo": pilot_no, "name": pilot_names[pilot_no]}
                   for pilot_no in sorted(pilot_names)],
        "rounds": rounds,
        "limitations": [limitation],
    }


def check_draw_completeness(records):
    """{"violations": [...], "gaps": [...]} — violations HARD, gaps soft.

    Violations: two rows with reflight 0 sharing (round, group, pilot);
    any reflight < 0; any round/group/pilot < 1 (pilot numbers are global
    DB ids >= 1). Gaps: per-pilot base-slot summaries deduped once per
    (pilot, round), plus a named shortfall flag for every pilot whose base
    round count is below the maximum flown by anyone.
    """
    violations = []
    gaps = []
    if not records:
        return {"violations": violations, "gaps": gaps}

    names = {}
    rows_with_reflight_below_zero = []
    base_rows_by_pilot = {}
    base_slot_counts = {}

    def note_domain(violation_text):
        violations.append(violation_text)

    for record in records:
        names.setdefault(record.pilot_no, record.pilot_name)
        r, g, rf, p = (record.round_no, record.group_no,
                       record.reflight_no, record.pilot_no)
        if rf < 0:
            rows_with_reflight_below_zero.append(
                f"negative reflight {rf} for (round {r}, group {g}, pilot {p})"
            )
        if r < 1:
            note_domain(f"round number {r} below 1 for (group {g}, "
                        f"pilot {p}, reflight {rf})")
        if g < 1:
            note_domain(f"group number {g} below 1 for (round {r}, "
                        f"pilot {p}, reflight {rf})")
        if p < 1:
            note_domain(f"pilot number {p} below 1 for (round {r}, "
                        f"group {g}, reflight {rf})")
        if rf == 0:
            triple = (r, g, p)
            base_slot_counts[triple] = base_slot_counts.get(triple, 0) + 1
            base_rows_by_pilot.setdefault(p, {}).setdefault(r, 0)
            base_rows_by_pilot[p][r] += 1

    for (r, g, p), count in sorted(base_slot_counts.items()):
        if count > 1:
            violations.append(
                f"duplicate base slot: {count} rows with reflight 0 share "
                f"(round {r}, group {g}, pilot {p})"
            )
    violations.extend(rows_with_reflight_below_zero)

    if base_rows_by_pilot:
        max_rounds = max(len(rounds_map) for rounds_map in base_rows_by_pilot.values())
        holder = min(
            pilot_no for pilot_no, rounds_map in base_rows_by_pilot.items()
            if len(rounds_map) == max_rounds
        )
        for pilot_no in sorted(base_rows_by_pilot):
            per_round = base_rows_by_pilot[pilot_no]
            summary = " ".join(f"r{round_no}:{count}"
                               for round_no, count in sorted(per_round.items()))
            gaps.append(
                f"pilot {pilot_no} ({names[pilot_no]}): base slots per round "
                f"{summary}; base rounds total {len(per_round)}"
            )
            if len(per_round) < max_rounds:
                missing = sorted(
                    {round_no for other in base_rows_by_pilot.values()
                     for round_no in other}
                    - set(per_round)
                )
                gaps.append(
                    f"pilot {pilot_no} ({names[pilot_no]}) flew base slots in "
                    f"{len(per_round)} of {max_rounds} rounds (max held by "
                    f"pilot {holder}); missing rounds: {missing}"
                )

    return {"violations": violations, "gaps": gaps}


def scrape_tasks(html, *, stderr=None):
    """{int round: str task_description} from one eScoring.aspx pilot screen.

    Best-effort by design: {} plus a one-line stderr warning whenever no
    round/task structure can be recognised; NEVER raises on weird html.
    """
    try:
        return _scrape_tasks_inner(html, stderr)
    except Exception as exc:  # tolerance is the contract
        if stderr is not None:
            try:
                stderr.write(
                    f"task scrape: unexpected parser error ({exc!r}); "
                    f"returning no tasks\n"
                )
            except Exception:
                pass
        return {}


def _scrape_tasks_inner(html, stderr):
    if html is None:
        text = ""
    elif isinstance(html, bytes):
        text = html.decode("utf-8", errors="replace")
    elif isinstance(html, str):
        text = html
    else:
        text = str(html)
    text = _SCRIPT_OR_STYLE_RE.sub(" ", text)
    text = _TAG_RE.sub("\n", text)
    for entity, replacement in _ENTITIES:
        text = text.replace(entity, replacement)

    matches = list(_ROUND_MARKER_RE.finditer(text))
    tasks = {}
    for index, match in enumerate(matches):
        end = match.end()
        if index + 1 < len(matches):
            next_start = matches[index + 1].start()
        else:
            next_start = len(text)
        task = None
        for line in text[end:next_start].splitlines():
            candidate = " ".join(line.split())
            if not candidate:
                continue
            candidate = candidate.lstrip(_TASK_LEADING_JUNK)
            if candidate:
                task = candidate
                break
        # Last-wins on duplicated round markers; an occurrence that finds no
        # task text does not clobber a task already scraped for that round.
        if task is not None:
            tasks[int(match.group(1))] = task
    if not tasks and stderr is not None:
        stderr.write(_EMPTY_SCRAPE_WARNING + "\n")
    return tasks
