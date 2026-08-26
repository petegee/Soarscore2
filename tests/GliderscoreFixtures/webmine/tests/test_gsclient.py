#!/usr/bin/env python3
"""Unit + property tests for the webmine safety kernel (gsclient).

No live network: every test drives GsClient through an injected fake transport,
fake monotonic clock and fake sleep. Properties target the WI-1 invariants
from kanban/in-progress/gliderscore-webmine-tool.md:
allowlist closure, throttle floor, audit completeness/append-onlyness.
"""

import json
import math
import string
import tempfile
from collections import namedtuple
from datetime import datetime, timedelta
from pathlib import Path

import pytest
from hypothesis import given, settings
from hypothesis import strategies as st

import gsclient

BASE = "https://gliderscore.com"
DEFAULT_USER_AGENT = gsclient.DEFAULT_USER_AGENT
COMP_ID = "2381887cb81b"       # verified public comp, gliderscore-online-data-mining.md 2.4
UPPER_COMP_ID = "2381887CB81B"  # CompIDs are case-sensitive server-side
FAKE_BODY = b"ok"
ZIP_BODY = b"PK\x03\x04offline-zip-bytes\xff\x00"

AUDIT_REQUIRED_KEYS = {"ts", "op", "method", "url", "status", "bytes", "refused"}
AUDIT_OPTIONAL_KEYS = {"error"}

READ_ONLY_OPS = ["validate_comp_id", "check_scores_exist", "create_download_archive"]
ALL_OPS = READ_ONLY_OPS + ["download_zip", "delete_download_file", "escoring_page", "online_scores"]


# ---------------------------------------------------------------- helpers


class FakeClock:
    def __init__(self, start=1000.0):
        self.now = start

    def __call__(self):
        return self.now

    def advance(self, seconds):
        self.now += seconds


def exact_sleep_factory(clock):
    def sleep(seconds):
        clock.advance(seconds)
    return sleep


def granular_sleep_factory(clock, quantum):
    """Overshoots like a coarse-granularity OS timer; exercises the max(now, target) guard."""
    def sleep(seconds):
        clock.advance(math.ceil(seconds / quantum) * quantum)
    return sleep


class FakeTransport:
    """Records every request; fails scripted call indexes, else serves canned bodies."""

    def __init__(self, bodies=(), statuses=(), fault_calls=None, clock=None):
        self.bodies = list(bodies)
        self.statuses = list(statuses)
        self.fault_calls = dict(fault_calls or {})
        self.requests = []
        self.sent_at = []
        self._clock = clock

    def __call__(self, request_dict):
        self.requests.append(dict(request_dict))
        if self._clock is not None:
            self.sent_at.append(self._clock())
        fault = self.fault_calls.get(len(self.requests) - 1)
        if fault is not None:
            raise fault
        status = self.statuses.pop(0) if self.statuses else 200
        body = self.bodies.pop(0) if self.bodies else FAKE_BODY
        if isinstance(body, str):
            body = body.encode("utf-8")
        return {"status": status, "body": body}


ClientHarness = namedtuple("ClientHarness", "client clock transport audit_path")


def make_client(tmp_path=None, *, min_interval_seconds=2.0, bodies=(), statuses=(),
                fault_calls=None, user_agent=None, audit_name="audit.jsonl"):
    clock = FakeClock(1000.0)
    transport = FakeTransport(bodies=bodies, statuses=statuses,
                              fault_calls=fault_calls, clock=clock)
    kwargs = {
        "base_url": BASE,
        "min_interval_seconds": min_interval_seconds,
        "transport": transport,
        "clock": clock,
        "sleep": exact_sleep_factory(clock),
    }
    if tmp_path is not None:
        kwargs["audit_path"] = tmp_path / audit_name
    if user_agent is not None:
        kwargs["user_agent"] = user_agent
    return ClientHarness(gsclient.GsClient(**kwargs), clock, transport, kwargs.get("audit_path"))


def read_audit_lines(path):
    text = Path(path).read_text(encoding="utf-8")
    return [json.loads(line) for line in text.splitlines()]


def check_common_audit_fields(record):
    keys = set(record)
    assert AUDIT_REQUIRED_KEYS <= keys <= AUDIT_REQUIRED_KEYS | AUDIT_OPTIONAL_KEYS
    stamp = record["ts"]
    assert isinstance(stamp, str)
    parsed = _parse_utc(stamp)
    assert parsed.utcoffset() == timedelta(0)
    assert record["refused"] in (True, False)
    assert record["status"] is None or isinstance(record["status"], int)
    assert record["bytes"] is None or isinstance(record["bytes"], int)
    assert record["method"] in (None, "GET", "POST")


def _parse_utc(text):
    parsed = datetime.fromisoformat(text)
    assert parsed.tzinfo is not None
    return parsed


def execute_op(client, op, comp_id, index):
    if op == "validate_comp_id":
        return client.validate_comp_id(comp_id)
    if op == "check_scores_exist":
        return client.check_scores_exist(comp_id, from_round=2 + index % 5, to_round=50 + index)
    if op == "create_download_archive":
        return client.create_download_archive(comp_id)
    if op == "download_zip":
        return client.download_zip(comp_id)
    if op == "delete_download_file":
        return client.delete_download_file(comp_id)
    if op == "escoring_page":
        return client.escoring_page(comp_id, pilot_no=100 + index)
    return client.online_scores()


_BAD_COMP_IDS = [
    "",                      # empty
    "012345678",             # 9 chars, too short
    "0123456789ABCDEF",      # 16 chars, too long
    "zzzzzzzzzz",            # non-hex
    "12345678 9",            # embedded space
    " 2381887cb81b",         # leading space
    "2381887cb81b\n",        # trailing newline
    None,
    2381887,
]


# ------------------------------------------------------- example-based tests


def test_validate_comp_id_exact_url_and_verdict():
    harness = make_client(bodies=(gsclient.VALID_COMP_ID,))
    result = harness.client.validate_comp_id(COMP_ID)
    assert result == "ValidCompID" == gsclient.VALID_COMP_ID
    request = harness.transport.requests[0]
    assert request["method"] == "GET" and request["data"] is None
    assert request["url"] == f"{BASE}/scoringdatamanage.aspx?ACTION=ValidateCompID&ID={COMP_ID}"


def test_check_scores_exist_exact_url_default_and_custom_rounds():
    harness = make_client()
    harness.client.check_scores_exist(COMP_ID)
    first = harness.transport.requests[0]["url"]
    assert first == f"{BASE}/scoringdatadownload.aspx?ACTION=CheckScoresExist&ID={COMP_ID}&FR=1&TR=99"
    harness.client.check_scores_exist(UPPER_COMP_ID, from_round=12, to_round=347)
    second = harness.transport.requests[1]["url"]
    assert second == f"{BASE}/scoringdatadownload.aspx?ACTION=CheckScoresExist&ID={UPPER_COMP_ID}&FR=12&TR=347"


def test_create_download_archive_exact_url():
    harness = make_client(bodies=(gsclient.DOWNLOAD_FILE_CREATION_SUCCESS,))
    result = harness.client.create_download_archive(COMP_ID)
    assert result == "DownloadFileCreationSuccess"
    request = harness.transport.requests[0]
    assert request["url"] == f"{BASE}/scoringdatadownload.aspx?ACTION=CreateScoringDataAsZipArchive&ID={COMP_ID}"


def test_delete_download_file_exact_url():
    harness = make_client(bodies=(gsclient.DOWNLOAD_FILE_DELETE_SUCCESS,))
    result = harness.client.delete_download_file(COMP_ID)
    assert result == "DownloadFileDeleteSuccess"
    first = harness.transport.requests[0]["url"]
    assert first == f"{BASE}/scoringdatadownload.aspx?ACTION=DeleteDownloadFile&ID={COMP_ID}&FR=1&TR=99"
    harness.client.delete_download_file(COMP_ID, from_round=3, to_round=6)
    second = harness.transport.requests[1]["url"]
    assert second == f"{BASE}/scoringdatadownload.aspx?ACTION=DeleteDownloadFile&ID={COMP_ID}&FR=3&TR=6"


def test_escoring_page_exact_url_accepts_int_and_digit_string_pilots():
    harness = make_client()
    result = harness.client.escoring_page(COMP_ID, pilot_no=151)
    assert result == FAKE_BODY.decode("utf-8")
    assert harness.transport.requests[0]["url"] == f"{BASE}/eScoring.aspx?ID={COMP_ID}&P=151"
    harness.client.escoring_page(COMP_ID, pilot_no="075")
    assert harness.transport.requests[1]["url"] == f"{BASE}/eScoring.aspx?ID={COMP_ID}&P=075"


def test_download_zip_exact_url_raw_bytes_and_byte_audit(tmp_path):
    harness = make_client(tmp_path=tmp_path, bodies=(ZIP_BODY,), statuses=(200,))
    payload = harness.client.download_zip(COMP_ID)
    assert payload == ZIP_BODY
    assert isinstance(payload, bytes)
    request = harness.transport.requests[0]
    assert request["url"] == f"{BASE}/scoredownload/{COMP_ID}_DownloadData.zip"
    lines = read_audit_lines(harness.audit_path)
    assert len(lines) == 1
    record = lines[0]
    check_common_audit_fields(record)
    assert record["bytes"] == len(ZIP_BODY) and record["status"] == 200
    assert record["op"] == "download_zip" and record["refused"] is False


def test_online_scores_get_has_no_body():
    harness = make_client()
    result = harness.client.online_scores()
    assert result == FAKE_BODY.decode("utf-8")
    request = harness.transport.requests[0]
    assert request["method"] == "GET" and request["data"] is None
    assert request["url"] == f"{BASE}/OnLineScores.aspx"
    assert "Content-Type" not in request["headers"]


def test_online_scores_post_encodes_form_fields_verbatim_in_order():
    harness = make_client()
    postback = {"__VIEWSTATE": "/wEPDwUKLmS", "__EVENTTARGET": "ctl00$Main$btnLoad"}
    harness.client.online_scores(postback=postback)
    request = harness.transport.requests[0]
    assert request["method"] == "POST"
    assert request["url"] == f"{BASE}/OnLineScores.aspx"
    assert request["data"] == "__VIEWSTATE=%2FwEPDwUKLmS&__EVENTTARGET=ctl00%24Main%24btnLoad"
    assert request["headers"]["Content-Type"] == "application/x-www-form-urlencoded"


def test_comp_id_guards_fire_before_network_throttle_or_audit(tmp_path):
    harness = make_client(tmp_path=tmp_path)
    for bad in _BAD_COMP_IDS:
        for op in ALL_OPS:
            if op == "online_scores":
                continue
            args = (bad, 7) if op == "escoring_page" else (bad,)
            with pytest.raises(ValueError):
                getattr(harness.client, op)(*args)
    assert harness.transport.requests == []
    assert harness.transport.sent_at == []
    assert not Path(harness.audit_path).exists()


def test_comp_id_is_case_preserving_not_lowercased():
    harness = make_client(bodies=(gsclient.VALID_COMP_ID,))
    assert harness.client.validate_comp_id(UPPER_COMP_ID) == "ValidCompID"
    url = harness.transport.requests[0]["url"]
    assert url.endswith(f"&ID={UPPER_COMP_ID}")
    assert url == f"{BASE}/scoringdatamanage.aspx?ACTION=ValidateCompID&ID={UPPER_COMP_ID}"


def test_delete_download_file_swallows_transport_fault_and_audits_error(tmp_path):
    harness = make_client(tmp_path=tmp_path,
                          fault_calls={0: ConnectionResetError("reset by peer")})
    result = harness.client.delete_download_file(COMP_ID)
    assert result is None
    assert len(harness.transport.requests) == 1
    lines = read_audit_lines(harness.audit_path)
    assert len(lines) == 1
    record = lines[0]
    check_common_audit_fields(record)
    assert record["refused"] is False
    assert record["status"] is None and record["bytes"] is None
    assert "error" in record and "TransportError" in record["error"]
    assert "ConnectionResetError" in record["error"]


def test_classify_action_is_exact_case_sensitive_membership():
    for action in gsclient.READ_ONLY_ACTIONS:
        assert gsclient.classify_action(action) is True
    for wrong in ["ValidateCompid", "validateCompID", "CHECKSCORESEXIST",
                  "ScoresBackup7", "ValidateCompID ", " ValidateCompID",
                  "", "UploadX", "DeleteDownloadFile2"]:
        assert gsclient.classify_action(wrong) is False


def test_action_constant_sets_match_contract():
    assert gsclient.READ_ONLY_ACTIONS == frozenset({
        "ValidateCompID", "CheckScoresExist",
        "CreateScoringDataAsZipArchive", "DeleteDownloadFile"})
    assert gsclient.REFUSED_ACTIONS == frozenset({
        "DeleteComp", "MakeScoresZero", "RemoveData", "InsertDataFromZipFile",
        "ScoreEntryOpen", "ScoreEntryClose", "ScoresBackup", "ScoresRestore",
        "DeleteAllTransferFiles", "Upload"})
    assert not (gsclient.READ_ONLY_ACTIONS & gsclient.REFUSED_ACTIONS)
    assert gsclient.VALID_COMP_ID == "ValidCompID"
    assert gsclient.INVALID_COMP_ID == "InvalidCompID"
    assert gsclient.SCORING_DATA_FOUND == "ScoringDataFound"
    assert gsclient.NO_SCORING_DATA_FOUND == "NoScoringDataFound"
    assert gsclient.DOWNLOAD_FILE_CREATION_SUCCESS == "DownloadFileCreationSuccess"
    assert gsclient.DOWNLOAD_FILE_DELETE_SUCCESS == "DownloadFileDeleteSuccess"


def test_builders_refuse_when_allowlist_is_emptied(tmp_path, monkeypatch):
    monkeypatch.setattr(gsclient, "READ_ONLY_ACTIONS", frozenset())
    harness = make_client(tmp_path=tmp_path)
    for op, args in [("validate_comp_id", (COMP_ID,)),
                     ("check_scores_exist", (COMP_ID,)),
                     ("create_download_archive", (COMP_ID,)),
                     ("delete_download_file", (COMP_ID,))]:
        with pytest.raises(gsclient.RefusedError):
            getattr(harness.client, op)(*args)
    assert harness.transport.requests == []
    lines = read_audit_lines(harness.audit_path)
    assert len(lines) == 4
    assert all(record["refused"] is True and "error" in record for record in lines)


def test_throttle_example_min_interval_between_consecutive_sends():
    harness = make_client(min_interval_seconds=1.25)
    for _ in range(3):
        harness.client.online_scores()
    sent_at = harness.transport.sent_at
    assert len(sent_at) == 3
    assert sent_at[1] - sent_at[0] >= 1.25
    assert sent_at[2] - sent_at[1] >= 1.25
    floor_client = gsclient.GsClient(min_interval_seconds=1.0, base_url=BASE,
                                     transport=FakeTransport(), clock=FakeClock(),
                                     sleep=lambda seconds: None)
    assert isinstance(floor_client, gsclient.GsClient)
    for below_floor in (0.5, 0.999):
        with pytest.raises(ValueError):
            make_client(min_interval_seconds=below_floor)


def test_user_agent_default_and_override():
    harness = make_client()
    harness.client.online_scores()
    request = harness.transport.requests[0]
    assert request["headers"]["User-Agent"] == DEFAULT_USER_AGENT
    custom = make_client(user_agent="soarscore-webmine-tester/0.1 (+https://example.org)")
    custom.client.online_scores()
    assert custom.transport.requests[0]["headers"]["User-Agent"] == \
        "soarscore-webmine-tester/0.1 (+https://example.org)"


def test_audit_schema_single_line_per_event_example(tmp_path):
    harness = make_client(tmp_path=tmp_path, bodies=("ValidCompID", FAKE_BODY))
    harness.client.validate_comp_id(COMP_ID)
    harness.client.online_scores()
    lines = read_audit_lines(harness.audit_path)
    assert len(lines) == 2
    first, second = lines
    check_common_audit_fields(first)
    check_common_audit_fields(second)
    assert first["op"] == "validate_comp_id" and first["method"] == "GET"
    assert first["url"] == f"{BASE}/scoringdatamanage.aspx?ACTION=ValidateCompID&ID={COMP_ID}"
    assert first["status"] == 200 and first["bytes"] == len("ValidCompID")
    assert first["refused"] is False and "error" not in first
    assert second["op"] == "online_scores" and second["method"] == "GET"
    assert set(first) == AUDIT_REQUIRED_KEYS


# ------------------------------------------------------- hypothesis properties


_HEX_ALPHABET = "0123456789abcdefABCDEF"
_ACTION_ALPHABET = string.ascii_letters + string.digits
_COMP_IDS = st.text(alphabet=_HEX_ALPHABET, min_size=10, max_size=15)


@st.composite
def _action_candidates(draw):
    readonly = sorted(gsclient.READ_ONLY_ACTIONS)
    refused = sorted(gsclient.REFUSED_ACTIONS)
    kind = draw(st.sampled_from(["readonly", "refused", "suffix", "mutation", "random"]))
    if kind == "readonly":
        return draw(st.sampled_from(readonly))
    if kind == "refused":
        return draw(st.sampled_from(refused))
    if kind == "suffix":
        stem = draw(st.sampled_from(readonly + refused))
        tail = draw(st.text(_ACTION_ALPHABET, min_size=0, max_size=4))
        digits = draw(st.integers(min_value=0, max_value=999))
        return f"{stem}{tail}{digits}"
    if kind == "mutation":
        chars = list(draw(st.sampled_from(readonly)))
        pos = draw(st.integers(min_value=0, max_value=len(chars) - 1))
        chars[pos] = draw(st.sampled_from(string.ascii_letters))
        mutated = "".join(chars)
        return mutated.lower() if draw(st.booleans()) else mutated.upper()
    return draw(st.text(_ACTION_ALPHABET, min_size=1, max_size=24))


@given(candidate=_action_candidates())
@settings(max_examples=50, deadline=None)
def test_allowlist_closure_binary_outcome_property(candidate):
    with tempfile.TemporaryDirectory() as tmp:
        audit_path = Path(tmp) / "audit.jsonl"
        transport = FakeTransport()
        client = gsclient.GsClient(base_url=BASE, transport=transport, clock=FakeClock(),
                                   sleep=lambda seconds: None, audit_path=audit_path)
        verdict = gsclient.classify_action(candidate)
        assert isinstance(verdict, bool)  # binary: bool identity, never None, never raises
        assert verdict == (candidate in gsclient.READ_ONLY_ACTIONS)
        if verdict:
            client._gate("probe", candidate)
            assert not audit_path.exists()
        else:
            with pytest.raises(gsclient.RefusedError):
                client._gate("probe", candidate)
            lines = read_audit_lines(audit_path)
            assert len(lines) == 1
            record = lines[0]
            check_common_audit_fields(record)
            assert record["refused"] is True and record["op"] == "probe"
            assert record["url"] is None and "error" in record
        assert transport.requests == []


@st.composite
def _throttle_scenarios(draw):
    interval = draw(st.one_of(st.just(1.0), st.floats(min_value=1.0, max_value=4.0)))
    sleep_mode = draw(st.sampled_from(["exact", "granular"]))
    ops = draw(st.lists(st.sampled_from(ALL_OPS), min_size=1, max_size=10))
    comps = draw(st.lists(_COMP_IDS, min_size=1, max_size=3))
    start = draw(st.floats(min_value=0.0, max_value=10000.0))
    return interval, sleep_mode, ops, comps, start


@given(scenario=_throttle_scenarios())
@settings(max_examples=50, deadline=None)
def test_throttle_floor_holds_for_generated_op_sequences(scenario):
    interval, sleep_mode, ops, comps, start = scenario
    clock = FakeClock(start)
    sleep = exact_sleep_factory(clock) if sleep_mode == "exact" \
        else granular_sleep_factory(clock, quantum=0.05)
    transport = FakeTransport(clock=clock)
    client = gsclient.GsClient(base_url=BASE, min_interval_seconds=interval,
                               transport=transport, clock=clock, sleep=sleep)
    for index, op in enumerate(ops):
        execute_op(client, op, comps[index % len(comps)], index)
    sent_at = transport.sent_at
    assert len(sent_at) == len(ops)
    for previous, current in zip(sent_at, sent_at[1:]):
        assert current - previous >= interval - 1e-9


@given(interval=st.floats(min_value=-10.0, max_value=0.9999))
@settings(max_examples=50, deadline=None)
def test_constructor_rejects_min_interval_below_one_second(interval):
    transport = FakeTransport()
    with pytest.raises(ValueError):
        gsclient.GsClient(base_url=BASE, min_interval_seconds=interval,
                          transport=transport, clock=FakeClock(), sleep=lambda seconds: None)
    assert transport.requests == []


@st.composite
def _audit_plans(draw):
    comps = draw(st.lists(_COMP_IDS, min_size=1, max_size=4))
    plan = []
    for index in range(draw(st.integers(min_value=1, max_value=8))):
        kind = draw(st.sampled_from(["send_ok", "send_fault", "refusal"]))
        if kind == "refusal":
            plan.append(("refusal", draw(st.sampled_from(sorted(gsclient.REFUSED_ACTIONS))), None))
        else:
            plan.append((kind, draw(st.sampled_from(ALL_OPS)), comps[index % len(comps)]))
    return plan


@given(plan=_audit_plans())
@settings(max_examples=50, deadline=None)
def test_audit_completeness_and_append_only_property(plan):
    with tempfile.TemporaryDirectory() as tmp:
        audit_path = Path(tmp) / "audit.jsonl"
        clock = FakeClock(500.0)
        fault_calls = {}
        call_index = 0
        for kind, _, _ in [(step[0], step[1], step[2]) for step in plan]:
            if kind == "send_fault":
                fault_calls[call_index] = ConnectionResetError("simulated reset")
            if kind in ("send_ok", "send_fault"):
                call_index += 1
        transport = FakeTransport(fault_calls=fault_calls, clock=clock)
        client = gsclient.GsClient(base_url=BASE, transport=transport, clock=clock,
                                   sleep=exact_sleep_factory(clock), audit_path=audit_path)
        next_call = 0
        for index, (kind, target, comp_id) in enumerate(plan):
            record_count_before = len(read_audit_lines(audit_path)) if audit_path.exists() else 0
            will_fault = False
            if kind == "refusal":
                op_label = f"probe{index}"
                with pytest.raises(gsclient.RefusedError):
                    client._gate(op_label, target)
            else:
                will_fault = next_call in fault_calls
                next_call += 1
                if will_fault and target != "delete_download_file":
                    with pytest.raises(gsclient.TransportError):
                        execute_op(client, target, comp_id, index)
                else:
                    result = execute_op(client, target, comp_id, index)
                    if will_fault:
                        assert result is None  # delete swallows the transport fault
            lines = read_audit_lines(audit_path)
            assert len(lines) == record_count_before + 1
            record = lines[-1]
            check_common_audit_fields(record)
            if kind == "refusal":
                assert record["refused"] is True and record["op"] == f"probe{index}"
                assert record["url"] is None and "error" in record
                continue
            # network-send records carry full evidence
            assert record["refused"] is False
            assert record["url"].startswith(BASE)
            assert record["method"] == "GET"
            if kind == "send_fault":
                assert record["status"] is None and record["bytes"] is None
                assert "error" in record
            else:
                assert record["status"] == 200 and record["bytes"] == len(FAKE_BODY)
        prefix_text = audit_path.read_text(encoding="utf-8")
        second = gsclient.GsClient(base_url=BASE, transport=FakeTransport(clock=clock),
                                   clock=clock, sleep=exact_sleep_factory(clock),
                                   audit_path=audit_path)
        second.validate_comp_id(COMP_ID)
        after_text = audit_path.read_text(encoding="utf-8")
        assert after_text[:len(prefix_text)] == prefix_text
        appended = [json.loads(line) for line in after_text[len(prefix_text):].splitlines()]
        assert [record["op"] for record in appended] == ["validate_comp_id"]
        assert all(check_common_audit_fields(record) is None for record in appended)
