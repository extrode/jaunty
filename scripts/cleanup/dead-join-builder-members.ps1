<#
.SYNOPSIS
    Removes three dead members from JoinedQueryBuilder.cs, 2026-09-02.

.DESCRIPTION
    Left behind when the joined-select path moved from name-based mapping to ordinals. All three
    are `internal` and have no callers anywhere in the repository - src, tests, samples and tools
    were searched for each name, and for any file type, not only .cs:

      1. MapEntity<TEntity>(EntityMetadata, IDataReader, string prefix, Dictionary<string,int>)
         The three-argument ordinal overload above it is the live one and stays.
      2. BuildOrdinalLookup(IDataReader), together with the two doc comments attached to it. One
         of those crefs member 1, so removing 1 alone would not compile under warnings-as-errors.
      3. JoinParameterRenameRegexes, an entire internal static class with no references at all.

    docs/plans/2026-09-01-006-elegant-join-sql.md:253 already records 1 and 2 as stale artefacts
    left in place at the time. The same file says at :110 that they were "kept, not removed - they
    have direct unit tests and other callers"; that line is stale, and the search above is what
    supersedes it.

    This edits source rather than deleting files, so it verifies before it acts: each region is
    identified by line range AND by the SHA-256 of its exact text. Any drift in the file - even a
    whitespace change - fails the hash and the script refuses rather than cutting the wrong lines.
    It also refuses if the file has uncommitted changes, so that what it removes lands as one
    reviewable diff.

    It does not build, test, stage or commit. Run the suite yourself afterwards:
        dotnet test Jaunty.slnx -c Release -f net10.0

    Bare invocation is the dry run. -Execute is the only way to act.

    Invoke with:  pwsh -NoProfile -File scripts/cleanup/dead-join-builder-members.ps1
#>
[CmdletBinding()]
param(
    [Alias('e')]
    [switch]$Execute
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = (git rev-parse --show-toplevel)
if (-not $repoRoot) { Write-Host 'abort: not inside a git repository'; exit 1 }
Set-Location $repoRoot

$relative = 'src/Jaunty.Fluent/Builders/Join/JoinedQueryBuilder.cs'
$target = Join-Path $repoRoot $relative

# Ranges are 1-based and inclusive, and each one starts on the blank line before its member so no
# double blank is left behind. Highest first: removing a later range cannot shift an earlier one.
$regions = @(
    [pscustomobject]@{
        What  = 'internal static class JoinParameterRenameRegexes'
        First = 721
        Last  = 730
        Sha   = '67df063f2aef180dd25d9934ec59d0c31d2f29b687c78442fd90c11c5d057f3b'
    }
    [pscustomobject]@{
        What  = 'MapEntity(prefix, ordinals) and BuildOrdinalLookup, with their doc comments'
        First = 633
        Last  = 719
        Sha   = '19dc09f2f16435f7cf5c1eb4a463c08ce7503ede93bb1837dce4d7b27420d495'
    }
)

if (-not (Test-Path $target)) {
    Write-Host "abort: $relative is not present"
    exit 1
}

$dirty = (git status --porcelain -- $relative)
if ($dirty) {
    Write-Host "abort: $relative has uncommitted changes."
    Write-Host '       Commit or stash them first, so this removal lands as one reviewable diff.'
    exit 1
}

# Read and write with the file's own encoding and endings: it is UTF-8 with a BOM and CRLF, and
# rewriting it as anything else would show up as a whole-file diff.
$bytes = [System.IO.File]::ReadAllBytes($target)
$hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
$text = [System.Text.Encoding]::UTF8.GetString($bytes)
if ($hasBom) { $text = $text.Substring(1) }

$newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
$lines = [System.Collections.Generic.List[string]]::new()
$lines.AddRange([string[]]($text -split "`r`n|`n"))

$sha = [System.Security.Cryptography.SHA256]::Create()
$failed = $false

foreach ($region in $regions) {
    $count = $region.Last - $region.First + 1

    if ($region.Last -gt $lines.Count) {
        Write-Host "abort: $relative has only $($lines.Count) lines; expected at least $($region.Last)."
        $failed = $true
        break
    }

    $segment = ($lines.GetRange($region.First - 1, $count)) -join $newline
    $actual = ($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($segment)) |
        ForEach-Object { $_.ToString('x2') }) -join ''

    if ($actual -ne $region.Sha) {
        Write-Host "abort: lines $($region.First)-$($region.Last) of $relative are not what this"
        Write-Host '       script was written against. The file has moved on; re-derive the ranges.'
        Write-Host "       expected $($region.Sha)"
        Write-Host "       actual   $actual"
        $failed = $true
        break
    }

    if ($Execute) {
        Write-Host "run:   remove lines $($region.First)-$($region.Last) ($count lines) - $($region.What)"
        $lines.RemoveRange($region.First - 1, $count)
    } else {
        Write-Host "would: remove lines $($region.First)-$($region.Last) ($count lines) - $($region.What)"
    }
}

if ($failed) { exit 1 }

if ($Execute) {
    $rewritten = ($lines -join $newline)
    $encoding = [System.Text.UTF8Encoding]::new($hasBom)
    [System.IO.File]::WriteAllText($target, $rewritten, $encoding)

    Write-Host ''
    Write-Host "removed 97 lines from $relative."
    Write-Host 'Nothing was built, staged or committed. Run the suite before you commit:'
    Write-Host '    dotnet test Jaunty.slnx -c Release -f net10.0'
} else {
    Write-Host ''
    Write-Host 'dry run. Re-run with -Execute to act.'
}

exit 0
