#!/usr/bin/env python3
"""Unit + property tests for the catalogue miner (mine_catalogue).

All HTML fixtures below are SYNTHETIC, hand-built from the documented OnLineScores.aspx
structure (gliderscore-online-data-mining.md 2.1: hidden WebForms fields, the
"*TabPanel1$CompList1" picker with hex CompIDs / "YYYY Mmm DD - Title (Venue)" texts, a
distractor select, submit-button markers); they are not captures. Live verification of
the page shape awaits the permission gate (courtesy email), per story Before-starting.

No live network: every test drives GsClient through an injected fake transport /
clock / sleep, self-contained here (independent of test_gsclient.py on purpose).
"""

import csv
import io
import json
import string
from collections import namedtuple
from datetime import date, datetime, timedelta
from urllib.parse import parse_qsl
from pathlib import Path

import pytest
from hypothesis import given, settings
from hypothesis import strategies as st

import gsclient
import mine_catalogue

BASE_URL = "https://gliderscore.com"
MINED_AT = "2026-08-27T09:30:00+00:00"
AUDIT_REQUIRED_KEYS = ("ts", "op", "method", "url")

RANGE_NAME = "ctl00$Main$TabPanel1$CompRange"
COMP_NAME = "ctl00$Main$TabPanel1$CompList1"
COUNTRY_NAME = "ctl00$Header$CountryList"
VIEWSTATE = "/wEPDwUKLTk2NTQzNjM3PGRkaGlkZGVu"
GENERATOR = "9F58E0AA63B0D9CE"
EVENTVALIDATION = "/wEWBALf3NvIDAKV1c2dBgLD0vvfAwKM54ugBgKS5bnUAw=="

# Hand-written picker rows: hex ids (mixed case on purpose), documented text shape.
LAST30_OPTIONS = [
    ("2381887cb81b", "2026 May 30 - F3K NI Round 2      (Haumoana)"),
    ("0123456789ab", "2026 Jun 07 - Millennium Cup Rnd 3   (HSL)"),
    ("ABCDEF1234", "2026 Jul 15 - Upper Hutt F5K        (TUAV)"),
    ("abcdefabcdef", "2026 Aug 20 - Waikato ALES Series R2"),
    ("ffff0000000", "2026 Aug 25 - Manawatu F3J Trophy   (Feilding)"),
]

ALL_OPTIONS = [
    ("2381887cb81b", "2026 May 30 - F3K NI Round 2      (Haumoana)"),
    ("99998Ee8888", "2026 Feb 15 - Northern F3F League   (Wellington)"),
    ("aaaabbbbcccc", "2026 Mar 10 - South Island F3J Open (Christchurch)"),
    ("ABCDEF1234", "2026 Jul 15 - Upper Hutt F5K        (TUAV)"),
    ("0000000aabb", "2026 Aug 22 - Otago F5K Winter Series"),
    ("deadbeef000", "Not A Date At All Here"),  # hex id, malformed text
]


# ---------------------------------------------------------------- fixtures


def option(value, text):
    return f'<option value="{value}">{text}</option>'


def build_page(comp_options, *, include_range_select=True):
    parts = [
        "<!DOCTYPE html><html><head><title>OnLineScores</title></head><body>",
        '<form name="aspnetForm" method="post" action="OnLineScores.aspx">',
        f'<input type="hidden" name="__VIEWSTATE" value="{VIEWSTATE}" />',
        f'<input type="hidden" name="__VIEWSTATEGENERATOR" value="{GENERATOR}" />',
        f'<input type="hidden" name="__EVENTVALIDATION" value="{EVENTVALIDATION}" />',
        '<input type="hidden" name="__EVENTTARGET" value="" />',
        '<input type="hidden" name="__EVENTARGUMENT" value="" />',
        '<input type="text" name="ctl00$SearchText" value="kiwi classics" />',
        '<textarea name="ctl00$Footer$Notes"></textarea>',
        '<input type="submit" name="ctl00$Main$btnShow" value="Show" />',
        f'<select name="{COUNTRY_NAME}">'
        + "".join(option(v, t) for v, t in
                  [("au", "Australia"), ("nz", "New Zealand"), ("gb", "United Kingdom")])
        + "</select>",
    ]
    range_options = ([("Last 30 days", "Last 30 days"),
                      ("All competitions", "All competitions")]
                     if include_range_select else [("Last 30 days", "Last 30 days")])
    range_markup = "".join(
        f'<option value="{v}"{"" if index else " selected"}>{t}</option>'
        for index, (v, t) in enumerate(range_options))
    parts.append(f'<select name="{RANGE_NAME}">{range_markup}</select>')
    parts.append(f'<select name="{COMP_NAME}" size="8">'
                 + "".join(option(v, t) for v, t in comp_options) + "</select>")
    parts.append("</form></body></html>")
    return "\n".join(parts)


PAGE_LAST30 = build_page(LAST30_OPTIONS)
PAGE_ALL = build_page(ALL_OPTIONS)


# -------------------------------------------------- fake client plumbing


class FakeClock:
    def __init__(self, start=1000.0):
        self.now = start

    def __call__(self):
        return self.now


def fake_sleep(clock):
    def sleep(seconds):
        clock.now += seconds
    return sleep


class FakeTransport:
    """Records every request dict; serves queued (status, body) responses."""

    def __init__(self, responses=()):
        self.responses = [(status, body) for status, body in responses]
        self.log = []

    def __call__(self, request_dict):
        self.log.append(dict(request_dict))
        status, body = self.responses.pop(0) if self.responses else (200, b"ok")
        if isinstance(body, str):
            body = body.encode("utf-8")
        return {"status": status, "body": body}


Harness = namedtuple("Harness", "client transport audit_path")


def make_harness(responses, tmp_path=None):
    clock = FakeClock()
    transport = FakeTransport(responses)
    kwargs = {
        "base_url": BASE_URL,
        "min_interval_seconds": 1.5,
        "transport": transport,
        "clock": clock,
        "sleep": fake_sleep(clock),
    }
    if tmp_path is not None:
        kwargs["audit_path"] = tmp_path / "audit.jsonl"
    return Harness(gsclient.GsClient(**kwargs), transport, kwargs.get("audit_path"))


def read_audit_lines(path):
    return [json.loads(line) for line in Path(path).read_text(encoding="utf-8").splitlines()]


# ------------------------------------------------------- example-based tests


def test_last30_flows_single_get_and_persists_exact_artifacts(tmp_path):
    harness = make_harness([(200, PAGE_LAST30)])
    summary = mine_catalogue.mine_catalogue(
        harness.client, want_range="last30", out_dir=tmp_path / "out",
        base_url=BASE_URL, mined_at=MINED_AT)

    # exactly ONE network op: the plain GET
    assert len(harness.transport.log) == 1
    request = harness.transport.log[0]
    assert request["method"] == "GET"
    assert request["data"] is None
    assert request["url"] == f"{BASE_URL}/OnLineScores.aspx"

    # parsed catalogue equals the fixture options, ids case-preserving, date-sorted
    assert [(comp["compId"], comp["name"]) for comp in summary["comps"]] == \
        [(value, text.strip()) for value, text in LAST30_OPTIONS]

    payload = json.loads((tmp_path / "out" / "comps.json").read_text(encoding="utf-8"))
    stamp = datetime.fromisoformat(payload["minedAt"])
    assert stamp.utcoffset() == timedelta(0)
    assert payload == {
        "minedAt": MINED_AT,
        "baseUrl": BASE_URL,
        "range": "last30",
        "count": len(LAST30_OPTIONS),
        "comps": [
            {"compId": "2381887cb81b", "name": "2026 May 30 - F3K NI Round 2      (Haumoana)",
             "date": "2026-05-30", "title": "F3K NI Round 2", "venue": "Haumoana"},
            {"compId": "0123456789ab", "name": "2026 Jun 07 - Millennium Cup Rnd 3   (HSL)",
             "date": "2026-06-07", "title": "Millennium Cup Rnd 3", "venue": "HSL"},
            {"compId": "ABCDEF1234", "name": "2026 Jul 15 - Upper Hutt F5K        (TUAV)",
             "date": "2026-07-15", "title": "Upper Hutt F5K", "venue": "TUAV"},
            {"compId": "abcdefabcdef", "name": "2026 Aug 20 - Waikato ALES Series R2",
             "date": "2026-08-20", "title": "Waikato ALES Series R2", "venue": ""},
            {"compId": "ffff0000000", "name": "2026 Aug 25 - Manawatu F3J Trophy   (Feilding)",
             "date": "2026-08-25", "title": "Manawatu F3J Trophy", "venue": "Feilding"},
        ],
    }

    raw_csv = (tmp_path / "out" / "comps.csv").read_text(encoding="utf-8")
    rows = list(csv.reader(io.StringIO(raw_csv)))
    assert rows[0] == ["compId", "name", "date", "title", "venue"]
    assert rows[1] == ["2381887cb81b",
                       "2026 May 30 - F3K NI Round 2      (Haumoana)",
                       "2026-05-30", "F3K NI Round 2", "Haumoana"]
    assert rows[-1] == ["ffff0000000",
                        "2026 Aug 25 - Manawatu F3J Trophy   (Feilding)",
                        "2026-08-25", "Manawatu F3J Trophy", "Feilding"]


def test_all_flow_posts_generic_webforms_postback_and_audits_both_ops(tmp_path):
    harness = make_harness([(200, PAGE_LAST30), (200, PAGE_ALL)], tmp_path=tmp_path)
    summary = mine_catalogue.mine_catalogue(
        harness.client, want_range="all", out_dir=tmp_path / "out",
        base_url=BASE_URL, mined_at=MINED_AT)

    # exactly TWO network ops in order: GET then POST, both on OnLineScores.aspx
    methods = [(request["method"], request["url"]) for request in harness.transport.log]
    assert methods == [("GET", f"{BASE_URL}/OnLineScores.aspx"),
                       ("POST", f"{BASE_URL}/OnLineScores.aspx")]

    # POST body carries every hidden field verbatim, __EVENTTARGET pointed at the
    # range select, the range select forced to "All competitions", submit buttons gone
    submitted = dict(parse_qsl(harness.transport.log[1]["data"], keep_blank_values=True))
    assert submitted == {
        "__VIEWSTATE": VIEWSTATE,
        "__VIEWSTATEGENERATOR": GENERATOR,
        "__EVENTVALIDATION": EVENTVALIDATION,
        "__EVENTTARGET": RANGE_NAME,
        "__EVENTARGUMENT": "",
        "ctl00$SearchText": "kiwi classics",
        "ctl00$Footer$Notes": "",
        COUNTRY_NAME: "au",
        COMP_NAME: LAST30_OPTIONS[0][0],   # every field goes back verbatim
        RANGE_NAME: "All competitions",
    }
    assert "ctl00$Main$btnShow" not in submitted

    # second lens on the op order/count: the append-only audit file
    lines = read_audit_lines(harness.audit_path)
    assert len(lines) == 2
    assert [(record["op"], record["method"]) for record in lines] == \
        [("online_scores", "GET"), ("online_scores", "POST")]
    for record in lines:
        for key in AUDIT_REQUIRED_KEYS:
            assert key in record
        assert record["status"] == 200 and record["bytes"] > 0

    assert summary["total"] == 6
    assert summary["dated"] == 5 and summary["undated"] == 1


def test_title_and_venue_derivation_rules():
    parse = mine_catalogue.option_to_comp
    plain = parse("aaaabbbbcccc", "2026 Mar 10 - South Island F3J Open   (Christchurch)")
    assert plain["title"] == "South Island F3J Open"
    assert plain["venue"] == "Christchurch"

    nested = parse("aaaabbbbcccc", "2026 Mar 10 - Nationals Final (Venue X (Sub))")
    assert nested["venue"] == "Venue X (Sub)"  # the LAST parenthesised group
    assert nested["title"] == "Nationals Final"

    internal_runs_kept = parse("aaaabbbbcccc", "2026 Jan 01 - Winter Series  Rnd 1    ")
    assert internal_runs_kept["title"] == "Winter Series  Rnd 1"  # trailing run trimmed
    assert internal_runs_kept["venue"] == ""
    assert internal_runs_kept["name"] == "2026 Jan 01 - Winter Series  Rnd 1"

    separated_title = parse("aaaabbbbcccc", "2026 Jan 01 - Spring Cup - Day Two")
    assert separated_title["title"] == "Spring Cup - Day Two"


def test_malformed_option_text_degrades_with_warning_others_unaffected(tmp_path, capsys):
    options = [
        ("cafebabedead", "2026 Jun 01 - Healthy Row   (Te Kuiti)"),
        ("deadbeef000", "Totally Not A Date At All"),
    ]
    harness = make_harness([(200, build_page(options))])
    summary = mine_catalogue.mine_catalogue(
        harness.client, out_dir=tmp_path, base_url=BASE_URL, mined_at=MINED_AT)

    stderr = capsys.readouterr().err
    assert "warning" in stderr and "deadbeef000" in stderr

    assert [comp["compId"] for comp in summary["comps"]] == ["cafebabedead", "deadbeef000"]
    damaged = summary["comps"][1]
    assert damaged["date"] is None
    assert damaged["title"] == damaged["name"] == "Totally Not A Date At All"
    assert damaged["venue"] == ""
    healthy = summary["comps"][0]
    assert healthy["date"] == date(2026, 6, 1)
    assert healthy["title"] == "Healthy Row" and healthy["venue"] == "Te Kuiti"
    assert summary["dated"] == 1 and summary["undated"] == 1


def test_missing_range_select_exits_nonzero_with_drift_evidence(tmp_path):
    options = [("aaaa000000", "2026 Jan 05 - Solo Comp (Somewhere)")]
    harness = make_harness([(200, build_page(options, include_range_select=False))])
    with pytest.raises(SystemExit) as caught:
        mine_catalogue.mine_catalogue(
            harness.client, want_range="all", out_dir=tmp_path, base_url=BASE_URL)
    assert caught.value.code != 0
    message = str(caught.value)
    assert "All competitions" in message
    assert COMP_NAME in message and COUNTRY_NAME in message  # what was found instead
    # failed before any write; only the initial GET ever went out
    assert len(harness.transport.log) == 1
    assert harness.transport.log[0]["method"] == "GET"

    # last30 tolerates the same drifted page: single GET, normal parse
    steady = make_harness([(200, build_page(options, include_range_select=False))])
    summary = mine_catalogue.mine_catalogue(
        steady.client, out_dir=tmp_path / "still", base_url=BASE_URL, mined_at=MINED_AT)
    assert summary["total"] == 1 and len(steady.transport.log) == 1


def test_duplicate_option_values_keep_first_occurrence(tmp_path):
    options = [
        ("bbbb11111111", "2026 Apr 04 - Second Entry (VenueB)"),
        ("aaaa00000000", "2026 Apr 04 - First Text Original   (VenueA)"),
        ("aaaa00000000", "DIFFERENT Later Label"),
        ("cccc22222222", "2026 Apr 04 - Third One"),
    ]
    harness = make_harness([(200, build_page(options))])
    summary = mine_catalogue.mine_catalogue(
        harness.client, out_dir=tmp_path, base_url=BASE_URL, mined_at=MINED_AT)
    assert summary["total"] == 3  # duplicate collapsed
    by_id = {comp["compId"]: comp for comp in summary["comps"]}
    survivor = by_id["aaaa00000000"]
    assert survivor["name"] == "2026 Apr 04 - First Text Original   (VenueA)"
    assert survivor["date"] == date(2026, 4, 4)
    assert survivor["title"] == "First Text Original"
    assert survivor["venue"] == "VenueA"


# ------------------------------------------------------- hypothesis properties


_MONTH_ABBRS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun",
                "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]
_TOKENS = st.text(alphabet=string.ascii_letters + string.digits,
                  min_size=1, max_size=10)
_COMP_ID_VALUES = st.text(alphabet="0123456789abcdefABCDEF",
                          min_size=10, max_size=15)


@st.composite
def _documented_row(draw):
    """One picker row built from documented parts, plus the parts themselves."""
    year = draw(st.integers(min_value=1998, max_value=2035))
    month_index = draw(st.integers(min_value=0, max_value=11))
    day = draw(st.integers(min_value=1, max_value=28))
    words = draw(st.lists(_TOKENS, min_size=1, max_size=5))
    gaps = [draw(st.integers(min_value=1, max_value=3)) for _ in range(len(words) - 1)]
    title = words[0] + "".join(" " * gap + word for gap, word in zip(gaps, words[1:]))
    padding = draw(st.integers(min_value=0, max_value=3))
    has_venue = draw(st.booleans())
    if has_venue:
        outer = draw(_TOKENS)
        if draw(st.booleans()):  # nested-parens venue like "(Venue X (Sub))"
            venue = f"{outer} ({draw(_TOKENS)})"
        else:
            venue = outer
        text = (f"{year} {_MONTH_ABBRS[month_index]} {day:02d} - {title}"
                + " " * padding + f"({venue})")
    else:
        venue = ""
        text = f"{year} {_MONTH_ABBRS[month_index]} {day:02d} - {title}" + " " * padding
    expected = {"date": date(year, month_index + 1, day),
                "title": title, "venue": venue, "name": text.strip()}
    return draw(_COMP_ID_VALUES), text, expected


@given(row=_documented_row())
@settings(max_examples=50, deadline=None)
def test_documented_option_format_round_trip(row):
    value, text, expected = row
    parsed = mine_catalogue.option_to_comp(value, text)
    assert parsed["compId"] == value
    assert parsed["name"] == expected["name"]          # full text minus outer whitespace
    assert parsed["date"] == expected["date"]          # the injected CALENDAR date
    assert parsed["title"] == expected["title"]        # trailing-run trim only
    assert parsed["venue"] == expected["venue"]        # last-parens-group rule


@st.composite
def _picker_scenarios(draw):
    """Unique-value option lists, some duplicated, shuffled into a document order."""
    unique_count = draw(st.integers(min_value=1, max_value=7))
    values = draw(st.lists(_COMP_ID_VALUES, min_size=unique_count,
                           max_size=unique_count, unique=True))
    rows = []
    for index, value in enumerate(values):
        year = draw(st.integers(min_value=1998, max_value=2035))
        month_index = draw(st.integers(min_value=0, max_value=11))
        day = draw(st.integers(min_value=1, max_value=28))
        word = draw(_TOKENS)
        venue_word = draw(_TOKENS)
        header = f"{year} {_MONTH_ABBRS[month_index]} {day:02d} - {word}"
        if index == 0:
            kind = "plain"     # guarantee the picker is locatable in every scenario
        else:
            kind = draw(st.sampled_from(["plain", "no_venue", "nested", "malformed"]))
        if kind == "plain":
            text, dated = f"{header} ({venue_word})", True
        elif kind == "no_venue":
            text, dated = header, True
        elif kind == "nested":
            text, dated = f"{header} ({venue_word} ({draw(_TOKENS)}))", True
        else:
            text, dated = f"Nope Not A Date {word}", False
        rows.append((value, text, dated))
    duplicates = [rows[draw(st.integers(min_value=0, max_value=len(rows) - 1))]
                  for _ in range(draw(st.integers(min_value=0, max_value=len(rows) * 2)))]
    document_order = draw(st.permutations(list(rows) + duplicates))
    return document_order


@given(document_order=_picker_scenarios())
@settings(max_examples=50, deadline=None)
def test_picker_dedupe_first_wins_and_sort_ordering_property(document_order):
    options = [(value, text) for value, text, _dated in document_order]
    comps = mine_catalogue.parse_comps(build_page(options))

    first_texts, dated_flags, unique_order = {}, {}, []
    for value, text, dated in document_order:
        if value not in first_texts:
            first_texts[value], dated_flags[value] = text, dated
            unique_order.append(value)
    assert len(comps) == len(unique_order)
    assert sorted(comp["compId"] for comp in comps) == sorted(unique_order)

    for comp in comps:
        value = comp["compId"]
        assert comp["name"] == first_texts[value]              # FIRST occurrence wins
        assert (comp["date"] is not None) == dated_flags[value]

    for first, second in zip(comps, comps[1:]):                # global pair consistency
        assert not (first["date"] is None and second["date"] is not None)
        if first["date"] is not None and second["date"] is not None:
            if first["date"] != second["date"]:
                assert first["date"] < second["date"]
            else:
                assert first["compId"] <= second["compId"]
