# Repair the local SQL Server "Northwind" database (2026-07-29).
#
# On 2026-07-29 06:46 a Jaunty seed script was pointed at the user's own Northwind
# database instead of a Jaunty-owned copy. Nothing was destroyed - every change was
# additive - but the database no longer matches what it was. This undoes exactly the
# objects that run created, identified by create_date, not by guesswork:
#
#   * 47 computed snake_case bridge columns added to 7 pre-existing tables
#   * 4 tables created outright (Region, Territories, CustomerDemographics,
#     CustomerCustomerDemo) with their 4 primary keys and 3 foreign keys
#   * 9 stored procedures (GetAllProducts, GetProductsByCategory, GetProductById,
#     GetProductCount, GetProductCountByCategory, GetProductCountWithOutput,
#     GetProductCountWithReturnValue, GetNoResults, UpdateProductPrice)
#
# Deliberately NOT touched:
#   * the 8 original tables and all their real columns and rows
#   * the 8 original stored procedures (create_date 2026-03-12)
#   * bulk_test and csv_import_test - test debris, but from 2026-03-23, so they
#     predate this incident and are not this script's to remove. Pass
#     -RemoveOlderTestTables if you want them gone too.
#
# A verified backup was taken before any of this:
#   C:\home\syed\databases\mssql\backups\Northwind-pre-repair-2026-07-29.bak
#
# Dry run (default):   .\scripts\cleanup\northwind-local-repair.ps1
# Execute:             .\scripts\cleanup\northwind-local-repair.ps1 --execute --drop-objects
# Also drop old debris:.\scripts\cleanup\northwind-local-repair.ps1 --execute --drop-objects -RemoveOlderTestTables

[CmdletBinding()]
param(
    [Alias('e')][switch]$Execute,
    [Alias('drop-objects')][switch]$DropObjects,
    [switch]$RemoveOlderTestTables,
    [string]$Server = 'lpc:localhost',
    [string]$Database = 'Northwind'
)

$ErrorActionPreference = 'Stop'

# Objects created by the 2026-07-29 06:46 run. Everything else is off limits.
$Cutoff = '2026-07-29 00:00:00'

$sqlcmd = Get-ChildItem 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\*\Tools\Binn\sqlcmd.exe' -ErrorAction SilentlyContinue |
    Select-Object -Last 1 -ExpandProperty FullName

function Step($t) { Write-Host ""; Write-Host "== $t" -ForegroundColor Cyan }
function Would($t) { if ($Execute -and $DropObjects) { Write-Host "   $t" } else { Write-Host "   [dry run] $t" -ForegroundColor Yellow } }

# -b matters: without it sqlcmd exits 0 even when the statement failed, so a
# DROP blocked by an index or constraint would be swallowed and the script would
# still print its success footer.
function Invoke-Sql([string]$query) {
    $out = & $sqlcmd -S $Server -E -C -b -h -1 -W -s '|' -d $Database -Q "SET NOCOUNT ON; $query" 2>&1
    if ($LASTEXITCODE -ne 0) { Write-Error (($out -join "`n")) }
    return $out
}

# Exactly what the 2026-07-29 06:46 run created. Anything outside these lists is
# left alone and reported, rather than trusted to a date comparison alone.
$ExpectedComputedColumns = 47
$ExpectedTables = @('CustomerCustomerDemo', 'CustomerDemographics', 'Territories', 'Region')
$ExpectedProcs = @(
    'GetAllProducts', 'GetProductsByCategory', 'GetProductById', 'GetProductCount',
    'GetProductCountByCategory', 'GetProductCountWithOutput', 'GetProductCountWithReturnValue',
    'GetNoResults', 'UpdateProductPrice'
)

# --- Safety gate ------------------------------------------------------------
if (-not $sqlcmd) { Write-Error "sqlcmd.exe not found - cannot continue."; exit 1 }

$svc = Get-Service MSSQLSERVER -ErrorAction SilentlyContinue
if (-not $svc -or $svc.Status -ne 'Running') { Write-Error "Local MSSQLSERVER instance is not running."; exit 1 }

if ($Database -ne 'Northwind') { Write-Error "This script only repairs Northwind; got '$Database'."; exit 1 }

$backup = 'C:\home\syed\databases\mssql\backups\Northwind-pre-repair-2026-07-29.bak'
if (-not (Test-Path $backup)) {
    Write-Host "Pre-repair backup not found at:" -ForegroundColor Red
    Write-Host "  $backup" -ForegroundColor Red
    if ($Execute -and $DropObjects) { Write-Error "Refusing to drop anything without the backup."; exit 1 }
    Write-Host "(dry run continues, but --execute would refuse)" -ForegroundColor Yellow
} else {
    Write-Host "Backup present: $backup" -ForegroundColor DarkGray
}

if ($Execute -and -not $DropObjects) {
    Write-Host ""
    Write-Host "--execute given without --drop-objects. Dropping columns, tables and" -ForegroundColor Yellow
    Write-Host "procedures is irreversible, so it needs the second flag. Still a dry run." -ForegroundColor Yellow
}
$live = $Execute -and $DropObjects

# --- 1. What is there now ---------------------------------------------------
Step "1. Current state"
Invoke-Sql "SELECT 'tables=' + CAST(COUNT(*) AS varchar(10)) FROM sys.tables;
            SELECT 'computed columns=' + CAST(COUNT(*) AS varchar(10)) FROM sys.columns WHERE is_computed = 1;
            SELECT 'procedures=' + CAST(COUNT(*) AS varchar(10)) FROM sys.procedures;" |
    ForEach-Object { if ($_ -match '=') { Write-Host "   $_" } }

# --- 2. Computed bridge columns on pre-existing tables ----------------------
Step "2. Computed snake_case columns added to pre-existing tables"
$cols = Invoke-Sql "SELECT QUOTENAME(t.name) + '|' + QUOTENAME(c.name)
                    FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id
                    WHERE c.is_computed = 1 AND t.create_date < '$Cutoff'
                    ORDER BY t.name, c.name;" | Where-Object { $_ -match '\|' }
if (-not $cols) { Write-Host "   none - already repaired" -ForegroundColor DarkGray }
# sys.columns carries no creation date, so this selects on the TABLE's date and
# would catch any pre-existing computed column too. Stock Northwind has none, so
# the count is the check: bail rather than drop a column that is not ours.
if ($cols -and $cols.Count -ne $ExpectedComputedColumns) {
    Write-Host "   Expected $ExpectedComputedColumns computed column(s), found $($cols.Count)." -ForegroundColor Red
    Write-Host "   Refusing to drop - a computed column here may not be from the 2026-07-29 run." -ForegroundColor Red
    exit 1
}
foreach ($row in $cols) {
    $t, $c = $row -split '\|', 2
    Would "ALTER TABLE $t DROP COLUMN $c"
    if ($live) { Invoke-Sql "ALTER TABLE $t DROP COLUMN $c;" | Out-Null }
}
Write-Host "   $($cols.Count) column(s)" -ForegroundColor DarkGray

# --- 3. Tables created by that run ------------------------------------------
Step "3. Tables created on 2026-07-29 (with their PKs and FKs)"
# Dropped child-first so the foreign keys go with them.
$order = $ExpectedTables
$new = Invoke-Sql "SELECT name FROM sys.tables WHERE create_date >= '$Cutoff';" | Where-Object { $_ -and $_.Trim() }
$new = $new | ForEach-Object { $_.Trim() }
foreach ($t in $order) {
    if ($new -notcontains $t) { continue }
    Would "DROP TABLE [$t]"
    if ($live) { Invoke-Sql "DROP TABLE [$t];" | Out-Null }
}
$unexpected = $new | Where-Object { $order -notcontains $_ }
if ($unexpected) {
    Write-Host "   NOT dropped - created today but not on the known list:" -ForegroundColor Yellow
    $unexpected | ForEach-Object { Write-Host "     $_" -ForegroundColor Yellow }
}

# --- 4. Stored procedures created by that run -------------------------------
Step "4. Stored procedures created on 2026-07-29"
$procs = Invoke-Sql "SELECT name FROM sys.procedures WHERE create_date >= '$Cutoff' ORDER BY name;" |
    Where-Object { $_ -and $_.Trim() } | ForEach-Object { $_.Trim() }
if (-not $procs) { Write-Host "   none - already repaired" -ForegroundColor DarkGray }
# The cutoff is midnight but the incident was 06:46, so a date test alone would
# also catch anything else created that morning. Drop only the known nine.
foreach ($p in $procs) {
    if ($ExpectedProcs -notcontains $p) {
        Write-Host "   NOT dropped - created today but not on the known list: $p" -ForegroundColor Yellow
        continue
    }
    Would "DROP PROCEDURE [$p]"
    if ($live) { Invoke-Sql "DROP PROCEDURE [$p];" | Out-Null }
}

# --- 5. Older test debris (opt-in) ------------------------------------------
Step "5. Test tables from 2026-03-23 (bulk_test, csv_import_test)"
if ($RemoveOlderTestTables) {
    foreach ($t in @('bulk_test', 'csv_import_test')) {
        Would "DROP TABLE [$t]"
        if ($live) { Invoke-Sql "IF OBJECT_ID('$t') IS NOT NULL DROP TABLE [$t];" | Out-Null }
    }
} else {
    Write-Host "   kept - they predate this incident. Pass -RemoveOlderTestTables to drop." -ForegroundColor DarkGray
}

# --- 6. Result --------------------------------------------------------------
Step "6. Result"
Invoke-Sql "SELECT 'tables=' + CAST(COUNT(*) AS varchar(10)) FROM sys.tables;
            SELECT 'computed columns=' + CAST(COUNT(*) AS varchar(10)) FROM sys.columns WHERE is_computed = 1;
            SELECT 'procedures=' + CAST(COUNT(*) AS varchar(10)) FROM sys.procedures;
            SELECT 'rows: Customers=' + CAST((SELECT COUNT(*) FROM Customers) AS varchar(10))
                 + ' Orders=' + CAST((SELECT COUNT(*) FROM Orders) AS varchar(10))
                 + ' [Order Details]=' + CAST((SELECT COUNT(*) FROM [Order Details]) AS varchar(10))
                 + ' Products=' + CAST((SELECT COUNT(*) FROM Products) AS varchar(10));" |
    ForEach-Object { if ($_ -match '=') { Write-Host "   $_" } }

if (-not $live) {
    Write-Host ""
    Write-Host "Dry run only. Re-run with --execute --drop-objects to apply." -ForegroundColor Yellow
    exit 0
}
Write-Host ""
Write-Host "Repair applied. Restore the backup if anything looks wrong:" -ForegroundColor Green
Write-Host "  RESTORE DATABASE Northwind FROM DISK = N'$backup' WITH REPLACE;" -ForegroundColor DarkGray
exit 0
