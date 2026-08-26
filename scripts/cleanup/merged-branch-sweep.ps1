# Sweep local branches already merged into dev (rounds 1-26 audit/fix/docs branches and older).
# Produced by the 2026-07-31 session after the round-27 close-out. Dry run by default;
# -e / --execute acts. Safe delete only (git branch -d): anything unmerged is skipped and listed,
# never forced. Branches checked out in a worktree are skipped too - run
# scripts/cleanup/stale-audit-worktrees.ps1 -e -RemoveWorktreeDirectories first to release those
# (2026-08-26: was round27-audit.ps1, which no longer holds any worktree).
$ErrorActionPreference = 'Stop'

$execute = $args -contains '--execute' -or $args -contains '-e'
$repo = 'C:\home\code\beparey.com\jaunty'
Set-Location $repo

# 1. Safety gate: only run from dev.
$branch = git branch --show-current
if ($branch -ne 'dev') { Write-Error "Expected branch 'dev', got '$branch'."; exit 1 }

# 2. Classify every local branch.
$protected = @('dev', 'main', 'wip/multientity-per-position-mappers')
$all = git for-each-ref refs/heads --format='%(refname:short)'
$inWorktree = git worktree list --porcelain |
    Where-Object { $_ -like 'branch refs/heads/*' } |
    ForEach-Object { $_ -replace '^branch refs/heads/', '' }
$merged = git branch --merged dev --format='%(refname:short)'

$toDelete = @()
$skipped = @()
foreach ($b in $all) {
    if ($protected -contains $b) { $skipped += "$b (protected)" }
    elseif ($inWorktree -contains $b) { $skipped += "$b (checked out in a worktree - run stale-audit-worktrees.ps1 first)" }
    elseif ($merged -notcontains $b) { $skipped += "$b (NOT merged into dev)" }
    else { $toDelete += $b }
}

# 3. Show / apply. -d only: git itself refuses anything unmerged as a second line of defence.
$failed = @()
foreach ($b in $toDelete) {
    if ($execute) {
        Write-Host "EXECUTE: git branch -d $b"
        git branch -d $b
        if ($LASTEXITCODE -ne 0) { $failed += $b }
    }
    else {
        Write-Host "DRY RUN (would do): git branch -d $b"
    }
}

# 4. Report skipped and remaining state.
Write-Host "`n--- skipped ($($skipped.Count)) ---"
$skipped | ForEach-Object { Write-Host "  $_" }
Write-Host "`n--- remaining branches ---"
git branch

if (-not $execute) {
    Write-Host "`nDry run only: $($toDelete.Count) branch(es) would be deleted. Re-run with -e or --execute to apply."
    exit 0
}
if ($failed.Count -gt 0) {
    Write-Error "These branches failed to delete (left in place): $($failed -join ', ')"
    exit 1
}
Write-Host "`nDeleted $($toDelete.Count) branch(es)."
exit 0
