# Cleanup for spec 010 (.NET 10 migration), written 2026-07-30 by the session that ran T8-T21.
# Dry run by default: bare invocation prints what it would do and changes nothing.
# --execute / -e is the only way to act.
#
# Scope: ONLY the branches this body of work created and merged into dev. The many older
# stale branches (audit rounds, r26 merges, net472 guards, ...) belong to earlier work and
# are deliberately not touched here.
#
# NOT removed, deliberately:
#   - probe/net10-feasibility - 010-spec.md §8 names it as the evidence record, keep it.
#   - docs/benchmark-artifacts/ - gitignored benchmark results; T20's delta references them.
#   - %TEMP%\jaunty-scratch\ac5 - AC5 sample publishes; session scratch, session owns it.

param(
    [Alias('e')][switch]$Execute
)

$ErrorActionPreference = 'Stop'

# -- 0. Safety gate ---------------------------------------------------------
$branch = git branch --show-current
if ($branch -ne 'dev') {
    Write-Host "Refusing to run: current branch is '$branch', expected 'dev'." -ForegroundColor Red
    if ($Execute) { exit 1 } else { exit 0 }
}

# -- 1. Merged 010 branches -------------------------------------------------
$branches = @(
    'chore/aot-scanner-markers-only'
    'chore/ci-dedupe-dev-runs'
    'chore/010-t17-ci-no-incremental'
    'chore/010-t8-baseline-record'
    'chore/010-t14-full-clean-build'
    'feat/010-t11-net10-pins'
    'feat/010-t13-loader-assertion'
    'feat/010-t15-t16-publish-diagnostics'
    'feat/010-t18-net10-ci-legs'
    'feat/010-t19-aot-publish-leg'
    'feat/010-t21-close-out'
    # Same-session follow-ups (2026-07-30), merged to dev alongside 010:
    'fix/mysql-schemareader-il2057'
    'fix/sqldialectfactory-order-flake'
    'chore/dependabot-runtime-bumps'
)

$failed = $false
foreach ($b in $branches) {
    git show-ref --verify --quiet "refs/heads/$b"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  absent (already gone): $b"
        continue
    }
    if ($Execute) {
        # -d, never -D: refuses if the branch is not fully merged.
        git branch -d $b
        if ($LASTEXITCODE -ne 0) { Write-Host "  FAILED (unmerged?): $b" -ForegroundColor Red; $failed = $true }
    } else {
        Write-Host "  [dry run] git branch -d $b"
    }
}

# -- 2. Result --------------------------------------------------------------
Write-Host ""
Write-Host "Remaining local branches:"
git branch

if (-not $Execute) {
    Write-Host ""
    Write-Host "Dry run only. Re-run with --execute (-e) to apply."
    exit 0
}
if ($failed) { exit 1 }
Write-Host "Done."
