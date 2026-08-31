# Line-coverage run over the tests/ suites, emitting cobertura for the coverage x complexity
# cross-reference in docs/plans/2026-08-27-003-testing-strategy-implementation.md.
#
#   pwsh -NoProfile -File scripts/coverage.ps1              # all suites
#   pwsh -NoProfile -File scripts/coverage.ps1 -Suite Jaunty.Tests
#
# Reports land in tmp/coverage/<suite>/ (gitignored). Nothing is deleted; re-runs overwrite.
#
# The four samples/ torture-test ports are deliberately out of scope: they live outside
# Jaunty.slnx, are not part of `dotnet test Jaunty.slnx`, and carry no coverlet reference.

[CmdletBinding()]
param(
    [string] $Suite,
    [string] $Configuration = 'Release',
    [string] $Framework = 'net10.0'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repoRoot 'tmp\coverage'
$settings = Join-Path $repoRoot 'coverage.runsettings'

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

    dotnet test $project `
        -c $Configuration -f $Framework `
        --settings $settings `
        --collect:"XPlat Code Coverage" `
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
