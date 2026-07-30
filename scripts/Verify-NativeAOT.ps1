#!/usr/bin/env pwsh
# NativeAOT Compatibility Verification Script for Jaunty
#
# Scans src/ for reflection patterns that break NativeAOT compilation. A site passes ONLY
# if a reviewed `AOT-SAFE:` comment sits on the match line or in the contiguous comment
# block directly above it, stating why the site survives trimming.
#
# Rewritten 2026-07-30. The previous version allowed 12 of its 17 findings by matching
# keep-patterns against WHOLE-FILE content (any file containing the string "IMapped" got a
# blanket pass) or path substrings ("ParameterCache.cs" also covered WriteParameterCache.cs).
# MappedCache.cs - the file whose trimmed-mapper defect spec 009 fixed - was structurally
# incapable of failing it. It also counted prose in doc comments as reflection, matched
# GetGetMethod( as GetMethod(, silently skipped any file with "Extensions" in its path, and
# scanned only src/Jaunty. All of that is gone:
#   - one allow mechanism: the adjacent AOT-SAFE marker, nothing pattern- or path-based
#   - all src/ projects scanned except the two where AOT does not apply (printed below)
#   - // comment lines are not scanned, so prose cannot register as reflection
#   - patterns are word-anchored, so GetGetMethod( no longer matches GetMethod(
#   - a marker elsewhere in the file but not adjacent is reported, not silently ignored
#     (that misplacement cost two rebuild cycles on 2026-07-30)
#
# This is still a text gate, not an AOT proof: only publishing and running the binary proves
# anything. Its job is to force every reflective call to carry a reviewed justification and
# to fail loudly when a new one appears without one.

param(
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'

# Reflection patterns that break (or flag) NativeAOT. Word-anchored: \b keeps
# GetGetMethod( from matching GetMethod(.
$ErrorPatterns = @(
    @{ Pattern = '\bGetMethod\(';                  Description = 'Reflection: GetMethod' },
    @{ Pattern = '\bGetMethods\(';                 Description = 'Reflection: GetMethods' },
    @{ Pattern = '\bGetProperties\(';              Description = 'Reflection: GetProperties' },
    @{ Pattern = '\bGetFields\(';                  Description = 'Reflection: GetFields' },
    @{ Pattern = '\bActivator\.CreateInstance\(';  Description = 'Reflection: Activator.CreateInstance' },
    @{ Pattern = '\bMakeGenericType\(';            Description = 'Reflection: MakeGenericType' },
    @{ Pattern = '\bMakeGenericMethod\(';          Description = 'Reflection: MakeGenericMethod' },
    @{ Pattern = '\bCreateDelegate\(';             Description = 'Reflection: CreateDelegate' },
    @{ Pattern = '\bRequiresUnreferencedCode\b';   Description = 'Attribute: RequiresUnreferencedCode' },
    @{ Pattern = '\bRequiresDynamicCode\b';        Description = 'Attribute: RequiresDynamicCode' }
)

# Projects where AOT does not apply, excluded by exact directory name and printed on
# every run so the exclusion is visible rather than implicit.
$ExcludedProjects = @(
    @{ Name = 'Jaunty.Extensions.Reflection'; Reason = 'reflection is its stated purpose; consumers opt out of AOT by referencing it' },
    @{ Name = 'Jaunty.SourceGenerator';       Reason = 'netstandard2.0 Roslyn component; runs inside the compiler, never published AOT' }
)

Write-Host ""
Write-Host "NativeAOT Compatibility Verification for Jaunty" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptPath
$srcRoot = Join-Path $projectRoot "src"

if (-not (Test-Path $srcRoot)) {
    Write-Host "Error: Could not find src directory at $srcRoot" -ForegroundColor Red
    exit 1
}

$excludedNames = $ExcludedProjects | ForEach-Object { $_.Name }
$scanDirs = Get-ChildItem -Path $srcRoot -Directory | Where-Object { $excludedNames -notcontains $_.Name }

Write-Host "Scanning:" -ForegroundColor Gray
$scanDirs | ForEach-Object { Write-Host "  src/$($_.Name)" -ForegroundColor Gray }
Write-Host "Excluded (AOT does not apply):" -ForegroundColor Gray
$ExcludedProjects | ForEach-Object { Write-Host "  src/$($_.Name) - $($_.Reason)" -ForegroundColor Gray }
Write-Host ""

$issues = @()
$allowed = @()
$staleMarkers = @()

function Test-CommentLine([string]$line) {
    $t = $line.TrimStart()
    return $t.StartsWith('//') -or $t.StartsWith('*') -or $t.StartsWith('/*')
}

foreach ($dir in $scanDirs) {
    $files = Get-ChildItem -Path $dir.FullName -Recurse -Filter "*.cs" |
             Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }

    foreach ($file in $files) {
        $lines = Get-Content $file.FullName
        $relativePath = $file.FullName.Replace($projectRoot, '').TrimStart('\', '/')

        # Line numbers (1-based) of every AOT-SAFE marker in the file, so a marker that
        # ends up allowing nothing can be reported as stale/misplaced.
        $markerLines = @()
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -cmatch '(?<!\w)AOT-SAFE:') { $markerLines += ($i + 1) }
        }
        $usedMarkers = @{}

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            $lineNum = $i + 1

            # Comment lines are never scanned: prose describing reflection is not reflection.
            if (Test-CommentLine $line) { continue }

            foreach ($patternInfo in $ErrorPatterns) {
                if ($line -notmatch $patternInfo.Pattern) { continue }

                # A site is allowed only by an AOT-SAFE marker on the match line or in the
                # contiguous comment block directly above it (no blank lines in between).
                $isAllowed = $false
                $allowedReason = ""
                if ($line -cmatch '(?<!\w)AOT-SAFE:') {
                    $isAllowed = $true
                    $allowedReason = ($line -creplace '.*(?<!\w)AOT-SAFE:\s*', '').Trim()
                    $usedMarkers[$lineNum] = $true
                }
                else {
                    for ($j = $i - 1; $j -ge 0; $j--) {
                        if (-not (Test-CommentLine $lines[$j])) { break }
                        if ($lines[$j] -cmatch '(?<!\w)AOT-SAFE:') {
                            $isAllowed = $true
                            $allowedReason = ($lines[$j] -creplace '.*(?<!\w)AOT-SAFE:\s*', '').Trim()
                            $usedMarkers[$j + 1] = $true
                            break
                        }
                    }
                }

                if ($isAllowed) {
                    $allowed += [PSCustomObject]@{
                        File = $relativePath
                        Line = $lineNum
                        Issue = $patternInfo.Description
                        Reason = $allowedReason
                    }
                } else {
                    $hint = ""
                    if ($markerLines.Count -gt 0) {
                        $hint = "file has AOT-SAFE marker(s) at line(s) $($markerLines -join ', ') but none adjacent to this site - the marker must be on the match line or in the comment block directly above it"
                    }
                    $issues += [PSCustomObject]@{
                        File = $relativePath
                        Line = $lineNum
                        Issue = $patternInfo.Description
                        Context = $line.Trim()
                        Hint = $hint
                    }
                }
            }
        }

        foreach ($m in $markerLines) {
            if (-not $usedMarkers.ContainsKey($m)) {
                $staleMarkers += [PSCustomObject]@{ File = $relativePath; Line = $m }
            }
        }
    }
}

# A marker that allows nothing is drift: either the call it covered moved away from it,
# or it was placed where the scanner does not look. Reported always, fails nothing.
if ($staleMarkers.Count -gt 0) {
    Write-Host "WARNING: $($staleMarkers.Count) AOT-SAFE marker(s) not adjacent to any reflection site:" -ForegroundColor Yellow
    $staleMarkers | ForEach-Object { Write-Host "  $($_.File):$($_.Line)" -ForegroundColor Yellow }
    Write-Host ""
}

if ($issues.Count -eq 0) {
    Write-Host "PASS: all $($allowed.Count) reflection sites carry a reviewed AOT-SAFE justification." -ForegroundColor Green

    if ($Verbose -and $allowed.Count -gt 0) {
        Write-Host ""
        Write-Host "Reviewed reflection inventory:" -ForegroundColor Cyan
        $allowed | Format-Table -AutoSize | Out-String | Write-Host
    }

    exit 0
}

Write-Host "FAIL: $($issues.Count) reflection site(s) without an adjacent AOT-SAFE justification:" -ForegroundColor Red
Write-Host ""

foreach ($group in ($issues | Group-Object -Property File)) {
    Write-Host "File: $($group.Name)" -ForegroundColor Yellow
    foreach ($issue in $group.Group) {
        Write-Host "   Line $($issue.Line): $($issue.Issue)" -ForegroundColor Red
        Write-Host "      $($issue.Context)" -ForegroundColor Gray
        if ($issue.Hint) {
            Write-Host "      NOTE: $($issue.Hint)" -ForegroundColor Yellow
        }
    }
    Write-Host ""
}

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Each site needs a comment on the line above it (or the comment block directly" -ForegroundColor White
Write-Host "above) reading 'AOT-SAFE: <why this survives trimming>'. If it cannot be" -ForegroundColor White
Write-Host "justified, the call belongs in Jaunty.Extensions.Reflection or behind source gen." -ForegroundColor White
Write-Host ""

exit $issues.Count
