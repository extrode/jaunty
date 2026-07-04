#!/usr/bin/env pwsh
# NativeAOT Compatibility Verification Script for Jaunty
# Scans Jaunty assembly source code for reflection patterns that break NativeAOT compilation.

param(
    [switch]$Verbose,
    [switch]$FixSuggestions
)

$ErrorActionPreference = 'Stop'

# Patterns that indicate reflection usage (potential NativeAOT issues)
$ErrorPatterns = @(
    @{ Pattern = 'GetMethod\('; Description = 'Reflection: GetMethod' },
    @{ Pattern = 'GetProperties\('; Description = 'Reflection: GetProperties' },
    @{ Pattern = 'GetFields\('; Description = 'Reflection: GetFields' },
    @{ Pattern = 'Activator\.CreateInstance\('; Description = 'Reflection: Activator.CreateInstance' },
    @{ Pattern = 'MakeGenericType\('; Description = 'Reflection: MakeGenericType' },
    @{ Pattern = 'CreateDelegate\('; Description = 'Reflection: CreateDelegate' },
    @{ Pattern = 'RequiresUnreferencedCode'; Description = 'Attribute: RequiresUnreferencedCode' },
    @{ Pattern = 'RequiresDynamicCode'; Description = 'Attribute: RequiresDynamicCode' }
)

# Patterns that indicate acceptable reflection (source-generated paths)
$KeepPatterns = @(
    @{ Pattern = 'IMapped'; Description = 'Source-generated mapper interface' },
    @{ Pattern = 'ReadEntity'; Description = 'Source-generated ReadEntity method' },
    @{ Pattern = 'BindParameters'; Description = 'Source-generated BindParameters method' },
    @{ Pattern = 'MappedCache'; Description = 'Source-generated mapper cache' }
)

# Patterns that require documentation (trim-safe with proper setup)
$DocumentPatterns = @(
    @{ Pattern = 'Jaunty.Init.cs'; Description = 'Extension loading (NativeAOT-safe with try-catch)' },
    @{ Pattern = 'ParameterCache.cs'; Description = 'Anonymous type binding (trim-safe)' }
)

Write-Host ""
Write-Host "NativeAOT Compatibility Verification for Jaunty" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptPath
$jauntySrc = Join-Path $projectRoot "src\Jaunty"

if (-not (Test-Path $jauntySrc)) {
    Write-Host "Error: Could not find Jaunty source directory at $jauntySrc" -ForegroundColor Red
    exit 1
}

Write-Host "Scanning: $jauntySrc" -ForegroundColor Gray
Write-Host ""

$files = Get-ChildItem -Path $jauntySrc -Recurse -Filter "*.cs" | 
         Where-Object { $_.FullName -notmatch 'Extensions' -and $_.FullName -notmatch 'obj' -and $_.FullName -notmatch 'bin' }

$issues = @()
$acceptableIssues = @()

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $relativePath = $file.FullName.Replace($projectRoot, '').TrimStart('\', '/')
    
    foreach ($patternInfo in $ErrorPatterns) {
        $pattern = $patternInfo.Pattern
        $description = $patternInfo.Description
        
        $matches = [regex]::Matches($content, $pattern)
        foreach ($match in $matches) {
            # Calculate line number
            $lineNum = ($content.Substring(0, $match.Index) -split "`n").Count
            
            # Get context (100 chars before and after)
            $startIndex = [Math]::Max(0, $match.Index - 100)
            $contextLength = [Math]::Min(200, $content.Length - $startIndex)
            $context = $content.Substring($startIndex, $contextLength) -replace "`r?`n", ' '
            
            # Check if this is in an acceptable context (check full file content)
            $isAllowed = $false
            $allowedReason = ""
            $requiresDocumentation = $false

            # Inline suppression: a reviewed call site carries an AOT-SAFE
            # comment on its own line or the line above, stating WHY it is
            # safe (graceful fallback, DynamicallyAccessedMembers, etc.).
            $lines = $content -split "`n"
            $matchLine = if ($lineNum -le $lines.Count) { $lines[$lineNum - 1] } else { "" }
            $prevLine = if ($lineNum -ge 2) { $lines[$lineNum - 2] } else { "" }
            if ($matchLine -match 'AOT-SAFE:' -or $prevLine -match 'AOT-SAFE:') {
                $requiresDocumentation = $true
                $reasonLine = if ($matchLine -match 'AOT-SAFE:') { $matchLine } else { $prevLine }
                $allowedReason = ($reasonLine -replace '.*AOT-SAFE:\s*', '').Trim()
            }
            
            foreach ($keepPattern in $KeepPatterns) {
                if ($content -match $keepPattern.Pattern) {
                    $isAllowed = $true
                    $allowedReason = $keepPattern.Description
                    break
                }
            }
            
            if (-not $isAllowed) {
                foreach ($docPattern in $DocumentPatterns) {
                    if ($relativePath -match $docPattern.Pattern) {
                        $requiresDocumentation = $true
                        $allowedReason = $docPattern.Description
                        break
                    }
                }
            }
            
            if ($isAllowed -or $requiresDocumentation) {
                if ($Verbose) {
                    $acceptableIssues += [PSCustomObject]@{
                        File = $relativePath
                        Line = $lineNum
                        Issue = $description
                        Reason = $allowedReason
                        RequiresDocs = $requiresDocumentation
                    }
                }
            } else {
                $issues += [PSCustomObject]@{
                    File = $relativePath
                    Line = $lineNum
                    Issue = $description
                    Context = $context.Trim()
                }
            }
        }
    }
}

# Report results
if ($issues.Count -eq 0) {
    Write-Host "PASS: No NativeAOT issues found!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Jaunty is ready for NativeAOT compilation." -ForegroundColor Cyan
    
    if ($acceptableIssues.Count -gt 0 -and $Verbose) {
        Write-Host ""
        Write-Host "Note: $($acceptableIssues.Count) acceptable reflection usages found:" -ForegroundColor Yellow
        $acceptableIssues | Format-Table -AutoSize | Out-String | Write-Host
    }
    
    exit 0
}

Write-Host "FAIL: Found $($issues.Count) potential NativeAOT issues:" -ForegroundColor Red
Write-Host ""

# Group by file for better readability
$groupedIssues = $issues | Group-Object -Property File

foreach ($group in $groupedIssues) {
    Write-Host "File: $($group.Name)" -ForegroundColor Yellow
    foreach ($issue in $group.Group) {
        Write-Host "   Line $($issue.Line): $($issue.Issue)" -ForegroundColor Red
        if ($FixSuggestions) {
            Write-Host "      Context: $($issue.Context.Substring(0, [Math]::Min(80, $issue.Context.Length)))..." -ForegroundColor Gray
        }
    }
    Write-Host ""
}

Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Recommendations:" -ForegroundColor Cyan
Write-Host "1. Move reflection-based code to Jaunty.Extensions.Reflection" -ForegroundColor White
Write-Host "2. Use source generators for performance-critical paths" -ForegroundColor White
Write-Host "3. Add DynamicDependency attributes for trim-safe reflection" -ForegroundColor White
Write-Host ""
Write-Host "Run with -FixSuggestions for more context on each issue." -ForegroundColor Gray
Write-Host ""

exit $issues.Count
