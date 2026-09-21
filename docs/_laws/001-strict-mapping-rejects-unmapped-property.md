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
Enacted on approval of the plan in ~/.claude/docs/_plans/2026-09-18-014-laws-and-proofs-workflow.md.
The plan named `DrDispatcher.cs` as the scope; the strict-versus-projection decision is made in
the Reflection extension's `MetadataCache.GetSetters`, so the scope was corrected to the code
that throws.
Mutation: MetadataCache.cs:367 strict missing-property throw disabled, rebuilt; L001 failed on the first generated subset with a missing column (expected InvalidOperationException, none thrown).

Mutation 2 (2026-09-19, sample-to-enumeration follow-up): the proof used to sample 500 random
(mask, extra) pairs via CsCheck; converted to enumerate all 32 combinations of the 4 columns x
extra-column flag outright, since that domain is small enough that enumeration is a strictly
stronger, cheaper substitute for sampling (32 runs instead of 500, and every combination is
guaranteed covered rather than merely almost-certainly covered). This is exhaustive over this
fixture's fixed 4-column entity, not a proof of the Statement's "for every entity type T" as
literally written -- the fixture's shape stands in for T. Mutation: inverted
`if (!matchedProperties[i])` to `if (matchedProperties[i])` at MetadataCache.cs:365, rebuilt;
L001 failed on the first mask with a missing column (message named the wrong property). Reverted
with `git checkout --`, rebuilt, both L001 and L002 pass in 0.2s (previously ~500 iterations
each).
