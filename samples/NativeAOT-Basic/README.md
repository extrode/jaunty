# NativeAOT-Basic

The smallest complete Jaunty program that publishes NativeAOT: `Query<T>`, `QueryFirst<T>`
and `QueryScalar<T>` against an in-memory SQLite database, with **no reflection anywhere**.

Mapping is done by `Jaunty.SourceGenerator`, referenced as an analyzer:

```xml
<ProjectReference Include="..\..\src\Jaunty.SourceGenerator\Jaunty.SourceGenerator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

The generator emits a `ReadEntity` for `Product` at compile time, so nothing has to inspect
the type at run time and the trimmer has nothing to remove by mistake. This is the reference
shape for a consumer's own project: a project reference to `Extrode.Jaunty`, the generator as
an analyzer, `<PublishAot>true</PublishAot>`, and no reference to
`Extrode.Jaunty.Extensions.Reflection`.

## Run it

```bash
dotnet run --project samples/NativeAOT-Basic -f net10.0
```

The sample creates its own schema and seeds five products, so it needs no database and no
configuration. Expected output is a five-row listing, one `QueryFirst` line, and a count of
four active products.

## Publish it AOT

Running under the JIT proves the code works; it does not prove it survives trimming. Only a
publish does that:

```bash
dotnet publish samples/NativeAOT-Basic -c Release -f net10.0 -r linux-x64
```

Substitute your own RID (`win-x64`, `osx-arm64`, …). A clean publish with no `IL2xxx` or
`IL3xxx` warnings is the result being demonstrated. `scripts/Verify-NativeAOT.ps1` is a
faster text-level gate over the whole tree, but it is a gate, not a proof — see the note at
the top of that script.

## See also

- [`docs/02-architecture/reflection-and-trimming.md`](../../docs/02-architecture/reflection-and-trimming.md)
  — which paths are reflection-free and which are not
- [`NativeAOT-CustomMapper`](../NativeAOT-CustomMapper) — the same thing without the generator
- [`NativeAOT-FluentQuery`](../NativeAOT-FluentQuery) — the fluent API under AOT
- [`NativeAOT-WithReflection`](../NativeAOT-WithReflection) — what the optional reflection
  extension buys, and what it costs
