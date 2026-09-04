# repo-state-tidy-2026-09-04.ps1
#
# Written 2026-09-04 by the session that cleared the small unblocked backlog: the docs-site
# line-ending flag, the SqlParameterParser comment, the async rollback null guard and the fuzz
# harness README note. Covers the branches those merges left behind, in three repositories, plus
# the scratch that produced them.
#
# Written, never run by the session. Run it yourself:
#
#   pwsh -NoProfile scripts/cleanup/repo-state-tidy-2026-09-04.ps1        # dry run
#   pwsh -NoProfile scripts/cleanup/repo-state-tidy-2026-09-04.ps1 -e     # apply
#   pwsh -NoProfile scripts/cleanup/repo-state-tidy-2026-09-04.ps1 -e -DeleteScratch
#
# -DeleteScratch is the second flag: it removes untracked files under tmp/, which git cannot
# give back.
#
# NOT covered, on purpose: jaunty/work/, the stale untracked duplicate of the status heartbeat.
# Whether it is deleted or re-linked is an owner decision, recorded in jaunty-audit's
# work/todo.md, and the on-disk swap in work/status/current.md item 5 may remove it anyway.

[CmdletBinding()]
param(
    [Alias('e')][switch]$Execute,
    [switch]$DeleteScratch
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$script:failed = $false

$PreRewrite = 'C:\home\code\extrode.com\jaunty'
$Public12   = 'C:\home\code\extrode.com\jaunty\tmp\jaunty-public12'
$Audit      = 'C:\home\code\extrode.com\jaunty-audit'

function Step {
    param([string]$Description, [scriptblock]$Action)
    if ($Execute) {
        Write-Host "run:   $Description"
        & $Action
    }
    else {
        Write-Host "would: $Description"
    }
}

function Remove-MergedBranch {
    param([string]$Repo, [string]$Branch)

    if (-not (Test-Path -LiteralPath $Repo)) {
        Write-Host "skip:  $Repo does not exist"
        return
    }

    git -C $Repo rev-parse --verify --quiet "refs/heads/$Branch" > $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "skip:  $Repo -> branch $Branch no longer exists"
        return
    }

    $current = (git -C $Repo rev-parse --abbrev-ref HEAD).Trim()
    if ($current -eq $Branch) {
        Write-Host "refuse: $Repo is standing on $Branch. Check out dev first. Left alone."
        $script:failed = $true
        return
    }

    Step "git -C $Repo branch -d $Branch" {
        git -C $Repo branch -d $Branch
        if ($LASTEXITCODE -ne 0) {
            Write-Host '  refused: not merged. Left alone.'
            $script:failed = $true
        }
    }
}

function Remove-Scratch {
    param([string]$Repo, [string]$Relative)

    $full = Join-Path $Repo $Relative
    if (-not (Test-Path -LiteralPath $full)) {
        Write-Host "skip:  $Relative is already gone"
        return
    }

    git -C $Repo ls-files --error-unmatch -- $Relative > $null 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "refuse: $Relative is tracked by git. A cleanup script does not delete committed"
        Write-Host "        content; propose it as a commit instead. Left alone."
        $script:failed = $true
        return
    }

    if (-not $DeleteScratch) {
        Write-Host "hold:  $Relative needs -DeleteScratch as well as -e"
        return
    }

    Step "remove $full" { Remove-Item -LiteralPath $full -Recurse -Force }
}

Write-Host ''
Write-Host '=== 1. Merged branches ==='
Write-Host ''

Remove-MergedBranch -Repo $PreRewrite -Branch 'chore/repo-state-tidy'
Remove-MergedBranch -Repo $Public12   -Branch 'fix/parser-comment-and-rollback-guard'
Remove-MergedBranch -Repo $Audit      -Branch 'chore/todo-npgsql-and-stale-work-copy'

Write-Host ''
Write-Host '=== 2. Scratch (needs -DeleteScratch) ==='
Write-Host ''

Remove-Scratch -Repo $PreRewrite -Relative 'tmp\port-patches'
Remove-Scratch -Repo $PreRewrite -Relative 'tmp\fix-rollback-null.py'
Remove-Scratch -Repo $Audit      -Relative 'tmp\close-todo-items.py'

Write-Host ''
Write-Host '=== 3. Remaining state ==='
Write-Host ''

foreach ($pair in @(@($PreRewrite, 'pre-rewrite'), @($Public12, 'public12'), @($Audit, 'audit'))) {
    $repo, $label = $pair
    if (-not (Test-Path -LiteralPath $repo)) {
        Write-Host "$label : missing"
        continue
    }
    $branches = (git -C $repo branch --list | Measure-Object).Count
    $dirty = (git -C $repo status --porcelain | Measure-Object).Count
    Write-Host ("{0,-12} branches={1,-4} uncommitted={2}" -f $label, $branches, $dirty)
}

Write-Host ''
if (-not $Execute) {
    Write-Host 'Dry run. Nothing changed. Re-run with -e to act.'
}

if ($Execute -and $script:failed) { exit 1 }
