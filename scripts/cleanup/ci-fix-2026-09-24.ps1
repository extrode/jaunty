# Cleanup for the 2026-09-24 CI fix session (nightly sqlite3 CLI on Windows, SQLite 3.53 REAL
# decimal read decision 013): local tmp/ scratch from log triage and the decimal-read
# micro-benchmark. Merged branches are left to scripts/cleanup/merged-branch-sweep.ps1, which
# re-scans `git branch --merged dev` at run time.
#
# Dry run by default. --execute / -e acts. Deleting the untracked scratch is irreversible and
# needs --delete-scratch on top of --execute.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$Execute       = $false
$DeleteScratch = $false

foreach ($a in $args) {
  switch ($a) {
    '--execute'        { $Execute = $true }
    '-e'               { $Execute = $true }
    '--delete-scratch' { $DeleteScratch = $true }
    default {
      [Console]::Error.WriteLine("unknown flag: $a")
      [Console]::Error.WriteLine('usage: ci-fix-2026-09-24.ps1 [--execute|-e] [--delete-scratch]')
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
Write-Host '1. Local tmp/ scratch (--delete-scratch)'
$tmpTargets = @(
  'tmp/decbench',
  'tmp/nightly.log',
  'tmp/dbprov.log',
  'tmp/full-test.log'
)
foreach ($rel in $tmpTargets) {
  if (-not (Test-Path $rel)) { Write-Host "   skip: $rel (already gone)"; continue }
  $tracked = git ls-files -- $rel
  if ($tracked) {
    Write-Host "   refuse: $rel is tracked by git. Propose it as a commit instead. Left alone."
    $Failed = $true
    continue
  }
  Write-Host "   $rel"
  if ($Execute -and $DeleteScratch) {
    Remove-Item -Recurse -Force -- $rel
  } elseif ($Execute) {
    Write-Host '   needs --delete-scratch as well; left alone'
  }
}
Write-Host ''

Write-Host 'Remaining:'
foreach ($rel in $tmpTargets) { if (Test-Path $rel) { Write-Host "   $rel" } }
Write-Host ''

if (-not $Execute) {
  Write-Host 'Dry run: nothing was changed.'
  exit 0
}
if ($Failed) {
  Write-Host 'Finished with errors: see the refused steps above.'
  exit 1
}
Write-Host 'Done.'
exit 0
