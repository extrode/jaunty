---
id: L001
title: Strict mapping rejects a result set that is not the entity's full shape
status: enacted
kind: property
scope: src/Extrode.Jaunty.Extensions.Reflection/Internals/MetadataCache.cs
proof: tests/Extrode.Jaunty.UnitTests/Unit/Laws/L001StrictMappingLawTests.cs
check: dotnet build tests/Extrode.Jaunty.UnitTests -c Release -f net10.0 --nologo -v q && dotnet tests/Extrode.Jaunty.UnitTests/bin/Release/net10.0/Extrode.Jaunty.UnitTests.dll -trait "Law=L001"
enacted: 2026-09-18
---

## Statement
For every entity type T and every result set, `MetadataCache<T>.GetSetters(reader,
MappingMode.Strict)` throws `InvalidOperationException` when any public writable property of T
has no matching column, or any column matches no property, and returns one setter per property
otherwise. When a property is missing the message names that property. Silent partial mapping
is a bug (docs/constitution.md, Architecture invariants).

## Counterexample looks like
`SELECT id, name FROM items` mapped onto an entity with `Id`, `Name`, `Value` in Strict mode
returning two setters and an entity whose `Value` stays 0.

## Proof notes
Enacted on approval of the plan in ~/.claude/docs/plans/2026-09-18-014-laws-and-proofs-workflow.md.
The plan named `DrDispatcher.cs` as the scope; the strict-versus-projection decision is made in
the Reflection extension's `MetadataCache.GetSetters`, so the scope was corrected to the code
that throws.
