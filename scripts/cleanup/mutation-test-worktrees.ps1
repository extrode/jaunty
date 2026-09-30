# Produced 2026-09-30 by the mutation-score test push (test/mutants-* branches and their audits).
# Removes the worktrees that work created, once their commits are reachable from dev, plus the
# untracked audit scratch folders that would make `git worktree remove` refuse.
#
#   pwsh -NoProfile scripts/cleanup/mutation-test-worktrees.ps1                    dry run
#   pwsh -NoProfile scripts/cleanup/mutation-test-worktrees.ps1 -e                 remove worktrees
#   pwsh -NoProfile scripts/cleanup/mutation-test-worktrees.ps1 -e -DeleteScratch  also delete untracked Scratch folders
#
# Branches are never deleted here.

[CmdletBinding()]
param(
    [Alias('e')][switch]$Execute,
    [switch]$DeleteScratch
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$repo = (git rev-parse --show-toplevel).Trim()
$failed = $false

$branch = (git -C $repo branch --show-current).Trim()
if ($branch -ne 'dev') {
    Write-Host "refuse: on '$branch', expected dev."
    if ($Execute) { exit 1 } else { exit 0 }
}

$trees = @(
    'test-mutants-sqlserver',
    'test-mutants-mysql-sqlite',
    'test-mutants-postgres-misc',
    'test-mutants-scaffolding',
    'test-mutants-scaffolding-fixes',
    'test-mutants-sourcegenerator',
    'test-mutants-duckdb-tests',
    'scaffolding-trial',
    'verify-duckdb-tests',
    'verify-tests'
)

$scratch = @(
    '.worktrees/test-mutants-duckdb-tests/tests/Extrode.Jaunty.FlatFiles.DuckDB.Tests/Scratch',
    '.worktrees/test-mutants-duckdb-tests/tests/Extrode.Jaunty.Tests/Scratch'
)

Write-Host '== 1. untracked audit scratch folders'
foreach ($rel in $scratch) {
    $path = Join-Path $repo $rel
    if (-not (Test-Path -LiteralPath $path)) { Write-Host "skip:  $rel no longer exists"; continue }
    $inner = $rel -replace '^\.worktrees/[^/]+/', ''
    $wt = Join-Path $repo ($rel -replace '^(\.worktrees/[^/]+)/.*$', '$1')
    git -C $wt ls-files --error-unmatch -- $inner > $null 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "refuse: $rel is tracked by git. Propose it as a commit instead. Left alone."
        $failed = $true
        continue
    }
    if (-not $DeleteScratch) { Write-Host "would delete (needs -DeleteScratch): $rel"; continue }
    if ($Execute) { Remove-Item -LiteralPath $path -Recurse -Force; Write-Host "deleted: $rel" }
    else { Write-Host "would delete: $rel" }
}

Write-Host '== 2. worktrees whose commits are reachable from dev'
foreach ($name in $trees) {
    $rel = ".worktrees/$name"
    $path = Join-Path $repo $rel
    if (-not (Test-Path -LiteralPath $path)) { Write-Host "skip:  $rel no longer exists"; continue }
    $head = (git -C $path rev-parse HEAD).Trim()
    git -C $repo merge-base --is-ancestor $head dev
    if ($LASTEXITCODE -ne 0) {
        Write-Host "refuse: $rel has commits ($($head.Substring(0,8))) not in dev. Left alone."
        $failed = $true
        continue
    }
    if (-not $Execute) { Write-Host "would remove: $rel"; continue }
    git -C $repo worktree remove $path
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  refused (dirty or locked). Left alone."
        $failed = $true
    } else {
        Write-Host "removed: $rel"
    }
}

if ($Execute) { git -C $repo worktree prune }

Write-Host '== remaining'
git -C $repo worktree list

if ($Execute -and $failed) { exit 1 }
