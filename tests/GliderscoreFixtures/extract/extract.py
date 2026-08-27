#!/usr/bin/env python3
"""Extract every table of a GliderScore Jet export into deterministic JSON fixtures.

Usage:
    python3 extract.py <export-file> [--out DIR] [--slug NAME]

Tolerant mode for exports the pinned access_parser 0.0.6 cannot read as-is
(the NZContests.mdb master off-by-one; opt-in, default behaviour unchanged):

    python3 extract.py <export-file> --tolerant \
        [--recovered-texts PATH] [--out DIR] [--slug NAME]

See README.md (same directory) for the full contract, including the NZ-master
caveat that motivates the two opt-in flags.
"""

import argparse
import hashlib
import json
import math
import sys
from collections import Counter
from datetime import date, datetime, time
from decimal import Decimal
from pathlib import Path

from access_parser import AccessParser
from access_parser import utils as apu

# ---------------------------------------------------------------------------
# Opt-in tolerant parsing (default OFF: without --tolerant nothing below runs
# and behaviour is byte-identical to the plain pinned-library extraction).
#
# Crash fingerprint this tolerates (NZContests.mdb, access_parser 0.0.6):
#   Table `Comps`, every row, inside access_parser/access_parser.py
#   `_parse_fixed_length_data`: the bounds check at line 315
#   (`if column.column_id > len(null_table)`) lets column_id ==
#   len(null_table) through to `null_table[column.column_id]` at line 320,
#   which raises IndexError on the first column past the null-bitmap
#   (here Comps.IsPublic, id 40 against a 40-slot bitmap; UseRegistration 41
#   and UseRegistrationIdx 42 fall into the adjacent `>` warning branch and
#   degrade instead of raising). The narrow fix wraps ONLY this method: the
#   out-of-range read is resolved exactly the way upstream resolves its own
#   warning branch — read what is readable (fixed-offset bytes; booleans
#   carry their value in the bitmap and degrade to None) and keep going,
#   recording each degraded table.column. No other table or column parses
#   differently: everything in range delegates to the pristine function, so
#   returned Python types are exactly the library's own.
#
# Note the library additionally SILENTLY DROPS Comps' 12 variable-length Text
# columns on this database (independent of the crash above). They are not
# restorable from within access_parser; when --recovered-texts points at the
# byte-validated recovery JSON they are ingested into the Comps payload and a
# field-provenance sibling file is emitted.
# ---------------------------------------------------------------------------

RECOVERED_TARGET_TABLE = "Comps"

TOLERANT_STATE = {
    "enabled": False,
    "table": None,          # table currently being parsed by the wrapper
    "hits": {},             # {(table, column): {"rows": int, "column_id": int}}
    "reported": set(),      # hit keys already loudly warned about
}


def warn(message):
    print(f"extract.py: WARNING: {message}", file=sys.stderr)


def _install_tolerant_parser_patch():
    """Monkeypatch AccessTable._parse_fixed_length_data, and only that."""
    import logging

    import access_parser.access_parser as ap

    # Mirror the proven analysis-machinery setup: with the off-by-one active,
    # upstream logs one warning per out-of-range read (2 per Comps row here);
    # degrade notices for humans are emitted once per affected table.column
    # below instead. Tolerant-mode-only; plain runs keep library defaults.
    logging.getLogger("access_parser").setLevel(logging.ERROR)

    state = TOLERANT_STATE
    original_fixed = ap.AccessTable._parse_fixed_length_data
    original_parse_table = ap.AccessParser.parse_table

    def parse_table_with_context(self, table_name):
        previous = state["table"]
        state["table"] = table_name
        try:
            return original_parse_table(self, table_name)
        finally:
            state["table"] = previous

    def tolerant_fixed_length_data(self, original_record, column, null_table):
        try:
            return original_fixed(self, original_record, column, null_table)
        except IndexError:
            # The lone IndexError site in the wrapped body is the
            # `null_table[column.column_id]` read past the bitmap end
            # (access_parser.py line ~320, off-by-one `>` at line ~315).
            # Mirror upstream's own out-of-range branch gracefully: booleans
            # encode their value in the bitmap and have nothing left to read
            # (degrade to None); for everything else read the fixed-offset
            # bytes verbatim, i.e. read what is readable.
            name = column.col_name_str
            hit = state["hits"].setdefault(
                (state["table"], name),
                {"rows": 0, "column_id": column.column_id},
            )
            hit["rows"] += 1
            if column.type == apu.TYPE_BOOLEAN:
                self.parsed_table[name].append(None)
                return
            record = original_record[column.fixed_offset:]
            self.parsed_table[name].append(
                ap.parse_type(
                    column.type,
                    record,
                    version=self.version,
                    props=column.extra_props or None,
                )
            )

    ap.AccessTable._parse_fixed_length_data = tolerant_fixed_length_data
    ap.AccessParser.parse_table = parse_table_with_context
    state["enabled"] = True


def report_degraded_hits(table_name):
    """Loudly warn (once per affected table.column) about degraded reads."""
    state = TOLERANT_STATE
    for (hit_table, column), info in sorted(state["hits"].items()):
        if hit_table != table_name or (hit_table, column) in state["reported"]:
            continue
        state["reported"].add((hit_table, column))
        warn(
            f"degraded-column: {hit_table}.{column} (Jet column id "
            f"{info['column_id']} lies past the null-bitmap; read via the "
            f"opt-in tolerant bounds workaround on {info['rows']} row(s); "
            f"its stored value/bit is not backed by the null-bitmap)"
        )


def summarize_tolerant_run():
    state = TOLERANT_STATE
    if not state["enabled"]:
        return
    if not state["hits"]:
        print("tolerant: no column needed the null-bitmap bounds workaround")
        return
    total = sum(info["rows"] for info in state["hits"].values())
    print(
        f"tolerant: {len(state['hits'])} degraded column(s) "
        f"(null-bitmap bounds workaround), {total} degraded read(s) total:"
    )
    for (hit_table, column), info in sorted(state["hits"].items()):
        print(f"  {hit_table}.{column}: {info['rows']} row(s)")


def load_recovered_texts(path):
    """Load and structurally verify the byte-validated Comps recovery JSON."""
    evidence_path = Path(path)
    if not evidence_path.is_file():
        raise SystemExit(f"extract.py: no such recovered-texts file: {evidence_path}")
    try:
        doc = json.loads(evidence_path.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        raise SystemExit(f"extract.py: recovered-texts file unreadable: {exc}")

    if not isinstance(doc, dict):
        raise SystemExit("extract.py: recovered-texts root must be a JSON object")
    for key in ("recordsRecovered", "recordsExpected", "slotToColumn"):
        if key not in doc:
            raise SystemExit(f"extract.py: recovered-texts missing {key!r}")
    cracks = doc.get("crackFailures")
    if cracks:
        raise SystemExit(
            f"extract.py: recovered-texts reports {len(cracks)} crack failure(s); "
            "refusing to ingest partially-cracked evidence"
        )
    recovered_n, expected_n = doc["recordsRecovered"], doc["recordsExpected"]
    if not isinstance(recovered_n, int) or not isinstance(expected_n, int):
        raise SystemExit("extract.py: recordsRecovered/recordsExpected must be integers")
    if recovered_n != expected_n or recovered_n != len(doc.get("comps") or {}):
        raise SystemExit(
            f"extract.py: recovery coverage inconsistent (recovered={recovered_n}, "
            f"expected={expected_n}, records={len(doc.get('comps') or {})})"
        )

    records = doc.get("comps")
    if not isinstance(records, dict) or not records:
        raise SystemExit("extract.py: recovered-texts 'comps' must be a non-empty object")

    slot_map = doc["slotToColumn"]
    if not isinstance(slot_map, dict) or not slot_map:
        raise SystemExit("extract.py: recovered-texts 'slotToColumn' must be a non-empty object")
    try:
        column_names = [
            slot_map[str(slot)]
            for slot in sorted(int(slot) for slot in slot_map)
        ]
    except (KeyError, ValueError) as exc:
        raise SystemExit(f"extract.py: recovered-texts slotToColumn malformed: {exc}")
    if len(set(column_names)) != len(column_names):
        raise SystemExit("extract.py: recovered-texts slotToColumn has duplicate columns")

    anomalies = {
        entry.get("compNo")
        for entry in doc.get("anomalies") or []
        if isinstance(entry, dict)
    }
    digest = hashlib.sha256(evidence_path.read_bytes()).hexdigest()
    return {
        "records": records,
        "column_names": column_names,
        "anomalies": anomalies,
        "method": doc.get("method"),
        "evidence_path": str(evidence_path),
        "evidence_sha256": digest,
    }


def merge_recovered_texts(schema, rows, definitions, definition_order, recovery):
    """Merge the recovered variable-length Text columns into the Comps payload.

    Per-row verification is anchored on the row's own CompNo; any
    unverifiable identity or structurally-broken matched record fails the run
    hard. Rows whose recovery is absent or flagged untrusted stay nulls and
    warn. Returns (schema, rows, provenance).
    """
    table = RECOVERED_TARGET_TABLE
    records = recovery["records"]
    columns = recovery["column_names"]

    for name in columns:
        if name not in definitions:
            raise SystemExit(
                f"extract.py: recovered column {name!r} has no {table} table-definition entry"
            )
        if definitions[name] != apu.TYPE_TEXT:
            raise SystemExit(
                f"extract.py: recovered column {name!r} is not declared Jet Text in {table}"
            )

    merged_per_column = Counter()
    null_rows_per_column = Counter()
    untrusted_rows = []
    uncovered_rows = []

    for index, row in enumerate(rows):
        comp_no = row.get("CompNo")
        if not isinstance(comp_no, int):
            raise SystemExit(
                f"extract.py: {table} row {index} carries no usable CompNo "
                f"({comp_no!r}); recovered texts cannot be verified per-row"
            )
        record = records.get(str(comp_no))
        if record is None:
            uncovered_rows.append(comp_no)
            for name in columns:
                null_rows_per_column[name] += 1
            continue
        if not isinstance(record, dict):
            raise SystemExit(
                f"extract.py: recovered record for CompNo {comp_no} is not a JSON object"
            )
        flags = record.get("_flags")
        trusted = isinstance(flags, dict) and flags.get("varTextTrusted") is True
        contaminated = comp_no in recovery["anomalies"]
        if not trusted or contaminated:
            why = (
                "flagged contaminated upstream"
                if contaminated
                else "varTextTrusted is not true"
            )
            untrusted_rows.append((comp_no, why))
            for name in columns:
                null_rows_per_column[name] += 1
            continue
        for name in columns:
            if name not in record:
                raise SystemExit(
                    f"extract.py: recovered record for CompNo {comp_no} is "
                    f"missing column {name!r}"
                )
            value = record[name]
            if not isinstance(value, str):
                raise SystemExit(
                    f"extract.py: recovered value for CompNo {comp_no} "
                    f"{name!r} is not a string ({type(value).__name__})"
                )
            row[name] = value
            merged_per_column[name] += 1

    all_names = list(dict.fromkeys(list(schema.keys()) + columns))
    unknown = [name for name in all_names if name not in definition_order]
    if unknown:
        raise SystemExit(
            f"extract.py: {table} column(s) missing from table-definition "
            f"order: {unknown}"
        )
    final_schema = {}
    for name in definition_order:
        if name in schema:
            final_schema[name] = schema[name]
        elif name in columns:
            final_schema[name] = type_name(definitions[name])
    final_rows = [{name: row.get(name) for name in definition_order} for row in rows]

    provenance = {
        "targetTable": table,
        "columns": {
            name: {
                "sourceKind": "master-db-recovered-var-text",
                "method": "ingested, byte-validated upstream",
                "varTextTrusted": null_rows_per_column[name] == 0,
                "jetType": type_name(definitions[name]),
                "rowsMerged": merged_per_column[name],
                "rowsNull": null_rows_per_column[name],
            }
            for name in columns
        },
    }
    if uncovered_rows:
        provenance["rowsWithoutRecovery"] = uncovered_rows
        warn(
            f"{table}: no recovered record for CompNo(s) {uncovered_rows}; "
            "their recovered columns stay null"
        )
    if untrusted_rows:
        provenance["rowsNotTrusted"] = [
            {"CompNo": comp, "why": why} for comp, why in untrusted_rows
        ]
        warn(
            f"{table}: recovery for CompNo(s) "
            f"{[comp for comp, _ in untrusted_rows]} not trusted ({why}); "
            "their recovered columns stay null"
        )
    provenance["evidenceFile"] = recovery["evidence_path"]
    provenance["evidenceSha256"] = recovery["evidence_sha256"]
    provenance["upstreamCrackMethod"] = recovery["method"]
    provenance["rowsMergedAny"] = max(merged_per_column.values(), default=0)
    return final_schema, final_rows, provenance


TYPE_NAMES = {
    apu.TYPE_BOOLEAN: "Boolean",
    apu.TYPE_INT8: "Byte",
    apu.TYPE_INT16: "Integer",
    apu.TYPE_INT32: "Long",
    apu.TYPE_MONEY: "Currency",
    apu.TYPE_FLOAT32: "Single",
    apu.TYPE_FLOAT64: "Double",
    apu.TYPE_DATETIME: "DateTime",
    apu.TYPE_BINARY: "Binary",
    apu.TYPE_TEXT: "Text",
    apu.TYPE_OLE: "OLE",
    apu.TYPE_MEMO: "Memo",
    apu.TYPE_GUID: "GUID",
    apu.TYPE_96_bit_17_BYTES: "Numeric",
    apu.TYPE_COMPLEX: "Complex",
}


def type_name(code):
    try:
        return TYPE_NAMES[code]
    except KeyError:
        raise SystemExit(f"extract.py: unknown Jet column type code {code!r}; extend TYPE_NAMES")


def encode(value):
    if value is None:
        return None
    if isinstance(value, bool):
        return value
    if isinstance(value, int):
        return value
    if isinstance(value, float):
        if math.isnan(value) or math.isinf(value):
            return {"$float": repr(value)}
        return value
    if isinstance(value, str):
        return value
    if isinstance(value, (bytes, bytearray, memoryview)):
        return {"$bytes": bytes(value).hex()}
    if isinstance(value, datetime):
        return {"$datetime": value.isoformat(sep=" ")}
    if isinstance(value, date):
        return {"$date": value.isoformat()}
    if isinstance(value, time):
        return {"$time": value.isoformat()}
    if isinstance(value, Decimal):
        return {"$decimal": str(value)}
    raise SystemExit(f"extract.py: no JSON encoding for value {value!r} of type {type(value).__name__}")


RECOVERY_PROVENANCE = {}


def extract_table(db, table_name, recovery=None):
    table = db.get_table(table_name)
    if table is None:
        raise SystemExit(f"extract.py: library could not open table {table_name!r}")
    definitions = {}
    definition_order = []
    for _, column in sorted(table.columns.items()):
        name = column.col_name_str
        if name in definitions:
            raise SystemExit(f"extract.py: duplicate column name {name!r} in table {table_name!r}")
        definitions[name] = column.type
        definition_order.append(name)

    data = db.parse_table(table_name)
    columns = list(data.keys())
    if not columns:
        raise SystemExit(f"extract.py: library returned no columns for table {table_name!r}")

    first = data[columns[0]]
    row_count = len(first) if isinstance(first, list) else 0
    for column in columns:
        values = data[column]
        if not isinstance(values, list):
            continue
        if len(values) != row_count:
            raise SystemExit(
                f"extract.py: ragged table {table_name!r}: column {column!r} has "
                f"{len(values)} rows, expected {row_count}"
            )

    schema = {}
    for column in columns:
        if column not in definitions:
            raise SystemExit(
                f"extract.py: parsed column {column!r} of {table_name!r} has no table-definition entry"
            )
        schema[column] = type_name(definitions[column])

    rows = [
        {column: encode(data[column][row]) for column in columns}
        for row in range(row_count)
    ]

    if recovery is not None and table_name == RECOVERED_TARGET_TABLE:
        schema, rows, provenance = merge_recovered_texts(
            schema, rows, definitions, definition_order, recovery
        )
        RECOVERY_PROVENANCE[table_name] = provenance

    return {"schema": schema, "rows": rows}


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Extract a GliderScore Jet (.mdb) export to one JSON file per table."
    )
    parser.add_argument("export_file", help="path to the GliderScore export file")
    parser.add_argument("--out", default=".", help="output root directory (default: current directory)")
    parser.add_argument("--slug", default=None, help="fixture slug (default: input filename stem)")
    parser.add_argument(
        "--tolerant",
        action="store_true",
        help=(
            "opt-in: tolerate the access_parser 0.0.6 null-bitmap off-by-one "
            "(IndexError in AccessTable._parse_fixed_length_data) by reading "
            "past-boundary columns gracefully and warning; default OFF"
        ),
    )
    parser.add_argument(
        "--recovered-texts",
        default=None,
        metavar="PATH",
        help=(
            "with --tolerant: ingest the byte-validated recovered "
            "variable-length Text columns from a comps-var-columns.json-style "
            "file into the Comps table and emit comps-field-provenance.json"
        ),
    )
    args = parser.parse_args(argv)

    if args.recovered_texts is not None and not args.tolerant:
        raise SystemExit("extract.py: --recovered-texts requires --tolerant")

    source = Path(args.export_file)
    if not source.is_file():
        raise SystemExit(f"extract.py: no such file: {source}")
    slug = args.slug if args.slug is not None else source.stem
    out_dir = Path(args.out) / slug / "extract"
    out_dir.mkdir(parents=True, exist_ok=True)

    if args.tolerant:
        _install_tolerant_parser_patch()
    recovery = load_recovered_texts(args.recovered_texts) if args.recovered_texts else None

    db = AccessParser(str(source))
    tables = sorted(db.catalog.keys())

    total_rows = 0
    merged_provenance = None
    for table_name in tables:
        payload = extract_table(db, table_name, recovery=recovery)
        total_rows += len(payload["rows"])
        if args.tolerant:
            report_degraded_hits(table_name)
        target = out_dir / f"{table_name}.json"
        text = json.dumps(payload, indent=2, ensure_ascii=False, allow_nan=False) + "\n"
        target.write_text(text, encoding="utf-8")
        print(f"{target}: schema={len(payload['schema'])} rows={len(payload['rows'])}")
        if recovery is not None and table_name == RECOVERED_TARGET_TABLE:
            merged_provenance = RECOVERY_PROVENANCE.get(table_name)
            provenance_target = out_dir / "comps-field-provenance.json"
            provenance_text = (
                json.dumps(merged_provenance, indent=2, ensure_ascii=False, allow_nan=False) + "\n"
            )
            provenance_target.write_text(provenance_text, encoding="utf-8")
            print(f"{provenance_target}: provenance for recovered text columns")

    summarize_tolerant_run()
    if merged_provenance is not None:
        column_names = list(merged_provenance["columns"])
        print(
            f"recovered-texts: merged {len(column_names)} column(s) x "
            f"{merged_provenance['rowsMergedAny']} Comps row(s) "
            f"({', '.join(column_names)}); rows without recovery: "
            f"{len(merged_provenance.get('rowsWithoutRecovery', []))}, "
            f"rows not trusted: {len(merged_provenance.get('rowsNotTrusted', []))}"
        )
    print(f"wrote {len(tables)} tables, {total_rows} rows to {out_dir}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
