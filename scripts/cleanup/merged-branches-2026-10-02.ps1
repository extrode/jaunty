# Deletes every local branch already merged into dev, in this repo and in ../jaunty-audit.
# On 2026-10-02 that was 154 branches here and 4 there. The list is computed at run time with
# `git branch --merged dev`, so the dry run shows exactly what -e would delete.
#
# Never touched: dev, main, and the branch currently checked out. Local branches only; nothing on
# origin is deleted.
#
# Dry run by default. --execute / -e acts. Branches go with `git branch -d` only, so an unmerged
# branch, or one checked out in a worktree, is refused by git, never forced.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$Execute = $false

foreach ($a in $args) {
  switch ($a) {
    '--execute' { $Execute = $true }
    '-e'        { $Execute = $true }
    default {
      [Console]::Error.WriteLine("unknown flag: $a")
      [Console]::Error.WriteLine('usage: merged-branches-2026-10-02.ps1 [--execute|-e]')
      exit 2
    }
  }
}

$repo = (git rev-parse --show-toplevel)
if (-not $repo) { Write-Error 'Not inside a git repository.'; exit 1 }
Set-Location $repo
$Failed = $false

$branch = (git rev-parse --abbrev-ref HEAD)
if ($branch -ne 'dev') { Write-Error "Run from dev (on '$branch')."; exit 1 }

if ($Execute) { Write-Host '=== EXECUTING ===' } else { Write-Host '=== DRY RUN (pass --execute to act) ===' }
Write-Host ''

function Remove-MergedBranches([string]$gitDir) {
  $current = (git -C $gitDir rev-parse --abbrev-ref HEAD)
  $names = @(git -C $gitDir branch --merged dev --format='%(refname:short)' |
    Where-Object { $_ -and $_ -ne 'dev' -and $_ -ne 'main' -and $_ -ne $current })
  if ($names.Count -eq 0) { Write-Host '  nothing merged to delete'; return }
  $deleted = 0
  foreach ($b in $names) {
    if ($Execute) {
      git -C $gitDir branch -d $b
      if ($LASTEXITCODE -ne 0) { Write-Host "  refused: $b. Left alone."; $script:Failed = $true; continue }
    }
    Write-Host "  branch: $b"
    $deleted++
  }
  Write-Host "  total: $deleted"
}

# --- 1. This repo ---------------------------------------------------------------
Write-Host '--- 1. merged branches in this repo (git branch -d)'
Remove-MergedBranches $repo
Write-Host ''

# --- 2. ../jaunty-audit ----------------------------------------------------------
Write-Host '--- 2. merged branches in ../jaunty-audit (git branch -d)'
$audit = Join-Path (Split-Path $repo -Parent) 'jaunty-audit'
if (-not (Test-Path (Join-Path $audit '.git'))) {
  Write-Host "  skip: $audit is not a git repository"
} elseif ((git -C $audit rev-parse --abbrev-ref HEAD) -ne 'dev') {
  Write-Host "  refused: $audit is not on dev. Left alone."
  $Failed = $true
} else {
  Remove-MergedBranches $audit
}
Write-Host ''

Write-Host '--- remaining branches'
git branch --format='  %(refname:short)'
if ($Failed) { exit 1 }
