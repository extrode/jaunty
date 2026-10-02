# Cleanup for the Fable review fixes and the generated naming resolvers, written 2026-10-02: the
# fix/fable-review-r38-survivors and feat/generated-naming-resolvers branches merged into dev, and
# this cleanup branch itself.
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
      [Console]::Error.WriteLine('usage: naming-resolver-branches-2026-10-02.ps1 [--execute|-e]')
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

# --- 1. Merged branches (git branch -d) ------------------------------
Write-Host '--- 1. merged branches (git branch -d)'
$branches = @(
  'fix/fable-review-r38-survivors',
  'feat/generated-naming-resolvers',
  'chore/cleanup-naming-resolver-branches'
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
git branch --list 'fix/fable-review-r38-survivors' 'feat/generated-naming-resolvers' 'chore/cleanup-naming-resolver-branches'
if ($Execute -and $Failed) { exit 1 }
