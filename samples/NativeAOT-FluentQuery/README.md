# NativeAOT-FluentQuery

`Jaunty.Fluent` under NativeAOT, with **no reference to `Jaunty.Extensions.Reflection`**. That
combination was impossible before spec 003 closed the gap: fluent queries now resolve
source-generated entity metadata the same reflection-free way `Query<T>` already did.

Covers the read path (`From<T>().Where(...).Select()`), fluent `Insert`/`Update`/`Delete`, and
two grouped-projection shapes.

## Why the grouped projection is here, and why it asserts

The `GroupBy(...).Select(g => new PriceBand { ... })` block at the end is the reason this
sample exists in its current form. That path goes through `GroupedJoinedResultMapper`, which
reflects over the projection type. Until the projection type carried
`[DynamicallyAccessedMembers]`, nothing rooted it — published AOT, the trimmer removed
`PriceBand`'s setters, `GetProperties()` returned empty, and every row arrived fully defaulted.
**No exception, no diagnostic, no output difference.** The sample used to stop before this
line, which meant the one unrooted reflection site on an AOT publish path was the one no AOT
binary ever ran.

So the block throws rather than prints:

```csharp
if (band.Count == 0 || band.Highest == 0m)
    throw new InvalidOperationException("... the projection type's members were trimmed away");
```

Printing the rows would look identical whether the mapping worked or silently produced zeros.

The anonymous-type form immediately after it takes the constructor path instead of the property
path and fails differently when trimmed — `MissingMethodException` rather than silent defaults.
Both failure modes are covered on purpose.

## Run it

```bash
dotnet run --project samples/NativeAOT-FluentQuery -f net10.0
```

In-memory SQLite, seeded by the sample. A non-zero exit or a thrown
`InvalidOperationException` is a real failure, not a configuration problem.

## Publish it AOT

Running under the JIT does **not** exercise what this sample is for — the trimmer is the thing
being tested. Publish and run the binary:

```bash
dotnet publish samples/NativeAOT-FluentQuery -c Release -f net10.0 -r linux-x64
./bin/Release/net10.0/linux-x64/publish/NativeAOT-FluentQuery
```

Substitute your own RID and adjust the path accordingly.

## See also

- [`docs/specs/003-fluent-nativeaot-metadata/`](../../docs/specs/003-fluent-nativeaot-metadata)
  — the spec that made this sample possible
- [`docs/02-architecture/reflection-and-trimming.md`](../../docs/02-architecture/reflection-and-trimming.md)
- [`NativeAOT-Basic`](../NativeAOT-Basic) — the same entity through the non-fluent API
