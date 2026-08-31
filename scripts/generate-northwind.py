#!/usr/bin/env python3
"""Generate data/{postgres,mysql}/create-northwind.sql from data/sqlite/Northwind.db.

Companion to generate-sqlserver-northwind.py. SQL Server needs a PascalCase
schema with a snake_case bridge; PostgreSQL, MySQL and MariaDB take the
SQLite reference schema's snake_case names unchanged, so the work here is
type translation, identifier-safe literals and date normalisation.

MariaDB reuses the MySQL output — the two dialects differ nowhere in this
schema.

Regenerate whenever the SQLite reference database changes:

    python scripts/generate-northwind.py
"""
import re
import sqlite3
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "data" / "sqlite" / "Northwind.db"

# SQLite stores dates as 'YYYY/M/D' text; PostgreSQL parses that ambiguously
# and MySQL rejects it outright, so both get ISO-8601.
DATE_RX = re.compile(r"^(\d{4})/(\d{1,2})/(\d{1,2})$")

# Entity contracts override declared SQLite types where providers differ:
# Product.CategoryId is short?, so the column must be smallint (SQLite's
# dynamic typing hid the mismatch). The products->categories FK is dropped
# because it would then straddle smallint and integer, matching what
# generate-sqlserver-northwind.py does for the same reason.
COLUMN_TYPE_OVERRIDES = {
    ("products", "category_id"): "smallint",
}
DROP_FKS = {"fk_products__categories"}

# Insert order must satisfy FK dependencies.
TABLE_ORDER = [
    "region", "territories", "customer_demographics", "customers",
    "customer_customer_demo", "employees", "employee_territories",
    "categories", "suppliers", "products", "shippers", "orders",
    "order_details",
]

DIALECTS = {
    "postgres": {
        "path": ("data", "postgres", "create-northwind.sql"),
        "usage": "psql -h localhost -p 5433 -U postgres -d northwind -f create-northwind.sql",
        # Product.UnitPrice is decimal?, so REAL becomes numeric rather than
        # double precision; the committed stored procedures also declare
        # unit_price DECIMAL and a function's result type must match exactly.
        "types": [
            (r"\bINTEGER\b", "integer"),
            (r"\bSMALLINT\b", "smallint"),
            (r"\bREAL\b", "numeric"),
            (r"\bTEXT\b", "text"),
            (r"\bBLOB\b", "bytea"),
            (r"\bVARCHAR\(", "varchar("),
            (r"\bCHAR\(", "char("),
            (r"\bDATE\b", "timestamp"),
            (r"\bBOOLEAN\b", "boolean"),
        ],
        "quote": '"',
        # PostgreSQL scopes CHECK constraint names per table, as SQLite does.
        "prefix_checks": False,
        "drop": "DROP TABLE IF EXISTS {t} CASCADE;",
        # PostgreSQL evaluates FK triggers at end of statement, so the
        # self-referencing employees.reports_to rows load in any order.
        "preamble": [],
        "postamble": [],
        # PascalCase must be quoted or PostgreSQL folds it to lower case.
        "bridge_quote": '"',
        "generated": "ALTER TABLE {t} ADD COLUMN {new} {type} GENERATED ALWAYS AS ({src}) STORED;",
    },
    "mysql": {
        "path": ("data", "mysql", "create-northwind.sql"),
        "usage": "mysql -h 127.0.0.1 -P 3308 -u root -p northwind < create-northwind.sql",
        "types": [
            (r"\bINTEGER\b", "int"),
            (r"\bSMALLINT\b", "smallint"),
            (r"\bREAL\b", "decimal(19,4)"),
            (r"\bTEXT\b", "text"),
            (r"\bBLOB\b", "longblob"),
            (r"\bVARCHAR\(", "varchar("),
            (r"\bCHAR\(", "char("),
            (r"\bDATE\b", "datetime"),
            (r"\bBOOLEAN\b", "tinyint(1)"),
        ],
        "quote": "`",
        # MySQL 8 requires CHECK constraint names to be unique per schema,
        # and the SQLite schema reuses names like ck_unit_price.
        "prefix_checks": True,
        "drop": "DROP TABLE IF EXISTS {t};",
        # MySQL enforces FKs row by row, so employees.reports_to fails on the
        # first manager who appears after their report.
        "preamble": ["SET FOREIGN_KEY_CHECKS = 0;"],
        "postamble": ["SET FOREIGN_KEY_CHECKS = 1;"],
        "bridge_quote": "`",
        "generated": "ALTER TABLE {t} ADD COLUMN {new} {type} AS ({src});",
    },
}


def translate_schema(sql: str, table: str, cfg: dict) -> str:
    for pattern, repl in cfg["types"]:
        sql = re.sub(pattern, repl, sql, flags=re.I)
    if cfg["prefix_checks"]:
        sql = re.sub(r"CONSTRAINT (ck_)", f"CONSTRAINT \\g<1>{table}_", sql)
    for (t, col), coltype in COLUMN_TYPE_OVERRIDES.items():
        if t == table:
            sql = re.sub(rf"^(\s*{col}\s+)\w+(\(\d+(,\d+)?\))?", rf"\g<1>{coltype}", sql, flags=re.M)
    for fk in DROP_FKS:
        sql = re.sub(
            rf",?\s*CONSTRAINT {fk} FOREIGN KEY\s*\([^)]*\)\s*REFERENCES\s+\w+\s*\([^)]*\)",
            "",
            sql,
        )
    # MySQL rejects a parenthesised literal default before 8.0.13 and MariaDB
    # treats it as an expression; both accept the bare literal.
    sql = re.sub(r"DEFAULT \((\w+)\)", r"DEFAULT \1", sql)
    # MySQL only treats "--" as a comment when a space follows it, and the
    # SQLite schema carries a commented-out constraint written "--CONSTRAINT".
    sql = re.sub(r"--(?=\S)", "-- ", sql)
    return sql


def pascal(name: str) -> str:
    return "".join(part.capitalize() for part in name.split("_"))


def column_type(decl: str, cfg: dict) -> str:
    out = decl
    for pattern, repl in cfg["types"]:
        out = re.sub(pattern, repl, out, flags=re.I)
    return out


def bridge_columns(table: str, cols: list[tuple[str, str]], cfg: dict) -> list[str]:
    """PascalCase generated columns mirroring the snake_case originals.

    The integration entities read PascalCase field names out of the reader
    (Product.ReadEntity uses ordinal["ProductId"]) while their [Column]
    attributes name the snake_case columns, so every table has to answer to
    both. generate-sqlserver-northwind.py adds the same bridge in the other
    direction. Only underscore names need it: a difference of case alone is
    resolved by Npgsql's case-insensitive ordinal lookup and by MySQL's
    case-insensitive column names.
    """
    stmts = []
    for name, decl in cols:
        if "_" not in name:
            continue
        coltype = column_type(
            COLUMN_TYPE_OVERRIDES.get((table, name), decl), cfg
        )
        q = cfg["quote"]
        stmts.append(cfg["generated"].format(
            t=f"{q}{table}{q}", new=f'{cfg["bridge_quote"]}{pascal(name)}{cfg["bridge_quote"]}',
            type=coltype, src=f"{q}{name}{q}",
        ))
    return stmts


def normalise_date(value):
    m = DATE_RX.match(value)
    if not m:
        return value
    year, month, day = m.groups()
    return f"{year}-{int(month):02d}-{int(day):02d}"


def sql_literal(value, decl_type: str, dialect: str) -> str:
    if value is None:
        return "NULL"
    upper = decl_type.upper()
    if upper.startswith("BOOLEAN"):
        truthy = bool(value)
        if dialect == "postgres":
            return "TRUE" if truthy else "FALSE"
        return "1" if truthy else "0"
    if isinstance(value, bytes):
        if not value:
            value = b"\x00"
        if dialect == "postgres":
            return "'\\x" + value.hex() + "'::bytea"
        return "X'" + value.hex() + "'"
    if isinstance(value, (int, float)):
        return repr(value)
    s = str(value)
    if upper.startswith("DATE"):
        s = normalise_date(s)
    s = s.replace("'", "''")
    if dialect == "mysql":
        # MySQL treats backslash as an escape character inside string literals.
        s = s.replace("\\", "\\\\")
    return f"'{s}'"


def build(dialect: str, cfg: dict, con: sqlite3.Connection, tables: dict) -> Path:
    q = cfg["quote"]
    out = [
        f"-- AUTO-GENERATED by scripts/generate-northwind.py — do not edit.",
        f"-- Bootstraps the Northwind test database for {dialect} integration tests.",
        "-- Assumes the target database already exists and is the connected database.",
        f"-- Usage: {cfg['usage']}",
        "",
    ]
    out.extend(cfg["preamble"])
    if cfg["preamble"]:
        out.append("")

    for t in reversed(TABLE_ORDER):
        out.append(cfg["drop"].format(t=f"{q}{t}{q}"))
    out.append("")

    col_meta: dict[str, list[tuple[str, str]]] = {}
    for t in TABLE_ORDER:
        cols = [(r[1], r[2]) for r in con.execute(f'PRAGMA table_info("{t}")')]
        col_meta[t] = cols
        out.append(translate_schema(tables[t], t, cfg) + ";")
        out.extend(bridge_columns(t, cols, cfg))
        out.append("")

    for t in TABLE_ORDER:
        rows = con.execute(f'SELECT * FROM "{t}"').fetchall()
        if not rows:
            continue
        cols = col_meta[t]
        col_list = ", ".join(f"{q}{c}{q}" for c, _ in cols)
        for i in range(0, len(rows), 100):
            chunk = rows[i: i + 100]
            values = ",\n".join(
                "("
                + ", ".join(
                    sql_literal(v, cols[j][1], dialect) for j, v in enumerate(row)
                )
                + ")"
                for row in chunk
            )
            out.append(f"INSERT INTO {q}{t}{q} ({col_list}) VALUES\n{values};")
        out.append("")

    out.extend(cfg["postamble"])
    if cfg["postamble"]:
        out.append("")

    dst = ROOT.joinpath(*cfg["path"])
    dst.parent.mkdir(parents=True, exist_ok=True)
    dst.write_text("\n".join(out), encoding="utf-8", newline="\n")
    return dst


def main() -> None:
    con = sqlite3.connect(str(SRC))
    con.text_factory = lambda b: b.decode("utf-8", "replace")
    tables = {
        name: sql
        for name, sql in con.execute(
            "SELECT name, sql FROM sqlite_master WHERE type='table'"
        )
    }
    missing = [t for t in TABLE_ORDER if t not in tables]
    if missing:
        raise SystemExit(f"tables missing from SQLite db: {missing}")

    for dialect, cfg in DIALECTS.items():
        dst = build(dialect, cfg, con, tables)
        print(f"wrote {dst} ({dst.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
