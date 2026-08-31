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
    (Join-Path $repoRoot 'tmp\product-identity-brief.md'),
    (Join-Path $repoRoot 'tmp\jauntyq-pricing-brief.md'),
    # Superseded 2026-08-29: this handoff was rewritten into the JauntyQ repo at
    # docs/decisions/2026-08-29-003-commercial-state-and-enforceability.md, which is
    # the version carrying the enforceability answer. Leaving both risks the stale
    # copy being read as current.
    (Join-Path $env:USERPROFILE 'private-archive.md')
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
