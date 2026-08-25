#!/usr/bin/env python3
"""Extract every table of a GliderScore Jet export into deterministic JSON fixtures.

Usage:
    PYTHONPATH=/var/data/python/lib/python3.13/site-packages \
        python3 extract.py <export-file> [--out DIR] [--slug NAME]

See README.md (same directory) for the full contract.
"""

import argparse
import json
import math
import sys
from datetime import date, datetime, time
from decimal import Decimal
from pathlib import Path

from access_parser import AccessParser
from access_parser import utils as apu

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


def extract_table(db, table_name):
    table = db.get_table(table_name)
    if table is None:
        raise SystemExit(f"extract.py: library could not open table {table_name!r}")
    definitions = {}
    for _, column in sorted(table.columns.items()):
        name = column.col_name_str
        if name in definitions:
            raise SystemExit(f"extract.py: duplicate column name {name!r} in table {table_name!r}")
        definitions[name] = column.type

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
    return {"schema": schema, "rows": rows}


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Extract a GliderScore Jet (.mdb) export to one JSON file per table."
    )
    parser.add_argument("export_file", help="path to the GliderScore export file")
    parser.add_argument("--out", default=".", help="output root directory (default: current directory)")
    parser.add_argument("--slug", default=None, help="fixture slug (default: input filename stem)")
    args = parser.parse_args(argv)

    source = Path(args.export_file)
    if not source.is_file():
        raise SystemExit(f"extract.py: no such file: {source}")
    slug = args.slug if args.slug is not None else source.stem
    out_dir = Path(args.out) / slug / "extract"
    out_dir.mkdir(parents=True, exist_ok=True)

    db = AccessParser(str(source))
    tables = sorted(db.catalog.keys())

    total_rows = 0
    for table_name in tables:
        payload = extract_table(db, table_name)
        total_rows += len(payload["rows"])
        target = out_dir / f"{table_name}.json"
        text = json.dumps(payload, indent=2, ensure_ascii=False, allow_nan=False) + "\n"
        target.write_text(text, encoding="utf-8")
        print(f"{target}: schema={len(payload['schema'])} rows={len(payload['rows'])}")

    print(f"wrote {len(tables)} tables, {total_rows} rows to {out_dir}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
