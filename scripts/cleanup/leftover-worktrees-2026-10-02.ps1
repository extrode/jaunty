# Cleanup for the five worktrees left after paged-writes-flatfiles-upsert-2026-10-02.ps1: three
# agent worktrees under .claude/worktrees, .worktrees/chore-docs-drop-underscore-prefixes, and the
# detached tmp/mc worktree. Every one was checked on 2026-10-02: its HEAD is merged into dev and it
# has no tracked or untracked changes. The agent worktrees each hold only an ignored
# .claude/settings.local.json, which goes with them.
#
# Then the four branches those worktrees had checked out, all merged into dev.
#
# Not touched: worktree-agent-a2c022eb1553dba5f and worktree-agent-a96d0d33351487abb. Their tips are
# merge commits on main, not on dev, so `git branch -d` from dev refuses them.
#
# Dry run by default. --execute / -e acts. Worktrees go with `git worktree remove` (no --force), so a
# dirty worktree is refused. Branches go with `git branch -d` only, so an unmerged branch is refused.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$Execute = $false

foreach ($a in $args) {
  switch ($a) {
    '--execute' { $Execute = $true }
    '-e'        { $Execute = $true }
    default {
      [Console]::Error.WriteLine("unknown flag: $a")
      [Console]::Error.WriteLine('usage: leftover-worktrees-2026-10-02.ps1 [--execute|-e]')
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

# --- 1. Worktrees (git worktree remove, then prune) ----------------------------
Write-Host '--- 1. worktrees'
$worktrees = @(
  '.claude/worktrees/agent-a2c022eb1553dba5f',
  '.claude/worktrees/agent-a918d08d675b32fdd',
  '.claude/worktrees/agent-a96d0d33351487abb',
  '.worktrees/chore-docs-drop-underscore-prefixes',
  'tmp/mc'
)
$list = (git worktree list --porcelain)
foreach ($rel in $worktrees) {
  $wt = Join-Path $repo $rel
  $registered = $list -match [regex]::Escape(($wt -replace '\\', '/'))
  if (-not $registered) { Write-Host "  skip: $rel no longer registered"; continue }
  $head = (git -C $wt rev-parse HEAD)
  git merge-base --is-ancestor $head dev
  if ($LASTEXITCODE -ne 0) { Write-Host "  refused: $rel has commits not on dev. Left alone."; $Failed = $true; continue }
  if ($Execute) {
    git worktree remove $wt
    if ($LASTEXITCODE -ne 0) { Write-Host "  refused: $rel is dirty. Left alone."; $Failed = $true; continue }
    Write-Host "  removed: $rel"
  } else {
    Write-Host "  worktree: $rel"
  }
}
if ($Execute) { git worktree prune }
Write-Host ''

# --- 2. Merged branches those worktrees held (git branch -d) -------------------
Write-Host '--- 2. merged branches (git branch -d)'
foreach ($b in @(
  'test/mutants-fluent-c',
  'test/mutants-fluent-b',
  'worktree-agent-a918d08d675b32fdd',
  'chore/docs-drop-underscore-prefixes'
)) {
  git rev-parse --verify --quiet "refs/heads/$b" *> $null
  if ($LASTEXITCODE -ne 0) { Write-Host "  skip: $b no longer exists"; continue }
  if ($Execute) {
    git branch -d $b
    if ($LASTEXITCODE -ne 0) { Write-Host "  refused: $b not merged or still checked out. Left alone."; $Failed = $true; continue }
  }
  Write-Host "  branch: $b"
}
Write-Host ''

Write-Host '--- remaining state'
git worktree list
if ($Failed) { exit 1 }
