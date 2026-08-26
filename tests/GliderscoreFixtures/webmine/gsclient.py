#!/usr/bin/env python3
"""Safety-kernel HTTP client for gliderscore.com offline-corpus mining.

Every request must go through one of the allowlisted URL builders on GsClient;
no other code path can issue traffic. Each builder hardcodes its endpoint path
and ACTION (verified read-only per kanban/in-progress/gliderscore-webmine-tool.md
"Safety contract" and gliderscore-online-data-mining.md 2.3/2.5), re-checks the
ACTION against classify_action() as defence-in-depth, and refuses anything else
with RefusedError. Any two network requests are separated by at least
min_interval_seconds (>= 1.0 s courtesy floor, default 2.0 s). If audit_path is
given, every event (response, transport failure, refusal) appends one JSONL
line so total traffic can be evidenced.
"""

import json
import re
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone

__all__ = [
    "GsClient",
    "RefusedError",
    "TransportError",
    "classify_action",
    "READ_ONLY_ACTIONS",
    "REFUSED_ACTIONS",
    "VALID_COMP_ID",
    "INVALID_COMP_ID",
    "SCORING_DATA_FOUND",
    "NO_SCORING_DATA_FOUND",
    "DOWNLOAD_FILE_CREATION_SUCCESS",
    "DOWNLOAD_FILE_DELETE_SUCCESS",
]

DEFAULT_USER_AGENT = "soarscore-webmine/0.1 (+offline corpus tooling)"

# The only server ACTIONs verified read-only; enforced by exact-match,
# case-sensitive membership (classify_action). This set is the mechanism.
READ_ONLY_ACTIONS = frozenset({
    "ValidateCompID",
    "CheckScoresExist",
    "CreateScoringDataAsZipArchive",
    "DeleteDownloadFile",
})

# Documentation-only refuse list: known MUTATING action families on the same
# pages / upload endpoints. Suffix variants of family bases (e.g.
# "ScoresBackup7", "UploadFoo") share the refusal because enforcement is
# exact-match allowlist membership, never this list's presence or absence.
REFUSED_ACTIONS = frozenset({
    "DeleteComp",
    "MakeScoresZero",
    "RemoveData",
    "InsertDataFromZipFile",
    "ScoreEntryOpen",
    "ScoreEntryClose",
    "ScoresBackup",
    "ScoresRestore",
    "DeleteAllTransferFiles",
    "Upload",
})

# Exact verdict strings returned by the read-only endpoints.
VALID_COMP_ID = "ValidCompID"
INVALID_COMP_ID = "InvalidCompID"
SCORING_DATA_FOUND = "ScoringDataFound"
NO_SCORING_DATA_FOUND = "NoScoringDataFound"
DOWNLOAD_FILE_CREATION_SUCCESS = "DownloadFileCreationSuccess"
DOWNLOAD_FILE_DELETE_SUCCESS = "DownloadFileDeleteSuccess"


def classify_action(action):
    """True iff action is exactly a read-only allowlisted ACTION (case-sensitive)."""
    return action in READ_ONLY_ACTIONS


class RefusedError(Exception):
    """Raised for any attempt outside the read-only allowlist."""


class TransportError(Exception):
    """Wraps OS/HTTP-level transport failures (never an allowlist refusal)."""


_COMP_ID_RE = re.compile(r"[0-9a-fA-F]{10,15}")
_PILOT_NO_RE = re.compile(r"\d{1,9}")

_ONLINE_SCORES_PATH = "OnLineScores.aspx"
_SCORING_DATA_MANAGE_PATH = "scoringdatamanage.aspx"
_SCORING_DATA_DOWNLOAD_PATH = "scoringdatadownload.aspx"
_ZIP_PATH_TEMPLATE = "scoredownload/{comp_id}_DownloadData.zip"
_ESCORING_PATH = "eScoring.aspx"


class _UrllibTransport:
    """Default transport: one urllib.request round trip per request dict."""

    def __init__(self, timeout_seconds):
        self._timeout_seconds = timeout_seconds

    def __call__(self, request_dict):
        data = request_dict["data"]
        request = urllib.request.Request(
            request_dict["url"],
            data=None if data is None else data.encode("utf-8"),
            headers=dict(request_dict["headers"]),
            method=request_dict["method"],
        )
        try:
            with urllib.request.urlopen(request, timeout=self._timeout_seconds) as response:
                return {"status": response.status, "body": response.read()}
        except urllib.error.HTTPError as exc:
            # Any HTTP answer (even 4xx/5xx) is an observation to audit, not a fault.
            return {"status": exc.code, "body": exc.read()}
        except (urllib.error.URLError, OSError) as exc:
            raise TransportError(f"{type(exc).__name__}: {exc}") from exc


class GsClient:
    """Read-only, rate-limited, auditable client for gliderscore.com."""

    def __init__(self, *, base_url="https://gliderscore.com", min_interval_seconds=2.0,
                 transport=None, clock=time.monotonic, sleep=time.sleep, audit_path=None,
                 timeout_seconds=30.0, user_agent=DEFAULT_USER_AGENT):
        if min_interval_seconds < 1.0:
            raise ValueError(
                f"min_interval_seconds must be >= 1.0 (courtesy floor), got {min_interval_seconds!r}"
            )
        self._base_url = base_url.rstrip("/")
        self._min_interval_seconds = min_interval_seconds
        self._transport = transport if transport is not None else _UrllibTransport(timeout_seconds)
        self._clock = clock
        self._sleep = sleep
        self._audit_path = audit_path
        self._user_agent = user_agent
        self._last_send_time = None

    # -- operations ----------------------------------------------------

    def online_scores(self, postback=None):
        url = f"{self._base_url}/{_ONLINE_SCORES_PATH}"
        if postback is None:
            return self._send_text("online_scores", "GET", url)
        data = urllib.parse.urlencode(postback)
        return self._send_text("online_scores", "POST", url, data=data)

    def validate_comp_id(self, comp_id):
        action = "ValidateCompID"
        self._gate("validate_comp_id", action)
        comp_id = self._check_comp_id(comp_id)
        url = f"{self._base_url}/{_SCORING_DATA_MANAGE_PATH}?ACTION={action}&ID={comp_id}"
        return self._send_text("validate_comp_id", "GET", url)

    def check_scores_exist(self, comp_id, from_round=1, to_round=99):
        action = "CheckScoresExist"
        self._gate("check_scores_exist", action)
        comp_id = self._check_comp_id(comp_id)
        url = self._scores_action_url(action, comp_id, from_round, to_round)
        return self._send_text("check_scores_exist", "GET", url)

    def create_download_archive(self, comp_id):
        action = "CreateScoringDataAsZipArchive"
        self._gate("create_download_archive", action)
        comp_id = self._check_comp_id(comp_id)
        url = f"{self._base_url}/{_SCORING_DATA_DOWNLOAD_PATH}?ACTION={action}&ID={comp_id}"
        return self._send_text("create_download_archive", "GET", url)

    def download_zip(self, comp_id):
        # No ACTION involved: closure comes from the hex-validated CompID,
        # which cannot contain path separators, plus the hardcoded path template.
        comp_id = self._check_comp_id(comp_id)
        url = f"{self._base_url}/{_ZIP_PATH_TEMPLATE.format(comp_id=comp_id)}"
        return self._send("download_zip", "GET", url)["body"]

    def delete_download_file(self, comp_id, from_round=1, to_round=99):
        action = "DeleteDownloadFile"
        self._gate("delete_download_file", action)
        comp_id = self._check_comp_id(comp_id)
        url = self._scores_action_url(action, comp_id, from_round, to_round)
        try:
            return self._send_text("delete_download_file", "GET", url)
        except TransportError:
            # Best-effort finaliser: already audited inside _send; never raise.
            return None

    def escoring_page(self, comp_id, pilot_no):
        comp_id = self._check_comp_id(comp_id)
        pilot_no = self._check_pilot_no(pilot_no)
        url = f"{self._base_url}/{_ESCORING_PATH}?ID={comp_id}&P={pilot_no}"
        return self._send_text("escoring_page", "GET", url)

    # -- internals -----------------------------------------------------

    def _gate(self, op, action):
        if classify_action(action):
            return
        detail = f"ACTION {action!r} is not on the read-only allowlist"
        self._audit(op=op, method=None, url=None, status=None, byte_count=None,
                    refused=True, error=repr(RefusedError(detail)))
        raise RefusedError(detail)

    def _scores_action_url(self, action, comp_id, from_round, to_round):
        fr = self._check_round(from_round)
        tr = self._check_round(to_round)
        return (
            f"{self._base_url}/{_SCORING_DATA_DOWNLOAD_PATH}"
            f"?ACTION={action}&ID={comp_id}&FR={fr}&TR={tr}"
        )

    @staticmethod
    def _check_comp_id(comp_id):
        if not isinstance(comp_id, str) or not _COMP_ID_RE.fullmatch(comp_id):
            raise ValueError(f"CompID must be 10-15 hex characters [0-9a-fA-F], got {comp_id!r}")
        return comp_id

    @staticmethod
    def _check_pilot_no(pilot_no):
        if isinstance(pilot_no, int) and not isinstance(pilot_no, bool):
            if pilot_no < 1:
                raise ValueError(f"pilot_no must be a positive int-like value, got {pilot_no!r}")
            return str(pilot_no)
        if isinstance(pilot_no, str) and _PILOT_NO_RE.fullmatch(pilot_no):
            return pilot_no
        raise ValueError(f"pilot_no must be a positive int-like value, got {pilot_no!r}")

    @staticmethod
    def _check_round(value):
        if isinstance(value, bool) or not isinstance(value, int) or value < 1:
            raise ValueError(f"round numbers must be ints >= 1, got {value!r}")
        return str(value)

    def _headers(self, data):
        headers = {"User-Agent": self._user_agent}
        if data is not None:
            headers["Content-Type"] = "application/x-www-form-urlencoded"
        return headers

    def _throttle(self):
        now = self._clock()
        if self._last_send_time is None:
            self._last_send_time = now
            return
        target = self._last_send_time + self._min_interval_seconds
        if now < target:
            self._sleep(target - now)
            now = self._clock()
        self._last_send_time = max(now, target)

    def _send(self, op, method, url, data=None):
        self._throttle()
        request_dict = {
            "method": method,
            "url": url,
            "data": data,
            "headers": self._headers(data),
        }
        try:
            response = self._transport(request_dict)
            status = response["status"]
            body = response["body"]
        except TransportError as exc:
            self._audit(op=op, method=method, url=url, status=None, byte_count=None,
                        refused=False, error=repr(exc))
            raise
        except Exception as exc:
            wrapped = TransportError(f"{type(exc).__name__}: {exc}")
            self._audit(op=op, method=method, url=url, status=None, byte_count=None,
                        refused=False, error=repr(wrapped))
            raise wrapped from exc
        payload = b"" if body is None else body
        self._audit(op=op, method=method, url=url, status=status,
                    byte_count=len(payload), refused=False)
        return {"status": status, "body": payload}

    def _send_text(self, op, method, url, data=None):
        return self._send(op, method, url, data)["body"].decode("utf-8", errors="replace")

    def _audit(self, *, op, method, url, status, byte_count, refused, error=None):
        if self._audit_path is None:
            return
        record = {
            "ts": datetime.now(timezone.utc).isoformat(),
            "op": op,
            "method": method,
            "url": url,
            "status": status,
            "bytes": byte_count,
            "refused": refused,
        }
        if error is not None:
            record["error"] = error
        with open(self._audit_path, "a", encoding="utf-8") as handle:
            handle.write(json.dumps(record, ensure_ascii=False) + "\n")
            handle.flush()
