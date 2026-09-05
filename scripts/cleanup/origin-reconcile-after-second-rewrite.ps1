# origin-reconcile-after-second-rewrite.ps1
#
# Written 2026-09-05 by the session that force-pushed the second history rewrite over
# origin/dev and origin/main (repo still private). Covers what that left behind: the two local
# tags that held origin's pre-push tips, the two Dependabot branches on origin that were opened
# against the old history (PRs #4 and #5, closed the same day), their remote-tracking refs here,
# and the scratch Sonnet wrote while diagnosing the divergence.
#
# Written, never run by the session. Run it yourself:
#
#   pwsh -NoProfile scripts/cleanup/origin-reconcile-after-second-rewrite.ps1        # dry run
#   pwsh -NoProfile scripts/cleanup/origin-reconcile-after-second-rewrite.ps1 -e     # apply
#   pwsh -NoProfile scripts/cleanup/origin-reconcile-after-second-rewrite.ps1 -e -DeleteBackupTags
#   pwsh -NoProfile scripts/cleanup/origin-reconcile-after-second-rewrite.ps1 -e -DeleteRemoteBranches
#   pwsh -NoProfile scripts/cleanup/origin-reconcile-after-second-rewrite.ps1 -e -DeleteScratch
#
# Second flags, one per irreversible class:
#   -DeleteBackupTags     drops the two backup/origin-* tags. After that the old origin tips are
#                         reachable only from C:\home\code\extrode.com\jaunty-pre-second-rewrite.
#   -DeleteRemoteBranches deletes the two stale dependabot/* branches on origin.
#   -DeleteScratch        removes untracked files under tmp/, which git cannot give back.
#
# NOT covered, on purpose: the four feature branches merged into dev on 2026-09-04/05
# (fix/drop-order-form-template-link, chore/xunit-v3-4.0-mtp-upgrade, chore/ci-mtp-migration,
# chore/nuget-consolidation-cpm) belong to the sessions that did that work; the five local
# dependabot/* branches predate this session and merged-branch-sweep.ps1 already covers merged
# branches by computation.

[CmdletBinding()]
param(
    [Alias('e')][switch]$Execute,
    [switch]$DeleteBackupTags,
    [switch]$DeleteRemoteBranches,
    [switch]$DeleteScratch
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$script:failed = $false

$Repo = 'C:\home\code\extrode.com\jaunty'

$BackupTags = @(
    'backup/origin-dev-before-force-push-2026-09-05',
    'backup/origin-main-before-force-push-2026-09-05'
)

# Remote branch -> the sha it had when PRs #4 and #5 were closed. A different sha means
# Dependabot has reused the name on the new history, and the branch is not ours to delete.
$StaleRemoteBranches = @{
    'dependabot/nuget/src/Jaunty.Extensions.Logging/dev/microsoft-extensions-3a33789cdb' = 'bafd6eb042c9b840d2785163a2af579a7a02b5fe'
    'dependabot/nuget/src/Jaunty.Extensions.Logging/dev/microsoft-extensions-695e6c4d38' = 'c5d72336524c23be6b863bb7c868c3e0a4fa4b15'
}

$Scratch = @(
    'tmp\reconcile-dev-with-origin.sh',
    'tmp\cherry-local-vs-origin.txt'
)

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

function Get-RemoteSha {
    param([string]$Ref)
    $line = git -C $Repo ls-remote origin $Ref 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $line) { return $null }
    return ($line -split "`t")[0].Trim()
}

Write-Host ''
Write-Host '=== 0. Gate: origin must already carry the rewritten dev and main ==='
Write-Host ''

if (-not (Test-Path -LiteralPath $Repo)) {
    Write-Host "refuse: $Repo does not exist."
    exit 1
}

git -C $Repo remote get-url origin > $null 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host 'refuse: no origin remote. Nothing here is safe to judge without it.'
    exit 1
}

foreach ($branch in @('dev', 'main')) {
    $local = (git -C $Repo rev-parse --verify --quiet "refs/heads/$branch").Trim()
    $remote = Get-RemoteSha "refs/heads/$branch"
    if (-not $remote) {
        Write-Host "refuse: origin has no $branch. Left alone."
        exit 1
    }
    git -C $Repo merge-base --is-ancestor $remote $local 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "refuse: origin/$branch ($($remote.Substring(0,8))) is not on local $branch's line."
        Write-Host '        The force-push this script cleans up after has not happened. Left alone.'
        exit 1
    }
    Write-Host "ok:    origin/$branch = $($remote.Substring(0,8)), reachable from local $branch"
}

Write-Host ''
Write-Host '=== 1. Backup tags (needs -DeleteBackupTags) ==='
Write-Host ''

foreach ($tag in $BackupTags) {
    git -C $Repo rev-parse --verify --quiet "refs/tags/$tag" > $null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "skip:  tag $tag no longer exists"
        continue
    }
    $remoteTag = Get-RemoteSha "refs/tags/$tag"
    if ($remoteTag) {
        Write-Host "refuse: tag $tag exists on origin. It was never meant to be pushed; remove it"
        Write-Host '        there by hand first, then re-run. Left alone.'
        $script:failed = $true
        continue
    }
    if (-not $DeleteBackupTags) {
        Write-Host "hold:  tag $tag needs -DeleteBackupTags as well as -e"
        continue
    }
    Step "git tag -d $tag" {
        git -C $Repo tag -d $tag
        if ($LASTEXITCODE -ne 0) { $script:failed = $true }
    }
}

Write-Host ''
Write-Host '=== 2. Stale Dependabot branches on origin (needs -DeleteRemoteBranches) ==='
Write-Host ''

foreach ($branch in $StaleRemoteBranches.Keys) {
    $expected = $StaleRemoteBranches[$branch]
    $remote = Get-RemoteSha "refs/heads/$branch"
    if (-not $remote) {
        Write-Host "skip:  origin/$branch is already gone"
        continue
    }
    if ($remote -ne $expected) {
        Write-Host "refuse: origin/$branch is at $($remote.Substring(0,8)), not the stale"
        Write-Host "        $($expected.Substring(0,8)). Dependabot has reused the name. Left alone."
        $script:failed = $true
        continue
    }
    if (-not $DeleteRemoteBranches) {
        Write-Host "hold:  origin/$branch needs -DeleteRemoteBranches as well as -e"
        continue
    }
    Step "git push origin --delete $branch" {
        git -C $Repo push origin --delete $branch
        if ($LASTEXITCODE -ne 0) {
            Write-Host '  refused by git or the pre-push hook. Left alone.'
            $script:failed = $true
        }
    }
}

Write-Host ''
Write-Host '=== 3. Remote-tracking refs ==='
Write-Host ''

Step 'git fetch --prune origin' {
    git -C $Repo fetch --prune origin
    if ($LASTEXITCODE -ne 0) { $script:failed = $true }
}

Write-Host ''
Write-Host '=== 4. Scratch (needs -DeleteScratch) ==='
Write-Host ''

foreach ($rel in $Scratch) {
    $full = Join-Path $Repo $rel
    if (-not (Test-Path -LiteralPath $full)) {
        Write-Host "skip:  $rel is already gone"
        continue
    }
    git -C $Repo ls-files --error-unmatch -- $rel > $null 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "refuse: $rel is tracked by git. A cleanup script does not delete committed"
        Write-Host '        content; propose it as a commit instead. Left alone.'
        $script:failed = $true
        continue
    }
    if (-not $DeleteScratch) {
        Write-Host "hold:  $rel needs -DeleteScratch as well as -e"
        continue
    }
    Step "remove $full" { Remove-Item -LiteralPath $full -Force }
}

Write-Host ''
Write-Host '=== 5. Remaining state ==='
Write-Host ''

$tags = (git -C $Repo tag --list 'backup/*' | Measure-Object).Count
$remoteDependabot = (git -C $Repo ls-remote --heads origin 'refs/heads/dependabot/*' | Measure-Object).Count
$trackingDependabot = (git -C $Repo branch -r --list 'origin/dependabot/*' | Measure-Object).Count
$remoteTags = (git -C $Repo ls-remote --tags origin | Measure-Object).Count
Write-Host ("backup tags={0}  origin dependabot branches={1}  tracking refs={2}  origin tags={3}" -f `
    $tags, $remoteDependabot, $trackingDependabot, $remoteTags)

Write-Host ''
if (-not $Execute) {
    Write-Host 'Dry run. Nothing changed. Re-run with -e to act.'
}

if ($Execute -and $script:failed) { exit 1 }
