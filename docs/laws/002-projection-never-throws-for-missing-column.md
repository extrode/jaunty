---
id: L002
title: Projection mapping never throws for a missing or extra column
status: enacted
kind: property
scope: src/Extrode.Jaunty.Extensions.Reflection/Internals/MetadataCache.cs
proof: tests/Extrode.Jaunty.UnitTests/Unit/Laws/L002ProjectionLawTests.cs
check: dotnet build tests/Extrode.Jaunty.UnitTests -c Release -f net10.0 --nologo -v q && dotnet tests/Extrode.Jaunty.UnitTests/bin/Release/net10.0/Extrode.Jaunty.UnitTests.dll -trait "Law=L002"
enacted: 2026-09-18
---

## Statement
For every entity type T and every result set, `MetadataCache<T>.GetSetters(reader,
MappingMode.Projection)` returns exactly one setter per column that matches a property of T,
never throws for a column that matches nothing, and never produces a setter for a property whose
column is absent, so that property keeps its default.

## Counterexample looks like
`SELECT id, 999 AS extra FROM items` in Projection mode throwing, or returning a setter for
`Name` when no `name` column was selected.

## Proof notes
Enacted on approval of the plan in ~/.claude/docs/plans/2026-09-18-014-laws-and-proofs-workflow.md.
Mutation: MetadataCache.cs:361 strict-only missing-column check made unconditional, rebuilt; L002 failed on the first generated subset with a missing column (projection threw).
