#!/usr/bin/env pwsh
# Tests for scripts/Verify-NativeAOT.ps1, written 2026-07-30 with its markers-only rewrite.
#
# Each case perturbs the tree with a temp probe file, runs the scanner, and asserts the exit
# code. Cases 2-5 pin the specific defects the rewrite fixed, so a regression to any of them
# turns this red. The probe is created and deleted here; a failure mid-run leaves it behind,
# and the next run overwrites it.

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$scanner = Join-Path (Split-Path -Parent $scriptDir) 'Verify-NativeAOT.ps1'
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)
$probe = Join-Path $repoRoot 'src\Jaunty\ScannerTestProbeExtensions.cs'

$failures = 0
function Assert-Exit([string]$name, [int]$expected, [scriptblock]$setup) {
    & $setup
    & pwsh -NoProfile -File $scanner *> $null
    $actual = $LASTEXITCODE
    if ($actual -eq $expected) {
        Write-Host "PASS $name" -ForegroundColor Green
    } else {
        Write-Host "FAIL $name - expected exit $expected, got $actual" -ForegroundColor Red
        $script:failures++
    }
}

Assert-Exit 'baseline tree passes' 0 { }

# The filename ends in Extensions.cs deliberately: the pre-rewrite scanner silently skipped
# any path containing "Extensions", so this case also pins that fix.
Assert-Exit 'unmarked reflection fails, even in a *Extensions.cs file' 1 {
    Set-Content $probe @'
using System.Reflection;
namespace Jaunty;
internal static class ScannerTestProbe
{
    public static PropertyInfo[] P(object o) => o.GetType().GetProperties();
}
'@
}

Assert-Exit 'lowercase prose "NativeAOT-safe:" does not allow a site' 1 {
    Set-Content $probe @'
using System.Reflection;
namespace Jaunty;
internal static class ScannerTestProbe
{
    // This helper is NativeAOT-safe: it only reads.
    public static PropertyInfo[] P(object o) => o.GetType().GetProperties();
}
'@
}

Assert-Exit 'marker separated by a blank line does not allow a site' 1 {
    Set-Content $probe @'
using System.Reflection;
namespace Jaunty;
internal static class ScannerTestProbe
{
    // AOT-SAFE: misplaced, a blank line breaks adjacency.

    public static PropertyInfo[] P(object o) => o.GetType().GetProperties();
}
'@
}

Assert-Exit 'adjacent marker allows a site' 0 {
    Set-Content $probe @'
using System.Reflection;
namespace Jaunty;
internal static class ScannerTestProbe
{
    // AOT-SAFE: test probe; deleted by the test that wrote it.
    public static PropertyInfo[] P(object o) => o.GetType().GetProperties();
}
'@
}

Assert-Exit 'GetGetMethod( is not counted as GetMethod(' 0 {
    Set-Content $probe @'
using System.Reflection;
namespace Jaunty;
internal static class ScannerTestProbe
{
    public static MethodInfo? P(PropertyInfo p) => p.GetGetMethod();
}
'@
}

Assert-Exit 'prose in a doc comment is not counted as reflection' 0 {
    Set-Content $probe @'
namespace Jaunty;
/// <summary>Historically used <c>typeof(T).GetMethod(name)</c>; no longer.</summary>
internal static class ScannerTestProbe
{
}
'@
}

Remove-Item $probe -ErrorAction SilentlyContinue
Assert-Exit 'tree restored, still passes' 0 { }

Write-Host ""
if ($failures -eq 0) {
    Write-Host "All scanner tests passed." -ForegroundColor Green
    exit 0
}
Write-Host "$failures scanner test(s) failed." -ForegroundColor Red
exit $failures
