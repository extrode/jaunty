# Cleanup for the 2026-10-01 afternoon session: the flake, JAUNTYGEN004 and generator-shape fixes,
# the docs-site regeneration, and the round 38 audit sweep. Covers the merged fix/docs branches and
# the tmp/ scratch that was still in use when this was written (mb1 job10 bundle and log, round 38
# dispatcher output). Earlier scratch from the same day is in mutation-coverage-round-2026-10-01.ps1.
#
# Dry run by default. --execute / -e acts. Branches go with `git branch -d` only, so an unmerged
# branch is refused by git, never forced. Round 38 worker output is kept until the round's findings
# are registered; --drop-round38-scratch is needed on top of --execute to remove it.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$Execute   = $false
$DropRound = $false

foreach ($a in $args) {
  switch ($a) {
    '--execute'              { $Execute = $true }
    '-e'                     { $Execute = $true }
    '--drop-round38-scratch' { $DropRound = $true }
    default {
      [Console]::Error.WriteLine("unknown flag: $a")
      [Console]::Error.WriteLine('usage: fixes-and-round38-2026-10-01.ps1 [--execute|-e] [--drop-round38-scratch]')
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

# --- 1. Merged branches ------------------------------------------------------
Write-Host '--- 1. merged branches (git branch -d)'
$branches = @(
  'fix/configuration-generation-test-race',
  'fix/residual-parallel-flake',
  'fix/jaunty-gen-004-wording',
  'fix/generator-entity-shapes',
  'docs/regenerate-docs-site',
  'chore/cleanup-fixes-and-round38'
)
foreach ($b in $branches) {
  git rev-parse --verify --quiet "refs/heads/$b" *> $null
  if ($LASTEXITCODE -ne 0) { Write-Host "  absent: $b"; continue }
  if ($Execute) {
    git branch -d $b
    if ($LASTEXITCODE -ne 0) { $Failed = $true }
  }
  Write-Host "  branch: $b"
}
Write-Host ''

# --- 2. tmp/ scratch ----------------------------------------------------------
Write-Host '--- 2. tmp/ scratch (gitignored, confirmed untracked)'
$scratch = @('tmp/dev10.bundle', 'tmp/mb1-job10.sh', 'tmp/job10-ut.out', 'tmp/ut4.json')
if ($DropRound) {
  $scratch += @(Get-ChildItem -Path tmp -Filter 'r38-*' -File | ForEach-Object { "tmp/$($_.Name)" })
} else {
  Write-Host '  kept: tmp/r38-* (pass --drop-round38-scratch once round 38 is registered)'
}
foreach ($rel in $scratch) {
  if (-not (Test-Path -LiteralPath $rel)) { continue }
  git ls-files --error-unmatch -- $rel *> $null
  if ($LASTEXITCODE -eq 0) {
    Write-Host "  refuse: $rel is tracked by git. Propose it as a commit instead. Left alone."
    $Failed = $true
    continue
  }
  if ($Execute) { Remove-Item -LiteralPath $rel -Force }
  Write-Host "  scratch: $rel"
}
Write-Host ''

Write-Host '--- remaining state'
git branch --list 'fix/*' 'docs/*' 'chore/cleanup-*'
if ($Failed) { exit 1 }
