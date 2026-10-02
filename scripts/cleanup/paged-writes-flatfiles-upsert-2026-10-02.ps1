# Cleanup for the 2026-10-02 paged-write fence, FlatFiles naming, dead-code sweep and
# generated-key upsert work: the .worktrees/docs-paged-writes worktree, the branches merged into
# dev, the matching todo branches in ../jaunty-audit, and the torture-mssql container that
# `docker compose up` created (and never started) while bringing the test databases back.
#
# Dry run by default. --execute / -e acts. Branches go with `git branch -d` only, so an unmerged
# branch is refused by git, never forced. The container needs a second flag, -RemoveContainer,
# and is only removed while it is still in the "created" state.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$Execute = $false
$RemoveContainer = $false

foreach ($a in $args) {
  switch ($a) {
    '--execute'        { $Execute = $true }
    '-e'               { $Execute = $true }
    '-RemoveContainer' { $RemoveContainer = $true }
    default {
      [Console]::Error.WriteLine("unknown flag: $a")
      [Console]::Error.WriteLine('usage: paged-writes-flatfiles-upsert-2026-10-02.ps1 [--execute|-e] [-RemoveContainer]')
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

function Remove-MergedBranches([string]$gitDir, [string[]]$names) {
  foreach ($b in $names) {
    git -C $gitDir rev-parse --verify --quiet "refs/heads/$b" *> $null
    if ($LASTEXITCODE -ne 0) { Write-Host "  skip: $b no longer exists"; continue }
    if ($Execute) {
      git -C $gitDir branch -d $b
      if ($LASTEXITCODE -ne 0) { Write-Host "  refused: $b not merged. Left alone."; $script:Failed = $true; continue }
    }
    Write-Host "  branch: $b"
  }
}

# --- 1. Worktree (git worktree remove, then prune) -----------------------------
Write-Host '--- 1. worktree .worktrees/docs-paged-writes'
$wt = Join-Path $repo '.worktrees/docs-paged-writes'
$registered = (git worktree list --porcelain) -match [regex]::Escape(($wt -replace '\\', '/'))
if (-not $registered) {
  Write-Host '  skip: worktree no longer registered'
} elseif ($Execute) {
  git worktree remove $wt
  if ($LASTEXITCODE -ne 0) { Write-Host '  refused: worktree is dirty. Left alone.'; $Failed = $true }
  else { git worktree prune; Write-Host "  removed: $wt" }
} else {
  Write-Host "  worktree: $wt"
}
Write-Host ''

# --- 2. Merged branches in this repo (git branch -d) ---------------------------
Write-Host '--- 2. merged branches (git branch -d)'
Remove-MergedBranches $repo @(
  'docs/paged-writes-blocked',
  'feat/generated-members-nested-class',
  'test/interceptor-elapsed-margin',
  'feat/paged-write-fence',
  'docs/rebuild-site-paged-writes',
  'feat/flatfiles-core-naming',
  'refactor/dead-code-sweep',
  'fix/flatfiles-stray-aot-marker',
  'test/serialize-clr-type-test',
  'fix/upsert-identity-key',
  'docs/duckdb-core-crud-limitation',
  'test/allocation-budget-serial',
  'chore/cleanup-paged-writes-upsert',
  'fix/review-upsert-followups',
  'chore/cleanup-review-followups'
)
Write-Host ''

# --- 3. Merged todo branches in ../jaunty-audit (git branch -d) ----------------
Write-Host '--- 3. merged branches in ../jaunty-audit (git branch -d)'
$audit = Join-Path (Split-Path $repo -Parent) 'jaunty-audit'
if (-not (Test-Path (Join-Path $audit '.git'))) {
  Write-Host "  skip: $audit is not a git repository"
} elseif ((git -C $audit rev-parse --abbrev-ref HEAD) -ne 'dev') {
  Write-Host "  refused: $audit is not on dev. Left alone."
  $Failed = $true
} else {
  Remove-MergedBranches $audit @('docs/todo-naming-and-paged-writes', 'docs/todo-close-three-findings')
}
Write-Host ''

# --- 4. Never-started torture-mssql container (needs -RemoveContainer) ---------
Write-Host '--- 4. container torture-mssql (only while still "created")'
$state = (docker inspect --format '{{.State.Status}}' torture-mssql 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $state) {
  Write-Host '  skip: container no longer exists'
} elseif ($state -ne 'created') {
  Write-Host "  refused: torture-mssql is '$state', not 'created'; it may hold data. Left alone."
  $Failed = $true
} elseif ($Execute -and $RemoveContainer) {
  docker rm torture-mssql | Out-Null
  if ($LASTEXITCODE -ne 0) { Write-Host '  refused: docker rm failed. Left alone.'; $Failed = $true }
  else { Write-Host '  removed: torture-mssql' }
} else {
  Write-Host '  container: torture-mssql (pass -e -RemoveContainer to remove)'
}
Write-Host ''

Write-Host '--- remaining state'
git worktree list
git branch --list 'docs/paged-writes-blocked' 'feat/generated-members-nested-class' 'test/interceptor-elapsed-margin' 'feat/paged-write-fence' 'docs/rebuild-site-paged-writes' 'feat/flatfiles-core-naming' 'refactor/dead-code-sweep' 'fix/flatfiles-stray-aot-marker' 'test/serialize-clr-type-test' 'fix/upsert-identity-key' 'docs/duckdb-core-crud-limitation' 'test/allocation-budget-serial' 'chore/cleanup-paged-writes-upsert' 'fix/review-upsert-followups' 'chore/cleanup-review-followups'
if ($Execute -and $Failed) { exit 1 }
