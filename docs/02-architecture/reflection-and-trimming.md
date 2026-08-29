# Reflection, trimming and NativeAOT

Every place Jaunty still uses reflection, why it is there, and what you have to do about it.

Jaunty's design goal is that a source-generated entity never reaches reflection at all. The sites
below are the exceptions that remain, and each one is reachable only from a feature you opt into.
If you use `[Table]` entities, the built-in dialects and the source generator, **none of the core
sites on this page execute**.

---

## Summary

| # | Where | Reached when | Trimmed outcome | What you do |
|---|---|---|---|---|
| 1 | `MappedCache` (5 sites) | You hand-write `IMapped<T>` instead of using `[Table]` | `No mapper found for type 'T'` | Build warning **JAUNTYGEN002**; add `[Table]`, implement `IGeneratedAccessors<T>`, or `[DynamicDependency]` |
| 2 | `WriteParameterCache` (2 sites) | You supply `BindInsert`/`BindUpdate`/`BindDelete` by convention | The command binds nothing | Build warning **JAUNTYGEN002**, same three fixes |
| 3 | `ParameterCache` (1 site) | Every query with a parameters object | Parameters silently missing | Generator emits `JauntyAot.PreserveParameters<T>()`; **JAUNTYGEN003** where it cannot. Two shapes stay uncovered — see below |
| 4 | `Jaunty.Init` (1 site) | Auto-discovery of `Jaunty.Extensions.Reflection` | Extension not enabled | Call `JauntyReflectionExtensions.UseReflectionMapping()` yourself, or don't use the package |
| 5 | `SqlDialectFactory` (2 sites) | A wrapped connection, or auto-discovery of the bulk-copy extension | Falls back to the undecorated behaviour | Nothing; degrades, never misbehaves |
| 6 | `GroupedJoinedResultMapper` (1 site) | Fluent `GroupBy(...).Select(...)` projections | Rooted, so nothing | Nothing — see the caveat on expression trees below |
| 7 | `Jaunty.FlatFiles.DuckDB` (2 sites) | The DuckDB flat-file provider | Columns silently unmapped | Root your DTOs, or don't publish this package trimmed |
| 8 | `Jaunty.Scaffolding` (1 site) | The scaffolding CLI, at design time | n/a | Nothing — a build-time tool, never published AOT |

`Jaunty.Extensions.Reflection` is not on this list. Reflection is its stated purpose: referencing
it is how you opt **out** of the AOT guarantee. `Jaunty.SourceGenerator` is not either — it runs
inside the compiler and is never published.

The count is enforced. `scripts/Verify-NativeAOT.ps1` fails the build if any reflection site in a
publishable assembly lacks a reviewed justification next to it, and `-Verbose` prints the whole
inventory. There are **15** at the time of writing.

---

## 1 and 2. Hand-written mappers and binders

`MappedCache` looks up `ReadEntity`/`CreateRowMapper` on your type by name;
`WriteParameterCache` looks up `BindInsert`, `BindUpdate` and `BindDelete` the same way. Both are
reached **only** when the type is not source-generated — a `[Table]` entity implements
`IGeneratedAccessors<T>` and hands its delegates over directly, with no reflection at all.

This is the one category where trimming produces a wrong answer rather than a graceful one: the
trimmer removes a method nothing statically calls, the lookup returns null, and a read fails with
`No mapper found for type 'T'` while a write binds no parameters.

Jaunty cannot arrange preservation from inside — the member belongs to your assembly. So it warns
at build time instead. **JAUNTYGEN002** names the type, says which members are at risk, and lists
the three ways out:

```csharp
// The fix Jaunty prefers: let the generator emit the members.
[Table("products")]
public partial class Product { ... }

// Or supply them yourself through the interface, which roots them statically.
public class Product : IGeneratedAccessors<Product> { ... }

// Or root the convention members explicitly.
[DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Product))]
```

The warning fires for JIT-only consumers too, who will never trim. That is deliberate: the
condition it reports is real, and lowering the severity would hide it from the people it is for.

## 3. Parameter objects

`ParameterCache.BuildMetadata` enumerates the public properties of whatever you pass as
`param:`. The type arrives as `parameters.GetType()` from an `object`, so no annotation can flow
to it and none is declared.

Preservation is arranged from outside instead. The source generator reads your `Query`/`Execute`
call sites and emits `JauntyAot.PreserveParameters<T>()` for each parameters type into a module
initializer in **your** assembly, which is what roots the getters. Where the call site is visible
but the type is not knowable — an `object`-typed variable, a type built by reflection — it reports
**JAUNTYGEN003** there rather than letting the failure surface after publish.

**Two shapes are neither preserved nor warned about**, and you have to root them yourself:

- a parameters object forwarded through an `object`-typed parameter into a helper that makes the
  Jaunty call — the generator sees the helper's call site, not the anonymous type at the caller;
- a Jaunty call made from inside a wrapper library the generator does not run over — your
  assembly has no call site to read.

Both root the same way:

```csharp
JauntyAot.PreserveParameters<MyParams>();
```

## 4. Optional extension auto-discovery

`Jaunty.Init` probes for `Jaunty.Extensions.Reflection` and calls its `UseReflectionMapping` if it
is there. Under trimming the probe finds nothing and the extension is simply not enabled.

This one is inherently reflective and cannot be replaced by having the extension register itself:
a `[ModuleInitializer]` in a referenced-but-untouched assembly does not run under the JIT (it does
under AOT), so self-registration would change behaviour for every JIT consumer. If you publish
trimmed and want the extension, call it explicitly:

```csharp
JauntyReflectionExtensions.UseReflectionMapping();
```

`SqlDialectFactory`'s bulk-copy probe (site 5) is the same pattern with the same reasoning.

## 5. Wrapped connections

`SqlDialectFactory` unwraps a decorated `IDbConnection` to find the real one — a property first,
then a single private field of a connection type, because plenty of wrappers expose nothing. The
type belongs to you, not to Jaunty, so nothing can be arranged for it.

Finding nothing is a supported outcome: you get your own error, which is the same thing a trimmed
build that had removed the field would produce. It degrades; it does not misbehave.

## 6. Fluent grouped projections

`GroupedJoinedResultMapper` reflects over the projection type of a
`GroupBy(...).Select(g => new Thing { ... })`. `TResult` is annotated with
`[DynamicallyAccessedMembers(PublicProperties | PublicConstructors)]` at every hop from the public
`Select` down to the mapper, so the trimmer keeps it. **You do not have to do anything.**

This was not always true. Until the 2026-08-29 audit the annotation was missing and an `IL2090`
pragma suppressed the only warning that said so; under trimming every row of a grouped projection
came back fully defaulted, with no exception. The pragma is gone — the build staying clean without
it is the evidence the annotation does the work.

**One caveat remains, and it is not removable by annotation.** `System.Linq.Expressions`'
`Expression.Bind` and `Expression.New` carry `RequiresUnreferencedCode`, so a grouped fluent
projection emits **IL2026** in *your* build, not Jaunty's. The published binary runs correctly —
the AOT sample exercises both the property and constructor paths and asserts on the values — but
the warning is the framework's, and the only way to silence it is to suppress it at your call site
or use a non-expression overload.

## 7. DuckDB flat files

`Jaunty.FlatFiles.DuckDB` maps result columns onto your DTO by enumerating its public properties.
This package has no source-generated path, so unlike the core sites there is no non-reflective
alternative to fall back to: under trimming, columns go silently unmapped.

Root your DTOs, or do not publish this package trimmed.

## 8. Scaffolding

`ReflectedConnectionFactory` calls `Activator.CreateInstance(connectionType, connectionString)` so
the scaffolding CLI can open a connection for a provider chosen at run time. It is a design-time
tool that generates source; it is never part of your published application.

---

## What the verifier does and does not prove

`scripts/Verify-NativeAOT.ps1` checks that a justification **exists** next to each reflection
site. It does not check that the justification is **true**, and it cannot trace a call graph.
Treat a PASS as "every site has been reviewed", not as a proof of AOT safety.

It also matches a fixed pattern list, which is not exhaustive. `GetConstructors(` is not scanned,
and `GetGetMethod(` sites — `ParameterCache.cs`, `Jaunty.Fluent/ExpressionEvaluator.cs`,
`Jaunty.FlatFiles.DuckDB/ExpressionTranslator.cs` — are not counted separately from the
`GetProperties` call that produced the `PropertyInfo`.

Separately: "no Jaunty assembly produces a trim or AOT warning" is accurate about build output and
incomplete as a safety claim. Several of the sites above are clean because of an
`UnconditionalSuppressMessage` or a pragma, which is an assertion by the author, not a proof by
the tool. The reasons are on this page so you can judge them.

---

## Related

- [`dependencies.md`](dependencies.md) — what each package pulls in, and why core has no
  dependencies on `net8.0`/`net10.0`
- [`../03-development/limitations.md`](../03-development/limitations.md) — known limits
- `samples/NativeAOT-Basic`, `NativeAOT-CustomMapper`, `NativeAOT-WithReflection`,
  `NativeAOT-FluentQuery` — publishable samples CI builds on every run
