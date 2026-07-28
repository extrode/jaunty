# Cleanup for the "seed Northwind for all four dialects" work (2026-07-29).
#
# Removes the docker-compose database stack that was brought up to run the
# dialect integration tests, and the northwind databases seeded inside it.
# Written by the session that did the seeding; never run automatically.
#
# Dry run (default):  .\scripts\cleanup\northwind-seed-all-dialects.ps1
# Execute:            .\scripts\cleanup\northwind-seed-all-dialects.ps1 --execute
# Also drop volumes:  .\scripts\cleanup\northwind-seed-all-dialects.ps1 --execute --delete-volumes

[CmdletBinding()]
param(
    [Alias('e')][switch]$Execute,
    [switch]$DeleteVolumes
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$compose  = Join-Path $repoRoot 'docker-compose.yml'
$containers = @('torture-mssql', 'torture-postgres', 'torture-mysql', 'torture-mariadb')

function Step($text) { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }
function Would($text) { if ($Execute) { Write-Host "   $text" } else { Write-Host "   [dry run] $text" -ForegroundColor Yellow } }

# --- Safety gate ------------------------------------------------------------
if (-not (Test-Path $compose)) {
    Write-Error "docker-compose.yml not found at $compose - refusing to run outside the jaunty repo."
    exit 1
}
if ($DeleteVolumes -and -not $Execute) {
    Write-Host "--delete-volumes has no effect without --execute; this is still a dry run." -ForegroundColor Yellow
}

# --- 1. What is running -----------------------------------------------------
Step "1. Containers this work started"
foreach ($c in $containers) {
    $status = docker ps -a --filter "name=^$c$" --format '{{.Status}}' 2>$null
    if ($status) { Write-Host "   $c - $status" } else { Write-Host "   $c - not present" -ForegroundColor DarkGray }
}

# --- 2. Stop and remove the stack ------------------------------------------
Step "2. Stop and remove the compose stack"
Would "docker compose -f `"$compose`" down"
if ($Execute) {
    docker compose -f $compose down
    if ($LASTEXITCODE -ne 0) { Write-Error "docker compose down failed."; exit 1 }
}

# --- 3. Volumes (opt-in, destroys the seeded data) --------------------------
Step "3. Named volumes (holds the seeded northwind data)"
$volumes = @('jaunty_mssql_data', 'jaunty_postgres_data', 'jaunty_mysql_data', 'jaunty_mariadb_data')
if ($DeleteVolumes) {
    foreach ($v in $volumes) {
        Would "docker volume rm $v"
        if ($Execute) { docker volume rm $v 2>$null | Out-Null }
    }
} else {
    Write-Host "   kept - pass --delete-volumes to remove:" -ForegroundColor DarkGray
    $volumes | ForEach-Object { Write-Host "     $_" -ForegroundColor DarkGray }
}

# --- 4. Local SQL Server database -------------------------------------------
Step "4. NorthwindJaunty on the local SQL Server instance"
Write-Host "   Created to run the CsvImport BULK INSERT tests, which need a server" -ForegroundColor DarkGray
Write-Host "   sharing a filesystem with the test host. Separate from your own" -ForegroundColor DarkGray
Write-Host "   Northwind database, which was left alone." -ForegroundColor DarkGray
if ($DeleteVolumes) {
    Would "sqlcmd -S lpc:localhost -E -C -Q `"DROP DATABASE NorthwindJaunty`""
    if ($Execute) {
        & sqlcmd -S 'lpc:localhost' -E -C -Q 'DROP DATABASE NorthwindJaunty'
    }
} else {
    Write-Host "   kept - pass --delete-volumes to drop it too." -ForegroundColor DarkGray
}

# --- 5. Not removed ---------------------------------------------------------
Step "5. Deliberately NOT removed"
Write-Host "   aud-r64-ms / aud-r64-my / aud-r64-pg - pre-existing containers from an" -ForegroundColor DarkGray
Write-Host "     earlier audit round; this work neither created nor used them." -ForegroundColor DarkGray
Write-Host "   data/postgres/create-northwind.sql, data/mysql/create-northwind.sql," -ForegroundColor DarkGray
Write-Host "     scripts/generate-northwind.py - committed deliverables, not scratch." -ForegroundColor DarkGray

# --- 6. Remaining state -----------------------------------------------------
Step "6. Remaining state"
docker ps -a --filter "name=torture-" --format '   {{.Names}}\t{{.Status}}'
if (-not $Execute) {
    Write-Host ""
    Write-Host "Dry run only. Re-run with --execute to apply." -ForegroundColor Yellow
}
exit 0
