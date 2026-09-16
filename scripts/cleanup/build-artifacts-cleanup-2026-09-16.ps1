#!/usr/bin/env pwsh
# build-artifacts-cleanup-2026-09-16.ps1
#
# Written 2026-09-16 during a machine-wide disk-space audit that found this
# project carrying 5.50 GB of build/tool artifacts (bin, obj, node_modules,
# dist, and similar directories -- see the list below). All of these are
# produced by the project's own build/install step and are safe to delete;
# each one is still re-checked against git at run time before being touched,
# so nothing tracked is ever removed even if this list goes stale.
#
# If your build layout changes (new artifact dirs, moved output paths,
# renamed projects), UPDATE THE LIST BELOW rather than leaving this stale --
# that is the whole point of keeping this script instead of doing this by
# hand once.
#
# Dry run is the default. --execute/-e authorises deletion.

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$Execute = $false
foreach ($a in $args) {
  switch ($a) {
    '--execute' { $Execute = $true }
    '-e'        { $Execute = $true }
    default {
      [Console]::Error.WriteLine("unknown argument: $a")
      [Console]::Error.WriteLine('usage: build-artifacts-cleanup-2026-09-16.ps1 [--execute|-e]')
      exit 2
    }
  }
}

$RepoRoot = (git -C $PSScriptRoot rev-parse --show-toplevel 2>$null)
if (-not $RepoRoot) {
  [Console]::Error.WriteLine('REFUSED: could not resolve the repo root from this script''s location.')
  exit 1
}
$RepoRoot = $RepoRoot.Trim() -replace '/', '\'

$Failed = $false
$script:Listed  = 0
$script:Removed = 0

function Mb($bytes) { '{0:N0} MB' -f ($bytes / 1MB) }

function Size($path) {
  if (-not (Test-Path -LiteralPath $path)) { return 0 }
  $item = Get-Item -LiteralPath $path
  if ($item.PSIsContainer) {
    return (Get-ChildItem -LiteralPath $path -Recurse -File -Force -ErrorAction SilentlyContinue |
            Measure-Object -Property Length -Sum).Sum
  }
  return $item.Length
}

function Drop($rel, $why) {
  $path = Join-Path $RepoRoot $rel
  if (-not (Test-Path -LiteralPath $path)) {
    Write-Host "   - $rel"
    Write-Host '     already gone'
    return
  }

  $gitRel = $rel -replace '\\', '/'
  git -C $RepoRoot ls-files --error-unmatch -- $gitRel *> $null
  if ($LASTEXITCODE -eq 0) {
    Write-Host "   - $rel"
    Write-Host "     REFUSED: tracked by git. Propose it as a commit instead. Left alone."
    $script:Failed = $true
    return
  }

  $bytes = Size $path
  Write-Host "   - $rel  ($(Mb $bytes))"
  Write-Host "     $why"
  $script:Listed += $bytes
  if ($Execute) {
    try {
      Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction Stop
      Write-Host '     removed'
      $script:Removed += $bytes
    } catch {
      Write-Host "     FAILED: $($_.Exception.Message)"
      $script:Failed = $true
    }
  }
}

Write-Host ''
Write-Host "extrode.com/jaunty build-artifacts cleanup 2026-09-16   mode: $(if ($Execute) { 'EXECUTE' } else { 'dry run' })"
Write-Host ''

Drop 'tests/Extrode.Jaunty.FlatFiles.DuckDB.Tests/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'benchmarks/Extrode.Jaunty.FlatFiles.Benchmarks/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.UnitTests/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.Tests/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.Scaffolding.Tests/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.SourceGenerator.Tests/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.Scaffolding.Cli.Tests/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.Fluent.SourceGen.Tests/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'benchmarks/Extrode.Jaunty.Benchmarks/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.Fluent.Tests/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'samples/NativeAOT-FluentQuery/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.Fluent.ConfigTests/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'samples/NativeAOT-WithReflection/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'samples/NativeAOT-CustomMapper/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'samples/NativeAOT-Basic/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.FlatFiles.Tests/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.Scaffolding.Cli/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.Tests/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.Extensions.Reflection/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.UnitTests/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.FlatFiles/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.Extensions.Npgsql/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.Extensions.Logging/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.Fluent/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.FlatFiles.DuckDB/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.Fluent.Tests/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.FlatFiles.DuckDB.Tests/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.Fluent/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.Scaffolding/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tools/Extrode.Jaunty.Fuzz/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.SourceGenerator.Tests/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.Scaffolding.Tests/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.FlatFiles.Tests/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.Extensions.Reflection/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.FlatFiles.DuckDB/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.Scaffolding.Cli.Tests/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.Fluent.SourceGen.Tests/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'benchmarks/Extrode.Jaunty.Benchmarks/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'tests/Extrode.Jaunty.Fluent.ConfigTests/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.FlatFiles/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'benchmarks/Extrode.Jaunty.FlatFiles.Benchmarks/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.Scaffolding.Cli/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.Extensions.Logging/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.SourceGenerator/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.Extensions.Npgsql/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'samples/NativeAOT-FluentQuery/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.Scaffolding/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'samples/NativeAOT-WithReflection/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'samples/NativeAOT-CustomMapper/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'samples/NativeAOT-Basic/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."
Drop 'src/Extrode.Jaunty.SourceGenerator/bin' "MSBuild output; 'dotnet build' regenerates it."
Drop 'tools/Extrode.Jaunty.Fuzz/obj' "MSBuild intermediate output; 'dotnet build' regenerates it."

Write-Host '--------'
if ($Execute) {
  Write-Host "Reclaimed: $(Mb $script:Removed)"
} else {
  Write-Host "Would reclaim: $(Mb $script:Listed)"
  Write-Host 'Pass --execute (or -e) to actually delete.'
}
Write-Host ''

if ($Execute -and $Failed) {
  Write-Host 'Finished with errors: see the refused steps above.'
  exit 1
}
exit 0
