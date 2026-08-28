#!/usr/bin/env python3
"""Strict parser for the GliderScore download CSV (pipe-delimited, headerless).

Wire contract — behaviour cited from
kanban/in-progress/gliderscore-webmine-tool.md §"Validation of the mining
approach" (the authoritative cross-reference) and
gliderscore-online-data-mining.md §2.4:

- Exactly 22 pipe-delimited fields per line, no header row. Column order:
      0 comp_id            1 comp_type     2 round_no    3 group_no
      4 reflight_no        5 pilot_no      6..12 data1..data7
          (class-dependent raw inputs; for F3K these are the pilot's flight
           times in seconds, one slot per task score)
      13 landing_over_75m  14 penalty (may be negative)   15 pilot_name
      16 model_id          17..20 flight1..flight4 (F3K/F5K flights)
      21 f5j_motor_re_started (int 0/1)
- Decimal points only: the server canonicalises decimal commas to "." before
  they reach the wire.
- Wire realities proven live 2026-08-27 against public CompID 2381887cb81b
  (F3K NI Round 2, Haumoana NZ; 41 rows — the earlier "absent values arrive
  as the literal string \"0\"\" assumption was incomplete and is corrected):
  * The two flag-shaped columns landing_over_75m and f5j_motor_re_started
    serialise .NET Booleans: exact-case 'True'/'False' (Boolean.ToString()),
    never '0'/'1' in the observed corpus. Both spellings are accepted:
    exact-case 'True'/'False' -> Python bool; numeric tokens parse as before
    (numbers in mixed representations across comps and rows are allowed and
    preserved as typed). Anything else — lower-case, padded, truncated —
    still raises CsvParseError naming the column.
  * Absent values are NOT uniformly "0": unused Flight1..4 arrive as EMPTY
    STRINGS (the parser maps exactly '' -> None for these four columns only),
    as does an unset ModelID (text column; passes through verbatim).
    Data1..7/Penalty keep their zero-padded decimal placeholder form
    ("0.000"); an empty token in any other numeric column still raises.
- CompID is case-preserving; IDs are opaque and case-sensitive server-side.
- Scores are NOT in this file — only raw inputs plus draw information.

This module trusts nothing: field counts, interior blank lines and
unparseable tokens raise CsvParseError naming the offending column, its
declared index and the offending value. Nothing is skipped or silently
coerced. Line endings may be LF or CRLF (handled via str.splitlines()). A
leading UTF-8 BOM character ("\\ufeff") — present when a caller decodes with
plain "utf-8" instead of "utf-8-sig" — is tolerated at document start only.

Internal field names are snake_case (`COLUMNS`); `SNAKE_TO_CAMEL` /
`CAMEL_COLUMNS` / `record_to_camel_dict` map records onto the fixed camelCase
keys consumed downstream (WI-4). `render_line(record)` is the inverse of
`parse_line` and is exported to support test fixtures.
"""

import dataclasses

__all__ = [
    "COLUMN_COUNT",
    "COLUMNS",
    "CAMEL_COLUMNS",
    "SNAKE_TO_CAMEL",
    "CsvParseError",
    "DownloadRecord",
    "parse_field",
    "parse_line",
    "parse_csv",
    "uniform_comp_type",
    "render_line",
    "record_to_camel_dict",
]

COLUMN_COUNT = 22

# Declared wire positions 0..21, snake_case. Keep DownloadRecord's fields in
# exactly this order so positional construction mirrors the wire.
COLUMNS = (
    "comp_id", "comp_type", "round_no", "group_no", "reflight_no",
    "pilot_no",
    "data1", "data2", "data3", "data4", "data5", "data6", "data7",
    "landing_over_75m", "penalty",
    "pilot_name", "model_id",
    "flight1", "flight2", "flight3", "flight4",
    "f5j_motor_re_started",
)

_INT_COLUMNS = frozenset(
    {"round_no", "group_no", "reflight_no", "pilot_no", "f5j_motor_re_started"}
)
_FLOAT_COLUMNS = frozenset(
    {"data1", "data2", "data3", "data4", "data5", "data6", "data7",
     "landing_over_75m", "penalty", "flight1", "flight2", "flight3", "flight4"}
)
_TEXT_COLUMNS = frozenset({"comp_id", "comp_type", "pilot_name", "model_id"})

# Flag-shaped columns (confirmed live 2026-08-27, CompID 2381887cb81b):
# .NET Booleans serialise as exact-case 'True'/'False' on the wire. Numeric
# tokens ('0'/'1'/…) remain accepted — comps may predate canonicalisation or
# mix representations; each cell keeps whatever type the wire carried.
_FLAG_COLUMNS = frozenset({"landing_over_75m", "f5j_motor_re_started"})
_BOOL_TOKEN_TO_PY = {"True": True, "False": False}

# The only columns proven to carry empty-string absents (unused Flight slots
# in an F3K download): exactly '' -> None, faithful absence rather than a
# silent 0.0 that would blur absent-vs-recorded-zero for downstream decoders.
_EMPTY_AS_NONE_COLUMNS = frozenset(
    {"flight1", "flight2", "flight3", "flight4"}
)

_KIND_BY_COLUMN = {}
for _name in COLUMNS:
    if _name in _INT_COLUMNS:
        _KIND_BY_COLUMN[_name] = "int"
    elif _name in _FLOAT_COLUMNS:
        _KIND_BY_COLUMN[_name] = "number"
    elif _name in _TEXT_COLUMNS:
        _KIND_BY_COLUMN[_name] = "text"
    else:  # pragma: no cover - guards an incomplete kind partition above
        raise AssertionError(f"column {_name!r} has no declared kind")
assert len(_KIND_BY_COLUMN) == COLUMN_COUNT

_DECLARED_INDEX = {name: index for index, name in enumerate(COLUMNS)}

# Fixed camelCase output keys for downstream consumers (WI-4); explicit
# because a generic snake-to-camel rule would misplace digit runs
# (landing_over_75m -> landingOver75M, not landingOver75m).
SNAKE_TO_CAMEL = {
    "comp_id": "compId",
    "comp_type": "compType",
    "round_no": "roundNo",
    "group_no": "groupNo",
    "reflight_no": "reflightNo",
    "pilot_no": "pilotNo",
    "data1": "data1",
    "data2": "data2",
    "data3": "data3",
    "data4": "data4",
    "data5": "data5",
    "data6": "data6",
    "data7": "data7",
    "landing_over_75m": "landingOver75m",
    "penalty": "penalty",
    "pilot_name": "pilotName",
    "model_id": "modelId",
    "flight1": "flight1",
    "flight2": "flight2",
    "flight3": "flight3",
    "flight4": "flight4",
    "f5j_motor_re_started": "f5jMotorReStarted",
}
assert set(SNAKE_TO_CAMEL) == set(COLUMNS)

CAMEL_COLUMNS = tuple(SNAKE_TO_CAMEL[name] for name in COLUMNS)


class CsvParseError(ValueError):
    """A download CSV line/document violates the wire contract."""


@dataclasses.dataclass(frozen=True)
class DownloadRecord:
    """One wire row with every field strictly typed (order = wire order)."""

    comp_id: str
    comp_type: str
    round_no: int
    group_no: int
    reflight_no: int
    pilot_no: int
    data1: float
    data2: float
    data3: float
    data4: float
    data5: float
    data6: float
    data7: float
    landing_over_75m: "float | bool"
    penalty: float
    pilot_name: str
    model_id: str
    flight1: "float | None"
    flight2: "float | None"
    flight3: "float | None"
    flight4: "float | None"
    f5j_motor_re_started: "int | bool"


def _convert(name, index, raw):
    kind = _KIND_BY_COLUMN.get(name)
    if kind is None:
        raise CsvParseError(f"unknown column name {name!r}")
    if raw == "" and name in _EMPTY_AS_NONE_COLUMNS:
        return None
    if kind == "text":
        return raw
    if raw in _BOOL_TOKEN_TO_PY and name in _FLAG_COLUMNS:
        return _BOOL_TOKEN_TO_PY[raw]
    try:
        if kind == "int":
            return int(raw)
        return float(raw)
    except (TypeError, ValueError) as exc:
        raise CsvParseError(
            f"column {name!r} (index {index}): cannot parse {raw!r} as {kind}: {exc}"
        ) from exc


def parse_field(name, raw):
    """Convert one raw wire token to its declared type, or raise CsvParseError.

    Integers come through int(raw) — "5.0" is rejected for int columns.
    Numbers come through float(raw). Text columns pass through verbatim
    (case-preserving). Proven wire additions: exact-case 'True'/'False' map
    to Python bools in the two flag columns only; '' maps to None in the
    four flight columns only. Errors name the column, its declared wire
    index and the offending value.
    """
    return _convert(name, _DECLARED_INDEX[name], raw)


def parse_line(line, line_number):
    """Parse one physical wire line into a DownloadRecord.

    The split must yield exactly COLUMN_COUNT fields — any other count is a
    CsvParseError naming the actual vs expected count, never a silent
    reinterpretation.
    """
    fields = line.split("|")
    if len(fields) != COLUMN_COUNT:
        raise CsvParseError(
            f"line {line_number}: expected exactly {COLUMN_COUNT} "
            f"pipe-delimited fields, found {len(fields)}"
        )
    values = []
    for index, (name, raw) in enumerate(zip(COLUMNS, fields)):
        try:
            values.append(_convert(name, index, raw))
        except CsvParseError as exc:
            raise CsvParseError(f"line {line_number}: {exc}") from exc
    return DownloadRecord(**dict(zip(COLUMNS, values)))


def parse_csv(text):
    """Parse a whole decoded CSV document into a list[DownloadRecord].

    Accepts LF or CRLF endings. Trailing empty lines at EOF are ignored;
    a blank line anywhere else raises CsvParseError. Line numbers in errors
    count physical lines from 1 across the whole document.
    """
    if text.startswith("\ufeff"):
        text = text[1:]
    lines = text.splitlines()
    while lines and lines[-1] == "":
        lines.pop()
    records = []
    for line_number, line in enumerate(lines, start=1):
        if line == "":
            raise CsvParseError(
                f"line {line_number}: blank line inside download CSV"
            )
        records.append(parse_line(line, line_number))
    return records


def uniform_comp_type(records):
    """Return the single comp_type across records; raise if more than one."""
    distinct = sorted({record.comp_type for record in records})
    if len(distinct) > 1:
        raise CsvParseError(
            "records span multiple comp types: " + ", ".join(distinct)
        )
    return distinct[0] if distinct else None


def record_to_camel_dict(record):
    """Flat camelCase dict in wire column order, typed values preserved."""
    return {
        SNAKE_TO_CAMEL[name]: getattr(record, name)
        for name in COLUMNS
    }


def render_line(record):
    """Render a DownloadRecord back to one wire line (test-support inverse).

    Inverse of parse_line over the widened domain: bool -> exact-case
    'True'/'False' (checked before the numeric branches because False == 0),
    None -> '' (the proven empty-string absent form, flight columns only).
    Floats print via repr (shortest round-trip form), except exact zeros
    which use the wire's "0" placeholder convention.
    """
    parts = []
    for name in COLUMNS:
        value = getattr(record, name)
        if isinstance(value, bool):
            parts.append("True" if value else "False")
        elif value is None:
            parts.append("")
        elif _KIND_BY_COLUMN[name] == "text":
            parts.append(value)
        elif value == 0.0:
            parts.append("0")
        elif _KIND_BY_COLUMN[name] == "int":
            parts.append(str(int(value)))
        else:
            parts.append(repr(float(value)))
    return "|".join(parts)
