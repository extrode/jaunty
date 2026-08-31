<#
.SYNOPSIS
    Cleanup for the zero-dependency-core work (public release T7/T8/T9), 2026-08-29.

.DESCRIPTION
    Written by the task that moved the ILogger and DI integration out of src/Jaunty into
    src/Jaunty.Extensions.Logging. Removes only what that task created and nothing it shipped.

    Bare invocation is the dry run. -Execute is the only way to act. -DeleteScratch is the second
    flag required for the irreversible part (deleting untracked pack output under tmp/).

    Invoke with:  pwsh -NoProfile -File scripts/cleanup/zero-dependency-core.ps1
#>
[CmdletBinding()]
param(
    [Alias('e')]
    [switch]$Execute,

    [switch]$DeleteScratch
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$script:failed = $false

$repoRoot = (git rev-parse --show-toplevel)
if (-not $repoRoot) { Write-Host 'abort: not inside a git repository'; exit 1 }
Set-Location $repoRoot

# A gate about the current state: describes the world the script is about to act on, and does not
# become false by the script succeeding.
$branch = (git rev-parse --abbrev-ref HEAD)
if ($branch -ne 'dev') {
    Write-Host "abort: expected to be on 'dev' but stand on '$branch'."
    Write-Host "       The branch this script deletes is merged into dev; run it from there."
    exit 1
}

function Step {
    param([string]$Label, [scriptblock]$Action)
    if ($Execute) {
        Write-Host "run:   $Label"
        & $Action
    } else {
        Write-Host "would: $Label"
    }
}

Write-Host ''
Write-Host '== 1. merged work branch =='

$targets = @(
    'feat/zero-dependency-core'
    'docs/aot-and-package-docs'
    'docs/cla-and-contributing'
    'feat/continuous-audit'
    'fix/the audit host-sync-glob'
    'fix/audit-script-exec-bit'
    'fix/audit-pipefail'
    'fix/audit-api-surface-regex'
    'docs/audit-triage-and-decisions'
    'chore/zero-dependency-cleanup-script'
)

foreach ($target in $targets) {
    if (-not (git rev-parse --verify --quiet "refs/heads/$target")) {
        Write-Host "skip:  branch $target no longer exists"
        continue
    }
    Step "git branch -d $target" {
        git branch -d $target
        if ($LASTEXITCODE -ne 0) {
            Write-Host '  refused: not merged. Left alone.'
            $script:failed = $true
        }
    }
}

Write-Host ''
Write-Host '== 2. scratch pack output (needs -DeleteScratch) =='

# tmp/ is gitignored scratch this task created to read dependency groups out of the packed
# nuspecs. Nothing here is an input to anything.
$scratch = @(
    'tmp/packtest',
    'tmp/packall',
    'tmp/publish-aot-net8',
    'tmp/publish-aot-net10',
    'tmp/audit-reports'
)

foreach ($path in $scratch) {
    if (-not (Test-Path $path)) {
        Write-Host "skip:  $path is already gone"
        continue
    }

    $size = '{0:N1} MB' -f ((Get-ChildItem $path -Recurse -File -ErrorAction SilentlyContinue |
                             Measure-Object -Property Length -Sum).Sum / 1MB)

    if (-not $DeleteScratch) {
        Write-Host "would: remove $path ($size) - requires -DeleteScratch"
        continue
    }

    Step "remove $path ($size)" {
        Remove-Item -Recurse -Force $path
    }
}

Write-Host ''
Write-Host ''
Write-Host '== 3. git bundles shipped to the audit host (needs -DeleteScratch) =='

$bundles = Get-ChildItem 'tmp' -Filter 'jaunty-*.bundle' -ErrorAction SilentlyContinue
if (-not $bundles) {
    Write-Host 'skip:  no bundles under tmp/'
} elseif (-not $DeleteScratch) {
    foreach ($b in $bundles) {
        Write-Host ("would: remove tmp/{0} ({1:N1} MB) - requires -DeleteScratch" -f $b.Name, ($b.Length / 1MB))
    }
} else {
    foreach ($b in $bundles) {
        Step ("remove tmp/{0}" -f $b.Name) { Remove-Item -Force $b.FullName }
    }
}

Write-Host ''
Write-Host '== 4. the audit host audit host (NOT removed) =='
Write-Host 'note:  the audit checkout on the audit host holds the audit checkout, bundles and reports.'
Write-Host '       It is the continuous audit loop and is meant to persist. To decommission it'
Write-Host '       entirely:  ssh the audit host "rm -rf the audit checkout"  - run that by hand, deliberately.'

Write-Host ''
Write-Host '== 5. build output this task rebuilt (NOT removed) =='
Write-Host 'note:  bin/ and obj/ under src/Jaunty.Extensions.Logging are ordinary build output.'
Write-Host '       They are gitignored and are rebuilt on demand; this script leaves them alone.'

Write-Host ''
Write-Host '== remaining state =='
Write-Host ("branch      : " + (git rev-parse --abbrev-ref HEAD))
Write-Host ("dev at      : " + (git log --oneline -1))
$unpushed = (git log --oneline '@{u}..HEAD' 2>$null | Measure-Object).Count
if ($LASTEXITCODE -eq 0) { Write-Host "unpushed    : $unpushed commit(s) - pushing is the owner's" }
Write-Host ("worktrees   : " + ((git worktree list | Measure-Object).Count) + ' (none created by this task)')

if (-not $Execute) {
    Write-Host ''
    Write-Host 'DRY RUN. Nothing was changed.'
    Write-Host 'To act:            pwsh -NoProfile -File scripts/cleanup/zero-dependency-core.ps1 -Execute'
    Write-Host 'Including scratch: pwsh -NoProfile -File scripts/cleanup/zero-dependency-core.ps1 -Execute -DeleteScratch'
}

if ($Execute -and $script:failed) { exit 1 }
