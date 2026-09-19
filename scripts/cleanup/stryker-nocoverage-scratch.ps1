# Cleanup for the 2026-09-19 Stryker NoCoverage gap-closing session: local tmp/
# scratch from the analysis pass, and the mb1.local benchmark checkouts renamed
# aside during the scope-fix verification runs. Merged-branch cleanup is not here
# - scripts/cleanup/merged-branch-sweep.ps1 already sweeps them by re-scanning
# `git branch --merged dev` at run time, so a second script naming the same two
# branches would just be a stale duplicate the moment either script runs.
#
# Dry run by default. --execute / -e acts. Removing the mb1 remote directories is
# irreversible (they are old benchmark checkouts, not a git repo mb1 tracks a
# remote for) and needs --remove-remote-backups on top of --execute.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$Execute             = $false
$RemoveRemoteBackups = $false

foreach ($a in $args) {
  switch ($a) {
    '--execute'                { $Execute = $true }
    '-e'                       { $Execute = $true }
    '--remove-remote-backups'  { $RemoveRemoteBackups = $true }
    default {
      [Console]::Error.WriteLine("unknown flag: $a")
      [Console]::Error.WriteLine('usage: stryker-nocoverage-scratch.ps1 [--execute|-e] [--remove-remote-backups]')
      exit 2
    }
  }
}

$repo = (git rev-parse --show-toplevel)
if (-not $repo) { Write-Error 'Not inside a git repository.'; exit 1 }
Set-Location $repo
$Failed = $false

if ($Execute) { Write-Host '=== EXECUTING ===' } else { Write-Host '=== DRY RUN (pass --execute to act) ===' }
Write-Host ''

# --- 1. Local tmp/ scratch --------------------------------------------------
# tmp/ is gitignored at this repo's root; every path below is confirmed untracked
# before it is touched, not assumed from this list.
Write-Host '1. Local tmp/ scratch'
$tmpTargets = @(
  'tmp/jaunty-dev-2026-09-19.bundle',
  'tmp/jaunty-dev-2026-09-19b.bundle',
  'tmp/nocoverage-mutants.json',
  'tmp/nocoverage-tests-prompt.txt'
)
foreach ($rel in $tmpTargets) {
  if (-not (Test-Path $rel)) { Write-Host "   skip: $rel (already gone)"; continue }
  git ls-files --error-unmatch -- $rel > $null 2>&1
  if ($LASTEXITCODE -eq 0) {
    Write-Host "   refuse: $rel is tracked by git. Propose it as a commit instead. Left alone."
    $Failed = $true
    continue
  }
  $len = (Get-Item $rel).Length
  Write-Host "   $rel  ($len bytes)"
  if ($Execute) { Remove-Item -Force -- $rel }
}
Write-Host ''

# --- 2. mb1.local stale benchmark checkouts ---------------------------------
# Renamed aside rather than deleted while the scope-fix run was verified against
# a fresh clone. Existence is checked live over SSH, not assumed.
Write-Host '2. mb1.local stale benchmark checkouts (--remove-remote-backups)'
$remoteDirs = @(
  '~/jaunty-bench-pre-rewrite-2026-09-19',
  '~/jaunty-bench-pre-scope-fix-2026-09-19'
)
foreach ($dir in $remoteDirs) {
  $exists = & ssh mb1.local "test -d $dir && echo yes || echo no"
  if ($exists -ne 'yes') { Write-Host "   skip: $dir (already gone)"; continue }
  Write-Host "   $dir"
  if ($Execute -and $RemoveRemoteBackups) {
    & ssh mb1.local "rm -rf -- $dir"
    if ($LASTEXITCODE -ne 0) { $Failed = $true }
  } elseif ($Execute) {
    Write-Host '   needs --remove-remote-backups as well; left alone'
  }
}
Write-Host ''

if (-not $Execute) {
  Write-Host 'Dry run: nothing was changed.'
  exit 0
}
if ($Failed) {
  Write-Host 'Finished with errors: see the skipped or refused steps above.'
  exit 1
}
Write-Host 'Done.'
exit 0
