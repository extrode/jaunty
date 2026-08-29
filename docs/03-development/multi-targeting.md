# Multi-Targeting

## What ships where

| Project | Targets |
|---|---|
| `Jaunty` | `netstandard2.0` `net8.0` `net10.0` |
| `Jaunty.Fluent` | `netstandard2.0` `net8.0` `net10.0` |
| `Jaunty.FlatFiles` | `netstandard2.0` `net8.0` `net10.0` |
| `Jaunty.Extensions.Reflection` | `netstandard2.0` `net8.0` `net10.0` |
| `Jaunty.Extensions.Logging` | `netstandard2.0` `net8.0` `net10.0` |
| `Jaunty.Extensions.Npgsql` | `netstandard2.0` `net8.0` `net10.0` |
| `Jaunty.FlatFiles.DuckDB` | `net8.0` `net10.0` |
| `Jaunty.Scaffolding`, `Jaunty.Scaffolding.Cli` | `net8.0` `net10.0` |
| `Jaunty.SourceGenerator` | `netstandard2.0` only |

Test projects target `net8.0` and `net10.0`; `Jaunty.Tests` and `Jaunty.UnitTests` add `net472`,
which is how the netstandard2.0 build is exercised against a real .NET Framework runtime rather
than only compiled.

`Jaunty.SourceGenerator` is netstandard2.0 and cannot be anything else: Roslyn analyzers load into
the compiler, which is what dictates the target. It is referenced with
`SetTargetFramework="TargetFramework=netstandard2.0"` so the reference does not renegotiate per
consuming TFM.

`LangVersion` is pinned to `13.0` at the repository root, deliberately, so the language available
does not drift with whatever SDK happens to be installed. Note what that pin does **not** cover:
expression trees in `Jaunty.Fluent` are built by the *consumer's* compiler, so the pin protects
this build and not the shipped product.

---

## Why netstandard2.0 at all

It is what makes Jaunty usable from .NET Framework 4.6.1+, Unity, Xamarin and older
`netstandard`-targeting libraries. The cost is that `netstandard2.0` predates a decade of BCL
convenience, so a chunk of core is written twice.

### The dependency contract

**On net8.0 and net10.0, `src/Jaunty` has no package references at all.** Nothing conditional,
nothing transitive. `PackageDependencyTests` asserts it, and that assertion is the reason to be
careful here: it is easy to add a `PackageReference` without a TFM condition and break the
contract for every modern consumer to satisfy one old one.

netstandard2.0 carries exactly two, both backports of types that are in-box on modern .NET:

| Package | Backports | In-box since |
|---|---|---|
| `System.Diagnostics.DiagnosticSource` | `DiagnosticSource`, `Activity` | .NET Core 3.0 |
| `Microsoft.Bcl.AsyncInterfaces` | `IAsyncEnumerable<T>`, `IAsyncDisposable` | .NET Core 3.0 |

Neither is a third-party library. Anything that is one belongs in an extension package. That is
why `Microsoft.Extensions.Logging.Abstractions` and `.DependencyInjection.Abstractions` moved out
to `Jaunty.Extensions.Logging` - an old comment claimed they were built in on net8.0, which is
false; they ship in the ASP.NET Core shared framework, and a class library never gets it. See
[`../02-architecture/dependencies.md`](../02-architecture/dependencies.md).

---

## Conditional compilation

Four symbols are in use across `src/`, in this proportion:

| Symbol | Sites | Meaning |
|---|---|---|
| `NET8_0_OR_GREATER` | ~905 | modern BCL available |
| `NET5_0_OR_GREATER` | ~73 | narrower modern-BCL guards |
| `ASYNC_ENUMERABLE_SUPPORT` | ~12 | `IAsyncEnumerable<T>` usable |
| `!NET8_0_OR_GREATER` | 4 | the netstandard-only arm, written explicitly |

`ASYNC_ENUMERABLE_SUPPORT` is defined on **every** target, including netstandard2.0, because
`Microsoft.Bcl.AsyncInterfaces` supplies the interfaces there. Both TFMs therefore expose the same
streaming API. The symbol exists so the intent reads as "async streaming is available" rather than
as a framework-version test; do not replace it with a TFM check.

### The argument-guard idiom

By far the most common `#if` in the codebase, and worth writing the same way every time:

```csharp
#if NET8_0_OR_GREATER
    ArgumentNullException.ThrowIfNull(connection);
    ArgumentNullException.ThrowIfNull(sql);
    ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
    if (connection is null) throw new ArgumentNullException(nameof(connection));
    if (sql is null) throw new ArgumentNullException(nameof(sql));
    if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
```

The two arms must throw the **same exception type with the same parameter name**. A test that
asserts on `ArgumentException.ParamName` runs on net472 as well.

### Nullable reference types

`<Nullable>enable</Nullable>` is set only for targets compatible with net8.0. The netstandard2.0
build compiles the same annotated source without the analysis, so a nullability mistake surfaces
on the modern legs and silently compiles on the old one. Do not read a green netstandard2.0 build
as nullability having been checked.

---

## APIs that are not there on netstandard2.0

The recurring ones, with what to write instead:

| Modern API | netstandard2.0 replacement |
|---|---|
| `ArgumentNullException.ThrowIfNull` | explicit `if (x is null) throw` |
| `string.Contains(string, StringComparison)` | `IndexOf(value, comparison) >= 0` |
| `DateOnly`, `TimeOnly` | no equivalent; guard the test, do not fake it |
| static abstract interface members | no equivalent; the whole member is guarded out |
| `Span`/`Memory` overloads on BCL types | often absent; check before using |
| `HashCode.Combine` | manual combination |

`string.Contains(string, StringComparison)` has broken `dev` on the net472 leg **three separate
times**, most recently in `DecimalBindingDialectTests`. It is netstandard2.1+, it compiles clean
on every modern leg, and it fails CS1501 on net472 only. If you write a `Contains` with a
`StringComparison`, build the net472 leg before merging.

### Guarding is a last resort

A `#if` around a whole test is a coverage hole that outlives whoever wrote it. Seventeen guards in
`tests/` were removed in one pass after each was proved stale by building and running net472 -
that pass took the suite from 4,787 to 5,006 passing. Every guard that survives now has either an
`#else` arm implementing the behaviour a different way, or a one-line comment saying why the
construct cannot exist on that framework.

The two genuinely impossible categories are `DateOnly`/`TimeOnly` (CS0246 on net472) and static
abstract interface members. Everything else deserves an attempt at an `#else` arm first.

---

## Building and testing a single leg

```bash
dotnet build Jaunty.slnx -c Release -f net10.0
dotnet test  tests/Jaunty.UnitTests -c Release -f net472 --nologo -v q
```

CI runs net8.0 and net10.0 as separate legs and invokes each test project by name in both. Adding
a project to one leg only is a bug `SolutionLayoutTests` exists to catch.

One known interaction: running the net8.0 and net10.0 legs of `Jaunty.Tests` concurrently on one
machine makes `SqlServerSchemaReaderTests` race itself, because it drops and recreates
fixed-name `scaffold_test_*` tables in the shared database. Each leg alone passes; CI runs them
as separate legs, so CI is unaffected. Run one framework at a time locally.

---

## See Also

- [`adding-new-methods.md`](adding-new-methods.md) - where a new method goes and what it must ship with
- [`../02-architecture/dependencies.md`](../02-architecture/dependencies.md) - the dependency graph and why it is empty
- [`../00-quick-start/build-and-test.md`](../00-quick-start/build-and-test.md) - the ordinary build and test loop
