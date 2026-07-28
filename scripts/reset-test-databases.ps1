# Reset every test database back to its seeded baseline.
#
# The integration tests mutate what they run against: DialectFixture creates
# bulk_test / csv_import_test tables, the write tests insert and update
# Northwind rows, and the SQLite fixture file is edited in place. Run this
# after any test run so the next one starts from a known state.
#
# Every seed script drops and recreates its tables, so this is idempotent.
#
# Dry run (default):  .\scripts\reset-test-databases.ps1
# Execute:            .\scripts\reset-test-databases.ps1 --execute
# Skip the local instance: .\scripts\reset-test-databases.ps1 --execute -SkipLocal

[CmdletBinding()]
param(
    [Alias('e')][switch]$Execute,
    [switch]$SkipLocal
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$pw       = 'Torture_Test_Pwd1!'
$failed   = $false

function Step($t) { Write-Host ""; Write-Host "== $t" -ForegroundColor Cyan }
function Would($t) { if ($Execute) { Write-Host "   $t" } else { Write-Host "   [dry run] $t" -ForegroundColor Yellow } }
function Fail($t) { Write-Host "   FAILED: $t" -ForegroundColor Red; $script:failed = $true }

# --- Safety gate ------------------------------------------------------------
if (-not (Test-Path (Join-Path $repoRoot 'Jaunty.slnx'))) {
    Write-Error "Jaunty.slnx not found under $repoRoot - refusing to run outside the jaunty repo."
    exit 1
}

function Running($name) {
    return [bool](docker ps --filter "name=^$name$" --format '{{.Names}}' 2>$null)
}

# The seed scripts only recreate the 13 Northwind tables. The tests also create
# their own (bulk_*, csv_import_test, execute_test, get_test, scaffold_test_*),
# so a reset that only re-seeds leaves those behind. Drop the whole database
# first - these are disposable test databases, rebuilt entirely by the seeds.

# --- 1. SQLite fixture ------------------------------------------------------
Step "1. data/sqlite/Northwind.db (edited in place by the tests)"
Push-Location $repoRoot
try {
    $dirty = git status --porcelain -- 'data/sqlite/Northwind.db'
    if ($dirty) {
        Would "git checkout -- data/sqlite/Northwind.db"
        if ($Execute) { git checkout -- 'data/sqlite/Northwind.db' }
    } else {
        Write-Host "   already clean" -ForegroundColor DarkGray
    }
} finally { Pop-Location }

# --- 2. SQL Server container ------------------------------------------------
Step "2. torture-mssql (Northwind)"
if (Running 'torture-mssql') {
    Would "drop database Northwind (recreated by the seed script)"
    if ($Execute) {
        $d = "IF DB_ID('Northwind') IS NOT NULL BEGIN ALTER DATABASE Northwind SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE Northwind; END"
        docker exec torture-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $pw -C -Q $d | Out-Null
    }
    foreach ($f in @('data/sqlserver/create-northwind.sql', 'data/sqlserver/create-stored-procedures.sql')) {
        $db = if ($f -match 'stored-procedures') { @('-d', 'Northwind') } else { @() }
        Would "sqlcmd < $f"
        if ($Execute) {
            docker cp (Join-Path $repoRoot $f) "torture-mssql:/tmp/r.sql" | Out-Null
            docker exec torture-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $pw -C @db -i /tmp/r.sql | Out-Null
            if ($LASTEXITCODE -ne 0) { Fail "torture-mssql: $f" }
        }
    }
} else { Write-Host "   not running - skipped" -ForegroundColor DarkGray }

# --- 3. PostgreSQL container ------------------------------------------------
Step "3. torture-postgres (northwind)"
if (Running 'torture-postgres') {
    Would "drop and recreate database northwind"
    if ($Execute) {
        docker exec -e PGPASSWORD=$pw torture-postgres psql -U postgres -d postgres -q `
            -c 'DROP DATABASE IF EXISTS northwind WITH (FORCE)' -c 'CREATE DATABASE northwind' | Out-Null
        if ($LASTEXITCODE -ne 0) { Fail "torture-postgres: recreate database" }
    }
    foreach ($f in @('data/postgres/create-northwind.sql', 'data/postgres/create-stored-procedures.sql')) {
        Would "psql -d northwind -f $f"
        if ($Execute) {
            docker cp (Join-Path $repoRoot $f) "torture-postgres:/tmp/r.sql" | Out-Null
            docker exec -e PGPASSWORD=$pw torture-postgres psql -U postgres -d northwind -v ON_ERROR_STOP=1 -q -f /tmp/r.sql | Out-Null
            if ($LASTEXITCODE -ne 0) { Fail "torture-postgres: $f" }
        }
    }
} else { Write-Host "   not running - skipped" -ForegroundColor DarkGray }

# --- 4. MySQL and MariaDB containers ----------------------------------------
foreach ($c in @(@('torture-mysql', 'mysql'), @('torture-mariadb', 'mariadb'))) {
    $name = $c[0]; $client = $c[1]
    Step "4. $name (northwind)"
    if (Running $name) {
        Would "drop and recreate database northwind"
        if ($Execute) {
            docker exec -e MYSQL_PWD=$pw $name $client -u root `
                -e 'DROP DATABASE IF EXISTS northwind; CREATE DATABASE northwind;' | Out-Null
            if ($LASTEXITCODE -ne 0) { Fail "${name}: recreate database" }
        }
        foreach ($f in @('data/mysql/create-northwind.sql', 'data/mysql/create-stored-procedures.sql')) {
            Would "$client northwind < $f"
            if ($Execute) {
                docker cp (Join-Path $repoRoot $f) "${name}:/tmp/r.sql" | Out-Null
                # MYSQL_PWD rather than -p: the client warns on stderr about a
                # command-line password, and that trips $ErrorActionPreference.
                docker exec -e MYSQL_PWD=$pw $name sh -c "$client -u root northwind < /tmp/r.sql" | Out-Null
                if ($LASTEXITCODE -ne 0) { Fail "${name}: $f" }
            }
        }
    } else { Write-Host "   not running - skipped" -ForegroundColor DarkGray }
}

# --- 5. Local SQL Server (NorthwindJaunty only) -----------------------------
Step "5. local SQL Server (NorthwindJaunty)"
Write-Host "   Your own Northwind database is never touched by this script." -ForegroundColor DarkGray
if ($SkipLocal) {
    Write-Host "   -SkipLocal given - skipped" -ForegroundColor DarkGray
} else {
    $sqlcmd = Get-ChildItem 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\*\Tools\Binn\sqlcmd.exe' -ErrorAction SilentlyContinue |
        Select-Object -Last 1 -ExpandProperty FullName
    $svc = Get-Service MSSQLSERVER -ErrorAction SilentlyContinue
    if (-not $sqlcmd -or -not $svc -or $svc.Status -ne 'Running') {
        Write-Host "   local instance not available - skipped" -ForegroundColor DarkGray
    } else {
        # The vendored script hardcodes the database name; rewrite it to the
        # Jaunty-owned copy so the user's own Northwind is never the target.
        $tmp = Join-Path ([IO.Path]::GetTempPath()) 'jaunty-reset-northwindjaunty.sql'
        Would "seed NorthwindJaunty from data/sqlserver/create-northwind.sql"
        if ($Execute) {
            $d = "IF DB_ID('NorthwindJaunty') IS NOT NULL BEGIN ALTER DATABASE NorthwindJaunty SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE NorthwindJaunty; END"
            & $sqlcmd -S 'lpc:localhost' -E -C -Q $d | Out-Null
            (Get-Content (Join-Path $repoRoot 'data/sqlserver/create-northwind.sql') -Raw).Replace('Northwind', 'NorthwindJaunty') |
                Set-Content $tmp -NoNewline -Encoding UTF8
            & $sqlcmd -S 'lpc:localhost' -E -C -i $tmp | Out-Null
            if ($LASTEXITCODE -ne 0) { Fail "NorthwindJaunty schema" }
            & $sqlcmd -S 'lpc:localhost' -E -C -d NorthwindJaunty -i (Join-Path $repoRoot 'data/sqlserver/create-stored-procedures.sql') | Out-Null
            if ($LASTEXITCODE -ne 0) { Fail "NorthwindJaunty stored procedures" }
            Remove-Item $tmp -ErrorAction SilentlyContinue
        }
    }
}

# --- 6. Result --------------------------------------------------------------
Step "6. Result"
if (-not $Execute) {
    Write-Host "   Dry run only. Re-run with --execute to apply." -ForegroundColor Yellow
    exit 0
}
if ($failed) { Write-Host "   One or more resets failed - see above." -ForegroundColor Red; exit 1 }
Write-Host "   All reachable test databases reset to baseline." -ForegroundColor Green
exit 0
