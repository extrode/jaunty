# docker-testcontainer-leak.ps1
#
# Written 2026-07-30 after vmmemWSL was found holding 15.7GB with 136 running containers, 133 of
# them Testcontainers left behind by CI runs on the self-hosted runner in the Debian WSL distro.
# The oldest had been up 18 hours. Reaps those, and nothing else.
#
# Dry run (default):   pwsh -NoProfile -File .\scripts\cleanup\docker-testcontainer-leak.ps1
# Remove stopped:      pwsh -NoProfile -File .\scripts\cleanup\docker-testcontainer-leak.ps1 --execute
# Also kill running:   pwsh -NoProfile -File .\scripts\cleanup\docker-testcontainer-leak.ps1 --execute --kill-running
#
# DELIBERATELY NOT REMOVED: torture-mysql, torture-mariadb, torture-postgres. Those are named
# fixtures, not leaks, and the seeded-baseline policy depends on them surviving. The filter is the
# org.testcontainers label, never age or image name, precisely so those three can never be caught.
#
# Volumes and images are NOT touched. A leaked container's anonymous volume is reclaimed with it;
# pulling images again costs bandwidth for no benefit here.

[CmdletBinding()]
param(
    [Alias('e')]
    [switch]$Execute,

    # Second flag, required on top of --execute: a running container may belong to a CI job that is
    # mid-test, and killing it fails that job rather than merely tidying after it.
    [switch]$KillRunning,

    # The safety gate below refuses to touch anything while a CI job is executing. This overrides it.
    [switch]$IgnoreActiveCi
)

$ErrorActionPreference = 'Stop'

$Label = 'label=org.testcontainers'

function Get-Containers([string]$State) {
    $out = docker ps --all --filter $Label --filter "status=$State" --format '{{.ID}}|{{.Image}}|{{.Names}}|{{.Status}}'
    if ($LASTEXITCODE -ne 0) { throw "docker ps failed. Is Docker Desktop running?" }
    $out | Where-Object { $_ } | ForEach-Object {
        $f = $_ -split '\|'
        [pscustomobject]@{ Id = $f[0]; Image = $f[1]; Name = $f[2]; Status = $f[3] }
    }
}

# --- Safety gate -------------------------------------------------------------------------------

$null = Get-Command docker -ErrorAction Stop

$activeCi = $null
try {
    $activeCi = wsl.exe -d Debian -- bash -lc "pgrep -a Runner.Worker 2>/dev/null | head -3"
}
catch {
    # WSL not reachable is not a reason to refuse; the docker CLI check above already gates that.
}

if ($activeCi) {
    Write-Host 'A CI job is executing on the self-hosted runner right now:' -ForegroundColor Yellow
    $activeCi | ForEach-Object { Write-Host "   $_" }
    Write-Host 'Containers it created are indistinguishable from leaked ones by label alone.' -ForegroundColor Yellow
    if (-not $IgnoreActiveCi) {
        Write-Host 'Refusing to remove anything. Wait for the job, or pass --ignore-active-ci.' -ForegroundColor Red
        if ($Execute) { exit 1 }
    }
    else {
        Write-Host 'Proceeding anyway (--ignore-active-ci). The running job will very likely fail.' -ForegroundColor Red
    }
}

# --- 1. What is there --------------------------------------------------------------------------

$running = @(Get-Containers 'running')
$stopped = @(Get-Containers 'exited') + @(Get-Containers 'created')

Write-Host ''
Write-Host '1. Leaked Testcontainers' -ForegroundColor Cyan
Write-Host "   running: $($running.Count)"
Write-Host "   stopped: $($stopped.Count)"

if ($running.Count -gt 0) {
    Write-Host '   by image (running):'
    $running | Group-Object Image | Sort-Object Count -Descending | ForEach-Object {
        Write-Host ("      {0,4}  {1}" -f $_.Count, $_.Name)
    }
}

# --- 2. Stopped containers ---------------------------------------------------------------------

Write-Host ''
Write-Host '2. Stopped containers' -ForegroundColor Cyan

if ($stopped.Count -eq 0) {
    Write-Host '   none'
}
elseif ($Execute -and (-not $activeCi -or $IgnoreActiveCi)) {
    $stopped | ForEach-Object { docker rm $_.Id | Out-Null }
    Write-Host "   removed $($stopped.Count)" -ForegroundColor Green
}
else {
    Write-Host "   would remove $($stopped.Count)"
}

# --- 3. Running containers ---------------------------------------------------------------------

Write-Host ''
Write-Host '3. Running containers' -ForegroundColor Cyan

if ($running.Count -eq 0) {
    Write-Host '   none'
}
elseif ($Execute -and $KillRunning -and (-not $activeCi -or $IgnoreActiveCi)) {
    $running | ForEach-Object { docker rm --force $_.Id | Out-Null }
    Write-Host "   killed and removed $($running.Count)" -ForegroundColor Green
}
else {
    Write-Host "   would kill and remove $($running.Count)"
    Write-Host '   (needs --execute AND --kill-running)'
}

# --- 4. Retained on purpose --------------------------------------------------------------------

Write-Host ''
Write-Host '4. Retained on purpose' -ForegroundColor Cyan
docker ps --all --format '{{.Names}}|{{.Status}}' |
    Where-Object { $_ -like 'torture-*' } |
    ForEach-Object {
        $f = $_ -split '\|'
        Write-Host ("   {0,-20} {1}  - named fixture, never a leak" -f $f[0], $f[1])
    }

# --- 5. Remaining state ------------------------------------------------------------------------

Write-Host ''
Write-Host '5. Remaining state' -ForegroundColor Cyan
Write-Host "   containers running: $((docker ps -q | Measure-Object).Count)"
Write-Host "   containers total:   $((docker ps -aq | Measure-Object).Count)"

Write-Host ''
if (-not $Execute) {
    Write-Host 'Dry run only. Re-run with --execute to remove stopped containers.'
    Write-Host 'Add --kill-running to also remove the running ones.'
}
else {
    Write-Host 'The leak itself is unfixed: CI will refill this within a run or two.'
    Write-Host 'See work/todo.md - the container lifecycle in the test suite is the actual defect.'
}
