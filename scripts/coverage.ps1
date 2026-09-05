# Line-coverage run over the tests/ suites, emitting cobertura for the coverage x complexity
# cross-reference in docs/plans/2026-08-27-003-testing-strategy-implementation.md.
#
#   pwsh -NoProfile -File scripts/coverage.ps1              # all suites
#   pwsh -NoProfile -File scripts/coverage.ps1 -Suite Jaunty.Tests
#
# Reports land in tmp/coverage/<suite>/ (gitignored). Nothing is deleted; re-runs overwrite.
#
# The four samples/ torture-test ports are deliberately out of scope: they live outside
# Jaunty.slnx, are not part of `dotnet test --solution Jaunty.slnx`, and carry no
# Microsoft.Testing.Extensions.CodeCoverage reference.
#
# Microsoft.Testing.Platform's built-in coverage extension has no RunSettings equivalent -
# core MTP dropped --settings/.runsettings in favour of testconfig.json, which coverage.runsettings
# was never converted to - so filtering is done with --coverage-output-format/--coverage-settings
# only if a suite needs it; none currently does.

[CmdletBinding()]
param(
    [string] $Suite,
    [string] $Configuration = 'Release',
    [string] $Framework = 'net10.0'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repoRoot 'tmp\coverage'

$suites = Get-ChildItem (Join-Path $repoRoot 'tests') -Directory |
          Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).csproj") } |
          Select-Object -ExpandProperty Name

if ($Suite) {
    if ($suites -notcontains $Suite) {
        throw "Unknown suite '$Suite'. Known: $($suites -join ', ')"
    }
    $suites = @($Suite)
}

New-Item -ItemType Directory -Force -Path $output | Out-Null

$failed = @()

foreach ($name in $suites) {
    $project = Join-Path $repoRoot "tests\$name\$name.csproj"
    $target = Join-Path $output $name

    Write-Host ''
    Write-Host "=== $name ==="

    # Each run writes into a fresh GUID subdirectory, so without this a re-run leaves the
    # previous report behind and the aggregation reads the same suite twice.
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }

    # --coverage-settings (coverage.runsettings, the coverlet-format XML this repo used to pass
    # to VSTest's XPlat collector) is not accepted by Microsoft.Testing.Extensions.CodeCoverage -
    # passing it silently makes the run discover and execute zero tests. The Include/Exclude
    # assembly filters and the GeneratedCodeAttribute-only exclusion documented in that file have
    # no drop-in replacement here; the report below covers every assembly reached by the run,
    # test assembly included, until a replacement config is written for the new extension.
    dotnet test --project $project `
        -c $Configuration -f $Framework `
        --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml `
        --results-directory $target `
        -v q --nologo

    if ($LASTEXITCODE -ne 0) { $failed += $name }
}

Write-Host ''
Write-Host '=== cobertura files ==='

$reports = Get-ChildItem $output -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue

foreach ($report in $reports) {
    $suiteName = $report.FullName.Substring($output.Length).TrimStart('\').Split('\')[0]
    Write-Host ("  {0,-40} {1:N0} KB" -f $suiteName, ($report.Length / 1KB))
}

if ($reports.Count -ne $suites.Count) {
    Write-Host ''
    Write-Host "WARNING: $($suites.Count) suite(s) ran but $($reports.Count) cobertura file(s) exist."
    Write-Host '         A suite that passes while emitting no report reads as uncovered product code.'
}

if ($failed) {
    Write-Host ''
    Write-Host "FAILED: $($failed -join ', ')"
    exit 1
}
