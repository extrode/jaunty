<#
.SYNOPSIS
    Cleanup for the analyzer-scope and README work, 2026-09-02.

.DESCRIPTION
    Written by the task that scoped a proposed string-qualifier analyzer onto the backlog, had it
    reviewed, measured the Fluent-only packaging question, documented HAVING parameter naming in
    README.md, and added `.claude/worktrees/` to .gitignore.

    Five merged branches across two repositories - two here, three in the private jaunty-audit
    repository that supplies work/ through a junction. Nothing shipped is touched, and no file is
    removed: the probe project and its package feed under tmp/ are scratch this task created and
    owns, so they are not listed here.

    Bare invocation is the dry run. -Execute is the only way to act.

    Invoke with:  pwsh -NoProfile -File scripts/cleanup/analyzer-scope-and-readme.ps1
#>
[CmdletBinding()]
param(
    [Alias('e')]
    [switch]$Execute
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$script:failed = $false

$repoRoot = (git rev-parse --show-toplevel)
if (-not $repoRoot) { Write-Host 'abort: not inside a git repository'; exit 1 }
Set-Location $repoRoot

function Remove-MergedBranches {
    param([string]$RepoPath, [string[]]$Branches)

    if (-not (Test-Path (Join-Path $RepoPath '.git'))) {
        Write-Host "skip:  $RepoPath is not a git repository here"
        return
    }

    Push-Location $RepoPath
    try {
        $branch = (git rev-parse --abbrev-ref HEAD)
        if ($branch -ne 'dev') {
            Write-Host "abort: $RepoPath stands on '$branch', not 'dev'."
            Write-Host '       The branches below are merged into dev; run it from there.'
            $script:failed = $true
            return
        }

        foreach ($target in $Branches) {
            if (-not (git rev-parse --verify --quiet "refs/heads/$target")) {
                Write-Host "skip:  branch $target no longer exists"
                continue
            }

            if ($Execute) {
                Write-Host "run:   git branch -d $target"
                git branch -d $target
                if ($LASTEXITCODE -ne 0) {
                    Write-Host '  refused: not merged. Left alone.'
                    $script:failed = $true
                }
            } else {
                Write-Host "would: git branch -d $target"
            }
        }
    } finally {
        Pop-Location
    }
}

Write-Host ''
Write-Host "== 1. merged branches in $repoRoot =="

Remove-MergedBranches -RepoPath $repoRoot -Branches @(
    'docs/having-parameter-naming-readme'
    'chore/ignore-claude-worktrees'
)

Write-Host ''
Write-Host '== 2. merged branches in the private jaunty-audit repository =='

# work/ in this tree is a junction into jaunty-audit, so the backlog commits landed there, not here.
$auditRoot = 'C:\home\code\extrode.com\jaunty-audit'

if (-not (Test-Path $auditRoot)) {
    Write-Host "skip:  $auditRoot is not present on this machine"
} else {
    Remove-MergedBranches -RepoPath $auditRoot -Branches @(
        'docs/analyzer-scope-backlog'
        'docs/analyzer-scope-review'
        'docs/fluent-packaging-measured'
    )
}

Write-Host ''
if (-not $Execute) {
    Write-Host 'dry run. Re-run with -Execute to act.'
}

if ($script:failed) { exit 1 }
exit 0
