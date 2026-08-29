# Cleanup for the product-identity README work (2026-08-29).
#
# Bare invocation is a dry run. Pass -Execute (or -e) to act.
# Nothing here is irreversible: every item is scratch this work created under the repo's own tmp/.

[CmdletBinding()]
param(
    [Alias('e')]
    [switch]$Execute
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

$targets = @(
    (Join-Path $repoRoot 'tmp\sqlprobe'),
    (Join-Path $repoRoot 'tmp\product-identity-brief.md')
)

foreach ($target in $targets) {
    if (-not (Test-Path -LiteralPath $target)) {
        Write-Host "skip (absent) : $target"
        continue
    }

    if ($Execute) {
        Remove-Item -LiteralPath $target -Recurse -Force
        Write-Host "removed       : $target"
    }
    else {
        Write-Host "would remove  : $target"
    }
}

if (-not $Execute) {
    Write-Host ''
    Write-Host 'Dry run. Re-run with -Execute to remove the items above.'
}
