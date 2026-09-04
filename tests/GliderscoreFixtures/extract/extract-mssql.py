#!/usr/bin/env python3
"""Extract a single competition slice from the GliderScore SQL Server database.

Usage:
    python3 extract-mssql.py <CompID> [--container NAME] [--db NAME] \
        [--sqlcmd PATH] [--out DIR] [--slug NAME] [--no-redact] \
        [--password-env NAME]

The gliderscore.com server database (a SQL Server backup restored into a
throwaway Docker container) is sliced one competition at a time: every table
that carries a `CompID` column and has at least one row for the given CompID
(a case-sensitive hex string) is dumped to the same output shape contract as
extract.py — one `<Table>.json` per table under `<out>/<slug>/extract/`, each

    {"schema": {column: type name, ...}, "rows": [row, ...]}

2-space indent, `ensure_ascii=False`, trailing newline, tables written in
sorted order. Values are queried read-only via `docker exec` + sqlcmd; text
cells travel hex-encoded (UTF-16LE) so output is byte-deterministic and
immune to column separators, code pages and embedded newlines.

PilotName/HelperName are redacted to deterministic Simpsons names by default
(--no-redact disables); FAI_ID is blanked. No redaction mapping is ever
written anywhere — redaction happens before serialisation.

See README.md (same directory) for the full contract, the type-name mapping
and the documented limitations. Developer-run offline tooling — nothing in
src/, tests or build may reference this script.
"""

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
from decimal import Decimal
from pathlib import Path

CONTAINER_DEFAULT = "mssql-gliderscore"
DB_DEFAULT = "gliderscore"
SQLCMD_DEFAULT = "/opt/mssql-tools18/bin/sqlcmd"
PASSWORD_ENV_DEFAULT = "EXTRACT_MSSQL_SA_PASSWORD"
# Throwaway container password only (documented); a real password must come
# from the environment and is never printed or written.
PASSWORD_FALLBACK = "Gliderscore!Restore1"
COLLATE_CS = "Latin1_General_CS_AS"
# Marks a SQL NULL on a transported text column. Empty string transports as
# an empty value, so the two are distinguished. A stored value equal to this
# literal would be misread as NULL — see README limitations.
NULL_SENTINEL = "<<__NULL_TOKEN__>>"

# Fixed-length char/nchar values are RTRIMmed: the padding is a column-width
# artifact, not data. varchar/nvarchar pass through untouched.
FIXED_LENGTH_TEXT = ("char", "nchar")
HEX_TRANSPORTED = ("char", "nchar", "varchar", "nvarchar", "text", "ntext",
                   "datetime2", "datetime", "smalldatetime")
# SQL Server style for datetime2/datetime -> text (ISO 8601; keeps full
# fractional precision for datetime2).
DATETIME_STYLE = 126

# DATA_TYPE -> simple schema name. Numbers follow the task's mapping; widths
# narrower than int collapse to Integer, int is Long (Jet nomenclature as in
# extract.py). Types not listed abort the run loudly.
SQL_TYPE_NAMES = {
    "char": "Text",
    "nchar": "Text",
    "varchar": "Text",
    "nvarchar": "Text",
    "text": "Text",
    "ntext": "Text",
    "tinyint": "Integer",
    "smallint": "Integer",
    "int": "Long",
    "numeric": "Decimal",
    "decimal": "Decimal",
    "bit": "Boolean",
    "datetime2": "DateTime",
    "datetime": "DateTime",
    "smalldatetime": "DateTime",
    "date": "Date",
    "float": "Double",
    "real": "Double",
    "bigint": "Long",
}

# Deterministic row order: natural key per table; every remaining column
# follows as a total-order tiebreaker.
TABLE_ORDER_KEYS = {
    "CompData": ["CompID"],
    "ScoringData": ["RoundNo", "GroupNo", "SeqNo", "ReFlightNo", "PilotNo"],
    "ScoringBackup": ["RoundNo", "GroupNo", "SeqNo", "ReFlightNo", "PilotNo"],
    "F3KData": ["RoundNo"],
    "F5KData": ["RoundNo"],
    "F5KBonusData": ["Metres"],
    "LandingData": ["Distance", "Points"],
    "TargetTimeByRound": ["RoundNo"],
    "DigitalTimerData": ["RoundNo", "GroupNo", "ReFlightNo"],
}

# Redaction pools. Pilot pool = the fixed Simpsons list from the story.
PILOT_NAMES = [
    "Homer Simpson", "Marge Simpson", "Bart Simpson", "Lisa Simpson",
    "Ned Flanders", "Mr Burns", "Moe Szyslak", "Krusty the Clown",
    "Apu Nahasapeemapetilon", "Milhouse Van Houten", "Nelson Muntz",
    "Lenny Leonard", "Carl Carlson", "Barney Gumble", "Seymour Skinner",
    "Edna Krabappel", "Kent Brockman", "Lionel Hutz", "Otto Mann", "Smithers",
]
# Helpers are distinct people: a disjoint pool of non-pilot characters.
NON_PILOT_NAMES = [
    "Itchy", "Scratchy", "Santa's Little Helper", "Snowball II", "Poochie",
    "Duffman", "Cletus Spuckler", "Professor Frink", "Comic Book Guy",
    "Chief Wiggum", "Ralph Wiggum", "Groundskeeper Willie", "Sideshow Bob",
    "Reverend Lovejoy", "Dr Hibbert", "Luigi Risotto", "Agnes Skinner",
    "Patty Bouvier", "Selma Bouvier", "Troy McClure",
]
if set(PILOT_NAMES) & set(NON_PILOT_NAMES):
    raise SystemExit("extract-mssql.py: pilot and non-pilot redaction pools overlap")


def warn(message):
    print(f"extract-mssql.py: WARNING: {message}", file=sys.stderr)


def quote_ident(name):
    """Bracket-quote an identifier after validating it is a plain name."""
    if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name):
        raise SystemExit(f"extract-mssql.py: refusing unexpected identifier {name!r}")
    return f"[{name}]"


def quote_literal(value):
    return "'" + value.replace("'", "''") + "'"


def run_sqlcmd(args, query):
    """Run one read-only query in the container; return data lines.

    sqlcmd flags: -y 0 removes the default 256-char display truncation
    (mutually exclusive with both -W and -h, hence absent here; the hex
    transport needs no trimming and this sqlcmd build prints no header
    block under these flags). A header block, if a build ever prints one,
    is detected and skipped; anything else malformed fails loudly.
    """
    password = os.environ.get(args.password_env) or PASSWORD_FALLBACK
    command = [
        "docker", "exec", args.container, args.sqlcmd,
        "-S", "localhost", "-U", "sa", "-P", password,
        "-C", "-No", "-d", args.db,
        "-Q", query,
        "-s", "|", "-f", "65001", "-b", "-y", "0",
    ]
    proc = subprocess.run(command, capture_output=True, text=False)
    if proc.returncode != 0:
        detail = proc.stderr.decode("utf-8", errors="replace").strip()
        raise SystemExit(
            f"extract-mssql.py: sqlcmd failed (exit {proc.returncode}):\n{detail}"
        )
    text = proc.stdout.decode("utf-8", errors="strict").lstrip("\ufeff")
    lines = [line.rstrip("\r") for line in text.split("\n")]

    def separator(line):
        return bool(line) and set(line) <= set("-| ")

    if len(lines) >= 2 and separator(lines[1]) and not separator(lines[0]):
        lines = lines[2:]  # column-name line + dash separator
    elif lines and separator(lines[0]):
        lines = lines[1:]
    return lines


def discover_tables(args, comp_id):
    """Comp-scoped tables with >=1 row for comp_id: {name: row count}."""
    tables = run_sqlcmd(
        args,
        "SET NOCOUNT ON;\n"
        "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.COLUMNS "
        "WHERE COLUMN_NAME = 'CompID' GROUP BY TABLE_NAME ORDER BY TABLE_NAME;\n",
    )
    selects = []
    for table in (line.strip() for line in tables):
        if not table:
            continue
        ident = quote_ident(table)
        column = quote_ident("CompID")
        selects.append(
            f"SELECT {quote_literal(table)}, COUNT_BIG(*) FROM {ident} "
            f"WHERE {column} = {quote_literal(comp_id)} COLLATE {COLLATE_CS}"
        )
    if not selects:
        return {}
    rows = run_sqlcmd(args, "SET NOCOUNT ON;\n" + "\nUNION ALL\n".join(selects) + ";")
    result = {}
    for line in rows:
        if not line.strip():
            continue
        parts = line.split("|")
        if len(parts) != 2:
            raise SystemExit(f"extract-mssql.py: malformed count row {line!r}")
        name, count = parts
        if not count.isdigit():
            raise SystemExit(f"extract-mssql.py: malformed row count {line!r}")
        if int(count) > 0:
            result[name] = int(count)
    return result


def fetch_columns(args, table):
    """Ordered [(column name, DATA_TYPE)] for a table."""
    rows = run_sqlcmd(
        args,
        "SET NOCOUNT ON;\n"
        "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS "
        f"WHERE TABLE_NAME = {quote_literal(table)} ORDER BY ORDINAL_POSITION;\n",
    )
    columns = []
    for line in rows:
        if not line.strip():
            continue
        parts = line.split("|")
        if len(parts) != 2:
            raise SystemExit(
                f"extract-mssql.py: malformed column row for {table}: {line!r}"
            )
        columns.append((parts[0], parts[1]))
    if not columns:
        raise SystemExit(f"extract-mssql.py: no columns discovered for {table!r}")
    return columns


def build_row_query(table, columns, comp_id, order_keys):
    """One hex-packed varchar(max) column per row; fields joined by '|'."""
    expressions = []
    for name, data_type in columns:
        if data_type in HEX_TRANSPORTED:
            if data_type in FIXED_LENGTH_TEXT:
                value = f"RTRIM(CONVERT(nvarchar(max), {quote_ident(name)}))"
            elif data_type in ("datetime2", "datetime", "smalldatetime"):
                value = f"CONVERT(nvarchar(max), {quote_ident(name)}, {DATETIME_STYLE})"
            else:
                value = f"CONVERT(nvarchar(max), {quote_ident(name)})"
            value = f"ISNULL({value}, N'{NULL_SENTINEL}')"
        else:
            value = f"CONVERT(nvarchar(max), {quote_ident(name)})"
        packed = (
            f"CONVERT(nvarchar(max), CONVERT(varbinary(max), {value}), 2)"
        )
        expressions.append(packed)
    select_list = "CONCAT(" + ", N'|', ".join(expressions) + ")"
    order_names = list(order_keys)
    for name, _ in columns:
        if name not in order_names:
            order_names.append(name)
    query = (
        "SET NOCOUNT ON;\n"
        f"SELECT {select_list}\nFROM {quote_ident(table)}\n"
        f"WHERE {quote_ident('CompID')} = {quote_literal(comp_id)} "
        f"COLLATE {COLLATE_CS}\n"
        f"ORDER BY {', '.join(quote_ident(name) for name in order_names)};"
    )
    return query


def parse_cell(type_name, text, table, column):
    if type_name == "Boolean":
        if text in ("0", "1"):
            return text == "1"
        raise SystemExit(
            f"extract-mssql.py: {table}.{column}: unexpected bit text {text!r}"
        )
    if type_name in ("Byte", "Integer", "Long"):
        try:
            return int(text)
        except ValueError as exc:
            raise SystemExit(
                f"extract-mssql.py: {table}.{column}: bad integer text {text!r}: {exc}"
            )
    if type_name in ("Decimal", "Currency"):
        try:
            return Decimal(text)
        except ArithmeticError as exc:
            raise SystemExit(
                f"extract-mssql.py: {table}.{column}: bad decimal text {text!r}: {exc}"
            )
    if type_name in ("Single", "Double"):
        try:
            return float(text)
        except ValueError as exc:
            raise SystemExit(
                f"extract-mssql.py: {table}.{column}: bad float text {text!r}: {exc}"
            )
    return text


def decode_cell(type_name, field, table, column):
    """Every field arrives as a hex string of UTF-16LE bytes (CONCAT packs
    each converted value; empty payload = empty string for hex-transported
    types, NULL for everything else, since no non-text conversion ever
    renders to the empty string)."""
    if field and not re.fullmatch(r"(?:[0-9a-fA-F]{2})+", field):
        raise SystemExit(
            f"extract-mssql.py: {table}.{column}: malformed cell payload "
            f"({len(field)} chars, not hex)"
        )
    raw = bytes.fromhex(field).decode("utf-16-le")
    if raw == NULL_SENTINEL:
        return None
    if raw == "" and type_name != "Text":
        return None
    if type_name == "Text":
        return raw
    return parse_cell(type_name, raw, table, column)


def parse_rows(lines, columns, table):
    rows = []
    for line_number, line in enumerate(lines, start=1):
        if not line.strip():
            continue
        fields = line.split("|")
        if len(fields) != len(columns):
            raise SystemExit(
                f"extract-mssql.py: {table}: line {line_number} has "
                f"{len(fields)} fields, expected {len(columns)}"
            )
        row = {}
        for (name, data_type), field in zip(columns, fields):
            row[name] = decode_cell(SQL_TYPE_NAMES[data_type], field, table, name)
        rows.append(row)
    return rows


def encode(value):
    """JSON encoding, identical conventions to extract.py."""
    if value is None:
        return None
    if isinstance(value, bool):
        return value
    if isinstance(value, int):
        return value
    if isinstance(value, float):
        if value != value or value in (float("inf"), float("-inf")):
            return {"$float": repr(value)}
        return value
    if isinstance(value, str):
        return value
    if isinstance(value, Decimal):
        return {"$decimal": str(value)}
    raise SystemExit(
        f"extract-mssql.py: no JSON encoding for value {value!r} "
        f"of type {type(value).__name__}"
    )


def pilot_key(comp_id, row):
    if row.get("PilotNo") is not None:
        return f"pilot|{comp_id}|{row['PilotNo']}"
    return f"pilotname|{comp_id}|{row.get('PilotName')}"


def assign_names(keys, pool):
    """Deterministic, collision-free while keys <= pool size: keys ordered by
    sha256 digest, names assigned from the fixed pool in order."""
    ordered = sorted(keys, key=lambda k: hashlib.sha256(k.encode("utf-8")).digest())
    return {key: pool[index % len(pool)] for index, key in enumerate(ordered)}


def apply_redaction(comp_id, tables, redact):
    """Redact PilotName/HelperName (Simpsons, deterministic per comp) and
    blank FAI_ID. In-place; the mapping exists only in memory."""
    if not redact:
        warn(
            "redaction DISABLED (--no-redact): real pilot names and FAI_ID "
            "licence numbers will be written — never commit this output"
        )
        return
    pilot_keys = set()
    helper_values = set()
    for table in tables.values():
        names = {name for name, _ in table["columns"]}
        if "PilotName" in names:
            for row in table["rows"]:
                if isinstance(row.get("PilotName"), str) and row["PilotName"]:
                    pilot_keys.add(pilot_key(comp_id, row))
        if "HelperName" in names:
            for row in table["rows"]:
                if isinstance(row.get("HelperName"), str) and row["HelperName"]:
                    helper_values.add(row["HelperName"])
    pilot_map = assign_names(pilot_keys, PILOT_NAMES)
    helper_map = assign_names(
        {f"helper|{comp_id}|{value}" for value in helper_values}, NON_PILOT_NAMES
    )
    pilots = helpers = licences = 0
    for table in tables.values():
        names = [name for name, _ in table["columns"]]
        if "FAI_ID" in names:
            for row in table["rows"]:
                if row.get("FAI_ID") not in (None, ""):
                    licences += 1
                row["FAI_ID"] = ""
        if "PilotName" in names:
            for row in table["rows"]:
                if isinstance(row.get("PilotName"), str) and row["PilotName"]:
                    row["PilotName"] = pilot_map[pilot_key(comp_id, row)]
                    pilots += 1
        if "HelperName" in names:
            for row in table["rows"]:
                if isinstance(row.get("HelperName"), str) and row["HelperName"]:
                    row["HelperName"] = helper_map[f"helper|{comp_id}|{row['HelperName']}"]
                    helpers += 1
    print(
        f"redacted: {len(pilot_map)} pilot name(s) ({pilots} cell(s)), "
        f"{len(helper_map)} helper name(s) ({helpers} cell(s)), "
        f"{licences} FAI_ID licence(s) blanked"
    )


def main(argv=None):
    parser = argparse.ArgumentParser(
        description=(
            "Extract a single GliderScore competition from the SQL Server "
            "database container to one JSON file per table (extract.py shape)."
        )
    )
    parser.add_argument("comp_id", help="CompID hex string, case-sensitive")
    parser.add_argument("--container", default=CONTAINER_DEFAULT,
                        help=f"Docker container name (default: {CONTAINER_DEFAULT})")
    parser.add_argument("--db", default=DB_DEFAULT,
                        help=f"database name (default: {DB_DEFAULT})")
    parser.add_argument("--sqlcmd", default=SQLCMD_DEFAULT,
                        help=f"sqlcmd path inside the container (default: {SQLCMD_DEFAULT})")
    parser.add_argument("--out", default=".",
                        help="output root directory (default: current directory)")
    parser.add_argument("--slug", default=None,
                        help="fixture slug (default: mssql-comp-<CompID>)")
    parser.add_argument("--no-redact", action="store_true",
                        help="write real PilotName/HelperName/FAI_ID values (never commit)")
    parser.add_argument("--password-env", default=PASSWORD_ENV_DEFAULT,
                        help=(
                            "environment variable holding the SA password "
                            f"(default: {PASSWORD_ENV_DEFAULT}; falls back to the "
                            "throwaway container's default password)"
                        ))
    args = parser.parse_args(argv)

    comp_id = args.comp_id.strip()
    if not comp_id:
        raise SystemExit("extract-mssql.py: CompID must not be empty")
    if not re.fullmatch(r"[0-9A-Za-z]+", comp_id):
        raise SystemExit(
            f"extract-mssql.py: CompID {comp_id!r} is not a plain hex-ish string"
        )
    slug = args.slug if args.slug is not None else f"mssql-comp-{comp_id}"
    out_dir = Path(args.out) / slug / "extract"
    out_dir.mkdir(parents=True, exist_ok=True)

    counts = discover_tables(args, comp_id)
    if not counts:
        raise SystemExit(
            f"extract-mssql.py: no comp-scoped rows found for CompID "
            f"{comp_id!r} in {args.db} on {args.container} — CompIDs are "
            "case-sensitive hex; check the case"
        )

    tables = {}
    for table in sorted(counts):
        columns = fetch_columns(args, table)
        types = {name: data_type for name, data_type in columns}
        for data_type in types.values():
            if data_type not in SQL_TYPE_NAMES:
                raise SystemExit(
                    f"extract-mssql.py: {table}.{data_type!r}: unknown SQL "
                    "Server type; extend SQL_TYPE_NAMES"
                )
        known = TABLE_ORDER_KEYS.get(table, [])
        missing = [key for key in known if key not in types]
        if missing:
            raise SystemExit(
                f"extract-mssql.py: TABLE_ORDER_KEYS for {table} names "
                f"missing column(s) {missing}"
            )
        query = build_row_query(table, columns, comp_id, known)
        lines = run_sqlcmd(args, query)
        rows = parse_rows(lines, columns, table)
        if len(rows) != counts[table]:
            raise SystemExit(
                f"extract-mssql.py: {table}: fetched {len(rows)} row(s), "
                f"expected {counts[table]}"
            )
        tables[table] = {
            "columns": columns,
            "schema": {name: SQL_TYPE_NAMES[data_type] for name, data_type in columns},
            "rows": rows,
        }

    apply_redaction(comp_id, tables, redact=not args.no_redact)

    total_rows = 0
    for table in sorted(tables):
        payload = {
            "schema": tables[table]["schema"],
            "rows": [
                {name: encode(row[name]) for name, _ in tables[table]["columns"]}
                for row in tables[table]["rows"]
            ],
        }
        total_rows += len(payload["rows"])
        target = out_dir / f"{table}.json"
        text = json.dumps(payload, indent=2, ensure_ascii=False, allow_nan=False) + "\n"
        target.write_text(text, encoding="utf-8")
        print(f"{target}: schema={len(payload['schema'])} rows={len(payload['rows'])}")
    print(f"wrote {len(tables)} tables, {total_rows} rows to {out_dir}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
