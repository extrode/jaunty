# Dependencies

What each shipped package drags into your project, and why. Nothing here is a surprise by design:
if a package reference exists, this page names it and says what it is for.

---

## The short version

**`Extrode.Jaunty` has no dependencies on `net8.0` or `net10.0`.** Not "few", not "only
Microsoft ones" — the dependency groups in the shipped `.nuspec` are empty:

```xml
<group targetFramework="net8.0" />
<group targetFramework="net10.0" />
<group targetFramework=".NETStandard2.0">
  <dependency id="Microsoft.Bcl.AsyncInterfaces" version="10.0.10" />
  <dependency id="System.Diagnostics.DiagnosticSource" version="10.0.10" />
</group>
```

`netstandard2.0` carries two, and both are **backports of types that are built into modern .NET**
rather than third-party libraries. Details below.

---

## Core: `Extrode.Jaunty`

| Target | Dependencies |
|---|---|
| `net10.0` | none |
| `net8.0` | none |
| `netstandard2.0` | `System.Diagnostics.DiagnosticSource`, `Microsoft.Bcl.AsyncInterfaces` |

### Why `netstandard2.0` is different

`netstandard2.0` exists for consumers who **cannot move off an older framework** — .NET Framework
4.6.1+ and older .NET Core, typically because something else in the application pins them there.
It is a compatibility target, not the recommended one.

That target predates two things Jaunty uses, so on that target only, they arrive as packages:

| Package | What it backports | In-box since |
|---|---|---|
| `Microsoft.Bcl.AsyncInterfaces` | `IAsyncEnumerable<T>`, `IAsyncDisposable` | .NET Core 3.0 |
| `System.Diagnostics.DiagnosticSource` | `DiagnosticSource`, `DiagnosticListener`, `Activity` | .NET Core 3.0 |

**`Microsoft.Bcl.AsyncInterfaces` is what makes the async and streaming API the same shape on both
targets.** Without it, `netstandard2.0` consumers would have no `IAsyncEnumerable<T>`, and
`QueryUnbuffered`, `GetAllStream` and the `GridReader` async paths would have to be absent there —
a different public surface per target, which is a worse trade than one package reference. If you
target `net8.0` or `net10.0` you get those APIs with no package at all, because the types are in
`Microsoft.NETCore.App`.

Neither package is a library in the ordinary sense. Both are published by Microsoft as part of the
platform, both are assembly-identity-compatible with the in-box types, and on a modern runtime the
reference unifies to the in-box assembly rather than loading a second copy.

### What used to be here

`Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions`
were referenced by core on **every** target framework until the split described below.

A comment in `Jaunty.csproj` claimed they were "built in on net8.0+" and referenced only "for
compatibility". **That was wrong.** Those two packages ship in the **ASP.NET Core** shared
framework, which a class library never gets — only `Microsoft.NETCore.App` is available to one.
They were real dependencies on every target, and they are gone now.

---

## Optional: `Extrode.Jaunty.Extensions.Logging`

The `ILogger` interceptor and the dependency-injection registration extensions live here, and this
package carries the two Microsoft.Extensions references so that core does not have to.

| Target | Dependencies |
|---|---|
| all | `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions` |

Install it if you use any of:

| Type or member | Namespace |
|---|---|
| `AddJauntyLogging(...)` | `Jaunty` |
| `AddJauntyInterceptor<T>(...)` | `Jaunty` |
| `ApplyJauntyInterceptors(...)` | `Jaunty` |
| `LoggingInterceptor` | `Jaunty.Interceptors` |
| `LoggingConfiguration` | `Jaunty.Configuration` |

**Namespaces are unchanged**, so migrating is a package reference and nothing else — no `using`
edits, no renames, no code changes.

### If you do not want the dependency

You do not need this package to observe what Jaunty is doing. Core has two hooks that cost nothing:

- **`ICommandInterceptor`**, registered with `JauntyConfig.AddInterceptor`. This is the same
  pipeline `LoggingInterceptor` plugs into — implement it against your own logger.
- **`JauntyDiagnosticListener`**, built on `System.Diagnostics.DiagnosticSource`, which is in-box on
  `net8.0`/`net10.0`. This is the route for OpenTelemetry and anything else that consumes
  `DiagnosticListener`.

`JauntyConfig.Logger` also exists as a plain `Action<string, object?>`. **It performs no redaction** —
the second argument is your parameter object exactly as supplied, passwords included. Masking lives
in `LoggingInterceptor`, in the satellite package.

---

## Other packages

Every extension depends on `Extrode.Jaunty` itself, which is not repeated below. "None" means no
*third-party* dependency beyond the Jaunty packages named.

| Package | Targets | `net8.0` / `net10.0` | `netstandard2.0` |
|---|---|---|---|
| `Extrode.Jaunty.Extensions.Reflection` | all three | none | none |
| `Extrode.Jaunty.Fluent` | all three | none | `Microsoft.Bcl.AsyncInterfaces`, `Microsoft.CSharp` |
| `Extrode.Jaunty.FlatFiles` | all three | none | `Microsoft.Bcl.AsyncInterfaces` |
| `Extrode.Jaunty.Extensions.Logging` | all three | the two Microsoft.Extensions packages above | same |
| `Extrode.Jaunty.Scaffolding` | `net8.0`, `net10.0` | none | n/a |
| `Extrode.Jaunty.FlatFiles.DuckDB` | `net8.0`, `net10.0` | `DuckDB.NET.Data.Full` | n/a |
| `Extrode.Jaunty.Scaffolding.Cli` | `net8.0`, `net10.0` | packed as a tool — see below | n/a |

`Extrode.Jaunty.FlatFiles.DuckDB` is the one package with an unavoidable third-party dependency:
the embedded query engine **is** the feature, so there is no version of it without DuckDB.

`Extrode.Jaunty.Scaffolding.Cli` is a **dotnet tool, not a library**. It references
`System.CommandLine`, `Microsoft.Data.SqlClient`, `Npgsql`, `MySqlConnector`,
`Microsoft.Data.Sqlite` and `SQLitePCLRaw.bundle_e_sqlite3` because it reads live database schemas
and needs a driver for every engine it supports. A tool package ships its dependencies inside
itself and declares none, so **nothing it references can reach your application**.

### ADO.NET providers

Jaunty core references **no database driver**. It works against `System.Data.Common`, so you bring
the provider you already use — `Microsoft.Data.SqlClient`, `Npgsql`, `MySqlConnector`,
`Microsoft.Data.Sqlite` or any other ADO.NET implementation. Jaunty never picks one for you and
never pulls one in transitively.

---

## NativeAOT

Fewer dependencies is directly an AOT win: a package you do not reference cannot produce a trim
warning. Verified after the split, 2026-08-29:

| Check | Result |
|---|---|
| `scripts/Verify-NativeAOT.ps1` | PASS — 21 reflection sites, all carrying a reviewed `AOT-SAFE` justification |
| NativeAOT publish, `net8.0` win-x64 (control) | binary produced, 36.98 MB |
| NativeAOT publish, `net10.0` win-x64 | binary produced and runs, 34.63 MB |
| Trim/AOT warnings from any **Jaunty** assembly | **none** |

Every `IL2104`/`IL3053` warning in that publish names a third-party driver or a BCL serialization
assembly reached through `Jaunty.Scaffolding.Cli` — `Microsoft.Data.SqlClient`, `MySqlConnector`,
`Microsoft.IdentityModel.Tokens`, `System.Data.Common`, `System.Private.Xml`. **None of these is
referenced by Jaunty core**, so they cannot reach a consumer who does not install the CLI tool.

`IsTrimmable` and `IsAotCompatible` are set for every `net8.0`+ target in
`src/Directory.Build.props`, and `TreatWarningsAsErrors` is on, so an AOT regression in Jaunty's own
code fails the build rather than being reported.

`Jaunty.Extensions.Reflection` is excluded from the AOT scan by design — reflection is its purpose,
and referencing it is how a consumer opts out of the guarantee.

---

## How the contract is enforced

Adding a `PackageReference` to a csproj succeeds silently; the only symptom is a dependency group in
a `.nuspec` that nobody reads. So it is asserted in tests rather than left to review:

`tests/Jaunty.UnitTests/Unit/PackageDependencyTests.cs`

| Fact | What it stops |
|---|---|
| `CoreReferencesNothingButTheNetStandardBackports` | Any new package reference in core |
| `EveryCoreReferenceIsGatedToNetStandard20` | A backport referenced unconditionally, which lands in every dependency group |
| `TheAsyncInterfacesFlagIsSetOnlyByNetStandard20` | Widening the `DefineConstants` flag that gates the backport, which would defeat the fact above |
| `TheLoggingSatelliteCarriesTheMicrosoftExtensionsPackages` | Dropping the references instead of moving them |

To check the shipped artifact directly rather than the build files:

```bash
dotnet pack src/Jaunty/Jaunty.csproj -c Release -o tmp/packtest
unzip -o -q tmp/packtest/Extrode.Jaunty.*.nupkg Extrode.Jaunty.nuspec -d tmp/packtest/nuspec-out
```

The `<dependencies>` element is the answer, and the `net8.0` and `net10.0` groups should be empty.

---

## See also

- [`docs/04-extensions/README.md`](../04-extensions/README.md) — what each extension package does
- [`docs/02-architecture/design-philosophy.md`](design-philosophy.md) — why the core is kept this small
