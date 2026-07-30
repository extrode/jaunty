# net10-langversion-work.ps1
#
# Written 2026-07-29 by the SDK-divergence / LangVersion-pin / .NET 10 spec work.
# Removes the merged branches that body of work produced. Nothing else.
#
# Dry run (default):  .\scripts\cleanup\net10-langversion-work.ps1
# Execute:            .\scripts\cleanup\net10-langversion-work.ps1 --execute
# Also drop worktree: .\scripts\cleanup\net10-langversion-work.ps1 --execute --delete-worktrees
#
# DELIBERATELY NOT REMOVED: probe/net10-feasibility. docs/specs/010-net10-migration/010-spec.md
# section 8 cites it as the evidence record for every measurement in section 2; deleting it makes
# the spec unreproducible. It is listed at the end as retained, not as a candidate.

[CmdletBinding()]
param(
    [Alias('e')]
    [switch]$Execute,

    # Second flag, required on top of --execute: removing a worktree also discards any
    # uncommitted state inside it, so it does not ride along with the branch deletions.
    [switch]$DeleteWorktrees
)

$ErrorActionPreference = 'Stop'

# Anchor on the script's own location so it works from any working directory. Without this the
# git calls below run wherever the caller happened to be, and the safety gate checks a different
# repository than the one being cleaned.
Set-Location (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))

# --- Safety gate -------------------------------------------------------------------------------

$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'dev') {
    Write-Host "Refusing to run: expected to be on 'dev', found '$branch'." -ForegroundColor Red
    if ($Execute) { exit 1 }
    Write-Host 'Dry run only. Re-run from dev.'
    exit 0
}

$repo = (git rev-parse --show-toplevel).Trim()
if (-not $repo.EndsWith('jaunty')) {
    Write-Host "Refusing to run: unexpected repository root '$repo'." -ForegroundColor Red
    exit 1
}

# --- 1. Merged branches from this body of work -------------------------------------------------

$candidates = @(
    'fix/net472-contains-overload',      # CS1501 at the call sites
    'fix/net472-contains-langversion',   # same break, second pass
    'chore/ci-dotnet-10',                # 10.0.x added to the three setup-dotnet blocks
    'chore/pin-langversion',             # root Directory.Build.props + src/ import chain
    'fix/langversion-13',                # pin corrected 14.0 -> 13.0 after the C# 14 bisect
    'docs/spec-010-net10',               # the .NET 10 migration spec
    'fix/csharp14-span-contains',        # MemoryExtensions.Contains translation
    'docs/spec-010-corrections',         # spec 010 factual corrections + Q1
    'docs/todo-net10-followups',         # follow-ups recorded in work/todo.md
    'fix/cleanup-script-cwd'             # this script, anchored on its own location
)

Write-Host ''
Write-Host '1. Merged branches from the .NET 10 / LangVersion work' -ForegroundColor Cyan

foreach ($candidate in $candidates) {
    $exists = git show-ref --verify --quiet "refs/heads/$candidate"; $found = $LASTEXITCODE -eq 0
    if (-not $found) {
        Write-Host "   skip   $candidate (does not exist)"
        continue
    }

    $merged = (git branch --merged dev --format='%(refname:short)') -contains $candidate
    if (-not $merged) {
        Write-Host "   KEEP   $candidate (not merged into dev)" -ForegroundColor Yellow
        continue
    }

    if ($Execute) {
        git branch -d $candidate | Out-Null    # -d, never -D: refuses anything unmerged
        Write-Host "   deleted $candidate" -ForegroundColor Green
    }
    else {
        Write-Host "   would delete $candidate"
    }
}

# --- 2. Measurement worktree -------------------------------------------------------------------

Write-Host ''
Write-Host '2. Measurement worktree' -ForegroundColor Cyan

$worktree = '.worktrees/net10-measure'
if (Test-Path $worktree) {
    if ($Execute -and $DeleteWorktrees) {
        git worktree remove $worktree --force | Out-Null
        git branch -D probe/net10-measurements | Out-Null   # -D: a probe branch is never merged
        Write-Host "   removed $worktree and probe/net10-measurements" -ForegroundColor Green
    }
    else {
        Write-Host "   would remove $worktree and branch probe/net10-measurements"
        Write-Host '   (needs --execute AND --delete-worktrees; holds the net10 retarget + publish output)'
    }
}
else {
    Write-Host "   $worktree not present"
}

# --- 3. Retained on purpose --------------------------------------------------------------------

Write-Host ''
Write-Host '3. Retained on purpose' -ForegroundColor Cyan
Write-Host '   probe/net10-feasibility  - evidence record for spec 010 section 2; do not delete'

# --- 4. Remaining state ------------------------------------------------------------------------

Write-Host ''
Write-Host '4. Remaining state' -ForegroundColor Cyan
$remaining = @(git branch --format='%(refname:short)').Count
Write-Host "   branches in repo: $remaining"
Write-Host "   HEAD: $(git rev-parse --short HEAD) on $branch"

Write-Host ''
if (-not $Execute) {
    Write-Host 'Dry run only. Re-run with --execute to apply.'
}
