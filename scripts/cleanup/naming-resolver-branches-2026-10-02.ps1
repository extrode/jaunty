# Cleanup for the Fable review fixes and the generated naming resolvers, written 2026-10-02: the
# fix/fable-review-r38-survivors and feat/generated-naming-resolvers branches merged into dev, and
# this cleanup branch itself; plus the stale tmp/mc worktree.
#
# Dry run by default. --execute / -e acts. Branches go with `git branch -d` only, so an unmerged
# branch is refused by git, never forced. The worktree goes with `git worktree remove` (no
# --force) only after its HEAD is confirmed in dev and its tree is clean.
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
  'chore/cleanup-naming-resolver-branches',
  'chore/cleanup-tmp-mc-worktree'
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

# --- 2. tmp/mc worktree (git worktree remove, never --force) ----------
# A detached checkout of 36722b50 left by an earlier mutation round. Checked 2026-10-02: that
# commit and every commit in its reflog are in dev and origin/dev, nothing is modified or
# untracked, and the only ignored content is bin/ and obj/ build output. The same checks run
# again here before anything is removed, so later work in it is never lost.
Write-Host '--- 2. worktree tmp/mc (git worktree remove)'
$wt = Join-Path $repo 'tmp/mc'
$registered = (git worktree list --porcelain) -match '^worktree .*/tmp/mc$'
if (-not $registered) {
  Write-Host '  skip: tmp/mc is not a registered worktree'
} else {
  $head = (git -C $wt rev-parse HEAD)
  $unmerged = [int](git rev-list --count $head --not dev)
  $dirty = @(git -C $wt status --porcelain)
  if ($unmerged -ne 0) {
    Write-Host "  refused: tmp/mc HEAD $head has $unmerged commit(s) not in dev. Left alone."; $Failed = $true
  } elseif ($dirty.Count -ne 0) {
    Write-Host "  refused: tmp/mc has $($dirty.Count) modified or untracked path(s). Left alone."; $dirty | ForEach-Object { Write-Host "    $_" }; $Failed = $true
  } else {
    if ($Execute) {
      git worktree remove $wt
      if ($LASTEXITCODE -ne 0) { Write-Host '  refused: git worktree remove failed. Left alone.'; $Failed = $true }
      else { Write-Host "  removed: tmp/mc ($head, in dev, clean)" }
    } else {
      Write-Host "  worktree: tmp/mc ($head, in dev, clean)"
    }
  }
}
Write-Host ''

Write-Host '--- remaining state'
git branch --list 'fix/fable-review-r38-survivors' 'feat/generated-naming-resolvers' 'chore/cleanup-naming-resolver-branches' 'chore/cleanup-tmp-mc-worktree'
git worktree list
if ($Execute -and $Failed) { exit 1 }
