# Cleanup for the round 38 mutation-survivor sweep, written 2026-10-02: the test/*-r38-survivor(s)
# branches and fix/mutate-ci-env merged into dev, and this cleanup branch itself.
#
# Dry run by default. --execute / -e acts. Branches go with `git branch -d` only, so an unmerged
# branch is refused by git, never forced. Nothing here touches files.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$Execute = $false

foreach ($a in $args) {
  switch ($a) {
    '--execute' { $Execute = $true }
    '-e'        { $Execute = $true }
    default {
      [Console]::Error.WriteLine("unknown flag: $a")
      [Console]::Error.WriteLine('usage: round38-mutation-survivor-branches-2026-10-02.ps1 [--execute|-e]')
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

# --- 1. Merged survivor branches (git branch -d) ------------------------------
Write-Host '--- 1. merged mutation-survivor branches (git branch -d)'
$branches = @(
  'test/unittests-r38-survivors',
  'fix/mutate-ci-env',
  'test/scaffolding-r38-survivor',
  'test/sourcegen-r38-survivors',
  'test/duckdb-r38-survivors',
  'test/jaunty-tests-r38-survivors',
  'chore/cleanup-r38-survivor-branches'
)
foreach ($b in $branches) {
  git rev-parse --verify --quiet "refs/heads/$b" *> $null
  if ($LASTEXITCODE -ne 0) { Write-Host "  skip: $b no longer exists"; continue }
  if ($Execute) {
    git branch -d $b
    if ($LASTEXITCODE -ne 0) { Write-Host "  refused: $b not merged. Left alone."; $Failed = $true; continue }
  }
  Write-Host "  branch: $b"
}
Write-Host ''

Write-Host '--- remaining state'
git branch --list 'test/*r38*' 'fix/mutate-ci-env' 'chore/cleanup-r38-survivor-*'
if ($Execute -and $Failed) { exit 1 }
