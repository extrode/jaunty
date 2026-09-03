<#
.SYNOPSIS
    Cleanup for the theme-agnostic-diagrams work, 2026-09-03.

.DESCRIPTION
    Written by the task that made docs/_assets/benchmarks/comparison.svg follow the reader's
    colour scheme and added diagrams to streaming-methods.md, bulk-copy-methods.md and the new
    dialect-resolution.md.

    That task dropped the only reference to docs/_assets/mapper-ladder.svg. The image was a
    hand-drawn copy of the mermaid ladder printed directly above it on why-jaunty.md, and unlike
    the mermaid it is dark-only: it is embedded as <img>, so the page's custom properties never
    reach it and a light-theme reader saw a black panel. The mermaid block renders the same five
    rungs and themes itself, so the image is now unreferenced.

    Nothing else this task produced is a candidate. The scratch under tmp/diagrams/ was removed
    by the task that made it.

    Bare invocation is the dry run. -Execute is the only way to act.

    Invoke with:  pwsh -NoProfile -File scripts/cleanup/theme-agnostic-diagrams.ps1
#>
[CmdletBinding()]
param(
    [Alias('e')]
    [switch]$Execute
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$target   = Join-Path $repoRoot 'docs/_assets/mapper-ladder.svg'

Write-Host ''
Write-Host 'Cleanup: theme-agnostic-diagrams' -ForegroundColor Cyan
Write-Host ''

if (-not (Test-Path $target)) {
    Write-Host '  already gone: docs/_assets/mapper-ladder.svg'
    Write-Host ''
    exit 0
}

$refs = @(Select-String -Path (Join-Path $repoRoot '*.md'), (Join-Path $repoRoot 'docs/**/*.md') `
    -Pattern 'mapper-ladder\.svg' -SimpleMatch:$false -ErrorAction SilentlyContinue)

if ($refs.Count -gt 0) {
    Write-Host '  REFUSING: mapper-ladder.svg is referenced again by:' -ForegroundColor Yellow
    $refs | ForEach-Object { Write-Host "    $($_.Path):$($_.LineNumber)" }
    Write-Host ''
    Write-Host '  Someone linked it after this script was written. Nothing removed.'
    Write-Host ''
    exit 1
}

$size = [math]::Round((Get-Item $target).Length / 1KB, 1)

if (-not $Execute) {
    Write-Host "  would remove: docs/_assets/mapper-ladder.svg ($size KB)"
    Write-Host ''
    Write-Host '  Dry run. Pass -Execute to act.' -ForegroundColor DarkGray
    Write-Host ''
    exit 0
}

Remove-Item $target -Force
Write-Host "  removed: docs/_assets/mapper-ladder.svg ($size KB)" -ForegroundColor Green
Write-Host ''
Write-Host '  It is still in git history; git checkout HEAD~1 -- the path restores it.'
Write-Host ''
