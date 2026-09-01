<#
.SYNOPSIS
    Cleanup for the elegant-join-sql work, 2026-09-01.

.DESCRIPTION
    Written by the task that made the fluent join builder infer table aliases from lambda
    parameter names, name WHERE parameters after their column, and rewrote README.md in
    GitHub-flavored markdown. Removes only what that task created.

    Two work branches, both merged into dev with --no-ff, and the win-x64 NativeAOT publish
    output produced to verify the change publishes clean. Nothing shipped is touched.

    Bare invocation is the dry run. -Execute is the only way to act. -DeleteScratch is the
    second flag required for the irreversible part (removing untracked build output).

    Invoke with:  pwsh -NoProfile -File scripts/cleanup/elegant-join-sql.ps1
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

$branch = (git rev-parse --abbrev-ref HEAD)
if ($branch -ne 'dev') {
    Write-Host "abort: expected to be on 'dev' but stand on '$branch'."
    Write-Host '       The branches this script deletes are merged into dev; run it from there.'
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
Write-Host '== 1. merged work branches =='

$targets = @(
    'feat/elegant-join-sql'
    'docs/readme-gfm'
    'chore/elegant-join-sql-followup'
    'fix/positional-alias-collision'
    'chore/cleanup-script-branch'
    'test/string-on-alias-diagnostic'
    'feat/derived-having-parameter-names'
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
Write-Host '== 2. NativeAOT publish output (needs -DeleteScratch) =='

# Produced by `dotnet publish samples/NativeAOT-FluentQuery -c Release -r win-x64 -f net10.0`
# to confirm the alias-inference change publishes with no Jaunty-assembly trim warnings. It is
# gitignored build output and an input to nothing; CI publishes its own.
$scratch = @(
    'samples/NativeAOT-FluentQuery/bin/Release/net10.0/win-x64'
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
if (-not $Execute) {
    Write-Host 'dry run. Re-run with -Execute to act, and add -DeleteScratch for section 2.'
}

if ($script:failed) { exit 1 }
exit 0
