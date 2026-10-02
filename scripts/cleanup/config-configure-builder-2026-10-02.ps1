# Cleanup for the JauntyConfig.Configure builder change (F3 with R), written 2026-10-02: the
# feat/config-configure-builder branch merged into dev, and the scratch projects it left in tmp/.
#
# Dry run by default. --execute / -e acts. The branch goes with `git branch -d` only, so an
# unmerged branch is refused by git, never forced. Scratch paths are removed only when git does not
# track them.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$Execute = $false

foreach ($a in $args) {
  switch ($a) {
    '--execute' { $Execute = $true }
    '-e'        { $Execute = $true }
    default {
      [Console]::Error.WriteLine("unknown flag: $a")
      [Console]::Error.WriteLine('usage: config-configure-builder-2026-10-02.ps1 [--execute|-e]')
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

# --- 1. Merged branch (git branch -d) -----------------------------------------
Write-Host '--- 1. merged branch (git branch -d)'
foreach ($b in @('feat/config-configure-builder')) {
  git rev-parse --verify --quiet "refs/heads/$b" *> $null
  if ($LASTEXITCODE -ne 0) { Write-Host "  skip: $b no longer exists"; continue }
  if ($Execute) {
    git branch -d $b
    if ($LASTEXITCODE -ne 0) { Write-Host "  refused: $b not merged. Left alone."; $Failed = $true; continue }
  }
  Write-Host "  branch: $b"
}
Write-Host ''

# --- 2. Scratch projects in tmp/ ----------------------------------------------
Write-Host '--- 2. scratch projects in tmp/'
foreach ($rel in @('tmp/guide-check', 'tmp/sqlite-probe', 'tmp/aotsmoke-pub')) {
  if (-not (Test-Path $rel)) { Write-Host "  skip: $rel no longer exists"; continue }
  git ls-files --error-unmatch -- $rel *> $null
  if ($LASTEXITCODE -eq 0) {
    Write-Host "  refuse: $rel is tracked by git. Propose it as a commit instead. Left alone."
    $Failed = $true
    continue
  }
  if ($Execute) { Remove-Item -Recurse -Force -- $rel }
  Write-Host "  remove: $rel"
}
Write-Host ''

Write-Host '--- remaining state'
git branch --list 'feat/config-configure-builder'
foreach ($rel in @('tmp/guide-check', 'tmp/sqlite-probe', 'tmp/aotsmoke-pub')) { if (Test-Path $rel) { Write-Host "  still present: $rel" } }
if ($Execute -and $Failed) { exit 1 }
