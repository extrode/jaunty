# Remove what the testing-strategy Phase 1 and Phase 2 work left behind, written 2026-08-27.
#
# Produced by docs/plans/2026-08-27-003-testing-strategy-implementation.md.
#
#   tests/Jaunty.UnitTests/StrykerOutput      337 MB  mutation reports, gitignored (.gitignore:385)
#   tests/Jaunty.Fluent.Tests/StrykerOutput    31 MB  same
#
# Both hold mutation-report.html/.json per run, and a single Jaunty.UnitTests report is ~176 MB of
# each. They are build output, not results anyone reads twice: the numbers that mattered are
# written into the plan document and into work/todo.md, which is the point of recording them there.
#
# NOT listed here, deliberately:
#
#   the merged branches (test/phase1-streaming-and-budgets, test/exists-visitor-emission-paths,
#   chore/todo-exists-followup) - once merged they are ordinary members of the backlog, and
#   scripts/cleanup/merged-branch-sweep.ps1 is the recurring script that drains it.
#
#   tools/Jaunty.Fuzz/corpus/ - 20 committed seed files, an input to the nightly fuzz job, not
#   scratch. out/corpus/ is where a run accumulates and that is CI-side, never on this machine.
#
#   tmp/ - scratch this work created itself. Mine to remove, and not the user's concern.
#
# Dry run is what a bare invocation does. -Execute / -e is the only way to act. Deleting untracked
# build output is irreversible, so it is gated behind a second flag, -DeleteStrykerOutput.
#
#   pwsh -NoProfile scripts/cleanup/testing-strategy-phase1-2.ps1
#   pwsh -NoProfile scripts/cleanup/testing-strategy-phase1-2.ps1 -e -DeleteStrykerOutput

[CmdletBinding()]
param(
    [Alias('e')]
    [switch]$Execute,

    [switch]$DeleteStrykerOutput
)

$ErrorActionPreference = 'Stop'
# Several checks below expect a non-zero exit from a native command rather than a throw.
$PSNativeCommandUseErrorActionPreference = $false

# Act on the repository this script lives in, not the caller's working directory.
Set-Location $PSScriptRoot
$mainRoot = (git worktree list --porcelain | Select-Object -First 1) -replace '^worktree ', ''
if (-not $mainRoot) {
    Write-Error 'Could not resolve the main worktree. Aborting rather than acting on a guess.'
    exit 1
}
Set-Location $mainRoot

if ($DeleteStrykerOutput -and -not $Execute) {
    Write-Error 'DeleteStrykerOutput requires -Execute. It deletes directories from disk.'
    exit 1
}

# A gate about the current state, so it stays true after a successful run: never delete a report
# directory out from under a mutation run that is still writing into it.
$running = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -and $_.CommandLine -match 'stryker' }
if ($running) {
    Write-Error "A Stryker run is in progress (PID $($running.ProcessId -join ', ')). Let it finish first."
    exit 1
}

$failed = $false
$targets = @(
    'tests/Jaunty.UnitTests/StrykerOutput',
    'tests/Jaunty.Fluent.Tests/StrykerOutput'
)

Write-Host ''
Write-Host '1. Stryker report directories'

foreach ($target in $targets) {
    if (-not (Test-Path $target)) {
        Write-Host "  skip:  $target no longer exists"
        continue
    }

    $size = (Get-ChildItem $target -Recurse -File -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum).Sum
    $mb = [math]::Round($size / 1MB)

    # Refuse to remove anything git is tracking. These are gitignored today; a future config change
    # that starts committing a report should stop this script, not be quietly undone by it.
    $tracked = git ls-files --error-unmatch $target 2>$null
    if ($LASTEXITCODE -eq 0 -and $tracked) {
        Write-Host "  skip:  $target is tracked by git. Left alone - remove it with git rm if that is meant."
        $failed = $true
        continue
    }

    if ($DeleteStrykerOutput) {
        Write-Host "  rm -r $target  ($mb MB)"
        Remove-Item $target -Recurse -Force
    }
    else {
        Write-Host "  would: rm -r $target  ($mb MB)"
    }
}

Write-Host ''
Write-Host '2. The merged branches'
Write-Host '   Not deleted here: test/phase1-streaming-and-budgets,'
Write-Host '   test/exists-visitor-emission-paths, chore/todo-exists-followup.'
Write-Host '   They are ordinary merged branches now, and'
Write-Host '   scripts/cleanup/merged-branch-sweep.ps1 drains that backlog.'

Write-Host ''
Write-Host 'Remaining state:'
foreach ($target in $targets) {
    if (Test-Path $target) { Write-Host "  present: $target" } else { Write-Host "  gone:    $target" }
}

Write-Host ''
if (-not $DeleteStrykerOutput) {
    # -Execute alone is a no-op here, because every action in this script deletes a directory.
    if ($Execute) {
        Write-Host 'Nothing done: -Execute alone is a no-op here, because every step in this script'
        Write-Host 'deletes a report directory. Add -DeleteStrykerOutput to apply.'
    }
    else {
        Write-Host 'Dry run. Re-run with -Execute -DeleteStrykerOutput to apply.'
    }
    exit 0
}
if ($failed) {
    Write-Error 'Something was left in place. See the skip lines above.'
    exit 1
}
exit 0
