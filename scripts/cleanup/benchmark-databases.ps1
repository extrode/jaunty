# Cleanup for the 2026-07-29 pre-GA benchmark run.
#
# benchmarks/Jaunty.Benchmarks creates its own database per provider (DatabaseSetup.
# EnsureDatabaseExists) and fills it with a benchmark_products table. The run was pointed at a
# dedicated jauntybench / JauntyBench so the Northwind test databases were never touched — these
# are the leftovers of that choice.
#
# Dry run is the default. Dropping a database is irreversible, so it needs -DropDatabases on top
# of -Execute: two flags, never one.

[CmdletBinding()]
param(
    [Alias('e')]
    [switch]$Execute,

    [switch]$DropDatabases
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repo

# --- Safety gate -----------------------------------------------------------------------------
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'dev' -and $branch -notlike 'docs/*' -and $branch -notlike 'chore/*' -and $branch -notlike 'fix/*') {
    Write-Error "Refusing to run on branch '$branch'; expected dev, docs/*, chore/* or fix/*."
}

$mode = if ($Execute -and $DropDatabases) { 'EXECUTE + DROP' }
        elseif ($Execute) { 'EXECUTE (no drops — add -DropDatabases)' }
        else { 'DRY RUN' }
Write-Host "== benchmark databases ($mode) — $repo"

$pwd_ = 'Torture_Test_Pwd1!'
$drop = $Execute -and $DropDatabases

# --- 1. Container databases ------------------------------------------------------------------
Write-Host "`n== 1. jauntybench in the docker-compose containers"

foreach ($t in @(
    @{ Name = 'torture-postgres'; Check = "docker exec torture-postgres psql -U postgres -tAc `"select datname from pg_database where datname='jauntybench'`"" },
    @{ Name = 'torture-mariadb';  Check = "docker exec torture-mariadb mariadb -uroot -p$pwd_ -N -e `"show databases like 'jauntybench'`"" }
)) {
    $present = $false
    try { $present = -not [string]::IsNullOrWhiteSpace((Invoke-Expression $t.Check 2>$null | Out-String).Trim()) }
    catch { Write-Host "   $($t.Name): unreachable; skipping"; continue }

    if (-not $present) { Write-Host "   $($t.Name): jauntybench not present"; continue }

    Write-Host "   $($t.Name): drop database jauntybench"
    if ($drop) {
        if ($t.Name -eq 'torture-postgres') {
            docker exec torture-postgres psql -U postgres -c 'DROP DATABASE IF EXISTS jauntybench' | Out-Null
        }
        else {
            docker exec torture-mariadb mariadb -uroot -p$pwd_ -e 'DROP DATABASE IF EXISTS jauntybench' | Out-Null
        }
        if ($LASTEXITCODE -ne 0) { Write-Error "Failed to drop jauntybench on $($t.Name)." }
    }
}

# --- 2. Local SQL Server ---------------------------------------------------------------------
# Your own Northwind is never touched. Only the benchmark database the run created.
Write-Host "`n== 2. JauntyBench on the local SQL Server"
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    Write-Host '   sqlcmd not on PATH; skipping'
}
else {
    $exists = (& sqlcmd -S localhost -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = 'JauntyBench'" 2>&1 | Out-String).Trim()
    if ($exists -ne '1') {
        Write-Host '   JauntyBench not present'
    }
    else {
        Write-Host '   drop database JauntyBench'
        if ($drop) {
            & sqlcmd -S localhost -E -C -b -Q "ALTER DATABASE [JauntyBench] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [JauntyBench];" | Out-Null
            if ($LASTEXITCODE -ne 0) { Write-Error 'Failed to drop JauntyBench.' }
        }
    }
}

# --- 3. BenchmarkDotNet artifacts ------------------------------------------------------------
# benchmarks/results/ is tracked in git and is the record of past runs. Not touched here.
Write-Host "`n== 3. BenchmarkDotNet scratch"
$artifacts = Join-Path $repo 'benchmarks/Jaunty.Benchmarks/BenchmarkDotNet.Artifacts'
if (Test-Path $artifacts) {
    Write-Host "   remove $artifacts"
    if ($Execute) { Remove-Item -LiteralPath $artifacts -Recurse -Force }
}
else {
    Write-Host '   no BenchmarkDotNet.Artifacts directory'
}
Write-Host '   benchmarks/results/ is tracked in git — NOT touched'

# --- 4. Result -------------------------------------------------------------------------------
Write-Host "`n== 4. Result"
if (-not $Execute) {
    Write-Host '   Dry run: nothing was changed. Re-run with -Execute (and -DropDatabases to drop).'
}
elseif (-not $DropDatabases) {
    Write-Host '   Databases left in place. Add -DropDatabases to remove them.'
}
