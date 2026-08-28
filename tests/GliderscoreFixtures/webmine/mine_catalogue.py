#!/usr/bin/env python3
"""Mine the gliderscore.com public competition catalogue into comps.json|csv.

All traffic goes through the WI-1 safety kernel (gsclient.GsClient): this tool
builds postbacks, never URLs or requests of its own.

Two ranges (gliderscore-online-data-mining.md 2.1):

- ``last30`` — one plain GET of OnLineScores.aspx (the server's default range).
- ``all`` — the same GET, then one ASP.NET WebForms postback rebuilt
  generically from the fetched page: every form field echoed back verbatim
  (incl. hidden __VIEWSTATE/__VIEWSTATEGENERATOR/__EVENTVALIDATION),
  __EVENTTARGET pointed at the range select, that select's value forced to
  "All competitions", submit-button markers dropped.

The competition picker is located structurally, never scraped blindly: the one
select whose option values overwhelmingly match ^[0-9a-fA-F]{10,15}$ AND whose
option texts match "YYYY Mmm DD - Title [     (Venue)]" (values are the
case-sensitive CompIDs). Dates are parsed with %Y %b %d so the lexical-month
sort bug warned about in the mining doc cannot recur. Individual malformed
rows never abort the run: they degrade to undated entries with a stderr
warning.

Safety contract (kanban/in-progress/gliderscore-webmine-tool.md "Safety
contract"): read-only by allowlist enforcement inside GsClient, >= 1 s between
requests (default 2 s), append-only JSONL audit log when --audit is given.
First live use waits on the courtesy permission gate; offline/unit work runs
against the fake transport in tests/test_mine_catalogue.py.

Usage:
    python3 mine_catalogue.py [--range {last30,all}] [--out DIR]
        [--base-url URL] [--min-interval SECONDS] [--audit PATH]
        [--user-agent UA]
"""

import argparse
import csv
import json
import re
import sys
from collections import namedtuple
from datetime import date, datetime, timezone
from html.parser import HTMLParser
from pathlib import Path

import gsclient

__all__ = [
    "DEFAULT_BASE_URL",
    "RANGE_VALUE",
    "RANGE_NAME",
    "FormScan",
    "SelectInfo",
    "scan_form",
    "extract_form_fields",
    "find_range_select",
    "build_range_postback",
    "fetch_catalogue",
    "is_comp_id_value",
    "OPTION_TEXT_RE",
    "option_to_comp",
    "locate_comp_select",
    "collect_comps",
    "parse_comps",
    "sort_comps",
    "write_comps",
    "mine_catalogue",
    "make_arg_parser",
    "main",
]

DEFAULT_BASE_URL = "https://gliderscore.com"
RANGE_VALUE = "All competitions"
RANGE_NAME = "all"

# Written per option text as documented: "YYYY Mmm DD - Title     (Venue)".
OPTION_TEXT_RE = re.compile(r"^\s*(\d{4}) ([A-Z][a-z]{2}) (\d{2}) - (.*)$")
_COMP_ID_VALUE_RE = re.compile(r"[0-9a-fA-F]{10,15}")

SubmitInputTypes = frozenset({"submit", "image", "button"})

SelectInfo = namedtuple("SelectInfo", "name options")  # options: [(value, text)]
FormScan = namedtuple("FormScan", "fields selects")


def is_comp_id_value(value):
    """True iff value looks like a CompID (10-15 hex chars, case-sensitive)."""
    return isinstance(value, str) and _COMP_ID_VALUE_RE.fullmatch(value) is not None


class _FormScanner(HTMLParser):
    """Collects form fields and selects-with-options from a WebForms page."""

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.fields = {}
        self.selects = []
        self._active_select = None   # dict(name, options, marked) or None
        self._current_option = None  # dict(value, chars, selected) or None
        self._textarea_name = None
        self._textarea_chars = []

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == "input":
            name = a.get("name")
            itype = (a.get("type") or "").lower()
            if not name or "disabled" in a or itype in SubmitInputTypes:
                return
            if itype in ("checkbox", "radio") and "checked" not in a:
                return
            self.fields[name] = a.get("value", "")
        elif tag == "textarea":
            if a.get("name") and "disabled" not in a:
                self._textarea_name = a["name"]
                self._textarea_chars = []
        elif tag == "select":
            if not a.get("name") or "disabled" in a:
                self._active_select = None
            else:
                self._active_select = {"name": a["name"], "options": [], "marked": None}
        elif tag == "option":
            if self._active_select is None:
                return
            self._current_option = {
                "value": a.get("value"),
                "chars": [],
                "selected": "selected" in a,
            }

    def handle_data(self, data):
        if self._current_option is not None:
            self._current_option["chars"].append(data)
        elif self._textarea_name is not None:
            self._textarea_chars.append(data)

    def handle_endtag(self, tag):
        if tag == "option" and self._current_option is not None:
            value = self._current_option["value"]
            text = "".join(self._current_option["chars"]).strip()
            if value is None:
                value = text  # <option>Label</option>: label doubles as value
            self._active_select["options"].append((value, text))
            if self._current_option["selected"]:
                self._active_select["marked"] = value
            self._current_option = None
        elif tag == "select" and self._active_select is not None:
            options = self._active_select["options"]
            value = self._active_select["marked"]
            if value is None:
                value = options[0][0] if options else ""
            self.fields[self._active_select["name"]] = value
            self.selects.append(SelectInfo(self._active_select["name"], options))
            self._active_select = None
        elif tag == "textarea" and self._textarea_name is not None:
            self.fields[self._textarea_name] = "".join(self._textarea_chars)
            self._textarea_name = None


def scan_form(html):
    """Parse every form field (input/textarea/select, hidden included) from html."""
    scanner = _FormScanner()
    scanner.feed(html)
    scanner.close()
    return FormScan(dict(scanner.fields), list(scanner.selects))


def extract_form_fields(html):
    """Reused-surface wrapper (WI-4): name -> current-value for every form field."""
    return scan_form(html).fields


def find_range_select(scan):
    """Name of the ONE select whose option texts include "All competitions",
    else None. More than one such select is treated as not-found (ambiguity)."""
    matches = [
        sel.name
        for sel in scan.selects
        if any(RANGE_VALUE in text for _, text in sel.options)
    ]
    return matches[0] if len(matches) == 1 else None


def build_range_postback(scan):
    """Generic WebForms postback triggering the range change.

    Every scanned field is echoed back; submit-button-ish inputs were dropped
    at scan time; __EVENTTARGET points at the range select and that select's
    value becomes "All competitions" (mining doc 2.1). Failures are SystemExit
    with the drift evidence, not exceptions."""
    target = find_range_select(scan)
    if target is None:
        found = "; ".join(
            f"{sel.name} ({len(sel.options)} options: "
            f"{', '.join(repr(text) for _, text in sel.options[:3])}...)"
            if sel.options else f"{sel.name} (no options)"
            for sel in scan.selects
        )
        raise SystemExit(
            "mine_catalogue.py: page structure drift — no range select located "
            f"(expected exactly one select offering {RANGE_VALUE!r}). "
            f"Selects found instead: {found}"
        )
    postback = dict(scan.fields)
    postback["__EVENTTARGET"] = target
    postback[target] = RANGE_VALUE
    return postback


def fetch_catalogue(client, want_range="last30"):
    """Return the OnLineScores.aspx HTML for the requested range through the
    safety kernel: one GET (last30) or GET + one generic postback (all)."""
    if want_range not in ("last30", "all"):
        raise SystemExit(
            f"mine_catalogue.py: unknown range {want_range!r} (choose 'last30' or 'all')"
        )
    html = client.online_scores()
    if want_range == "last30":
        return html
    return client.online_scores(postback=build_range_postback(scan_form(html)))


def _split_tail(tail):
    """Split the post-date remainder into (title, venue).

    venue = contents of the LAST (outermost, balanced-at-the-end) parenthesised
    group when the tail ends in ')', else ""; title = everything before that
    group, trimmed of trailing whitespace runs."""
    stripped = tail.rstrip()
    if stripped.endswith(")"):
        depth = 0
        for index in range(len(stripped) - 1, -1, -1):
            char = stripped[index]
            if char == ")":
                depth += 1
            elif char == "(":
                depth -= 1
                if depth == 0:
                    return tail[:index].rstrip(), stripped[index + 1:-1]
    return stripped, ""


def option_to_comp(value, text):
    """Derive {compId, name, date, title, venue} from one picker option.

    Never raises: an option whose text lacks the documented date-prefix shape
    degrades to date=None, title=name, venue="" (caller warns)."""
    name = text.strip()
    match = OPTION_TEXT_RE.match(name)
    if match is None:
        return {"compId": value, "name": name, "date": None, "title": name, "venue": ""}
    tail = match.group(4)
    title, venue = _split_tail(tail)
    try:
        when = datetime.strptime(
            f"{match.group(1)} {match.group(2)} {match.group(3)}", "%Y %b %d"
        ).date()
    except ValueError:  # calendar-invalid, e.g. "Feb 30": keep as malformed row
        return {"compId": value, "name": name, "date": None, "title": name, "venue": ""}
    return {"compId": value, "name": name, "date": when, "title": title, "venue": venue}


def locate_comp_select(scan):
    """The select holding the competition picker: the one whose options satisfy
    BOTH the hex-CompID value shape AND the documented text shape in the
    greatest number. Non-comp selects (country choosers, task pickers, ...)
    score far fewer or zero hits. Returns None when nothing qualifies."""
    best = None
    best_hits = 0
    for sel in scan.selects:
        hits = sum(
            1
            for value, text in sel.options
            if is_comp_id_value(value) and OPTION_TEXT_RE.match(text)
        )
        if hits > best_hits:
            best, best_hits = sel, hits
    return best


def collect_comps(options, warn=None):
    """Turn (value, text) picker options into comp dicts: non-CompID-shaped
    values skipped, duplicates deduped by value keeping the FIRST occurrence
    (IDs are case-sensitive), malformed texts degraded with a warning."""
    if warn is None:
        warn = _warn_to_stderr
    seen = set()
    comps = []
    for value, text in options:
        if not is_comp_id_value(value) or value in seen:
            continue
        seen.add(value)
        comp = option_to_comp(value, text)
        if comp["date"] is None:
            warn(f"skipping undated/malformed option (kept as undated entry): "
                 f"compId={value!r} text={text!r}")
        comps.append(comp)
    return comps


def sort_comps(comps):
    """Ascending by (date-or-maxdate, compId); undated rows sort last, each
    block deterministically by compId."""
    return sorted(comps, key=lambda comp: (comp["date"] or date.max, comp["compId"]))


def parse_comps(html, warn=None):
    """Locate the comp picker in the page and return the deduped, sorted
    catalogue. Fails with SystemExit when no select qualifies (drift)."""
    scan = scan_form(html)
    picker = locate_comp_select(scan)
    if picker is None:
        found = "; ".join(
            f"{sel.name} ({len(sel.options)} options)"
            for sel in scan.selects
        ) or "no selects at all"
        raise SystemExit(
            "mine_catalogue.py: page structure drift — no competition-picker "
            "select located (expected options shaped 'YYYY Mmm DD - Title "
            f"(Venue)' with hex CompID values). Selects found instead: {found}"
        )
    return sort_comps(collect_comps(picker.options, warn=warn))


def write_comps(comps, out_dir, *, base_url, range_name, mined_at):
    """Write --out/comps.json (indent=2, \\n-terminated) and --out/comps.csv
    (header exactly compId,name,date,title,venue). Returns the two paths."""
    out_dir.mkdir(parents=True, exist_ok=True)
    serialisable = [
        {
            "compId": comp["compId"],
            "name": comp["name"],
            "date": comp["date"].isoformat() if comp["date"] is not None else None,
            "title": comp["title"],
            "venue": comp["venue"],
        }
        for comp in comps
    ]
    payload = {
        "minedAt": mined_at,
        "baseUrl": base_url,
        "range": range_name,
        "count": len(serialisable),
        "comps": serialisable,
    }
    json_path = out_dir / "comps.json"
    json_path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n",
                         encoding="utf-8")
    csv_path = out_dir / "comps.csv"
    with open(csv_path, "w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["compId", "name", "date", "title", "venue"])
        for comp in serialisable:
            writer.writerow([
                comp["compId"], comp["name"], comp["date"] or "",
                comp["title"], comp["venue"],
            ])
    return json_path, csv_path


def _warn_to_stderr(message):
    print(f"warning: {message}", file=sys.stderr)


def mine_catalogue(client, *, want_range="last30", out_dir=".",
                   base_url=DEFAULT_BASE_URL, mined_at=None, warn=None):
    """Fetch (via the safety kernel), parse and persist the catalogue.

    Returns a summary dict: {range, baseUrl, minedAt, total, dated, undated,
    comps, jsonPath, csvPath}. All network volume: one GET (last30) or two
    requests (all)."""
    if want_range not in ("last30", "all"):
        raise SystemExit(
            f"mine_catalogue.py: unknown range {want_range!r} (choose 'last30' or 'all')"
        )
    if mined_at is None:
        mined_at = datetime.now(timezone.utc).isoformat()
    html = fetch_catalogue(client, want_range)
    comps = parse_comps(html, warn=warn)
    json_path, csv_path = write_comps(
        comps, Path(out_dir), base_url=base_url, range_name=want_range, mined_at=mined_at
    )
    dated = sum(1 for comp in comps if comp["date"] is not None)
    return {
        "range": want_range,
        "baseUrl": base_url,
        "minedAt": mined_at,
        "total": len(comps),
        "dated": dated,
        "undated": len(comps) - dated,
        "comps": comps,
        "jsonPath": str(json_path),
        "csvPath": str(csv_path),
    }


def make_arg_parser():
    parser = argparse.ArgumentParser(
        prog="mine_catalogue.py",
        description="Mine the public gliderscore.com competition catalogue "
                    "(read-only, throttled, audited via gsclient.GsClient).",
    )
    parser.add_argument("--range", dest="range_name", choices=("last30", "all"),
                        default="last30",
                        help="'last30' = one plain GET (server default range); "
                             "'all' = GET plus one WebForms range postback "
                             "(default: %(default)s)")
    parser.add_argument("--out", type=Path, default=Path("."),
                        help="output directory for comps.json|csv (created if missing; "
                             "default: %(default)s)")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL,
                        help=f"site origin (default: {DEFAULT_BASE_URL})")
    parser.add_argument("--min-interval", type=float, default=2.0,
                        help="minimum seconds between any two requests, courtesy "
                             "floor 1.0 (default: %(default)s)")
    parser.add_argument("--audit", default=None,
                        help="path for the append-only JSONL request audit log "
                             "(client-managed; default: none)")
    parser.add_argument("--user-agent", default=gsclient.DEFAULT_USER_AGENT,
                        help="User-Agent header value (default: %(default)s)")
    return parser


def main(argv=None):
    args = make_arg_parser().parse_args(argv)
    try:
        client = gsclient.GsClient(
            base_url=args.base_url,
            min_interval_seconds=args.min_interval,
            audit_path=args.audit,
            user_agent=args.user_agent,
        )
    except ValueError as exc:
        raise SystemExit(f"mine_catalogue.py: {exc}")
    try:
        summary = mine_catalogue(
            client, want_range=args.range_name, out_dir=args.out, base_url=args.base_url
        )
    except gsclient.TransportError as exc:
        raise SystemExit(f"mine_catalogue.py: transport failed: {exc}")
    print(
        f"{summary['range']}: {summary['total']} unique comps "
        f"({summary['dated']} dated, {summary['undated']} undated); "
        f"wrote {summary['jsonPath']} and {summary['csvPath']}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
