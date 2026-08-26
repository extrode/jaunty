# Remove what the Northwind fixture fix left behind, written 2026-08-27.
#
#   .worktrees/fix-northwind-fixture-copy   created because the main worktree held uncommitted
#                                           work and could not check dev out. Its branch,
#                                           fix/northwind-fixture-copy, is merged into dev at
#                                           5bce6673.
#
# The per-process database copies the fix creates under
# tests/Jaunty.Tests/bin/*/northwind-work/ are NOT listed here. They live in build output, they
# are gitignored, `dotnet clean` removes them, and NorthwindDatabase prunes the ones whose owning
# process has exited on the next run. Nothing accumulates that needs a person.
#
# Dry run is what a bare invocation does. -Execute / -e is the only way to act. Removing a worktree
# deletes its directory from disk, so that is gated behind a second flag, -RemoveWorktreeDirectories.

[CmdletBinding()]
param(
    [Alias('e')]
    [switch]$Execute,

    [switch]$RemoveWorktreeDirectories
)

$ErrorActionPreference = 'Stop'
# Several checks below expect a non-zero exit from a native command rather than a throw.
$PSNativeCommandUseErrorActionPreference = $false

# Act on the repository this script lives in, not the caller's working directory.
Set-Location (Split-Path -Parent $PSScriptRoot | Split-Path -Parent)

git rev-parse --verify --quiet refs/heads/dev > $null
if ($LASTEXITCODE -ne 0) {
    Write-Error 'No dev branch in this repository. Aborting rather than guessing what is merged.'
    exit 1
}

if ($RemoveWorktreeDirectories -and -not $Execute) {
    Write-Error 'RemoveWorktreeDirectories requires -Execute. Removing a worktree deletes its directory.'
    exit 1
}

$failed = $false
$target = '.worktrees/fix-northwind-fixture-copy'
$branch = 'fix/northwind-fixture-copy'

Write-Host ''
Write-Host '1. Confirm the branch is already on dev'

# Refuse to touch anything if the work is not merged. A worktree removed while its branch still
# holds unmerged commits loses nothing immediately, but it hides the branch from view.
$unmerged = git rev-list --count "dev..$branch"
if ($LASTEXITCODE -ne 0) {
    Write-Host "  skip: $branch does not exist. Nothing to check."
}
elseif ([int]$unmerged -ne 0) {
    Write-Error "$branch has $unmerged commit(s) not on dev. Merge it before cleaning up."
    exit 1
}
else {
    Write-Host "  ok: $branch is fully merged into dev"
}

Write-Host ''
Write-Host '2. The worktree'

$registered = git worktree list --porcelain |
    Where-Object { $_ -like 'worktree *' } |
    ForEach-Object { $_ -replace '^worktree ', '' } |
    Where-Object { $_.Replace('\', '/').EndsWith($target) }

if (-not $registered) {
    Write-Host "  skip: $target is not a registered worktree"
}
else {
    # git worktree remove refuses a dirty tree. That refusal is the wanted behaviour: say so and
    # move on. Never --force - uncommitted work in there is work, not cruft.
    $dirty = git -C $target status --porcelain
    if ($dirty) {
        Write-Host "  skip: $target has uncommitted changes. Left alone - inspect it by hand."
        $failed = $true
    }
    elseif ($RemoveWorktreeDirectories) {
        Write-Host "  git worktree remove $target"
        git worktree remove $target
        if ($LASTEXITCODE -ne 0) { Write-Host '  refused by git. Left in place.'; $failed = $true }
    }
    else {
        Write-Host "  would: git worktree remove $target"
        Write-Host '         (needs -Execute -RemoveWorktreeDirectories; deletes the directory)'
    }
}

Write-Host ''
Write-Host '3. Prune the registrations'

if ($RemoveWorktreeDirectories) {
    git worktree prune
    Write-Host '  git worktree prune'
}
else {
    Write-Host '  would: git worktree prune'
}

Write-Host ''
Write-Host "4. The branch $branch"
Write-Host '   Not deleted here. Once the tree is gone it is an ordinary merged branch, and'
Write-Host '   scripts/cleanup/merged-branch-sweep.ps1 picks it up with the rest of the backlog.'

Write-Host ''
Write-Host 'Remaining state:'
git worktree list

Write-Host ''
if (-not $RemoveWorktreeDirectories) {
    # Every action in this script deletes a directory, so -Execute alone changes nothing. Say that
    # rather than printing a dry run under an -e that looks like it should have done something.
    if ($Execute) {
        Write-Host 'Nothing done: -Execute alone is a no-op here, because every step in this script'
        Write-Host 'removes a worktree directory. Add -RemoveWorktreeDirectories to apply.'
    }
    else {
        Write-Host 'Dry run. Re-run with -Execute -RemoveWorktreeDirectories to apply.'
    }
    exit 0
}
if ($failed) {
    Write-Error 'The worktree was left in place. See the skip lines above.'
    exit 1
}
exit 0
