# Cleanup for the 2026-07-29 BULK INSERT ROWTERMINATOR probe.
#
# Settling whether SQL Server expands the '\n' escape differently on Windows and Linux needed the
# same two CSVs read server-side by both. That left artefacts in three places outside the repo:
# a staging directory the Windows service account could read, the same files inside the
# torture-mssql container, and a dbo.term_probe table in master on each server.
#
# Dry run is the default. Removing the probe tables is irreversible, so it needs -DropTables on
# top of -Execute: two flags, never one.

[CmdletBinding()]
param(
    [Alias('e')]
    [switch]$Execute,

    [switch]$DropTables
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repo

$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'dev' -and $branch -notlike 'docs/*' -and $branch -notlike 'chore/*' -and $branch -notlike 'fix/*') {
    Write-Error "Refusing to run on branch '$branch'; expected dev, docs/*, chore/* or fix/*."
}

$mode = if ($Execute -and $DropTables) { 'EXECUTE + DROP TABLES' }
        elseif ($Execute) { 'EXECUTE (files only - add -DropTables)' }
        else { 'DRY RUN' }
Write-Host "== BULK INSERT terminator probe ($mode)"

# --- 1. Windows staging directory ------------------------------------------------------------
Write-Host "`n== 1. C:\ProgramData\jaunty-term"
$stage = 'C:\ProgramData\jaunty-term'
if (Test-Path $stage) {
    Write-Host "   remove $stage (crlf.csv, lf.csv)"
    if ($Execute) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
else {
    Write-Host '   not present'
}

# --- 2. Files inside the container -------------------------------------------------------------
Write-Host "`n== 2. /tmp/term inside torture-mssql"
$running = (docker ps --filter 'name=torture-mssql' --format '{{.Names}}' 2>$null | Out-String).Trim()
if (-not $running) {
    Write-Host '   torture-mssql not running; nothing to do'
}
else {
    Write-Host '   remove /tmp/term (crlf.csv, lf.csv, probe.sql)'
    if ($Execute) {
        docker exec torture-mssql rm -rf /tmp/term | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Error 'Failed to remove /tmp/term in torture-mssql.' }
    }
}

# --- 3. Probe tables ---------------------------------------------------------------------------
# dbo.term_probe in master, on both servers. Nothing else reads it.
Write-Host "`n== 3. dbo.term_probe in master"
$sql = "IF OBJECT_ID('dbo.term_probe') IS NOT NULL DROP TABLE dbo.term_probe;"
$sqlPath = Join-Path $env:TEMP 'jaunty-term-probe-cleanup.sql'

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Host '   sqlcmd not on PATH; skipping the local Windows instance'
}
else {
    Write-Host '   local Windows instance: remove dbo.term_probe from master'
    if ($Execute -and $DropTables) {
        Set-Content -LiteralPath $sqlPath -Value $sql -Encoding UTF8
        & sqlcmd -S lpc:localhost -E -C -b -d master -i $sqlPath | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Error 'Failed to remove dbo.term_probe on the local instance.' }
        Remove-Item -LiteralPath $sqlPath -Force
    }
}

if ($running) {
    Write-Host '   torture-mssql: remove dbo.term_probe from master'
    if ($Execute -and $DropTables) {
        docker exec torture-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Torture_Test_Pwd1!' -C -b -d master -Q $sql | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Error 'Failed to remove dbo.term_probe in torture-mssql.' }
    }
}

# --- 4. Result ---------------------------------------------------------------------------------
Write-Host "`n== 4. Result"
Write-Host '   The Northwind test databases were never touched by the probe.'
if (-not $Execute) {
    Write-Host '   Dry run: nothing was changed. Re-run with -Execute (and -DropTables for the tables).'
}
elseif (-not $DropTables) {
    Write-Host '   Probe tables left in place. Add -DropTables to remove them.'
}
