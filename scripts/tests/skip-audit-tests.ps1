#!/usr/bin/env pwsh
# Tests for scripts/skip-audit.ps1, written 2026-09-20 alongside the port from JauntyQ.
#
# Case 2 pins the exact defect a naive port would have shipped with: MTP's --report-trx puts a
# skip's reason in <Output><StdOut>, not the VSTest-classic <Message> element the JauntyQ original
# reads. A regression back to reading <Message> would make every case here report zero skips
# found instead of failing loudly, so case 2 also asserts the skip count, not just the exit code.

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$scriptsDir = Split-Path -Parent $scriptDir
$auditor = Join-Path $scriptsDir 'skip-audit.ps1'

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("skip-audit-test-" + [guid]::NewGuid())
New-Item -ItemType Directory -Force -Path $work | Out-Null

function New-Trx {
    param([string] $Path, [string[]] $Results)
    $body = $Results -join "`n"
    @"
<?xml version="1.0" encoding="UTF-8"?>
<TestRun id="00000000-0000-0000-0000-000000000000" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results>
$body
  </Results>
</TestRun>
"@ | Set-Content -LiteralPath $Path -Encoding UTF8
}

function New-PassedResult([string] $Name) {
    return "    <UnitTestResult testName=""$Name"" outcome=""Passed"" />"
}

function New-SkippedResult([string] $Name, [string] $Reason) {
    return @"
    <UnitTestResult testName="$Name" outcome="NotExecuted">
      <Output>
        <StdOut>$Reason</StdOut>
      </Output>
    </UnitTestResult>
"@
}

$failures = 0
function Assert-Exit([string]$name, [int]$expected, [scriptblock]$setup) {
    Get-ChildItem $work -Filter '*.trx' -ErrorAction SilentlyContinue | Remove-Item -Force
    & $setup
    $output = & pwsh -NoProfile -File $auditor -ResultsDirectory $work 2>&1
    $actual = $LASTEXITCODE
    if ($actual -eq $expected) {
        Write-Host "PASS $name" -ForegroundColor Green
    } else {
        Write-Host "FAIL $name - expected exit $expected, got $actual" -ForegroundColor Red
        Write-Host ($output -join "`n")
        $script:failures++
    }
    return $output
}

Assert-Exit 'no trx files fails' 1 { }

Assert-Exit 'all passed, no skips' 0 {
    New-Trx (Join-Path $work 'a.trx') @((New-PassedResult 'T1'), (New-PassedResult 'T2'))
}

$out = Assert-Exit 'allowlisted skip passes and is counted' 0 {
    New-Trx (Join-Path $work 'a.trx') @(
        (New-PassedResult 'T1'),
        (New-SkippedResult 'Extrode.Jaunty.Tests.Foo' 'SQL Server not configured. Set JAUNTY_TEST_SQLSERVER or ConnectionStrings:SqlServer.')
    )
}
if (($out -join "`n") -notmatch '1 skipped test') {
    Write-Host "FAIL allowlisted skip passes and is counted - expected the skip to be tallied, got:" -ForegroundColor Red
    Write-Host ($out -join "`n")
    $script:failures++
}

Assert-Exit 'dialect "configured but not reachable" uses DialectInfo.Name, not the constructor prose name' 0 {
    # Pins a real bug caught by running this against the full Extrode.Jaunty.Tests suite: the
    # "configured but not reachable" message interpolates DialectInfo.Name ("Postgres",
    # "MariaDB", "SqlServer"), not the "PostgreSQL"/"MariaDB/MySQL" prose used in the separate
    # "not configured" message. A pattern written against the wrong one silently reported ~1620
    # legitimate skips as UNEXPLAINED.
    New-Trx (Join-Path $work 'a.trx') @(
        (New-SkippedResult 'T1' 'Postgres is configured but not reachable: Failed to connect to 127.0.0.1:5433'),
        (New-SkippedResult 'T2' 'MariaDB is configured but not reachable: Unable to connect to any of the specified MySQL hosts')
    )
}

Assert-Exit 'unrecognised skip reason fails' 1 {
    New-Trx (Join-Path $work 'a.trx') @(
        (New-PassedResult 'T1'),
        (New-SkippedResult 'Extrode.Jaunty.Tests.Foo' 'Something nobody wrote an allowlist entry for')
    )
}

Assert-Exit 'skip with no recorded reason at all fails (silence cannot be its own excuse)' 1 {
    New-Trx (Join-Path $work 'a.trx') @(
        (New-PassedResult 'T1'),
        '    <UnitTestResult testName="Extrode.Jaunty.Tests.Bar" outcome="NotExecuted" />'
    )
}

Assert-Exit 'CI-only perf skip is allowlisted' 0 {
    New-Trx (Join-Path $work 'a.trx') @(
        (New-SkippedResult 'Extrode.Jaunty.FlatFiles.DuckDB.Tests.Caching_ImprovesPerformance' 'Wall-clock performance thresholds are unreliable on shared CI runners.')
    )
}

Assert-Exit 'skips split across multiple trx files are all read' 1 {
    New-Trx (Join-Path $work 'a.trx') @((New-SkippedResult 'T1' 'SQL Server not configured. Set JAUNTY_TEST_SQLSERVER or ConnectionStrings:SqlServer.'))
    New-Trx (Join-Path $work 'b.trx') @((New-SkippedResult 'T2' 'unexplained reason'))
}

Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue

if ($failures -gt 0) {
    Write-Host "$failures test(s) failed" -ForegroundColor Red
    exit 1
}
Write-Host "All skip-audit tests passed" -ForegroundColor Green
exit 0
