# Cleanup for the 2026-09-24 docs underscore-prefix rename (branch
# chore/docs-drop-underscore-prefixes): the untracked plan-promote copy of
# docs/plans/2026-09-21-001-...-stryker-mutat.md in the main checkout. The rename makes that path
# tracked, so the untracked copy blocks `git merge` / `git checkout` of a dev that has the rename.
# It is removed only while a tracked copy exists at the same path on dev or the branch.
#
# Works from the main checkout or any worktree: targets the main checkout.
# Dry run by default. --execute / -e acts. Deleting the untracked file is irreversible and needs
# --delete-untracked on top of --execute.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$Execute         = $false
$DeleteUntracked = $false

foreach ($a in $args) {
  switch ($a) {
    '--execute'          { $Execute = $true }
    '-e'                 { $Execute = $true }
    '--delete-untracked' { $DeleteUntracked = $true }
    default {
      [Console]::Error.WriteLine("unknown flag: $a")
      [Console]::Error.WriteLine('usage: docs-drop-underscore-prefixes.ps1 [--execute|-e] [--delete-untracked]')
      exit 2
    }
  }
}

$common = (git rev-parse --path-format=absolute --git-common-dir)
if (-not $common) { Write-Error 'Not inside a git repository.'; exit 1 }
$repo = Split-Path $common -Parent
Set-Location $repo
$Failed = $false

if ($Execute) { Write-Host '=== EXECUTING ===' } else { Write-Host '=== DRY RUN (pass --execute to act) ===' }
Write-Host "repo: $repo"
Write-Host ''

# --- 1. Stale untracked plan copy --------------------------------------------
Write-Host '1. Stale untracked plan copy (--delete-untracked)'
$targets = @(
  'docs/plans/2026-09-21-001-coverage-refresh-full-method-level-enumeration-stryker-mutat.md'
)
foreach ($rel in $targets) {
  if (-not (Test-Path $rel)) { Write-Host "   skip: $rel (already gone)"; continue }
  $tracked = git ls-files -- $rel
  if ($tracked) { Write-Host "   skip: $rel (now tracked; the untracked copy is gone)"; continue }
  $copy = $null
  foreach ($ref in 'dev', 'chore/docs-drop-underscore-prefixes') {
    git cat-file -e "${ref}:$rel" 2>$null
    if ($LASTEXITCODE -eq 0) { $copy = $ref; break }
  }
  if (-not $copy) {
    Write-Host "   refuse: no tracked copy of $rel on dev or the branch. Left alone."
    $Failed = $true
    continue
  }
  Write-Host "   $rel (tracked copy on $copy)"
  if ($Execute -and $DeleteUntracked) {
    Remove-Item -Force -- $rel
    if (-not (Get-ChildItem docs/plans -Force -ErrorAction SilentlyContinue)) { Remove-Item docs/plans }
  } elseif ($Execute) {
    Write-Host '   needs --delete-untracked as well; left alone'
  }
}
Write-Host ''

Write-Host 'Remaining:'
foreach ($rel in $targets) { if (Test-Path $rel) { Write-Host "   $rel" } }
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
