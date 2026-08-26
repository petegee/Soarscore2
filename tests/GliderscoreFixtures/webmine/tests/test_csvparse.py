#!/usr/bin/env python3
"""Unit + property tests for the webmine download-CSV parser (csvparse).

Self-contained: no imports from test_gsclient. Properties target the WI-3
invariants from kanban/in-progress/gliderscore-webmine-tool.md — no silent
column reordering, record count equals parsed line count, strictness closure
(any structural corruption raises CsvParseError). The wire contract was
validated against the story §"Validation of the mining approach" and then
CORRECTED against live data confirmed 2026-08-27 (CompID 2381887cb81b,
fixture committed at tests/fixtures/): dot decimals, flag columns carry
exact-case 'True'/'False', and unused Flight1..4 slots arrive as '' -> None.
"""

import string
from dataclasses import replace
from pathlib import Path

import pytest
from hypothesis import given, settings
from hypothesis import strategies as st

import csvparse
import triage

COMP_ID = "2381887cb81b"

# Real download CSV captured live 2026-08-27 (CompID 2381887cb81b, F3K NI
# Round 2 Haumoana NZ; public competition scoring data). 41 rows x 22 cols;
# carries the wire realities this suite now encodes: VB 'False' flags,
# empty-string ModelID and Flight1..4 slots.
REAL_FIXTURE_CSV = (
    Path(__file__).resolve().parent / "fixtures" / f"{COMP_ID}_DownloadData.csv"
)

# One distinctive token per column; joined in COLUMNS order these form a
# fully-default valid wire line used by the position-sentinel and
# strictness tests.
DEFAULT_TOKENS = {
    "comp_id": COMP_ID,
    "comp_type": "F3K",
    "round_no": "34",
    "group_no": "7",
    "reflight_no": "2",
    "pilot_no": "151",
    "data1": "249.500",
    "data2": "400.000",
    "data3": "500.250",
    "data4": "0",
    "data5": "0",
    "data6": "0",
    "data7": "0",
    "landing_over_75m": "75.250",
    "penalty": "-30.500",
    "pilot_name": "Pawson-Ødegård Tschüß",
    "model_id": "Vesper AG-04",
    "flight1": "301.000",
    "flight2": "302.500",
    "flight3": "303.750",
    "flight4": "304.125",
    "f5j_motor_re_started": "1",
}

EXPECTED_DEFAULT_RECORD = csvparse.DownloadRecord(
    comp_id=COMP_ID,
    comp_type="F3K",
    round_no=34,
    group_no=7,
    reflight_no=2,
    pilot_no=151,
    data1=249.500,
    data2=400.000,
    data3=500.250,
    data4=0.0,
    data5=0.0,
    data6=0.0,
    data7=0.0,
    landing_over_75m=75.250,
    penalty=-30.500,
    pilot_name="Pawson-Ødegård Tschüß",
    model_id="Vesper AG-04",
    flight1=301.000,
    flight2=302.500,
    flight3=303.750,
    flight4=304.125,
    f5j_motor_re_started=1,
)


def default_line():
    return "|".join(DEFAULT_TOKENS[name] for name in csvparse.COLUMNS)


def document(*lines):
    return "\n".join(lines) + "\n"


def assert_record_typed_equal(parsed, expected):
    assert parsed == expected
    for name in csvparse.COLUMNS:
        got = getattr(parsed, name)
        want = getattr(expected, name)
        assert type(got) is type(want), (
            f"column {name}: parsed {got!r} is {type(got).__name__}, "
            f"expected {want!r} ({type(want).__name__})"
        )
        assert got == want


# ------------------------------------------------------- example-based tests


def test_columns_declared_exactly_22_in_wire_order():
    assert csvparse.COLUMN_COUNT == 22
    assert len(csvparse.COLUMNS) == 22
    assert csvparse.COLUMNS == (
        "comp_id", "comp_type", "round_no", "group_no", "reflight_no",
        "pilot_no",
        "data1", "data2", "data3", "data4", "data5", "data6", "data7",
        "landing_over_75m", "penalty",
        "pilot_name", "model_id",
        "flight1", "flight2", "flight3", "flight4",
        "f5j_motor_re_started",
    )
    # camelCase output keys are fixed for WI-4, including digit runs.
    assert csvparse.SNAKE_TO_CAMEL["landing_over_75m"] == "landingOver75m"
    assert csvparse.SNAKE_TO_CAMEL["f5j_motor_re_started"] == "f5jMotorReStarted"
    assert set(csvparse.CAMEL_COLUMNS) == set(csvparse.SNAKE_TO_CAMEL.values())
    assert len(set(csvparse.CAMEL_COLUMNS)) == 22


def test_column_positions_sentinel_every_field_lands_at_its_name():
    # (a) Positive proof: a fully-default line parses into exactly the
    # expected typed values at every declared position.
    parsed = csvparse.parse_csv(document(default_line()))[0]
    assert_record_typed_equal(parsed, EXPECTED_DEFAULT_RECORD)

    # (b) Negative proof per index: replacing one numeric column's token with
    # an unparseable marker must raise naming THAT column's name and index —
    # silent reordering would surface as a different column blamed.
    marker = "@sentinel@"
    for index, name in enumerate(csvparse.COLUMNS):
        if csvparse._KIND_BY_COLUMN[name] == "text":
            continue  # text columns are proven by (c); ints/floats here
        fields = [DEFAULT_TOKENS[column] for column in csvparse.COLUMNS]
        fields[index] = marker
        with pytest.raises(csvparse.CsvParseError) as caught:
            csvparse.parse_line("|".join(fields), 1)
        message = str(caught.value)
        assert f"column '{name}'" in message, message
        assert f"(index {index})" in message, message
        assert repr(marker) in message, message

    # (c) Text columns prove their landing spot by unique marker values.
    for index, name in enumerate(csvparse.COLUMNS):
        if name not in ("comp_id", "comp_type", "pilot_name", "model_id"):
            continue
        fields = [DEFAULT_TOKENS[column] for column in csvparse.COLUMNS]
        marker = f"text{index}-marker"
        fields[index] = marker
        record = csvparse.parse_line("|".join(fields), 1)
        assert getattr(record, name) == marker


def test_types_zero_placeholders_negatives_and_accents():
    row = "|".join([
        "2381887CB81B",       # case-preserving CompID
        "F5J",
        "12", "3", "1", "82",
        "0", "0", "990.5", "0", "-0.25", "0", "7",   # data1..data7
        "0",                  # landing_over_75m placeholder zero
        "-30",                # negative penalty arrives without decimals
        "Renée Müller Åse",   # accents + spaces survive verbatim
        "V",
        "100.1", "200.2", "300.3", "400.4",
        "0",
    ])
    record = csvparse.parse_line(row, 1)
    assert record.comp_id == "2381887CB81B"
    assert record.comp_type == "F5J"
    assert type(record.round_no) is int and record.round_no == 12
    assert type(record.penalty) is float and record.penalty == -30.0
    assert record.data1 == 0.0 and record.data2 == 0.0   # "0" placeholders
    assert record.landing_over_75m == 0.0
    assert type(record.data3) is float and record.data3 == 990.5
    assert type(record.f5j_motor_re_started) is int
    assert record.pilot_name == "Renée Müller Åse"
    assert record.model_id == "V"


def test_flag_columns_accept_vb_boolean_tokens_exactly_and_numerics():
    fields = [DEFAULT_TOKENS[name] for name in csvparse.COLUMNS]

    # Proven live wire form: exact-case VB tokens in the two flag columns.
    fields[13] = "False"
    fields[21] = "True"
    record = csvparse.parse_line("|".join(fields), 1)
    assert record.landing_over_75m is False
    assert type(record.landing_over_75m) is bool
    assert record.f5j_motor_re_started is True
    assert type(record.f5j_motor_re_started) is bool

    # Swapped flags prove each lands at its own position.
    fields[13] = "True"
    fields[21] = "False"
    record = csvparse.parse_line("|".join(fields), 2)
    assert record.landing_over_75m is True
    assert record.f5j_motor_re_started is False


def test_flag_columns_mixed_representations_within_one_row():
    # Reality may mix numeric and boolean spellings across comps — and
    # within one file/row: every cell keeps the type its own token implies.
    fields = [DEFAULT_TOKENS[name] for name in csvparse.COLUMNS]
    fields[13] = "True"
    fields[21] = "0"
    record = csvparse.parse_line("|".join(fields), 1)
    assert record.landing_over_75m is True
    assert record.f5j_motor_re_started == 0
    assert type(record.f5j_motor_re_started) is int

    fields[13] = "0.0"          # numeric landing keeps float type
    fields[21] = "1"
    record = csvparse.parse_line("|".join(fields), 1)
    assert record.landing_over_75m == 0.0 and type(record.landing_over_75m) is float
    assert record.f5j_motor_re_started == 1 and type(record.f5j_motor_re_started) is int


@pytest.mark.parametrize("bad_token", ["true", "TRUE", "FALSE", "false ", "Yes", "Fals"])
@pytest.mark.parametrize("flag_index", [13, 21])
def test_bool_tokens_are_case_exact_at_flag_positions(flag_index, bad_token):
    fields = [DEFAULT_TOKENS[name] for name in csvparse.COLUMNS]
    fields[flag_index] = bad_token
    with pytest.raises(csvparse.CsvParseError) as caught:
        csvparse.parse_line("|".join(fields), 9)
    message = str(caught.value)
    assert f"column '{csvparse.COLUMNS[flag_index]}'" in message
    assert f"(index {flag_index})" in message
    assert repr(bad_token) in message


def test_boolean_and_empty_tokens_never_leak_into_other_numeric_columns():
    numeric_indexes = [
        index for index, name in enumerate(csvparse.COLUMNS)
        if csvparse._KIND_BY_COLUMN[name] != "text"
        and name not in csvparse._FLAG_COLUMNS
        and name not in csvparse._EMPTY_AS_NONE_COLUMNS
    ]
    for index in numeric_indexes:
        for token in ("True", "False"):
            fields = [DEFAULT_TOKENS[column] for column in csvparse.COLUMNS]
            fields[index] = token
            with pytest.raises(csvparse.CsvParseError) as caught:
                csvparse.parse_line("|".join(fields), 1)
            assert f"column '{csvparse.COLUMNS[index]}'" in str(caught.value)


def test_empty_flight_slots_become_none_nowhere_else():
    fields = [DEFAULT_TOKENS[name] for name in csvparse.COLUMNS]
    for index in (17, 18, 19, 20):
        fields[index] = ""
    record = csvparse.parse_line("|".join(fields), 1)
    for name in ("flight1", "flight2", "flight3", "flight4"):
        assert getattr(record, name) is None

    # Empty tokens stay loud everywhere else (strictness not loosened).
    for index in (2, 5, 6, 7, 14):
        fields = [DEFAULT_TOKENS[column] for column in csvparse.COLUMNS]
        fields[index] = ""
        with pytest.raises(csvparse.CsvParseError) as caught:
            csvparse.parse_line("|".join(fields), 1)
        assert f"column '{csvparse.COLUMNS[index]}'" in str(caught.value)


def test_render_line_round_trips_bools_and_absent_flights():
    record = replace(EXPECTED_DEFAULT_RECORD,
                     landing_over_75m=True, f5j_motor_re_started=False,
                     flight1=None, flight4=0.0)
    rendered = csvparse.render_line(record)
    parts = rendered.split("|")
    assert parts[13] == "True"
    assert parts[21] == "False"
    assert parts[17] == ""
    assert parts[20] == "0"          # recorded zero stays the "0" placeholder

    parsed = csvparse.parse_line(rendered, 1)
    assert parsed.landing_over_75m is True
    assert parsed.f5j_motor_re_started is False
    assert parsed.flight1 is None
    assert parsed.flight4 == 0.0 and type(parsed.flight4) is float


def test_real_download_fixture_parses_end_to_end_and_triares_clean():
    text = REAL_FIXTURE_CSV.read_text(encoding="utf-8-sig")
    physical_lines = [line for line in text.splitlines() if line != ""]
    records = csvparse.parse_csv(text)

    assert len(records) == len(physical_lines) == 41
    distinct_pilots = {record.pilot_no for record in records}
    assert len(distinct_pilots) == 7

    doc = triage.convert_records(records)
    triage_pilots = {pilot["pilotNo"] for pilot in doc["pilots"]}
    assert triage_pilots == distinct_pilots
    assignment_total = sum(len(bucket["assignments"]) for bucket in doc["rounds"])
    assert assignment_total == 41

    completeness = triage.check_draw_completeness(records)
    assert completeness["violations"] == []

    for record in records:
        # Flags arrive as booleans; nothing boolean sits where arithmetic
        # fields belong.
        assert record.landing_over_75m is False
        assert record.f5j_motor_re_started is False
        assert all(type(getattr(record, f"data{slot}")) is float
                   for slot in range(1, 8))
        assert type(record.penalty) is float
        assert all(getattr(record, flight) is None
                   for flight in ("flight1", "flight2", "flight3", "flight4"))
        assert record.model_id == ""
    for bucket in doc["rounds"]:
        for assignment in bucket["assignments"]:
            assert all(type(value) is float for value in assignment["raw"])
            assert all(type(value) is float
                       for value in assignment.get("flights", []))


def test_utf8_sig_bom_at_document_start_is_tolerated():
    bom_text = "\ufeff" + document(default_line(), default_line())
    records = csvparse.parse_csv(bom_text)
    assert len(records) == 2
    assert_record_typed_equal(records[0], EXPECTED_DEFAULT_RECORD)
    assert_record_typed_equal(records[1], EXPECTED_DEFAULT_RECORD)

    # Byte-level variant: caller decoded utf-8-sig bytes with plain utf-8,
    # leaving the BOM character at the front of the handed-in str.
    raw = ("\ufeff" + default_line() + "\r\n").encode("utf-8")
    text = raw.decode("utf-8")
    records = csvparse.parse_csv(text)
    assert_record_typed_equal(records[0], EXPECTED_DEFAULT_RECORD)


def test_crlf_and_lf_endings_both_accepted():
    crlf_doc = (default_line() + "\r\n") * 2
    lf_doc = (default_line() + "\n") * 2
    for text in (crlf_doc, lf_doc):
        records = csvparse.parse_csv(text)
        assert len(records) == 2
        assert_record_typed_equal(records[-1], EXPECTED_DEFAULT_RECORD)


@pytest.mark.parametrize(
    "mutate, substrings",
    [
        # 21 fields
        (
            lambda fields: fields[:-1],
            ["expected exactly 22 pipe-delimited fields, found 21"],
        ),
        # 23 fields
        (
            lambda fields: fields + ["extra"],
            ["expected exactly 22 pipe-delimited fields, found 23"],
        ),
    ],
)
def test_field_count_violations_are_loud(mutate, substrings):
    fields = [DEFAULT_TOKENS[name] for name in csvparse.COLUMNS]
    bad_line = "|".join(mutate(fields))
    with pytest.raises(csvparse.CsvParseError) as caught:
        csvparse.parse_line(bad_line, 3)
    for substring in substrings:
        assert substring in str(caught.value), str(caught.value)


def test_interior_blank_line_raises_naming_its_physical_line():
    doc = default_line() + "\n\n" + default_line() + "\n"
    with pytest.raises(csvparse.CsvParseError) as caught:
        csvparse.parse_csv(doc)
    assert "line 2" in str(caught.value)
    assert "blank line inside download CSV" in str(caught.value)


def test_non_numeric_round_no_names_column_index_and_value():
    fields = [DEFAULT_TOKENS[name] for name in csvparse.COLUMNS]
    fields[2] = "x"
    with pytest.raises(csvparse.CsvParseError) as caught:
        csvparse.parse_line("|".join(fields), 1)
    message = str(caught.value)
    assert "column 'round_no'" in message
    assert "(index 2)" in message
    assert "'x'" in message


def test_decimal_looking_group_no_rejected_for_int_column():
    fields = [DEFAULT_TOKENS[name] for name in csvparse.COLUMNS]
    fields[3] = "5.0"
    with pytest.raises(csvparse.CsvParseError) as caught:
        csvparse.parse_line("|".join(fields), 4)
    message = str(caught.value)
    assert "column 'group_no'" in message
    assert "(index 3)" in message
    assert "'5.0'" in message


def test_uniform_comp_type_single_passes_mixed_raises():
    solo = csvparse.parse_csv(document(default_line()))
    assert csvparse.uniform_comp_type(solo) == "F3K"

    other = replace(EXPECTED_DEFAULT_RECORD, comp_type="F5K")
    mixed = solo + [other]
    with pytest.raises(csvparse.CsvParseError) as caught:
        csvparse.uniform_comp_type(mixed)
    message = str(caught.value)
    assert "F3K" in message and "F5K" in message


def test_empty_document_parses_to_no_records():
    assert csvparse.parse_csv("") == []
    assert csvparse.parse_csv("\n\n\n") == []


def test_render_line_is_inverse_of_parse_line_on_defaults():
    rendered = csvparse.render_line(EXPECTED_DEFAULT_RECORD)
    # render normalises formatting (repr floats, "0" placeholder for zeros),
    # so verify by parsing back rather than string-identity.
    assert_record_typed_equal(csvparse.parse_line(rendered, 1),
                              EXPECTED_DEFAULT_RECORD)
    tokens = rendered.split("|")
    assert tokens[0] == DEFAULT_TOKENS["comp_id"]      # text passthrough
    assert tokens[1] == DEFAULT_TOKENS["comp_type"]
    assert tokens[2] == DEFAULT_TOKENS["round_no"]     # ints unchanged
    assert tokens[14] == "-30.5"                       # repr trims trailing zeros
    zero_record = replace(EXPECTED_DEFAULT_RECORD,
                          data1=0.0, penalty=0.0, flight4=0.0)
    parts = csvparse.render_line(zero_record).split("|")
    assert parts[6] == "0" and parts[14] == "0" and parts[20] == "0"


# ------------------------------------------------------- hypothesis properties


_HEX_ALPHABET = "0123456789abcdefABCDEF"
_NAME_ALPHABET = string.ascii_letters + string.digits + " éüñáçøåßð'-._"

_COMP_IDS = st.text(_HEX_ALPHABET, min_size=10, max_size=15)
_COMP_TYPES = st.sampled_from(["F3K", "F5K", "F5J", "F3J", "F3B", "ALES"])
_NAMES = st.text(_NAME_ALPHABET, min_size=0, max_size=24).filter(
    lambda value: value == value.strip()
)

# Dot-decimal floats only: scaled integers whose shortest repr is always a
# plain decimal (never scientific notation, never commas — the wire carries
# dots because the server canonicalised commas before we ever see it).
_DECIMAL_FLOATS = st.builds(
    lambda scaled, divisor: scaled / divisor,
    st.integers(min_value=-99_999_999, max_value=99_999_999),
    st.sampled_from((1, 10, 100, 1000)),
)


@st.composite
def _download_records(draw):
    # Flag columns and flight slots span the PROVEN wire domain: flags are
    # sometimes VB booleans, sometimes numerics (mixed representations are
    # real); unused flights arrive as '' -> None. Round-trip properties must
    # hold over this whole widened domain.
    return csvparse.DownloadRecord(
        comp_id=draw(_COMP_IDS),
        comp_type=draw(_COMP_TYPES),
        round_no=draw(st.integers(min_value=0, max_value=99)),
        group_no=draw(st.integers(min_value=0, max_value=99)),
        reflight_no=draw(st.integers(min_value=0, max_value=9)),
        pilot_no=draw(st.integers(min_value=1, max_value=99999)),
        data1=draw(_DECIMAL_FLOATS),
        data2=draw(_DECIMAL_FLOATS),
        data3=draw(_DECIMAL_FLOATS),
        data4=draw(_DECIMAL_FLOATS),
        data5=draw(_DECIMAL_FLOATS),
        data6=draw(_DECIMAL_FLOATS),
        data7=draw(_DECIMAL_FLOATS),
        landing_over_75m=draw(st.one_of(_DECIMAL_FLOATS, st.booleans())),
        penalty=draw(_DECIMAL_FLOATS),
        pilot_name=draw(_NAMES),
        model_id=draw(_NAMES),
        flight1=draw(st.one_of(_DECIMAL_FLOATS, st.none())),
        flight2=draw(st.one_of(_DECIMAL_FLOATS, st.none())),
        flight3=draw(st.one_of(_DECIMAL_FLOATS, st.none())),
        flight4=draw(st.one_of(_DECIMAL_FLOATS, st.none())),
        f5j_motor_re_started=draw(
            st.one_of(st.integers(min_value=0, max_value=1), st.booleans())),
    )


@st.composite
def _wire_documents(draw):
    records = [
        draw(_download_records())
        for _ in range(draw(st.integers(min_value=1, max_value=8)))
    ]
    trailing = draw(st.sampled_from(("", "\n", "\n\n", "\r\n")))
    text = "\n".join(csvparse.render_line(r) for r in records) + trailing
    return records, text


def _remove_separator(line, occurrence):
    seen = -1
    for offset, character in enumerate(line):
        if character == "|":
            seen += 1
            if seen == occurrence:
                return line[:offset] + line[offset + 1:]
    raise AssertionError("no such separator on line")


@given(payload=_wire_documents())
@settings(max_examples=50, deadline=None)
def test_round_trip_preserves_every_generated_field_value_and_type(payload):
    expected_records, text = payload
    parsed_records = csvparse.parse_csv(text)
    assert len(parsed_records) == len(expected_records)
    for parsed, expected in zip(parsed_records, expected_records):
        assert parsed == expected
        for name in csvparse.COLUMNS:
            got = getattr(parsed, name)
            want = getattr(expected, name)
            assert type(got) is type(want), f"column {name}: {got!r} vs {want!r}"
            assert got == want, f"column {name}: {got!r} != {want!r}"


@given(payload=_wire_documents())
@settings(max_examples=50, deadline=None)
def test_record_count_equals_number_of_generated_lines(payload):
    expected_records, text = payload
    physical_lines = [line for line in text.splitlines() if line != ""]
    parsed_records = csvparse.parse_csv(text)
    assert len(expected_records) == len(physical_lines)
    assert len(parsed_records) == len(physical_lines)


@st.composite
def _corrupted_documents(draw):
    lines = [
        csvparse.render_line(r)
        for r in (draw(_download_records())
                  for _ in range(draw(st.integers(min_value=2, max_value=6))))
    ]
    corruption = draw(st.sampled_from(
        ("drop_separator", "interior_blank", "stray_junk")))
    if corruption == "drop_separator":
        line_index = draw(st.integers(min_value=0, max_value=len(lines) - 1))
        separator_index = draw(
            st.integers(min_value=0, max_value=csvparse.COLUMN_COUNT - 2))
        lines[line_index] = _remove_separator(lines[line_index], separator_index)
    else:
        splice_index = draw(st.integers(min_value=1, max_value=len(lines) - 1))
        filler = "" if corruption == "interior_blank" else "stray noise between records"
        lines.insert(splice_index, filler)
    return "\n".join(lines) + draw(st.sampled_from(("", "\n")))


@given(text=_corrupted_documents())
@settings(max_examples=50, deadline=None)
def test_structural_mutations_always_raise_parse_error(text):
    with pytest.raises(csvparse.CsvParseError):
        csvparse.parse_csv(text)
