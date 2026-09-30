# Cleanup for the 2026-09-30/10-01 mutation-coverage session (Fluent + UnitTests survivors):
# probe worktree, leftover agent worktrees, merged test/mutants-* branches, and tmp/ scratch
# (bundles, probe scripts, job logs, downloaded reports).
#
# Dry run by default. --execute / -e acts. Removing a worktree that still has uncommitted
# changes needs --force-worktrees on top of --execute. Branches go with `git branch -d` only,
# so an unmerged branch is refused by git, never forced.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$Execute       = $false
$ForceWorktree = $false

foreach ($a in $args) {
  switch ($a) {
    '--execute'         { $Execute = $true }
    '-e'                { $Execute = $true }
    '--force-worktrees' { $ForceWorktree = $true }
    default {
      [Console]::Error.WriteLine("unknown flag: $a")
      [Console]::Error.WriteLine('usage: mutation-coverage-round-2026-10-01.ps1 [--execute|-e] [--force-worktrees]')
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

# --- 1. Worktrees -----------------------------------------------------------
Write-Host '--- 1. worktrees'
$worktrees = @(
  'tmp/mc',
  '.claude/worktrees/agent-a2c022eb1553dba5f',
  '.claude/worktrees/agent-a918d08d675b32fdd',
  '.claude/worktrees/agent-a96d0d33351487abb'
)
foreach ($rel in $worktrees) {
  if (-not (Test-Path -LiteralPath $rel)) { Write-Host "  absent: $rel"; continue }
  $dirty = (git -C $rel status --porcelain)
  if ($dirty -and -not $ForceWorktree) {
    Write-Host "  refuse: $rel has uncommitted changes (use --force-worktrees to drop them)"
    $Failed = $true
    continue
  }
  if ($Execute) {
    if ($ForceWorktree) { git worktree remove --force $rel } else { git worktree remove $rel }
  }
  Write-Host "  worktree: $rel"
}
Write-Host ''

# --- 2. Merged branches ------------------------------------------------------
Write-Host '--- 2. merged branches (git branch -d)'
$merged = @(git branch --merged dev --format='%(refname:short)') | Where-Object {
  $_ -like 'test/mutants-*' -or $_ -like 'worktree-agent-*'
}
foreach ($b in $merged) {
  if ($Execute) { git branch -d $b }
  Write-Host "  branch: $b"
}
Write-Host ''

# --- 3. tmp/ scratch ----------------------------------------------------------
Write-Host '--- 3. tmp/ scratch (gitignored, confirmed untracked)'
$scratch = @(
  'tmp/dev5.bundle', 'tmp/dev6.bundle', 'tmp/dev7.bundle',
  'tmp/mb1-job3.sh', 'tmp/mb1-job4.sh', 'tmp/mb1-job5.sh', 'tmp/mb1-job6.sh', 'tmp/mb1-job7.sh',
  'tmp/job5-fluent.out', 'tmp/job6-fluent.out', 'tmp/job6-ut.out', 'tmp/job7-ut.out',
  'tmp/probe-ut-pb.out', 'tmp/disable-review-prompt.txt', 'tmp/disable-review.out',
  'tmp/mutprobe.py', 'tmp/ut-file.py', 'tmp/ut-sum.py', 'tmp/fl-list.py', 'tmp/addtests.py',
  'tmp/edit1.py', 'tmp/edit2.py', 'tmp/edit3.py', 'tmp/edit4.py', 'tmp/edit5.py',
  'tmp/edit6.py', 'tmp/edit7.py', 'tmp/edit8.py', 'tmp/edit9.py',
  'tmp/ut.json', 'tmp/fl'
)
foreach ($rel in $scratch) {
  if (-not (Test-Path -LiteralPath $rel)) { continue }
  git ls-files --error-unmatch -- $rel *> $null
  if ($LASTEXITCODE -eq 0) {
    Write-Host "  refuse: $rel is tracked by git. Propose it as a commit instead. Left alone."
    $Failed = $true
    continue
  }
  if ($Execute) { Remove-Item -LiteralPath $rel -Recurse -Force }
  Write-Host "  scratch: $rel"
}
Write-Host ''

Write-Host '--- remaining state'
git worktree list
git branch --list 'test/mutants-*'
if ($Failed) { exit 1 }
