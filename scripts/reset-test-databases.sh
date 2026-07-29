#!/usr/bin/env bash
#
# Reset every test database back to its seeded baseline.
#
# Bash port of reset-test-databases.ps1, for machines without PowerShell (macOS,
# Linux). Same containers, same scripts, same order, same dry-run-by-default
# contract. Keep the two in step.
#
# The integration tests mutate what they run against: DialectFixture creates
# bulk_test / csv_import_test tables, the write tests insert and update Northwind
# rows, and the SQLite fixture file is edited in place. Run this after any test run
# so the next one starts from a known state.
#
# Every seed script drops and recreates its tables, so this is idempotent.
#
#   Dry run (default):  ./scripts/reset-test-databases.sh
#   Execute:            ./scripts/reset-test-databases.sh --execute
#
# Not ported from the .ps1: step 5, which reseeds a locally installed Windows SQL
# Server instance. It depends on Windows service lookup and integrated auth, and
# has no meaning here. Containers are steps 2-4 and are covered.

set -uo pipefail

EXECUTE=0
for arg in "$@"; do
  case "$arg" in
    --execute|-e) EXECUTE=1 ;;
    -h|--help) sed -n '2,25p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "unknown argument: $arg" >&2; exit 2 ;;
  esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PW='Torture_Test_Pwd1!'
FAILED=0

# Safety gate - the steps below drop databases, so refuse to run anywhere else.
if [[ ! -f "$REPO_ROOT/Jaunty.slnx" ]]; then
  echo "Jaunty.slnx not found under $REPO_ROOT - refusing to run outside the jaunty repo." >&2
  exit 1
fi

step()  { printf '\n== %s\n' "$1"; }
would() { if [[ "$EXECUTE" == 1 ]]; then printf '   %s\n' "$1"; else printf '   [dry run] %s\n' "$1"; fi; }
fail()  { printf '   FAILED: %s\n' "$1" >&2; FAILED=1; }
skip()  { printf '   %s\n' "$1"; }

running() { [[ -n "$(docker ps --filter "name=^$1$" --format '{{.Names}}' 2>/dev/null)" ]]; }

# --- 1. SQLite fixture ------------------------------------------------------
step "1. data/sqlite/Northwind.db (edited in place by the tests)"
if [[ -n "$(git -C "$REPO_ROOT" status --porcelain -- data/sqlite/Northwind.db)" ]]; then
  would "git checkout -- data/sqlite/Northwind.db"
  if [[ "$EXECUTE" == 1 ]]; then
    git -C "$REPO_ROOT" checkout -- data/sqlite/Northwind.db || fail "sqlite fixture"
  fi
else
  skip "already clean"
fi

# --- 2. SQL Server container ------------------------------------------------
# No arm64 image exists for mcr.microsoft.com/mssql/server, so on Apple silicon
# this container only runs under Rosetta emulation and is often simply absent.
step "2. torture-mssql (Northwind)"
if running torture-mssql; then
  would "drop database Northwind (recreated by the seed script)"
  if [[ "$EXECUTE" == 1 ]]; then
    # -b: without it sqlcmd exits 0 even when the statement fails.
    docker exec torture-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$PW" -C -b \
      -Q "IF DB_ID('Northwind') IS NOT NULL BEGIN ALTER DATABASE Northwind SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE Northwind; END" \
      >/dev/null || fail "torture-mssql: drop database"
  fi
  for f in data/sqlserver/create-northwind.sql data/sqlserver/create-stored-procedures.sql; do
    would "sqlcmd < $f"
    if [[ "$EXECUTE" == 1 ]]; then
      db=()
      [[ "$f" == *stored-procedures* ]] && db=(-d Northwind)
      docker cp "$REPO_ROOT/$f" torture-mssql:/tmp/r.sql >/dev/null
      docker exec torture-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$PW" -C -b \
        "${db[@]}" -i /tmp/r.sql >/dev/null || fail "torture-mssql: $f"
    fi
  done
else
  skip "not running - skipped"
fi

# --- 3. PostgreSQL container ------------------------------------------------
step "3. torture-postgres (northwind)"
if running torture-postgres; then
  would "drop and recreate database northwind"
  if [[ "$EXECUTE" == 1 ]]; then
    docker exec -e PGPASSWORD="$PW" torture-postgres psql -U postgres -d postgres -q \
      -c 'DROP DATABASE IF EXISTS northwind WITH (FORCE)' -c 'CREATE DATABASE northwind' \
      >/dev/null || fail "torture-postgres: recreate database"
  fi
  for f in data/postgres/create-northwind.sql data/postgres/create-stored-procedures.sql; do
    would "psql -d northwind -f $f"
    if [[ "$EXECUTE" == 1 ]]; then
      docker cp "$REPO_ROOT/$f" torture-postgres:/tmp/r.sql >/dev/null
      docker exec -e PGPASSWORD="$PW" torture-postgres \
        psql -U postgres -d northwind -v ON_ERROR_STOP=1 -q -f /tmp/r.sql \
        >/dev/null || fail "torture-postgres: $f"
    fi
  done
else
  skip "not running - skipped"
fi

# --- 4. MySQL and MariaDB containers ----------------------------------------
for pair in "torture-mysql mysql" "torture-mariadb mariadb"; do
  set -- $pair
  name="$1"; client="$2"
  step "4. $name (northwind)"
  if running "$name"; then
    would "drop and recreate database northwind"
    if [[ "$EXECUTE" == 1 ]]; then
      # MYSQL_PWD rather than -p: the client warns on stderr about a command-line
      # password, which is noise at best and a failed step at worst.
      docker exec -e MYSQL_PWD="$PW" "$name" "$client" -u root \
        -e 'DROP DATABASE IF EXISTS northwind; CREATE DATABASE northwind;' \
        >/dev/null || fail "$name: recreate database"
    fi
    for f in data/mysql/create-northwind.sql data/mysql/create-stored-procedures.sql; do
      would "$client northwind < $f"
      if [[ "$EXECUTE" == 1 ]]; then
        docker cp "$REPO_ROOT/$f" "$name:/tmp/r.sql" >/dev/null
        docker exec -e MYSQL_PWD="$PW" "$name" sh -c "$client -u root northwind < /tmp/r.sql" \
          >/dev/null || fail "$name: $f"
      fi
    done
  else
    skip "not running - skipped"
  fi
done

# --- 5. Result --------------------------------------------------------------
step "5. Result"
if [[ "$EXECUTE" != 1 ]]; then
  skip "Dry run only. Re-run with --execute to apply."
  exit 0
fi
if [[ "$FAILED" != 0 ]]; then
  skip "One or more resets failed - see above."
  exit 1
fi
skip "All reachable test databases reset to baseline."
