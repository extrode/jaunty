# Written 2026-09-24 by Claude. Rendering the /code-review HTML report with
# ~/.claude/tools/html/build.js wrote a hub page and its assets into this repo's docs/, beside
# the real docs. The report itself is tmp/review-dev.html and is not touched. Removes only the
# files the renderer added, each checked against git at run time; docs/assets/ itself is tracked
# and stays.
#
# Dry run (default):  pwsh scripts/cleanup/review-artifact-docs-output.ps1
# Apply:              pwsh scripts/cleanup/review-artifact-docs-output.ps1 -Execute
param([Alias('e')][switch]$Execute)
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$targets = @(
    'docs/index.html',
    'docs/assets/artifact.css',
    'docs/assets/artifact.js',
    'docs/assets/LICENSE-fonts.txt',
    'docs/assets/fonts'
)

Write-Host ($(if ($Execute) { '== apply ==' } else { '== dry run (pass -Execute to apply) ==' }))

# 1. Gate: must be this repo.
$top = (git -C $repo rev-parse --show-toplevel 2>$null)
if (-not $top -or -not (Test-Path (Join-Path $repo 'Jaunty.slnx'))) {
    Write-Host "refuse: $repo is not the Jaunty repo."; exit 1
}

$failed = $false

# 2. Each target.
foreach ($rel in $targets) {
    $path = Join-Path $repo $rel
    if (-not (Test-Path $path)) {
        Write-Host "skip:  $rel is already gone"
        continue
    }
    $tracked = git -C $repo ls-files -- $rel
    if ($tracked) {
        Write-Host "refuse: $rel is tracked by git. A cleanup script does not delete committed"
        Write-Host "        content; propose it as a commit instead. Left alone."
        $failed = $true
        continue
    }
    if ($Execute) {
        Write-Host "-- remove $rel"
        Remove-Item -LiteralPath $path -Recurse -Force
    } else {
        Write-Host "would remove $rel"
    }
}

# 3. What is left.
Write-Host '== untracked under docs/ =='
git -C $repo status --short --untracked-files=all -- docs

if ($Execute -and $failed) { exit 1 }
