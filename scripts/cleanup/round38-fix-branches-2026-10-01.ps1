# Cleanup for the round 38 fix sweep (AUD-R38-001..147), written 2026-10-01: the fix/r38-* and
# docs/r38-* branches merged into dev in this repo, this cleanup branch itself, and the
# docs/r38-resolutions branch merged into dev in the jaunty-audit repo.
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
      [Console]::Error.WriteLine('usage: round38-fix-branches-2026-10-01.ps1 [--execute|-e]')
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

function Remove-MergedBranch([string]$gitDir, [string]$b) {
  git -C $gitDir rev-parse --verify --quiet "refs/heads/$b" *> $null
  if ($LASTEXITCODE -ne 0) { Write-Host "  absent: $b"; return }
  git -C $gitDir merge-base --is-ancestor "refs/heads/$b" dev
  if ($LASTEXITCODE -ne 0) {
    Write-Host "  refuse: $b has commits not in dev. Left alone."
    $script:Failed = $true
    return
  }
  if ($Execute) {
    git -C $gitDir branch -d $b
    if ($LASTEXITCODE -ne 0) { $script:Failed = $true }
  }
  Write-Host "  branch: $b"
}

# --- 1. Merged round 38 branches in this repo --------------------------------
Write-Host '--- 1. jaunty: merged fix/r38-*, docs/r38-* and this cleanup branch (git branch -d)'
$branches = @(git for-each-ref --format='%(refname:short)' 'refs/heads/fix/r38-*' 'refs/heads/docs/r38-*')
$branches += 'chore/cleanup-r38-branches'
foreach ($b in $branches) { Remove-MergedBranch $repo $b }
Write-Host ''

# --- 2. jaunty-audit registry branch ------------------------------------------
Write-Host '--- 2. jaunty-audit: docs/r38-resolutions (git branch -d)'
$audit = Join-Path (Split-Path $repo -Parent) 'jaunty-audit'
if (-not (Test-Path -LiteralPath (Join-Path $audit '.git'))) {
  Write-Host "  skip: no jaunty-audit repo at $audit"
} elseif ((git -C $audit rev-parse --abbrev-ref HEAD) -ne 'dev') {
  Write-Host '  refuse: jaunty-audit is not on dev. Left alone.'
  $Failed = $true
} else {
  Remove-MergedBranch $audit 'docs/r38-resolutions'
}
Write-Host ''

Write-Host '--- remaining state'
git branch --list 'fix/r38-*' 'docs/r38-*' 'chore/cleanup-r38-*'
if ($Failed) { exit 1 }
