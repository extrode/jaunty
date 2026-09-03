# NativeAOT-WithReflection

The one sample that **does** take the optional reflection extension, showing what it buys and
what it costs. The other three NativeAOT samples avoid it; this one is the counterexample, not
the recommendation.

## What it buys

Mapping to special types the source generator does not emit a mapper for — here,
`Dictionary<string, object>`:

```csharp
var dicts = connection.Query<Dictionary<string, object>>(
    "SELECT category_id, category_name, description FROM categories");
```

## What it costs

Two things, both visible in this project:

1. **Explicit registration.** AOT has no `Assembly.Load`-based discovery to fall back on, so
   the extension is wired up by hand at start-up:

   ```csharp
   JauntyReflectionExtensions.UseReflectionMapping();
   SpecialTypeMappers.Register();
   ```

2. **A trimmer root.** The `.csproj` has to keep the whole extension assembly, because the
   trimmer cannot see through the reflection to know what is used:

   ```xml
   <TrimmerRootAssembly Include="Jaunty.Extensions.Reflection" />
   ```

   That is a size cost paid unconditionally, and it is why the reflection extension is a
   separate package rather than part of `Extrode.Jaunty`.

The typed `Query<Category>` call at the top of the sample needs none of this — it goes through
the generated mapper exactly as it does in [`NativeAOT-Basic`](../NativeAOT-Basic). Reach for
the extension for the cases that need it, not by default.

## Run it

```bash
dotnet run --project samples/NativeAOT-WithReflection -f net10.0
```

In-memory SQLite with three categories, created by the sample.

## Publish it AOT

```bash
dotnet publish samples/NativeAOT-WithReflection -c Release -f net10.0 -r linux-x64
```

Substitute your own RID. Compare the published size against `NativeAOT-Basic` to see what the
rooted assembly costs.

## See also

- [`docs/02-architecture/reflection-and-trimming.md`](../../docs/02-architecture/reflection-and-trimming.md)
  — the full account of which paths reflect and which do not
