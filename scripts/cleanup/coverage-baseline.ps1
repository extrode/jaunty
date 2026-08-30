# Cleanup for the testing-strategy close-out (work of 2026-08-30).
#
# Produced while closing the last three open items in
# docs/plans/2026-08-27-003-testing-strategy-implementation.md: the EXISTS visitor's dispatcher
# coverage, the streaming lifecycle's SqlServer variants, and the fuzz harness's first real run.
#
# Only the merged branch is listed. The scratch this work created under the project's own tmp/
# (tmp/fuzz-0830, tmp/coverage, tmp/exists-coverage.py, tmp/ExistsExpressionVisitor.cs.bak) is
# gitignored and belongs to the work that made it, so it is not this script's business.
#
# Written, never run. Bare invocation is the dry run; -e / -Execute acts.
#
#   pwsh -NoProfile scripts/cleanup/coverage-baseline.ps1
#   pwsh -NoProfile scripts/cleanup/coverage-baseline.ps1 -e

[CmdletBinding()]
param(
    [Alias('e')][switch]$Execute
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

# --- Gate: describes the world this script acts on, not its own success -------------------------
$target = 'test/coverage-baseline'
$branch = (git rev-parse --abbrev-ref HEAD).Trim()

if ($branch -eq $target) {
    Write-Host "Refusing: you are standing on the branch this script deletes. Switch to dev first." -ForegroundColor Red
    exit 1
}

# --- 1. The merged branch -----------------------------------------------------------------------
Section "1. branch $target"

if (-not (git rev-parse --verify --quiet "refs/heads/$target")) {
    Write-Host "   skip:  branch $target no longer exists"
}
else {
    Step "git branch -d $target" {
        # -d, never -D: it refuses anything not merged, which is the check we want to keep.
        git branch -d $target
        if ($LASTEXITCODE -ne 0) {
            Write-Host '  refused: not merged. Left alone.'
            $script:failed = $true
        }
    }
}

# --- 2. Remaining state -------------------------------------------------------------------------
Section "2. Result"

if (-not $Execute) {
    Write-Host "   Dry run only. Re-run with --execute to apply." -ForegroundColor Yellow
    exit 0
}

Write-Host "   branches now:"
git branch --list | ForEach-Object { Write-Host "     $_" }

if ($script:failed) {
    Write-Host "   One or more items were refused and left alone." -ForegroundColor Yellow
    exit 1
}

Write-Host "   Nothing left to do."
