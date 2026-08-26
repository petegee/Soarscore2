#!/usr/bin/env python3
"""Developer tool: fetch one competition's scoring data from gliderscore.com.

Runs the app's own four-step read-only sequence
(CheckScoresExist -> CreateScoringDataAsZipArchive -> GET
scoredownload/<CompID>_DownloadData.zip -> DeleteDownloadFile); endpoints per
gliderscore-online-data-mining.md §2.3, sequence and leave-no-trace contract
per kanban/in-progress/gliderscore-webmine-tool.md (Safety contract + Plan
WI-3). Every HTTP call is made exclusively through the gsclient.GsClient
safety kernel — allowlist, throttle and audit log all apply.

Sequence, as the GliderScore app does it:
  1. CheckScoresExist — ScoringDataFound continues; NoScoringDataFound exits
     with a friendly "nothing to fetch"; any other body is a loud exit naming
     the excerpt. This is the sole early exit before server-side artifacts.
  2. CreateScoringDataAsZipArchive — DownloadFileCreationSuccess continues.
  3. GET the zip bytes.
  4. Zip-entry guard: exactly one member "<CompID>_DownloadData.csv" is
      expected; any surprise prints EVERY entry name for triage evidence and
      fails loudly (server-side zip richness is unverified per comp type).
 Steps 2..7 run inside try/finally so DeleteDownloadFile always fires,
 regardless of how they end (the kernel makes delete best-effort/never
 raising). Artifacts written into --out: the exact extracted CSV member bytes
 and <CompID>_records.json holding flat camelCase records for WI-4.

 WI-4 conversion then runs via triage.convert_records +
 triage.check_draw_completeness, producing the primary artifact
 <CompID>_triage.json:
   - draw-completeness VIOLATIONS are printed each to stderr and abort with
     a nonzero exit AFTER the delete finaliser; no triage file is written
     (csv + records json remain on disk as evidence).
   - informational gaps land under "drawGaps"; --name stamps "name";
     --tasks adds ONE extra kernel-audited eScoring.aspx call for a single
     pilot (default OFF — extra traffic costs server courtesy) and merges
     non-empty tasksByRound, or records a limitation line otherwise.

 Usage:
     python3 tests/GliderscoreFixtures/webmine/fetch_comp.py <CompID> \
         [--out DIR] [--from-round N] [--to-round M] [--base-url URL] \
         [--min-interval SECONDS] [--audit PATH] [--user-agent UA] \
         [--keep-zip] [--tasks] [--escoring-pilot N] [--name NAME]

 Exit codes: 0 on success; nonzero SystemExit messages elsewhere (protocol
 deviations, parse failures, empty downloads, draw violations). CompIDs are
 treated case-exactly everywhere (client validation included; filenames use
 the ID verbatim). This tooling is developer-run offline-style support code —
 never a build or runtime dependency of Soarscore proper.
"""

import argparse
import io
import json
import sys
import zipfile
from pathlib import Path

import csvparse
import gsclient
import triage

_DEFAULT_BASE_URL = "https://gliderscore.com"
_EXCERPT_LIMIT = 160


class _ProtocolAbort(Exception):
    """Internal step failure carrying an already-worded complaint."""


def _emitter(stream):
    def write(line):
        stream.write(line + "\n")

    return write


def _excerpt(body):
    text = " ".join(str(body).split())
    if len(text) > _EXCERPT_LIMIT:
        text = text[:_EXCERPT_LIMIT] + "..."
    return repr(text)


def _perform_fetch(client, comp_id, *, from_round, to_round, out_dir,
                   keep_zip, announce, complain, stderr_stream,
                   name=None, tasks=False, escoring_pilot=None):
    steps = []

    # Step 1 sits outside the try/finally: with no scores there is nothing
    # to delete, and this is the only early exit before any server-side
    # artifact could exist.
    steps.append("check_scores_exist")
    check_verdict = client.check_scores_exist(comp_id, from_round=from_round,
                                              to_round=to_round)
    if check_verdict == gsclient.NO_SCORING_DATA_FOUND:
        raise SystemExit(
            f"fetch-comp: no scoring data on the server for CompID {comp_id} "
            f"(rounds {from_round}-{to_round}) — nothing to fetch"
        )
    if check_verdict != gsclient.SCORING_DATA_FOUND:
        raise SystemExit(
            f"fetch-comp: unexpected CheckScoresExist response for CompID "
            f"{comp_id}: {_excerpt(check_verdict)}"
        )

    try:
        # Step 2 may fail after a previous run left residue on the server,
        # so it stays inside the try whose finally always deletes.
        steps.append("create_download_archive")
        create_verdict = client.create_download_archive(comp_id)
        if create_verdict != gsclient.DOWNLOAD_FILE_CREATION_SUCCESS:
            raise _ProtocolAbort(
                f"unexpected CreateScoringDataAsZipArchive response for "
                f"CompID {comp_id}: {_excerpt(create_verdict)}"
            )

        steps.append("download_zip")
        try:
            zip_bytes = client.download_zip(comp_id)
        except gsclient.TransportError as exc:
            raise _ProtocolAbort(
                f"transport failure downloading zip for CompID {comp_id}: {exc}"
            )

        keep_note = ""
        if keep_zip:
            out_dir.mkdir(parents=True, exist_ok=True)
            kept_zip_path = out_dir / f"{comp_id}_DownloadData.zip"
            kept_zip_path.write_bytes(zip_bytes)
            keep_note = f"; raw zip kept for triage: {kept_zip_path}"

        expected_member = f"{comp_id}_DownloadData.csv"
        try:
            with zipfile.ZipFile(io.BytesIO(zip_bytes)) as archive:
                names = archive.namelist()
                if names != [expected_member]:
                    complaint = (
                        f"unexpected zip contents for CompID {comp_id}: "
                        f"expected exactly one member {expected_member!r}, "
                        f"got {len(names)} member(s){keep_note}"
                    )
                    complain(f"fetch-comp: {complaint}")
                    for position, entry_name in enumerate(names):
                        complain(f"  zip entry {position}: {entry_name!r}")
                    raise _ProtocolAbort(complaint)
                member_bytes = archive.read(expected_member)
        except zipfile.BadZipFile as exc:
            raise _ProtocolAbort(
                f"downloaded payload for CompID {comp_id} is not a valid zip "
                f"({len(zip_bytes)} bytes): {exc}{keep_note}"
            )

        try:
            records = csvparse.parse_csv(member_bytes.decode("utf-8-sig"))
            comp_type = csvparse.uniform_comp_type(records)
        except (UnicodeDecodeError, csvparse.CsvParseError) as exc:
            raise _ProtocolAbort(
                f"cannot parse {expected_member} for CompID {comp_id}: {exc}"
            )

        out_dir.mkdir(parents=True, exist_ok=True)
        csv_path = out_dir / expected_member
        csv_path.write_bytes(member_bytes)

        payload = {
            "compId": comp_id,
            "compType": comp_type,
            "csvEntry": expected_member,
            "recordCount": len(records),
            "records": [csvparse.record_to_camel_dict(record) for record in records],
        }
        json_path = out_dir / f"{comp_id}_records.json"
        json_path.write_text(
            json.dumps(payload, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )

        # WI-4: convert to the primary triage artifact. Violations print
        # each and abort AFTER the finally below runs its delete; gaps ride
        # along as "drawGaps"; --tasks adds exactly one audited scrape call.
        triage_doc = triage.convert_records(records)
        triage_doc["name"] = name
        completeness = triage.check_draw_completeness(records)
        if completeness["violations"]:
            for message in completeness["violations"]:
                complain(f"fetch-comp: draw violation: {message}")
            raise _ProtocolAbort(
                f"{len(completeness['violations'])} draw-completeness "
                f"violation(s) in {expected_member} for CompID {comp_id} — "
                f"triage withheld; csv and records json kept as evidence"
            )
        if completeness["gaps"]:
            triage_doc["drawGaps"] = completeness["gaps"]
        if tasks:
            pilot_to_scrape = (
                escoring_pilot
                if escoring_pilot is not None
                else min(record.pilot_no for record in records)
            )
            steps.append("escoring_page")
            try:
                html = client.escoring_page(comp_id, pilot_to_scrape)
            except gsclient.TransportError as exc:
                complain(f"fetch-comp: task scrape transport failure: {exc}")
                triage_doc["limitations"].append(
                    f"task scrape transport failure: {exc}"
                )
            else:
                scraped = triage.scrape_tasks(html, stderr=stderr_stream)
                if scraped:
                    triage_doc["tasksByRound"] = scraped
                else:
                    triage_doc["limitations"].append(
                        triage.TASK_SCRAPE_EMPTY_LIMITATION
                    )
        triage_path = out_dir / f"{comp_id}_triage.json"
        triage_path.write_text(
            json.dumps(triage_doc, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )

    finally:
        # Leave no trace server-side: best-effort finaliser, every time.
        steps.append("delete_download_file")
        client.delete_download_file(comp_id, from_round=from_round,
                                    to_round=to_round)

    pilots = {record.pilot_no for record in records}
    groups = {(record.round_no, record.group_no) for record in records}
    announce(f"fetch-comp: CompID {comp_id} ({comp_type}) — "
             f"steps: {' -> '.join(steps)}")
    announce(f"  records: {len(records)}; pilots: {len(pilots)}; "
             f"round-group pairs: {len(groups)}")
    announce(f"  wrote: {csv_path}")
    announce(f"  wrote: {json_path}")
    announce(f"  wrote (primary triage): {triage_path}")
    announce(f"  triage: {len(triage_doc['pilots'])} pilots, "
             f"{len(triage_doc['rounds'])} round/group/reflight buckets, "
             f"{len(triage_doc['limitations'])} limitation(s)")
    return payload


def fetch_competition(client, comp_id, *, from_round=1, to_round=99,
                      out_dir=".", keep_zip=False, name=None, tasks=False,
                      escoring_pilot=None, stdout=None, stderr=None):
    """Run the four-step sequence plus WI-4 conversion for one CompID.

    Returns the records JSON payload dict on success; raises SystemExit with
    a friendly/loud message on protocol deviations, transport faults, parse
    failures and draw-completeness violations. The network surface is exactly
    the GsClient passed in.
    """
    announce = _emitter(stdout if stdout is not None else sys.stdout)
    stderr_stream = stderr if stderr is not None else sys.stderr
    complain = _emitter(stderr_stream)
    try:
        return _perform_fetch(
            client, comp_id,
            from_round=from_round, to_round=to_round,
            out_dir=Path(out_dir), keep_zip=keep_zip,
            announce=announce, complain=complain,
            stderr_stream=stderr_stream,
            name=name, tasks=tasks, escoring_pilot=escoring_pilot,
        )
    except _ProtocolAbort as exc:
        raise SystemExit(f"fetch-comp: {exc}") from exc


def main(argv=None, *, client=None):
    parser = argparse.ArgumentParser(
        prog="fetch_comp.py",
        description="Fetch one gliderscore.com competition's scoring data "
                    "(read-only four-step sequence via the gsclient kernel).",
    )
    parser.add_argument("comp_id", help="GliderScore CompID (10-15 hex chars, CASE-SENSITIVE)")
    parser.add_argument("--out", default=".", help="artifact output directory (default: current directory)")
    parser.add_argument("--from-round", type=int, default=1, help="first round to include (default: 1)")
    parser.add_argument("--to-round", type=int, default=99, help="last round to include (default: 99)")
    parser.add_argument("--base-url", default=_DEFAULT_BASE_URL, help="gliderscore.com base URL")
    parser.add_argument("--min-interval", type=float, dest="min_interval_seconds",
                        default=2.0, help="minimum seconds between requests (default: 2.0)")
    parser.add_argument("--audit", default=None, help="append-only JSONL audit log path")
    parser.add_argument("--user-agent", default=gsclient.DEFAULT_USER_AGENT,
                        help="User-Agent header value")
    parser.add_argument("--keep-zip", action="store_true",
                        help="retain the raw downloaded zip beside the artifacts")
    parser.add_argument("--tasks", action="store_true",
                        help="scrape ONE pilot's eScoring.aspx screen for "
                             "per-round tasks (default OFF — each extra "
                             "call costs server courtesy)")
    parser.add_argument("--escoring-pilot", type=int, default=None,
                        help="pilot number for --tasks scrape (default: "
                             "smallest pilot number present in the records)")
    parser.add_argument("--name", default=None,
                        help="competition name stamped into triage.json "
                             "(default: null)")
    args = parser.parse_args(argv)
    if args.from_round < 1 or args.to_round < 1:
        parser.error("--from-round/--to-round must be >= 1")
    if args.min_interval_seconds < 1.0:
        parser.error("--min-interval must be >= 1.0 seconds (courtesy floor)")
    if args.escoring_pilot is not None and args.escoring_pilot < 1:
        parser.error("--escoring-pilot must be >= 1")

    if client is None:
        client = gsclient.GsClient(
            base_url=args.base_url,
            min_interval_seconds=args.min_interval_seconds,
            audit_path=args.audit,
            user_agent=args.user_agent,
        )
    fetch_competition(client, args.comp_id, from_round=args.from_round,
                      to_round=args.to_round, out_dir=args.out,
                      keep_zip=args.keep_zip, name=args.name,
                      tasks=args.tasks,
                      escoring_pilot=args.escoring_pilot)
    return 0


if __name__ == "__main__":
    sys.exit(main())
