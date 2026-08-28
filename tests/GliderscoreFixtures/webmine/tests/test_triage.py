#!/usr/bin/env python3
"""Unit + property tests for the WI-4 triage converter (triage).

The scrape fixtures below are SYNTHETIC, hand-built from the documented
eScoring.aspx structure (gliderscore-online-data-mining.md §2.3/§2.4:
per-pilot screen showing name, round/group list and task description per
round) — live verification against gliderscore.com awaits the permission
gate (story Safety contract item 5).

Properties target the Plan-WI-4 invariants:
  A — a valid draw universe (each pilot at most one base row per
      (round, group), re-flights only as distinct quads) never produces
      completeness violations;
  B — render_line -> parse_csv -> convert_records keeps record count equal
      to assignment count and travels pilotNo / penalty / landingOver75m /
      f5jMotorReStarted through intact per record;
  C — converting a shuffled record set yields the identical document
      (deterministic normalisation).
"""

import io
import random

import pytest
from hypothesis import given, settings
from hypothesis import strategies as st

import csvparse
import triage

COMP_ID = "2381887cb81b"


def make_record(**overrides):
    values = dict(
        comp_id=COMP_ID, comp_type="F3K",
        round_no=1, group_no=1, reflight_no=0, pilot_no=75,
        data1=0.0, data2=0.0, data3=0.0, data4=0.0, data5=0.0, data6=0.0,
        data7=0.0, landing_over_75m=0.0, penalty=0.0,
        pilot_name="Botherway", model_id="Vesper",
        flight1=0.0, flight2=0.0, flight3=0.0, flight4=0.0,
        f5j_motor_re_started=0,
    )
    values.update(overrides)
    return csvparse.DownloadRecord(**values)


COMMON_KEYS = {"pilotNo", "pilotName", "penalty", "landingOver75m",
               "f5jMotorReStarted", "raw"}


def assignments_of(doc):
    return [assignment
            for bucket in doc["rounds"]
            for assignment in bucket["assignments"]]


# ------------------------------------------------------- family decodes


def test_duration_split_math_exact_for_both_timekeepers():
    record = make_record(
        comp_type="F5J",
        data1=2.0,    # laps passthrough
        data2=4.0, data3=31.0,   # time1 = 4*60 + 31
        data4=5.0, data5=12.0,   # time2 = 5*60 + 12
        data6=-25.5,             # deduction sign preserved, never flipped
        data7=88.0,
    )
    doc = triage.convert_records([record])
    assignment = doc["rounds"][0]["assignments"][0]
    assert assignment["time1Seconds"] == 271
    assert type(assignment["time1Seconds"]) is int
    assert assignment["time2Seconds"] == 312
    assert assignment["laps"] == 2
    assert assignment["deduction"] == -25.5
    assert assignment["landing"] == 88.0
    assert assignment["raw"] == [2.0, 4.0, 31.0, 5.0, 12.0, -25.5, 88.0]
    assert len(doc["limitations"]) >= 1             # family always documents itself


def test_duration_only_time2_present_and_limitation_recorded():
    record = make_record(comp_type="ALES", data4=0.0, data5=45.0)
    doc = triage.convert_records([record])
    assignment = doc["rounds"][0]["assignments"][0]
    assert "time1Seconds" not in assignment       # zero slots stay out...
    assert assignment["time2Seconds"] == 45       # ...but decode when nonzero
    assert "time2Seconds" in assignment and "laps" not in assignment
    assert "deduction" not in assignment and "landing" not in assignment
    assert assignment["raw"] == [0.0, 0.0, 0.0, 0.0, 45.0, 0.0, 0.0]
    assert len(doc["limitations"]) >= 1


def test_duration_all_zero_drops_every_decoded_key_but_keeps_raw():
    record = make_record(comp_type="Thml")
    doc = triage.convert_records([record])
    assignment = doc["rounds"][0]["assignments"][0]
    assert set(assignment) == COMMON_KEYS
    assert assignment["raw"] == [0.0] * 7


@pytest.mark.parametrize("comp_type", ["F5K", "F5K2024"])
def test_f5k_flights_from_flight_columns_plus_exact_heights_limitation(
        comp_type):
    record = make_record(
        comp_type=comp_type,
        flight1=301.0, flight2=302.5, flight3=0.0, flight4=404.125,
        data1=111.0,   # data slots mean nothing decodable here; raw only
    )
    doc = triage.convert_records([record])
    assignment = doc["rounds"][0]["assignments"][0]
    assert assignment["flights"] == [301.0, 302.5, 404.125]
    assert assignment["raw"] == [111.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]
    assert any("launch heights not in download CSV" in line
               for line in doc["limitations"])


def test_f3k_nonzero_data_slots_become_in_order_seconds_flights():
    record = make_record(
        comp_type="F3K",
        data1=249.5, data2=400.25, data3=0.0, data4=500.125,
        flight1=999.0,   # F3K reads Data slots only; flights stay untouched
    )
    doc = triage.convert_records([record])
    assignment = doc["rounds"][0]["assignments"][0]
    assert assignment["flights"] == [249.5, 400.25, 500.125]
    assert all(type(value) is float for value in assignment["flights"])
    assert assignment["raw"] == [249.5, 400.25, 0.0, 500.125, 0.0, 0.0, 0.0]
    # Flight1..4 columns are NOT consumed for F3K (999.0 stays out).
    assert "flights" in assignment and 999.0 not in assignment["flights"]
    # Task-letter mapping deliberately unresolved — limitation says so.
    assert any("task-letter mapping NOT resolved" in line
               for line in doc["limitations"])


@pytest.mark.parametrize("comp_type", ["F3B", "Speed", "Distance"])
def test_unknown_comp_types_are_raw_passthrough_naming_the_type(comp_type):
    record = make_record(
        comp_type=comp_type, data1=10.0, data7=70.0, penalty=-30.0,
        landing_over_75m=80.0, f5j_motor_re_started=1,
    )
    doc = triage.convert_records([record])
    assignment = doc["rounds"][0]["assignments"][0]
    assert set(assignment) == COMMON_KEYS
    assert assignment["raw"] == [10.0, 0.0, 0.0, 0.0, 0.0, 0.0, 70.0]
    assert assignment["penalty"] == -30.0
    assert assignment["landingOver75m"] == 80.0
    assert assignment["f5jMotorReStarted"] == 1
    assert any(comp_type in line and "passthrough" in line
               for line in doc["limitations"])


def test_boolean_flags_pass_through_natively_and_never_reach_decodes():
    # Proven live wire form (2026-08-27, CompID 2381887cb81b): flags arrive
    # as Python bools from exact-case 'True'/'False' tokens and must ride
    # the common fields verbatim while family decoders keep to their own
    # arithmetic slots.
    record = make_record(
        comp_type="F5J",
        data1=1.0, data2=4.0, data3=12.0,
        landing_over_75m=True, f5j_motor_re_started=False,
    )
    doc = triage.convert_records([record])
    assignment = doc["rounds"][0]["assignments"][0]
    assert assignment["landingOver75m"] is True
    assert type(assignment["landingOver75m"]) is bool
    assert assignment["f5jMotorReStarted"] is False
    assert type(assignment["f5jMotorReStarted"]) is bool
    # Decoded slots stay numeric: no flag leaked into arithmetic fields.
    assert assignment["time1Seconds"] == 252      # 4*60 + 12, flags untouched
    assert all(type(value) is float for value in assignment["raw"])

    rendered = csvparse.render_line(record)
    parsed = csvparse.parse_csv("\n".join([rendered]) + "\n")[0]
    assert parsed.landing_over_75m is True
    assert parsed.f5j_motor_re_started is False
    assert triage.convert_records([parsed])["rounds"][0]["assignments"][0] \
        == assignment


def test_absent_flight_slots_decode_as_absent_not_zero():
    # '' -> None flights (F5K corpus reality): absent stays out of decoded
    # flights exactly like recorded zeros do; raw Data evidence untouched.
    record = make_record(comp_type="F5K", flight1=301.0, flight4=None)
    doc = triage.convert_records([record])
    assignment = doc["rounds"][0]["assignments"][0]
    assert assignment["flights"] == [301.0]
    assert len(doc["rounds"][0]["assignments"][0]["flights"]) == 1


def test_empty_records_loud_exit_and_pilots_unique_sorted():
    with pytest.raises(SystemExit) as caught:
        triage.convert_records([])
    assert "no records" in str(caught.value)

    scrambled = [
        make_record(round_no=2, pilot_no=82, pilot_name="Chao-Hui Wu"),
        make_record(round_no=1, pilot_no=151, pilot_name="Pawson"),
        make_record(round_no=3, pilot_no=75, pilot_name="Botherway"),
        make_record(round_no=1, pilot_no=75, pilot_name="Botherway"),
    ]
    doc = triage.convert_records(scrambled)
    assert doc["name"] is None                       # caller stamps --name
    assert [pilot["pilotNo"] for pilot in doc["pilots"]] == [75, 82, 151]
    assert len(doc["pilots"]) == 3                   # unique despite repeats
    assert isinstance(doc["compId"], str) and doc["compType"] == "F3K"
    assert doc["limitations"]                        # always non-empty


# ------------------------------------------------------------- ordering


def test_rounds_ascending_by_tuple_regardless_of_file_order():
    records = [
        make_record(round_no=2, group_no=2, reflight_no=3),
        make_record(round_no=1, group_no=9, reflight_no=0),
        make_record(round_no=2, group_no=2, reflight_no=1),
        make_record(round_no=2, group_no=1, reflight_no=0),
        make_record(round_no=1, group_no=1, reflight_no=2),
    ]
    doc = triage.convert_records(records)
    keys = [(bucket["round"], bucket["group"], bucket["reflight"])
            for bucket in doc["rounds"]]
    assert keys == sorted(keys) == [
        (1, 1, 2), (1, 9, 0), (2, 1, 0), (2, 2, 1), (2, 2, 3),
    ]


def test_assignments_within_bucket_canonicalised_by_pilot():
    records = [
        make_record(pilot_no=82, pilot_name="Wu", data1=20.0),
        make_record(pilot_no=75, pilot_name="B", data1=10.0),
    ]
    doc = triage.convert_records(records)
    bucket = doc["rounds"][0]
    assert [a["pilotNo"] for a in bucket["assignments"]] == [75, 82]


# ---------------------------------------------------------- completeness


def test_duplicate_base_slot_is_violation_named_with_triple():
    records = [
        make_record(round_no=1, group_no=2, reflight_no=0, pilot_no=75),
        make_record(round_no=1, group_no=2, reflight_no=0, pilot_no=75),
    ]
    result = triage.check_draw_completeness(records)
    assert set(result) == {"violations", "gaps"}
    assert result["violations"], "duplicate base must be flagged HARD"
    text = "\n".join(result["violations"])
    assert "(round 1, group 2, pilot 75)" in text
    assert "reflight 0" in text
    clean_gaps = [line for line in result["gaps"]
                  if "missing rounds" in line]
    assert not clean_gaps


def test_clean_universe_has_no_violations_and_no_shortfall_flags():
    records = [
        make_record(round_no=r, group_no=1, reflight_no=0, pilot_no=p,
                    pilot_name=f"P{p}")
        for p in (75, 82) for r in (1, 2)
    ]
    result = triage.check_draw_completeness(records)
    assert result["violations"] == []
    assert not any("missing rounds" in gap for gap in result["gaps"])
    summaries = [gap for gap in result["gaps"] if gap.startswith("pilot")]
    assert len(summaries) == 2                      # each pilot reported once
    assert any("r1:1 r2:1" in gap for gap in summaries)


@pytest.mark.parametrize(
    "overrides, fragment",
    [
        (dict(round_no=0), "round number 0"),
        (dict(group_no=0), "group number 0"),
        (dict(pilot_no=0), "pilot number 0"),
        (dict(reflight_no=-1), "negative reflight -1"),
    ],
)
def test_domain_bounds_are_hard_violations(overrides, fragment):
    record = make_record(**overrides)
    result = triage.check_draw_completeness([record])
    assert result["violations"], overrides
    assert fragment in "\n".join(result["violations"])


def test_jerilderie_style_absent_round_is_informational_gap_not_violation():
    records = []
    full_rounds = (75, 82)
    short_pilot = 90
    for round_no in range(1, 6):
        for pilot_no in full_rounds:
            records.append(make_record(round_no=round_no, group_no=1,
                                       reflight_no=0, pilot_no=pilot_no))
        if round_no < 5:
            records.append(make_record(round_no=round_no, group_no=1,
                                       reflight_no=0, pilot_no=short_pilot,
                                       pilot_name="Late Entry"))
    result = triage.check_draw_completeness(records)
    assert result["violations"] == []               # gaps never fail the run
    shortfall = [gap for gap in result["gaps"]
                 if "missing rounds" in gap]
    assert len(shortfall) == 1
    assert "pilot 90" in shortfall[0]
    assert "[5]" in shortfall[0]
    assert "4 of 5 rounds" in shortfall[0]
    assert any(gap.startswith("pilot 90") and "r1:1 r2:1 r3:1 r4:1" in gap
               for gap in result["gaps"])


def test_check_draw_completeness_empty_input_is_all_clear():
    assert triage.check_draw_completeness([]) == {"violations": [],
                                                  "gaps": []}


# ------------------------------------------------------------- scraping
#
# Synthetic fixtures built from the documented page shape; live checks wait
# on the permission gate.

TABLE_SCREEN = """
<html><head><title>GliderScore eScoring</title></head><body>
<h2>Pilot screen</h2>
<table>
<tr><th>Round</th><th>Task</th></tr>
<tr><td>Round 1</td><td>L1 5max in 7m</td></tr>
<tr><td>Round 2</td><td>AllUp 3:00*3</td></tr>
<tr><td>Round 3</td><td>Big Ladder</td></tr>
</table>
<p>Footer noise nobody parses.</p>
</body></html>
"""


def test_scrape_finds_rounds_from_documented_table_shape():
    sink = io.StringIO()
    tasks = triage.scrape_tasks(TABLE_SCREEN, stderr=sink)
    assert tasks == {1: "L1 5max in 7m", 2: "AllUp 3:00*3", 3: "Big Ladder"}
    assert all(type(key) is int for key in tasks)
    assert sink.getvalue() == ""                    # no warning on success


def test_scrape_handles_single_line_marker_task_pairs():
    html = "<ul><li>ROUND 12 - Slot racers</li><li>Round 03 : Poker</li></ul>"
    tasks = triage.scrape_tasks(html)
    assert tasks == {12: "Slot racers", 3: "Poker"}


def test_scrape_last_wins_on_duplicate_round_markers():
    html = ("Round 1 Alpha first pass\n"
            "Round 2 Beta\n"
            "Round 1 Gamma retried")
    assert triage.scrape_tasks(html) == {1: "Gamma retried", 2: "Beta"}


def test_scrape_trailing_text_after_last_marker_does_not_pollute_task():
    html = "Round 1 Poker\n<footer>Scores computed by GliderScore 6.79 U5</footer>"
    assert triage.scrape_tasks(html) == {1: "Poker"}


@pytest.mark.parametrize(
    "html",
    ["", None, "<div><span></span></div>", "@#$%^&*", 12345,
     "<script>var x='Round 99';</script><p>y</p>"],
)
def test_scrape_ignores_junk_html_with_one_stderr_warning(html):
    sink = io.StringIO()
    tasks = triage.scrape_tasks(html, stderr=sink)
    assert tasks == {}
    lines = sink.getvalue().splitlines()
    assert len(lines) == 1
    assert "task scrape" in lines[0]


def test_scrape_script_block_is_stripped_before_marker_hunt():
    html = ("<script>Round 42 fake</script>"
            "<table><tr><td>Round 2</td><td>AllUp 3:00*3</td></tr></table>")
    assert triage.scrape_tasks(html) == {2: "AllUp 3:00*3"}


def test_scrape_marker_without_following_text_yields_no_entry():
    html = "some heading Round 7 and then nothing but the end"
    # The nearest following text exists ("and then nothing..."), so round 7
    # picks it up; only a marker at true EOF yields no entry.
    assert triage.scrape_tasks(html) == {7: "and then nothing but the end"}
    html_end = "heading text ending with Round 9"
    sink = io.StringIO()
    assert triage.scrape_tasks(html_end, stderr=sink) == {}
    assert len(sink.getvalue().splitlines()) == 1


# ------------------------------------------------------------ properties

_HEX_ALPHABET = "0123456789abcdefABCDEF"
_NAME_ALPHABET = ("abcde ghijklmnopqrstuvwxyzABCDEGKOPRSTUVWZ"
                  "-._' éüñáçøåßð")
_ALL_TYPES = st.sampled_from(
    ["F3J", "F5J", "ALES", "Thml", "F3K", "F5K", "F5K2024", "F3B"])

# Dot-decimal floats whose repr round-trips (copied approach from
# test_csvparse: scaled integers can never emit NaN/inf/scientific notation).
_DECIMAL_FLOATS = st.builds(
    lambda scaled, divisor: scaled / divisor,
    st.integers(min_value=-99_999_999, max_value=99_999_999),
    st.sampled_from((1, 10, 100, 1000)),
)


@st.composite
def _valid_draw_universes(draw):
    """Quads (round, group, reflight, pilot) that satisfy invariant A."""
    comp_type = draw(_ALL_TYPES)
    pilots = draw(st.lists(st.integers(min_value=1, max_value=999),
                           min_size=1, max_size=8, unique=True))
    n_rounds = draw(st.integers(min_value=1, max_value=6))
    n_groups = draw(st.integers(min_value=1, max_value=3))
    records = []
    seen_quads = set()
    for pilot_no in pilots:
        for round_no in range(1, n_rounds + 1):
            if not draw(st.booleans()):
                continue
            group_no = draw(st.integers(min_value=1, max_value=n_groups))
            base_quad = (round_no, group_no, 0, pilot_no)
            if base_quad in seen_quads:
                continue
            seen_quads.add(base_quad)
            records.append(make_record(
                comp_type=comp_type, round_no=round_no, group_no=group_no,
                reflight_no=0, pilot_no=pilot_no,
                pilot_name=f"Pilot{pilot_no}",
                data1=float(100 + pilot_no % 50)))
            if draw(st.booleans()):
                if draw(st.booleans()):
                    reflight_no = draw(st.integers(min_value=1, max_value=3))
                    quad = (round_no, group_no, reflight_no, pilot_no)
                elif n_groups > 1:
                    other_group = ((group_no - 1 + 1) % n_groups) + 1
                    quad = (round_no, other_group, 0, pilot_no)
                else:
                    quad = base_quad
                if quad not in seen_quads:
                    seen_quads.add(quad)
                    records.append(make_record(
                        comp_type=comp_type, round_no=quad[0],
                        group_no=quad[1], reflight_no=quad[2],
                        pilot_no=quad[3], pilot_name=f"Pilot{pilot_no}",
                        data1=float(200 + pilot_no % 50)))
    if not records:
        records.append(make_record(comp_type=comp_type, round_no=1,
                                   group_no=1, reflight_no=0,
                                   pilot_no=pilots[0],
                                   pilot_name=f"Pilot{pilots[0]}"))
    return comp_type, records


@st.composite
def _convertible_record_sets(draw):
    comp_type = draw(_ALL_TYPES)
    count = draw(st.integers(min_value=1, max_value=8))
    names = st.text(_NAME_ALPHABET, min_size=0, max_size=16).filter(
        lambda value: "|" not in value and "\n" not in value)
    records = []
    seen_quads = set()
    for _ in range(count):
        while True:  # unique (round, group, reflight, pilot) per set
            round_no = draw(st.integers(min_value=1, max_value=6))
            group_no = draw(st.integers(min_value=1, max_value=3))
            reflight_no = draw(st.integers(min_value=0, max_value=3))
            pilot_no = draw(st.integers(min_value=1, max_value=999))
            quad = (round_no, group_no, reflight_no, pilot_no)
            if quad not in seen_quads:
                seen_quads.add(quad)
                break
        records.append(csvparse.DownloadRecord(
            comp_id=COMP_ID, comp_type=comp_type,
            round_no=round_no, group_no=group_no, reflight_no=reflight_no,
            pilot_no=pilot_no,
            data1=draw(_DECIMAL_FLOATS), data2=draw(_DECIMAL_FLOATS),
            data3=draw(_DECIMAL_FLOATS), data4=draw(_DECIMAL_FLOATS),
            data5=draw(_DECIMAL_FLOATS), data6=draw(_DECIMAL_FLOATS),
            data7=draw(_DECIMAL_FLOATS),
            landing_over_75m=draw(st.one_of(_DECIMAL_FLOATS, st.booleans())),
            penalty=draw(_DECIMAL_FLOATS),
            pilot_name=draw(names), model_id=draw(names),
            flight1=draw(st.one_of(_DECIMAL_FLOATS, st.none())),
            flight2=draw(st.one_of(_DECIMAL_FLOATS, st.none())),
            flight3=draw(st.one_of(_DECIMAL_FLOATS, st.none())),
            flight4=draw(st.one_of(_DECIMAL_FLOATS, st.none())),
            f5j_motor_re_started=draw(
                st.one_of(st.sampled_from((0, 1)), st.booleans())),
        ))
    return comp_type, records


@given(payload=_valid_draw_universes())
@settings(max_examples=50, deadline=None)
def test_property_a_valid_draw_never_violates(payload):
    _comp_type, records = payload
    result = triage.check_draw_completeness(records)
    assert result["violations"] == []


@given(payload=_convertible_record_sets())
@settings(max_examples=50, deadline=None)
def test_property_b_record_count_equality_and_field_identity(payload):
    original_type, records = payload
    text = "\n".join(csvparse.render_line(record) for record in records) + "\n"
    parsed = csvparse.parse_csv(text)
    assert parsed == records                       # wire round-trip identity

    doc = triage.convert_records(parsed)
    total_assignments = sum(len(bucket["assignments"])
                            for bucket in doc["rounds"])
    assert total_assignments == len(parsed)

    key_multiset_actual = sorted(
        (a["pilotNo"], a["penalty"], a["landingOver75m"],
         a["f5jMotorReStarted"])
        for a in assignments_of(doc)
    )
    key_multiset_expected = sorted(
        (r.pilot_no, r.penalty, r.landing_over_75m, r.f5j_motor_re_started)
        for r in parsed
    )
    assert key_multiset_actual == key_multiset_expected

    buckets = {(b["round"], b["group"], b["reflight"]): b["assignments"]
               for b in doc["rounds"]}
    for record in parsed:
        expected = _expected_assignment(record, original_type)
        actual_bucket = buckets[
            (record.round_no, record.group_no, record.reflight_no)]
        assert expected in actual_bucket


def _expected_assignment(record, comp_type):
    """Mirror of the family decode used to prove plumbing (not formulas;
    formula correctness is owned by the example tests above)."""
    common = {
        "pilotNo": record.pilot_no,
        "pilotName": record.pilot_name,
        "penalty": record.penalty,
        "landingOver75m": record.landing_over_75m,
        "f5jMotorReStarted": record.f5j_motor_re_started,
    }
    raw = [record.data1, record.data2, record.data3, record.data4,
           record.data5, record.data6, record.data7]
    if comp_type in triage.DURATION_TYPES:
        family = {}
        for key, value in (
                ("laps", record.data1),
                ("time1Seconds", round(record.data2) * 60 + round(record.data3)),
                ("time2Seconds", round(record.data4) * 60 + round(record.data5)),
                ("deduction", record.data6),
                ("landing", record.data7)):
            if value != 0:
                family[key] = value
    elif comp_type == triage.F3K_TYPE:
        family = {"flights": [v for v in raw if v != 0]}
    elif comp_type in triage.F5K_TYPES:
        family = {"flights": [v for v in (record.flight1, record.flight2,
                                          record.flight3, record.flight4)
                              if v is not None and v != 0]}
    else:
        family = {}
    return {**common, **family, "raw": raw}


@given(payload=_valid_draw_universes())
@settings(max_examples=50, deadline=None)
def test_property_c_shuffled_input_converges_to_same_document(payload):
    _comp_type, records = payload
    reference = triage.convert_records(records)
    rng = random.Random(20260827)
    shuffled = list(records)
    rng.shuffle(shuffled)
    assert triage.convert_records(shuffled) == reference
