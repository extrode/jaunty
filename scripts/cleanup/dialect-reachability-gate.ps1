# Cleanup for the dialect reachability gate (work of 2026-08-30).
#
# Produced while adding the reachability probe and JAUNTY_REQUIRE_<ENGINE> switch to the dialect
# test attributes. Everything here is scratch that work created: one throwaway worktree used to
# prove the 477 net472 failures in the main tree are environmental rather than a regression, its
# temporary merge commit, the branch itself once merged, and the trx dumps under tmp/.
#
# Written, never run. Bare invocation is the dry run; -e / -Execute acts.
# Deleting the trx dumps needs the second flag as well, since they are untracked.
#
#   pwsh -NoProfile scripts/cleanup/dialect-reachability-gate.ps1
#   pwsh -NoProfile scripts/cleanup/dialect-reachability-gate.ps1 -e

[CmdletBinding()]
param(
    [Alias('e')][switch]$Execute,
    [switch]$DeleteScratchFiles
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repoRoot

$script:failed = $false

function Step($text, [scriptblock]$action) {
    if ($Execute) {
        Write-Host "   $text"
        & $action
    }
    else {
        Write-Host "   [dry run] $text" -ForegroundColor Yellow
    }
}

function Section($n) { Write-Host "`n== $n" -ForegroundColor Cyan }

# --- Gate: describes the world this script is about to act on, not its own success -------------
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -eq 'test/dialect-reachability-gate') {
    Write-Host "Refusing: you are standing on the branch this script deletes. Switch to dev first." -ForegroundColor Red
    exit 1
}

# --- 1. The throwaway worktree ------------------------------------------------------------------
Section "1. worktree .worktrees/net472-baseline"

$worktree = Join-Path $repoRoot '.worktrees/net472-baseline'
if (-not (Test-Path $worktree)) {
    Write-Host "   skip:  already gone"
}
else {
    Step "git worktree remove .worktrees/net472-baseline" {
        # Refuses a dirty tree, which is the behaviour we want. It holds an untracked
        # appsettings.json written by hand and a temporary merge commit on a detached HEAD,
        # so expect a refusal unless --force is added by a human who has looked.
        git worktree remove .worktrees/net472-baseline
        if ($LASTEXITCODE -ne 0) {
            Write-Host '  refused: tree is dirty (untracked appsettings.json). Left alone.'
            $script:failed = $true
        }
    }
}

Step "git worktree prune" { git worktree prune }

# --- 2. The branch, once it is merged -----------------------------------------------------------
Section "2. branch test/dialect-reachability-gate"

$target = 'test/dialect-reachability-gate'
if (-not (git rev-parse --verify --quiet "refs/heads/$target")) {
    Write-Host "   skip:  branch $target no longer exists"
}
else {
    Step "git branch -d $target" {
        git branch -d $target
        if ($LASTEXITCODE -ne 0) {
            Write-Host '  refused: not merged. Left alone.'
            $script:failed = $true
        }
    }
}

# --- 3. Scratch trx dumps (second flag) ---------------------------------------------------------
Section "3. tmp/trx dumps"

$trx = Join-Path $repoRoot 'tmp/trx'
if (-not (Test-Path $trx)) {
    Write-Host "   skip:  already gone"
}
elseif (-not $DeleteScratchFiles) {
    Write-Host "   held:  tmp/trx exists. Pass -DeleteScratchFiles to remove it." -ForegroundColor Yellow
}
else {
    Step "remove tmp/trx" { Remove-Item -Recurse -Force $trx }
}

# --- 4. Remaining state -------------------------------------------------------------------------
Section "4. Result"

if (-not $Execute) {
    Write-Host "   Dry run only. Re-run with --execute to apply." -ForegroundColor Yellow
    exit 0
}

Write-Host "   worktrees now:"
git worktree list | ForEach-Object { Write-Host "     $_" }

if ($script:failed) {
    Write-Host "   One or more items were refused and left alone." -ForegroundColor Yellow
    exit 1
}

Write-Host "   Nothing left to do."
