---
id: L003
title: The core package ships with no dependencies on modern .NET
status: enacted
kind: guard
scope: src/Extrode.Jaunty/Extrode.Jaunty.csproj
proof: tests/Extrode.Jaunty.UnitTests/Unit/PackageDependencyTests.cs
check: dotnet build tests/Extrode.Jaunty.UnitTests -c Release -f net10.0 --nologo -v q && dotnet tests/Extrode.Jaunty.UnitTests/bin/Release/net10.0/Extrode.Jaunty.UnitTests.dll -trait "Law=L003"
enacted: 2026-09-18
---

## Statement
Every `PackageReference` in `src/Extrode.Jaunty/Extrode.Jaunty.csproj` is one of the two BCL
backports (`System.Diagnostics.DiagnosticSource`, `Microsoft.Bcl.AsyncInterfaces`) and is
conditioned on `netstandard2.0`, so the net8.0 and net10.0 packages carry no dependency group.
This is the constitution's "Zero dependencies in core" as it holds in fact.

## Counterexample looks like
A `PackageReference` to `Microsoft.Extensions.Logging.Abstractions` added to the core project
because a logging call was convenient, and the shipped nuspec growing a dependency group.

## Proof notes
Enacted on approval of the plan in ~/.claude/docs/plans/2026-09-18-014-laws-and-proofs-workflow.md.
The plan's draft said "no PackageReference at all"; the csproj has two, both netstandard2.0-only
backports that `PackageDependencyTests` already pins, so the statement was corrected to the
contract that is true and that test became the proof.
