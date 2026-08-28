#!/usr/bin/env python3
"""Unit tests for the webmine comp fetcher (fetch_comp).

Self-contained fakes (no imports from test_gsclient.py): a scripted fake
transport records every request and can raise faults per call index; a fake
monotonic clock plus no-op sleep satisfy the kernel's throttle without delay.
Synthetic zips are built in-test with zipfile. No live network.
"""

import dataclasses
import io
import json
import zipfile
from collections import namedtuple
from pathlib import Path

import pytest

import csvparse
import fetch_comp
import gsclient

BASE = "https://gliderscore.com"
COMP_ID = "2381887cb81b"        # verified public comp (mining doc 2.4)
UPPER_COMP_ID = "2381887CB81B"  # CompIDs are case-sensitive server-side

FOUND = gsclient.SCORING_DATA_FOUND
CREATE_OK = gsclient.DOWNLOAD_FILE_CREATION_SUCCESS
DELETE_OK = gsclient.DOWNLOAD_FILE_DELETE_SUCCESS


def _record(**overrides):
    values = dict(
        comp_id=COMP_ID, comp_type="F3K",
        round_no=0, group_no=0, reflight_no=0, pilot_no=75,
        data1=0.0, data2=0.0, data3=0.0, data4=0.0, data5=0.0, data6=0.0,
        data7=0.0, landing_over_75m=0.0, penalty=0.0,
        pilot_name="", model_id="",
        flight1=0.0, flight2=0.0, flight3=0.0, flight4=0.0,
        f5j_motor_re_started=0,
    )
    values.update(overrides)
    return csvparse.DownloadRecord(**values)


# Three-row draw: pilots {75, 82}; (round, group) pairs {(1,1), (1,2), (2,1)}.
FIXTURE_RECORDS = [
    _record(round_no=1, group_no=1, data1=249.5,
            pilot_name="Botherway", model_id="Vesper"),
    _record(round_no=1, group_no=2, data1=400.25, penalty=-30.5,
            pilot_name="Pawson-Ødegård", model_id="Argo X2"),
    _record(round_no=2, group_no=1, pilot_no=82, data1=500.125,
            pilot_name="Chao-Hui Wu", model_id="Foamtec"),
]


def fixture_csv_bytes():
    text = "\n".join(csvparse.render_line(record) for record in FIXTURE_RECORDS)
    return (text + "\n").encode("utf-8")


def make_zip_bytes(members):
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w") as archive:
        for name, payload in members:
            archive.writestr(name, payload)
    return buffer.getvalue()


def csv_member_name(comp_id):
    return f"{comp_id}_DownloadData.csv"


ZIP_BYTES = make_zip_bytes([(csv_member_name(COMP_ID), fixture_csv_bytes())])


def replace_comp_id(record, comp_id):
    return dataclasses.replace(record, comp_id=comp_id)

# Hand-written expected records JSON: flat camelCase keys, typed values,
# deliberately independent of csvparse.record_to_camel_dict.
EXPECTED_RECORDS_JSON = {
    "compId": COMP_ID,
    "compType": "F3K",
    "csvEntry": f"{COMP_ID}_DownloadData.csv",
    "recordCount": 3,
    "records": [
        {
            "compId": COMP_ID, "compType": "F3K",
            "roundNo": 1, "groupNo": 1, "reflightNo": 0, "pilotNo": 75,
            "data1": 249.5, "data2": 0.0, "data3": 0.0, "data4": 0.0,
            "data5": 0.0, "data6": 0.0, "data7": 0.0,
            "landingOver75m": 0.0, "penalty": 0.0,
            "pilotName": "Botherway", "modelId": "Vesper",
            "flight1": 0.0, "flight2": 0.0, "flight3": 0.0, "flight4": 0.0,
            "f5jMotorReStarted": 0,
        },
        {
            "compId": COMP_ID, "compType": "F3K",
            "roundNo": 1, "groupNo": 2, "reflightNo": 0, "pilotNo": 75,
            "data1": 400.25, "data2": 0.0, "data3": 0.0, "data4": 0.0,
            "data5": 0.0, "data6": 0.0, "data7": 0.0,
            "landingOver75m": 0.0, "penalty": -30.5,
            "pilotName": "Pawson-Ødegård", "modelId": "Argo X2",
            "flight1": 0.0, "flight2": 0.0, "flight3": 0.0, "flight4": 0.0,
            "f5jMotorReStarted": 0,
        },
        {
            "compId": COMP_ID, "compType": "F3K",
            "roundNo": 2, "groupNo": 1, "reflightNo": 0, "pilotNo": 82,
            "data1": 500.125, "data2": 0.0, "data3": 0.0, "data4": 0.0,
            "data5": 0.0, "data6": 0.0, "data7": 0.0,
            "landingOver75m": 0.0, "penalty": 0.0,
            "pilotName": "Chao-Hui Wu", "modelId": "Foamtec",
            "flight1": 0.0, "flight2": 0.0, "flight3": 0.0, "flight4": 0.0,
            "f5jMotorReStarted": 0,
        },
    ],
}


# ---------------------------------------------------------------- fake kernel


class FakeClock:
    def __init__(self, start=1000.0):
        self.now = start

    def __call__(self):
        return self.now

    def advance(self, seconds):
        self.now += seconds


class FakeTransport:
    """Records every request; raises scripted faults, else serves canned bodies."""

    def __init__(self, bodies=(), fault_calls=None):
        self.bodies = list(bodies)
        self.fault_calls = dict(fault_calls or {})
        self.requests = []

    def __call__(self, request_dict):
        self.requests.append(dict(request_dict))
        fault = self.fault_calls.get(len(self.requests) - 1)
        if fault is not None:
            raise fault
        body = self.bodies.pop(0) if self.bodies else b"ok"
        if isinstance(body, str):
            body = body.encode("utf-8")
        return {"status": 200, "body": body}


Harness = namedtuple("Harness", "client transport")


def make_client(bodies=(), fault_calls=None, audit_path=None):
    clock = FakeClock(1000.0)
    transport = FakeTransport(bodies=bodies, fault_calls=fault_calls)
    client = gsclient.GsClient(
        base_url=BASE, min_interval_seconds=1.0, transport=transport,
        clock=clock, sleep=clock.advance, audit_path=audit_path,
    )
    return Harness(client, transport)


def read_audit_lines(path):
    return [json.loads(line) for line in Path(path).read_text(encoding="utf-8").splitlines()]


def fetch(tmp_path, harness, *, keep_zip=False, from_round=1, to_round=99,
          out_dir=None, name=None, tasks=False, escoring_pilot=None):
    stdout_io, stderr_io = io.StringIO(), io.StringIO()
    out = out_dir if out_dir is not None else tmp_path / "out"
    payload = fetch_comp.fetch_competition(
        harness.client, COMP_ID,
        from_round=from_round, to_round=to_round, out_dir=out,
        keep_zip=keep_zip, stdout=stdout_io, stderr=stderr_io,
        name=name, tasks=tasks, escoring_pilot=escoring_pilot,
    )
    return payload, out, stdout_io.getvalue(), stderr_io.getvalue()


def check_urls(harness):
    return [request["url"] for request in harness.transport.requests]


# ------------------------------------------------------------ example-based


def test_happy_path_four_network_calls_then_exact_artifacts(tmp_path):
    harness = make_client(bodies=(FOUND, CREATE_OK, ZIP_BYTES, DELETE_OK))
    payload, out_dir, stdout_text, _ = fetch(
        tmp_path, harness, from_round=2, to_round=6)

    assert len(harness.transport.requests) == 4
    methods = {request["method"] for request in harness.transport.requests}
    assert methods == {"GET"}
    urls = check_urls(harness)
    assert urls == [
        f"{BASE}/scoringdatadownload.aspx"
        f"?ACTION=CheckScoresExist&ID={COMP_ID}&FR=2&TR=6",
        f"{BASE}/scoringdatadownload.aspx"
        f"?ACTION=CreateScoringDataAsZipArchive&ID={COMP_ID}",
        f"{BASE}/scoredownload/{COMP_ID}_DownloadData.zip",
        f"{BASE}/scoringdatadownload.aspx"
        f"?ACTION=DeleteDownloadFile&ID={COMP_ID}&FR=2&TR=6",
    ]

    csv_path = out_dir / csv_member_name(COMP_ID)
    json_path = out_dir / f"{COMP_ID}_records.json"
    assert csv_path.is_file() and json_path.is_file()
    # Exact extracted member bytes land verbatim as the CSV artifact.
    assert csv_path.read_bytes() == fixture_csv_bytes()

    loaded = json.loads(json_path.read_text(encoding="utf-8"))
    assert loaded == EXPECTED_RECORDS_JSON
    assert loaded == payload
    # Typed round-trip through JSON: ints stay ints, floats stay floats.
    assert type(loaded["records"][0]["roundNo"]) is int
    assert type(loaded["records"][0]["reflightNo"]) is int
    assert type(loaded["records"][0]["f5jMotorReStarted"]) is int
    assert type(loaded["records"][1]["penalty"]) is float
    assert type(loaded["records"][2]["data1"]) is float
    assert loaded["records"][0]["data2"] == 0.0
    assert type(loaded["records"][0]["data2"]) is float  # "0" placeholder -> float zero

    assert "steps:" in stdout_text
    assert "check_scores_exist" in stdout_text
    assert "create_download_archive" in stdout_text
    assert "download_zip" in stdout_text
    assert "delete_download_file" in stdout_text
    assert "records: 3" in stdout_text
    assert "pilots: 2" in stdout_text
    assert "round-group pairs: 3" in stdout_text

    # WI-4 triage artifact is written alongside and named as primary.
    triage_path = out_dir / f"{COMP_ID}_triage.json"
    assert triage_path.is_file()
    assert f"{COMP_ID}_triage.json" in stdout_text
    assert "primary triage" in stdout_text

    doc = json.loads(triage_path.read_text(encoding="utf-8"))
    assert doc["compId"] == COMP_ID
    assert doc["compType"] == "F3K"
    assert doc["name"] is None                      # no --name given
    assert doc["pilots"] == [
        {"pilotNo": 75, "name": "Botherway"},
        {"pilotNo": 82, "name": "Chao-Hui Wu"},
    ]
    assert [(b["round"], b["group"], b["reflight"]) for b in doc["rounds"]] \
        == [(1, 1, 0), (1, 2, 0), (2, 1, 0)]
    flights_per_bucket = [[a["flights"] for a in b["assignments"]]
                          for b in doc["rounds"]]
    assert flights_per_bucket == [[[249.5]], [[400.25]], [[500.125]]]
    assert all(len(b["assignments"]) == 1 for b in doc["rounds"])
    penalty_travels = [a["penalty"] for a in doc["rounds"][1]["assignments"]]
    assert penalty_travels == [-30.5]
    assert doc["limitations"] and "F3K" in doc["limitations"][0]
    assert any("task-letter mapping NOT resolved" in line
               for line in doc["limitations"])
    # Informational per-pilot summaries ride along under "drawGaps". Both
    # pilots here flew exactly one base round each (pilot 75 across two
    # groups), so no whole-round shortfall flag applies to this fixture.
    assert doc["drawGaps"] == [
        "pilot 75 (Botherway): base slots per round r1:2; "
        "base rounds total 1",
        "pilot 82 (Chao-Hui Wu): base slots per round r2:1; "
        "base rounds total 1",
    ]


def test_no_scoring_data_found_short_circuits_after_exactly_one_call():
    harness = make_client(bodies=(gsclient.NO_SCORING_DATA_FOUND,))
    with pytest.raises(SystemExit) as caught:
        fetch_comp.fetch_competition(harness.client, COMP_ID)
    assert "nothing to fetch" in str(caught.value)
    assert len(harness.transport.requests) == 1
    assert harness.transport.requests[0]["url"].endswith(
        f"ACTION=CheckScoresExist&ID={COMP_ID}&FR=1&TR=99")


def test_unexpected_check_body_is_a_loud_exit():
    harness = make_client(bodies=("<html>error 500</html>",))
    with pytest.raises(SystemExit) as caught:
        fetch_comp.fetch_competition(harness.client, COMP_ID)
    message = str(caught.value)
    assert "unexpected CheckScoresExist response" in message
    assert "html" in message  # body excerpt survives into the complaint


def test_create_failure_exits_nonzero_and_delete_is_attempted_and_audited(tmp_path):
    audit_path = tmp_path / "audit.jsonl"
    harness = make_client(bodies=(FOUND, "WarpDriveEngaged", DELETE_OK),
                          audit_path=audit_path)
    with pytest.raises(SystemExit) as caught:
        fetch(tmp_path, harness)
    assert "CreateScoringDataAsZipArchive" in str(caught.value)
    assert "'WarpDriveEngaged'" in str(caught.value)
    # Leave no trace: delete fired despite the failed create.
    urls = check_urls(harness)
    assert len(urls) == 3
    assert f"ACTION=DeleteDownloadFile&ID={COMP_ID}&FR=1&TR=99" in urls[-1]
    lines = read_audit_lines(audit_path)
    assert [line["op"] for line in lines] == [
        "check_scores_exist", "create_download_archive", "delete_download_file"]
    assert all(line["refused"] is False for line in lines)
    assert all({"ts", "op", "url"} <= set(line) for line in lines)


def test_transport_fault_during_zip_download_still_deletes(tmp_path):
    audit_path = tmp_path / "audit.jsonl"
    harness = make_client(bodies=(FOUND, CREATE_OK, DELETE_OK),
                          fault_calls={2: OSError("connection reset mid-body")},
                          audit_path=audit_path)
    with pytest.raises(SystemExit) as caught:
        fetch(tmp_path, harness)
    assert "transport failure downloading zip" in str(caught.value)
    assert len(harness.transport.requests) == 4
    urls = check_urls(harness)
    assert f"{BASE}/scoredownload/{COMP_ID}_DownloadData.zip" in urls[2]
    assert f"ACTION=DeleteDownloadFile&ID={COMP_ID}&FR=1&TR=99" in urls[-1]
    lines = read_audit_lines(audit_path)
    assert lines[-1]["op"] == "delete_download_file"
    for name in (csv_member_name(COMP_ID), f"{COMP_ID}_records.json"):
        assert not (tmp_path / "out" / name).exists()


def test_zip_surprise_extra_entry_lists_every_entry_and_writes_nothing(tmp_path):
    surprise_zip = make_zip_bytes([
        (csv_member_name(COMP_ID), fixture_csv_bytes()),
        ("setup.xml", b"<setup/>"),
    ])
    harness = make_client(bodies=(FOUND, CREATE_OK, surprise_zip, DELETE_OK))
    err_io = io.StringIO()
    with pytest.raises(SystemExit) as caught:
        fetch_comp.fetch_competition(
            harness.client, COMP_ID, out_dir=tmp_path / "out",
            stdout=io.StringIO(), stderr=err_io)
    assert "unexpected zip contents" in str(caught.value)
    # Nothing was written under --out: no csv, no records json.
    out_dir = tmp_path / "out"
    assert not out_dir.exists() or not any(out_dir.iterdir())
    # Every entry name was enumerated as triage evidence.
    stderr_text = err_io.getvalue()
    assert csv_member_name(COMP_ID) in stderr_text
    assert "setup.xml" in stderr_text
    assert "expected exactly one member" in stderr_text


def test_zip_surprise_wrong_single_member_with_keep_zip_keeps_evidence(tmp_path):
    weird_zip = make_zip_bytes([("totally_different.csv", b"stuff")])
    harness = make_client(bodies=(FOUND, CREATE_OK, weird_zip, DELETE_OK))
    with pytest.raises(SystemExit):
        fetch_comp.fetch_competition(
            harness.client, COMP_ID, out_dir=tmp_path / "out",
            keep_zip=True, stdout=io.StringIO(), stderr=io.StringIO())
    out_dir = tmp_path / "out"
    kept = out_dir / f"{COMP_ID}_DownloadData.zip"
    assert kept.is_file()
    assert kept.read_bytes() == weird_zip
    names = sorted(path.name for path in out_dir.iterdir())
    assert names == [kept.name]  # evidence only; no artifacts


def test_case_sensitive_comp_id_survives_end_to_end(tmp_path):
    upper_records = [
        replace_comp_id(record, UPPER_COMP_ID) for record in FIXTURE_RECORDS
    ]
    text = "\n".join(csvparse.render_line(r) for r in upper_records) + "\n"
    upper_zip = make_zip_bytes([(csv_member_name(UPPER_COMP_ID),
                                 text.encode("utf-8"))])
    harness = make_client(bodies=(FOUND, CREATE_OK, upper_zip, DELETE_OK))
    stdout_io = io.StringIO()
    payload = fetch_comp.fetch_competition(
        harness.client, UPPER_COMP_ID, out_dir=tmp_path / "out",
        stdout=stdout_io)
    # Case-exact everywhere: URL query value, filename, top-level key, rows.
    first_url = harness.transport.requests[0]["url"]
    assert f"ID={UPPER_COMP_ID}" in first_url
    assert (tmp_path / "out" / csv_member_name(UPPER_COMP_ID)).is_file()
    assert (tmp_path / "out" / f"{UPPER_COMP_ID}_records.json").is_file()
    assert payload["compId"] == UPPER_COMP_ID
    assert all(row["compId"] == UPPER_COMP_ID for row in payload["records"])
    assert csv_member_name(COMP_ID) != csv_member_name(UPPER_COMP_ID)


# ------------------------------------------------- WI-4 triage conversion

DURATION_RECORDS = [
    _record(comp_type="F5J", round_no=1, group_no=1, pilot_no=75,
            pilot_name="Botherway", model_id="Vesper",
            data1=2, data2=4, data3=31, data4=5, data5=12,
            data6=-25.5, data7=88),
    _record(comp_type="F5J", round_no=2, group_no=1, pilot_no=75,
            pilot_name="Botherway", model_id="Vesper"),
]

# Synthetic pilot screen built from the documented eScoring.aspx shape
# (mining doc §2.3); live verification awaits the permission gate.
PILOT_SCREEN_HTML = (
    "<html><body><h2>Pilot screen</h2>"
    "<table>"
    "<tr><td>Round 1</td><td>L1 5max in 7m</td></tr>"
    "<tr><td>Round 2</td><td>AllUp 3:00*3</td></tr>"
    "</table>"
    "<p>Unrelated footer.</p>"
    "</body></html>"
)


def duration_csv_bytes():
    text = "\n".join(csvparse.render_line(r) for r in DURATION_RECORDS)
    return (text + "\n").encode("utf-8")


def read_triage_doc(out_dir):
    path = out_dir / f"{COMP_ID}_triage.json"
    return json.loads(path.read_text(encoding="utf-8"))


# Happy-path CSV written exactly as the live server writes it (confirmed
# 2026-08-27 against CompID 2381887cb81b): VB 'True'/'False' flags and an
# unset ModelID. Hand-authored strings, deliberately NOT via render_line,
# so the wire spellings themselves are what the pipeline consumes.
BOOLEAN_TOKEN_CSV_LINES = [
    f"{COMP_ID}|F5J|1|1|0|75|0.000|4.000|31.000|5.000|12.000|-25.500"
    f"|88.000|False|0.000|Botherway|Vesper|||||True",
    f"{COMP_ID}|F5J|2|1|0|82|0.000|0.000|0.000|0.000|0.000|0.000|0.000"
    f"|True|0.000|Chao-Hui Wu|Foamtec|||||False",
]


def boolean_token_csv_bytes():
    return ("\n".join(BOOLEAN_TOKEN_CSV_LINES) + "\n").encode("utf-8")


def test_happy_path_boolean_token_csv_emits_native_bools_keeps_decodes(tmp_path):
    flag_zip = make_zip_bytes([
        (csv_member_name(COMP_ID), boolean_token_csv_bytes())
    ])
    harness = make_client(bodies=(FOUND, CREATE_OK, flag_zip, DELETE_OK))
    payload, out_dir, stdout_text, _ = fetch(tmp_path, harness)

    assert payload["recordCount"] == 2
    loaded = json.loads(
        (out_dir / f"{COMP_ID}_records.json").read_text(encoding="utf-8"))
    # Triage JSON emits true/false natively (never 0/1).
    first = loaded["records"][0]
    second = loaded["records"][1]
    assert type(first["f5jMotorReStarted"]) is bool and first["f5jMotorReStarted"] is True
    assert type(first["landingOver75m"]) is bool and first["landingOver75m"] is False
    assert type(second["landingOver75m"]) is bool and second["landingOver75m"] is True
    assert type(second["f5jMotorReStarted"]) is bool and second["f5jMotorReStarted"] is False

    doc = read_triage_doc(out_dir)
    assert doc["compType"] == "F5J"
    round1 = doc["rounds"][0]["assignments"][0]
    # Duration decode math is untouched by the boolean passthrough.
    assert round1["time1Seconds"] == 271           # 4*60 + 31
    assert round1["time2Seconds"] == 312           # 5*60 + 12
    assert round1["deduction"] == -25.5
    assert round1["landing"] == 88.0
    assert round1["landingOver75m"] is False
    assert round1["f5jMotorReStarted"] is True
    round2 = doc["rounds"][1]["assignments"][0]
    assert round2["landingOver75m"] is True
    assert round2["f5jMotorReStarted"] is False

    assert "records: 2" in stdout_text
    assert len(check_urls(harness)) == 4


def test_duration_split_math_flows_through_whole_pipeline(tmp_path):
    duration_zip = make_zip_bytes([
        (csv_member_name(COMP_ID), duration_csv_bytes())
    ])
    harness = make_client(bodies=(FOUND, CREATE_OK, duration_zip, DELETE_OK))
    _, out_dir, stdout_text, _ = fetch(tmp_path, harness)

    doc = read_triage_doc(out_dir)
    assert doc["compType"] == "F5J"
    round1 = doc["rounds"][0]["assignments"][0]
    assert round1["time1Seconds"] == 271          # 4*60 + 31 via zip+parse+convert
    assert type(round1["time1Seconds"]) is int
    assert round1["time2Seconds"] == 312
    assert round1["laps"] == 2
    assert round1["deduction"] == -25.5           # sign never transformed
    assert round1["landing"] == 88.0              # Data7 slot, distinct from…
    assert round1["landingOver75m"] == 0.0        # …the always-common column
    assert round1["raw"] == [2.0, 4.0, 31.0, 5.0, 12.0, -25.5, 88.0]
    round2 = doc["rounds"][1]["assignments"][0]   # all-zero slots decode away
    assert set(round2) - {"raw"} == {
        "pilotNo", "pilotName", "penalty", "landingOver75m",
        "f5jMotorReStarted"}
    assert round2["raw"] == [0.0] * 7
    assert any("minute/second splits" in line for line in doc["limitations"])
    assert "tasksByRound" not in doc              # --tasks default OFF
    assert len(check_urls(harness)) == 4


def test_draw_violations_exit_nonzero_after_delete_and_withhold_triage(tmp_path):
    audit_path = tmp_path / "audit.jsonl"
    dup_rows = [
        _record(round_no=1, group_no=1, reflight_no=0, pilot_no=75,
                pilot_name="Botherway"),
        _record(round_no=1, group_no=1, reflight_no=0, pilot_no=75,
                pilot_name="Botherway"),
        _record(round_no=2, group_no=1, reflight_no=0, pilot_no=82,
                pilot_name="Chao-Hui Wu"),
    ]
    text = "\n".join(csvparse.render_line(r) for r in dup_rows) + "\n"
    dup_zip = make_zip_bytes([(csv_member_name(COMP_ID), text.encode("utf-8"))])
    harness = make_client(bodies=(FOUND, CREATE_OK, dup_zip, DELETE_OK),
                          audit_path=audit_path)
    err_io = io.StringIO()
    with pytest.raises(SystemExit) as caught:
        fetch_comp.fetch_competition(
            harness.client, COMP_ID, out_dir=tmp_path / "out",
            stdout=io.StringIO(), stderr=err_io)
    message = str(caught.value)
    assert "draw-completeness violation" in message
    # Every violation printed as evidence before the abort.
    assert err_io.getvalue().count("(round 1, group 1, pilot 75)") == 1
    # Triage withheld; csv + records json stay on disk as evidence.
    out_dir = tmp_path / "out"
    assert not (out_dir / f"{COMP_ID}_triage.json").exists()
    assert (out_dir / csv_member_name(COMP_ID)).is_file()
    assert (out_dir / f"{COMP_ID}_records.json").is_file()
    # Delete STILL fired (leave no trace); no extra network ops occurred.
    urls = check_urls(harness)
    assert len(urls) == 4
    assert f"ACTION=DeleteDownloadFile&ID={COMP_ID}&FR=1&TR=99" in urls[-1]
    lines = read_audit_lines(audit_path)
    assert [line["op"] for line in lines] == [
        "check_scores_exist", "create_download_archive", "download_zip",
        "delete_download_file"]


def test_empty_download_is_loud_even_when_the_zip_guard_passes(tmp_path):
    empty_zip = make_zip_bytes([(csv_member_name(COMP_ID), b"")])
    harness = make_client(bodies=(FOUND, CREATE_OK, empty_zip, DELETE_OK))
    with pytest.raises(SystemExit) as caught:
        fetch_comp.fetch_competition(
            harness.client, COMP_ID, out_dir=tmp_path / "out",
            stdout=io.StringIO(), stderr=io.StringIO())
    assert "no records" in str(caught.value)
    urls = check_urls(harness)
    assert len(urls) == 4
    assert f"ACTION=DeleteDownloadFile&ID={COMP_ID}&FR=1&TR=99" in urls[-1]
    assert not (tmp_path / "out" / f"{COMP_ID}_triage.json").exists()


def test_tasks_flag_adds_exactly_one_escoring_call_then_deletes_last(tmp_path):
    audit_path = tmp_path / "audit.jsonl"
    harness = make_client(bodies=(FOUND, CREATE_OK, ZIP_BYTES,
                                  PILOT_SCREEN_HTML, DELETE_OK),
                          audit_path=audit_path)
    _, out_dir, stdout_text, stderr_text = fetch(tmp_path, harness,
                                                 tasks=True)
    urls = check_urls(harness)
    assert len(urls) == 5
    # Default pilot = smallest pilot number present in the records (75).
    assert f"{BASE}/eScoring.aspx?ID={COMP_ID}&P=75" in urls[3]
    assert "ACTION=DeleteDownloadFile&ID={0}&FR=1&TR=99".format(COMP_ID) \
        in urls[4]
    lines = read_audit_lines(audit_path)
    assert len(lines) == 5
    assert [line["op"] for line in lines] == [
        "check_scores_exist", "create_download_archive", "download_zip",
        "escoring_page", "delete_download_file"]
    doc = read_triage_doc(out_dir)
    # JSON object keys are stringified round numbers.
    assert doc["tasksByRound"] == {"1": "L1 5max in 7m", "2": "AllUp 3:00*3"}
    assert stderr_text == ""                      # clean scrape, no warning
    assert "escoring_page" in stdout_text


def test_escoring_pilot_override_targets_maximal_pilot(tmp_path):
    harness = make_client(bodies=(FOUND, CREATE_OK, ZIP_BYTES,
                                  PILOT_SCREEN_HTML, DELETE_OK))
    _, out_dir, _, _ = fetch(tmp_path, harness, tasks=True,
                             escoring_pilot=82)
    urls = check_urls(harness)
    assert f"{BASE}/eScoring.aspx?ID={COMP_ID}&P=82" in urls[3]
    assert read_triage_doc(out_dir)["tasksByRound"] == {
        "1": "L1 5max in 7m", "2": "AllUp 3:00*3"}


def test_task_scrape_transport_failure_warns_but_keeps_artifacts(tmp_path):
    harness = make_client(bodies=(FOUND, CREATE_OK, ZIP_BYTES, DELETE_OK),
                          fault_calls={3: OSError("reset during scrape")})
    err_io = io.StringIO()
    payload = fetch_comp.fetch_competition(
        harness.client, COMP_ID, out_dir=tmp_path / "out",
        stdout=io.StringIO(), stderr=err_io, tasks=True)
    # Best-effort scrape: zip artifacts still exist and exit stays zero.
    assert payload["recordCount"] == 3
    assert (tmp_path / "out" / f"{COMP_ID}_triage.json").is_file()
    assert "task scrape transport failure" in err_io.getvalue()
    urls = check_urls(harness)
    assert "ACTION=DeleteDownloadFile" in urls[-1]


def test_name_flag_stamps_triage_while_default_stays_null(tmp_path):
    harness = make_client(bodies=(FOUND, CREATE_OK, ZIP_BYTES, DELETE_OK))
    _, out_dir, _, _ = fetch(tmp_path, harness,
                             name="F3K NI Round 2 — Haumoana")
    doc = read_triage_doc(out_dir)
    assert doc["name"] == "F3K NI Round 2 — Haumoana"


def test_audit_receives_one_line_per_network_op_on_happy_path(tmp_path):
    audit_path = tmp_path / "audit.jsonl"
    harness = make_client(bodies=(FOUND, CREATE_OK, ZIP_BYTES, DELETE_OK),
                          audit_path=audit_path)
    fetch(tmp_path, harness)
    lines = read_audit_lines(audit_path)
    assert [line["op"] for line in lines] == [
        "check_scores_exist", "create_download_archive", "download_zip",
        "delete_download_file"]
    assert len(lines) == 4
    for line in lines:
        assert {"ts", "op", "method", "url", "status", "bytes", "refused"} <= set(line)
        assert line["refused"] is False
        assert line["status"] == 200
        assert line["bytes"] > 0
        assert line["method"] == "GET"


def test_cli_flags_pass_through_without_network(tmp_path):
    audit_path = tmp_path / "cli-audit.jsonl"
    harness = make_client(bodies=(FOUND, CREATE_OK, ZIP_BYTES, DELETE_OK),
                          audit_path=audit_path)
    out_dir = tmp_path / "cli-out"
    code = fetch_comp.main(
        [COMP_ID, "--out", str(out_dir),
         "--from-round", "3", "--to-round", "7",
         "--min-interval", "1.5",
         "--user-agent", "soarscore-webmine-test/0.1"],
        client=harness.client,
    )
    assert code == 0
    urls = check_urls(harness)
    assert urls[0].endswith(f"ACTION=CheckScoresExist&ID={COMP_ID}&FR=3&TR=7")
    assert urls[-1].endswith(f"ACTION=DeleteDownloadFile&ID={COMP_ID}&FR=3&TR=7")
    assert (out_dir / csv_member_name(COMP_ID)).is_file()
    assert (out_dir / f"{COMP_ID}_records.json").is_file()
    assert len(read_audit_lines(audit_path)) == 4


@pytest.mark.parametrize(
    "argv_fragment",
    [["--min-interval", "0.9"], ["--from-round", "0"], ["--to-round", "-4"]],
)
def test_cli_rejects_bad_internals_before_any_request(argv_fragment):
    harness = make_client(bodies=(FOUND,))
    with pytest.raises(SystemExit) as caught:
        fetch_comp.main([COMP_ID] + argv_fragment, client=harness.client)
    assert caught.value.code == 2
    assert harness.transport.requests == []


def test_cli_wires_tasks_escoring_pilot_and_name_flags(tmp_path):
    audit_path = tmp_path / "cli-audit.jsonl"
    harness = make_client(bodies=(FOUND, CREATE_OK, ZIP_BYTES,
                                  PILOT_SCREEN_HTML, DELETE_OK),
                          audit_path=audit_path)
    out_dir = tmp_path / "cli-out"
    code = fetch_comp.main(
        [COMP_ID, "--out", str(out_dir), "--tasks",
         "--escoring-pilot", "82", "--name", "CLI Stamped Name"],
        client=harness.client,
    )
    assert code == 0
    urls = check_urls(harness)
    assert f"{BASE}/eScoring.aspx?ID={COMP_ID}&P=82" in urls[-2]
    assert "ACTION=DeleteDownloadFile" in urls[-1]
    assert len(read_audit_lines(audit_path)) == 5
    doc = read_triage_doc(out_dir)
    assert doc["name"] == "CLI Stamped Name"
    assert doc["tasksByRound"]["1"] == "L1 5max in 7m"


@pytest.mark.parametrize(
    "pilot_flag",
    [["--escoring-pilot", "0"], ["--escoring-pilot", "-3"]],
)
def test_cli_rejects_bad_escoring_pilot_before_any_request(pilot_flag):
    harness = make_client(bodies=(FOUND,))
    with pytest.raises(SystemExit) as caught:
        fetch_comp.main([COMP_ID] + pilot_flag, client=harness.client)
    assert caught.value.code == 2
    assert harness.transport.requests == []


def test_fetch_comp_touches_network_only_through_the_kernel():
    source = Path(fetch_comp.__file__).read_text(encoding="utf-8")
    for forbidden in ("urllib", "http.client", "socket", "requests."):
        assert forbidden not in source
    assert "gsclient.GsClient(" in source
