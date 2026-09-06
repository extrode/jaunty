# nuget-smoke-test.ps1
#
# Written 2026-09-06, after live-testing the published v1.0.0-rc.2 NuGet packages against the
# GitHub Packages feed in two throwaway console projects: one under the repo's own tmp/ (aborted
# after it was found to attach to the repo's central Directory.Packages.props via central package
# management, since tmp/ is inside the repo tree), and one rebuilt under the OS temp dir instead,
# where the actual smoke test ran and passed. Removes both.
#
# General repo build output (bin/, obj/, TestResults/, BenchmarkDotNet.Artifacts/) accumulated
# from the net472 rebuild done during the same session is not a target here: that is
# scripts/cleanup/build-artifacts.ps1's job, already written and recurring. Re-run that one too if
# the checkout has gotten fat.
#
#   pwsh -NoProfile scripts/cleanup/nuget-smoke-test.ps1                    # dry run, with sizes
#   pwsh -NoProfile scripts/cleanup/nuget-smoke-test.ps1 -e -DeleteScratch  # delete

[CmdletBinding()]
param(
    [Alias('e')][switch]$Execute,
    [switch]$DeleteScratch
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$script:failed = $false

$Repo = 'C:\home\code\extrode.com\jaunty'
$Targets = @(
    (Join-Path $Repo 'tmp\nuget-smoke-test'),
    (Join-Path $env:TEMP 'jaunty-nuget-smoke')
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

function Get-SizeMB {
    param([string]$Path)
    $bytes = (Get-ChildItem -LiteralPath $Path -Recurse -Force -File -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum).Sum
    return [math]::Round(($bytes / 1MB), 1)
}

Write-Host ''
Write-Host '=== 0. Gate ==='
Write-Host ''

if (-not (Test-Path -LiteralPath (Join-Path $Repo '.git'))) {
    Write-Host "refuse: $Repo is not a git checkout."
    exit 1
}
Write-Host "ok:    $Repo"

Write-Host ''
Write-Host '=== 1. Scratch directories (needs -DeleteScratch as well as -e) ==='
Write-Host ''

$totalMB = 0
$deletedMB = 0
foreach ($full in $Targets) {
    if (-not (Test-Path -LiteralPath $full)) {
        Write-Host "skip:  $full does not exist"
        continue
    }

    $item = Get-Item -LiteralPath $full -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        Write-Host "refuse: $full is a junction or symlink. Left alone."
        $script:failed = $true
        continue
    }

    if ($full.StartsWith($Repo, [StringComparison]::OrdinalIgnoreCase)) {
        $rel = $full.Substring($Repo.Length + 1)
        git -C $Repo check-ignore -q -- $rel
        if ($LASTEXITCODE -ne 0) {
            Write-Host "refuse: $rel is not gitignored. Left alone."
            $script:failed = $true
            continue
        }
        $tracked = git -C $Repo ls-files -- $rel
        if ($tracked) {
            Write-Host "refuse: $rel contains tracked files. A cleanup script does not delete committed"
            Write-Host '        content; propose it as a commit instead. Left alone.'
            $script:failed = $true
            continue
        }
    }

    $mb = Get-SizeMB $full
    $totalMB += $mb

    if (-not $DeleteScratch) {
        Write-Host ("hold:  {0,8} MB  {1}  needs -DeleteScratch as well as -e" -f $mb, $full)
        continue
    }

    Step ("remove {0,8} MB  {1}" -f $mb, $full) {
        try {
            Remove-Item -LiteralPath $full -Recurse -Force -ErrorAction Stop
            $script:deletedMB += $mb
        }
        catch {
            Write-Host "  locked or refused: $($_.Exception.Message.Trim()). Left alone."
            $script:failed = $true
        }
    }
}

Write-Host ''
Write-Host '=== 2. Remaining state ==='
Write-Host ''

Write-Host ("targets={0}  reclaimable={1} MB  deleted={2} MB" -f $Targets.Count, $totalMB, $deletedMB)
$dirty = (git -C $Repo status --porcelain | Measure-Object).Count
Write-Host "uncommitted changes in tracked files: $dirty (a cleanup must not change this number)"

Write-Host ''
if (-not $Execute) {
    Write-Host 'Dry run. Nothing changed. Re-run with -e -DeleteScratch to act.'
}

if ($Execute -and $script:failed) { exit 1 }
