#!/usr/bin/env pwsh
# Fails a run that reported success while silently skipping tests.
#
#   pwsh -NoProfile -File scripts/skip-audit.ps1 [-ResultsDirectory <dir>]
#
# Ported 2026-09-20 from JauntyQ's scripts/skip-audit.js (see
# docs/handoffs/2026-09-20-coverage-mutation-gaps-from-jaunty-probe.md in that repo), itself
# written after JauntyQ measured (2026-08-17) an 8-run sample where one run silently skipped 101
# tests across 3 assemblies and exited 0 - a bare `dotnet test` carries no record of what it
# skipped or why. Every jaunty CI step already logs a trx (`--report-trx`), so the same class of
# bug here would at least leave evidence; nothing yet reads that evidence and fails the run over
# it.
#
# STRUCTURAL DIFFERENCE FROM THE JAUNTYQ ORIGINAL: that script's regex targets a VSTest-classic
# <Message> element. Microsoft.Testing.Platform's --report-trx (which every jaunty test project
# uses; JauntyQ's does not, it's on the classic VSTest collector) writes a skip's reason into
# <Output><StdOut> instead - confirmed 2026-09-20 by forcing a real skip
# (FlatFiles.DuckDB.Tests's Caching_ImprovesPerformance under CI=true) and reading the emitted
# trx. A port that kept the <Message> lookup would silently match zero skips forever, which is
# worse than not existing: it would report "every skip is accounted for" without ever having
# looked at one.
#
# The rule: every skip must match a reason this repo has decided is legitimate. Anything else
# fails the run and prints the offending test names, so the NEXT occurrence arrives diagnosed
# instead of invisible.

[CmdletBinding()]
param(
    [string] $ResultsDirectory
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $ResultsDirectory) { $ResultsDirectory = $repoRoot }

# Each entry's reason has to justify itself - an allowlist nobody can explain is how a suite
# starts lying again, one forgotten line at a time. Sourced 2026-09-20 from every Assert.Skip /
# DataAttribute.Skip call site under tests/ at that date; a new skip message needs a new line
# here, not a wider existing pattern.
$script:AllowedSkips = @(
    @{ Pattern = '^(SQL Server|PostgreSQL|MariaDB/MySQL|MySQL) not configured\. Set '
       Why     = 'dialect connection string not configured (env-gated, optional engine)' }
    @{ Pattern = '^JAUNTY_TEST_(SQLSERVER|POSTGRESQL|MYSQL|MARIADB) not set$'
       Why     = 'source-gen integration test, env-gated live engine' }
    @{ Pattern = '^(SqlServer|Postgres|MariaDB) is configured but not reachable: '
       Why     = 'dialect configured but engine not running (dev-box only - CI sets JAUNTY_REQUIRE_* for the engines it provisions, which turns this into a failure instead of a skip)' }
    @{ Pattern = '^(SQL Server|MySQL|MariaDB/MySQL|PostgreSQL) not reachable: '
       Why     = 'OpenOrSkip-style connect probe failed (dev-box only, same JAUNTY_REQUIRE_* backstop)' }
    @{ Pattern = '^Microsoft\.Data\.Sqlite provider/native library is not available in this runtime\.$'
       Why     = 'provider not available on this runtime/TFM' }
    @{ Pattern = '^System\.Data\.SQLite provider is not available\.$'
       Why     = 'provider not available on this runtime/TFM' }
    @{ Pattern = '^Wall-clock performance thresholds are unreliable on shared CI runners\.$'
       Why     = 'intentional CI-only perf-test skip' }
    @{ Pattern = '^No customers available in seed data to exercise string-collection parameter binding\.$'
       Why     = 'known seed-data limitation, narrow exact match' }
)

function Get-TrxSkips {
    param([string] $Path)
    [xml] $doc = Get-Content -Raw -LiteralPath $Path
    $ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $ns.AddNamespace('tt', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $results = $doc.SelectNodes('//tt:UnitTestResult[@outcome="NotExecuted"]', $ns)
    $skips = @()
    foreach ($r in $results) {
        $stdout = $r.SelectSingleNode('./tt:Output/tt:StdOut', $ns)
        $reason = if ($stdout) { $stdout.InnerText.Trim() } else { '' }
        $skips += [pscustomobject]@{ Name = $r.testName; Reason = $reason }
    }
    return $skips
}

function Test-AuditSkips {
    param([array] $Skips)
    $tally = [ordered]@{}
    $unexplained = @()
    foreach ($skip in $Skips) {
        $hit = $script:AllowedSkips | Where-Object { $skip.Reason -match $_.Pattern } | Select-Object -First 1
        $key = if ($hit) { $hit.Why } else { 'UNEXPLAINED' }
        if ($tally.Contains($key)) { $tally[$key]++ } else { $tally[$key] = 1 }
        if (-not $hit) { $unexplained += $skip }
    }
    return [pscustomobject]@{ Total = $Skips.Count; Unexplained = $unexplained; Tally = $tally }
}

if ($MyInvocation.InvocationName -ne '.') {
    $trxFiles = Get-ChildItem -Path $ResultsDirectory -Recurse -Filter '*.trx' -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj|node_modules|\.git)[\\/]' }

    if (-not $trxFiles -or $trxFiles.Count -eq 0) {
        Write-Host "skip-audit: no .trx files found under $ResultsDirectory."
        Write-Host "The run produced no results to verify. Treating that as a failure."
        exit 1
    }

    $allSkips = @()
    foreach ($f in $trxFiles) { $allSkips += Get-TrxSkips -Path $f.FullName }

    $audit = Test-AuditSkips -Skips $allSkips

    Write-Host "skip-audit: $($trxFiles.Count) trx file(s), $($audit.Total) skipped test(s)."
    foreach ($key in ($audit.Tally.Keys | Sort-Object { $audit.Tally[$_] } -Descending)) {
        Write-Host ("  {0,4}  {1}" -f $audit.Tally[$key], $key)
    }

    if ($audit.Unexplained.Count -eq 0) {
        Write-Host "skip-audit: every skip is accounted for."
        exit 0
    }

    Write-Host ""
    Write-Host "skip-audit: $($audit.Unexplained.Count) skipped test(s) with no sanctioned reason." -ForegroundColor Red
    Write-Host "A run that skips these and exits 0 is reporting success for work it did not do." -ForegroundColor Red
    Write-Host ""

    # Grouped by reason: a wholesale assembly skip is one cause and many lines, and the cause is
    # the part worth reading.
    $byReason = $audit.Unexplained | Group-Object Reason
    foreach ($g in ($byReason | Sort-Object Count -Descending)) {
        $reasonText = if ($g.Name) { $g.Name } else { '<no reason recorded>' }
        Write-Host "  $($g.Count) test(s): $reasonText"
        foreach ($n in ($g.Group | Select-Object -First 3)) { Write-Host "      $($n.Name)" }
        if ($g.Count -gt 3) { Write-Host "      ... and $($g.Count - 3) more" }
        Write-Host ""
    }

    exit 1
}
