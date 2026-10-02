# F3 with R: configure Jaunty once, through `JauntyConfig.Configure`

## Context

An operation reads `JauntyConfig` more than once (`InsertCore.cs:18-21` reads the binder, then the
SQL; a generated binder reads names a third time). A mapping setting changed by another thread
between those reads pairs SQL from one configuration with parameters from another. Probed
2026-10-02 (`tmp/sqlite-probe`): both SQLite drivers throw and write nothing, so the risk is a
confusing failure, not data loss.

Five options were compared: A docs only, F1 opt-in `Freeze()`, F2 auto-freeze, F3 `Configure`
builder, B per-call snapshot. Fable was consulted twice, the second time as a plan review in the
same session, `config-f3`; its must-fix and should-fix items are folded in below.

The owner chose **F3 with R**: mapping settings are set once through `JauntyConfig.Configure`, and
their public setters are **removed**. This is a breaking change before 1.0. The owner also wants the
decision and discussion documented, a warning for existing users, and an upgrade guide covering
what to change and why.

## Design

**Frozen (moved into the builder; public setters removed):**
- the 3 naming resolvers;
- the 7 `Reflection*Resolver` properties and `SpecialTypeMapperResolver`;
- `CopyImportFactory`, `DefaultEnumStorage`;
- `RegisterTypeHandler` (both overloads).

`RemoveTypeHandler` is removed from the public API. Tests in projects with InternalsVisibleTo use
`TypeHandlerRegistry.Remove`.

**Still settable at runtime, unchanged:** `Logger`, the capacities, interceptors,
`BulkCopyConfiguration`, `SqlDialectFactory.RegisterDialect`. They are read once per use, and
`DuckDb.cs:82` registers a dialect at runtime.

**API (`src/Extrode.Jaunty/Configuration/`):**

```csharp
public static void Configure(Action<JauntyConfigBuilder> configure);
public static bool IsConfigured { get; }
public sealed class JauntyConfigBuilder { /* frozen settings get/set, RegisterTypeHandler<T>(...) */ }
internal static void Reconfigure(Action<JauntyConfigBuilder> change);   // tests only
```

- **Reads are unchanged.** Getters stay and read one immutable internal `JauntySettings` held in
  a volatile field, so read sites and generated code are untouched.
- **What `Configure` does, in order:**
  1. Start from defaults.
  2. Run auto reflection setup, if the extension is present.
  3. Apply the caller's lambda, which wins.
  4. Swap the snapshot in one write, register the type handlers, and call
     `ConfigurationGeneration.Invalidate()` once.
- **Once-only is a compare-and-swap** on a state field (`Interlocked.CompareExchange`), so two
  threads calling `Configure` together cannot both win (Fable 4).
- **A second `Configure`:**
  - **With the same settings, it does nothing.** Each delegate is compared with `Delegate.Equals`
    (same method, same target) and every value is compared. This lets `WebApplicationFactory` and
    Aspire tests that run `Program.cs` twice keep working (Fable 3; owner's choice).
  - **With different settings, it throws.** The message points to `IsConfigured`.
  - **`IsConfigured`** is the explicit guard for settings captured in closures:
    `if (!JauntyConfig.IsConfigured) JauntyConfig.Configure(...)`.
- **`Configure` after first use throws.** Every frozen getter sets a "settings read" flag with a
  check-then-set, so one read on the hot path. That covers `DefaultEnumStorage` and the type
  handler lookups that bypass the generation (Fable 5): `ParameterBinder.cs:1273`, the row readers,
  and `TypeHandlerRegistry` reads routed through one internal accessor.
- **The three auto-reflection paths (Fable 2):**
  - *`Configure` called first:* step 2 runs inside it. The static constructor in `Jaunty.Init.cs`
    sees `IsConfigured` and does nothing. A failure in step 2 throws to the `Configure` caller
    and is not recorded in `ReflectionMappingInitializationError`.
  - *`Configure` never called:* the static constructor calls an internal `InstallAutoReflection`
    that bypasses the once-only rule and the read flag. This matters because generated statics
    (`JauntyGenerator.cs:1775-1782`) and `FluentMetadataCache.cs:29` can read settings before
    `Jaunty` is touched. It fills only settings that are still unset, keeping the AUD-R35-099
    guard, and leaves `IsConfigured` false, so a later `Configure` still throws "after first use".
  - *The hook* is a hidden `[EditorBrowsable(Never)]` method on the Reflection assembly, called
    by name as today, keeping its `AOT-SAFE` justification.
- **`Reset()` stays public, documented as test-only.** It restores the defaults and clears both
  flags. The docs warn that `Reset` plus `Configure` races in parallel test collections (Fable 6).
- **Internal `Reconfigure`** copies the current snapshot, applies the change and swaps it, at any
  time. Jaunty's own suite uses it everywhere and never uses `Reset` plus `Configure`.
- **InternalsVisibleTo** is added for `Extrode.Jaunty.FlatFiles.Tests`,
  `Extrode.Jaunty.SourceGenerator.Tests` and `Extrode.Jaunty.Scaffolding.Tests`
  (`Extrode.Jaunty.csproj:67-107`) (Fable 1).

**Extension packages (R: old entry points removed):**

| Removed | Replacement |
|---|---|
| `JauntyReflectionExtensions.UseReflectionMapping()` | `c.UseReflectionMapping()` |
| `JauntyNpgsql.Use()` | `c.UseNpgsqlCopy()` |

Both replacements are builder extensions. `UseNativeBulkCopy()` is unchanged, since
BulkCopyConfiguration stays settable at runtime.

**Text that becomes false or names the old API (Fable 9):**
- error messages: `CrudSqlCache.cs:59`, `FluentMetadataCache.cs:43`, `CsvImport.cs:909,1336`;
- the `JAUNTYGEN004` diagnostic (`JauntyGenerator.cs:93`) and its snapshots;
- XML remarks:
  - `JauntyConfig.cs:17-27`, `420-445`;
  - `ConfigurationGeneration.cs:27`, `WriteParameterCache.cs:67`, `Jaunty.Init.cs:50`;
  - `TypeHandler.cs:15,50`;
- the comment at `EntityCodeGenerator.cs:144`.

## Docs

- **Decision record:** `docs/decisions/2026-10-02-014-configuration-is-set-once-at-startup.md`.
  - The problem, the five options with costs, and Fable's ranking.
  - The SQLite probe.
  - R vs O, and why R.
  - Fable's plan review.
  - The known residual: `Reset` is a test-only escape hatch.
- **Upgrade guide:** `docs/06-releases/upgrading-to-configure.md`.
  - A warning box: "this release breaks your build if you set any of these".
  - A before/after table for every removed member, with the compile errors users will see
    (CS0200, CS0117) so a search finds the guide.
  - Startup and ASP.NET.
  - Extension packages.
  - Type handlers. An adapter package that registered handlers in its own `Use()` must now be
    called inside the `Configure` lambda (Fable 7).
  - **Multi-host tests:** same settings are fine, otherwise use the `IsConfigured` guard (Fable 3).
  - **The user's own tests:** `Reset` then `Configure`, only in serialized collections (Fable 6).
  - **Ordering:** reading any generated `TableName`, or running a Fluent query or any query,
    before `Configure` now makes `Configure` throw.
  - **Runtime switching is not supported, and why:** per-request naming would need
    connection-scoped settings.
- **`CHANGELOG.md` Unreleased > Breaking changes:** a top entry with the warning and a link to the
  guide.
- **Pages:**
  - `configuration.md`, `attributes.md` and the other 25 docs files with old examples
    (`docs/archive` stays frozen);
  - the `README.md` quick start, 6 samples, `tests/Extrode.Jaunty.AotSmoke/Program.cs`.
- Rebuild `dist/docs-site`.

## Order of work (branch `feat/config-configure-builder` off dev, one commit per green step)

1. **Core**, as designed above, plus the 3 InternalsVisibleTo entries. New break-verified tests:
   - `Configure` applies everything;
   - a second identical call is a no-op;
   - a second different call throws;
   - concurrent `Configure` calls: exactly one wins;
   - after first use: it throws, including after only a `DefaultEnumStorage` or type-handler read;
   - `Reset` allows it again;
   - auto reflection inside `Configure`, with the caller's value winning;
   - **the static-constructor path still enables reflection when `Configure` was never called and
     a generated static was read first** (Fable);
   - one generation bump per `Configure`;
   - `IsConfigured`.
2. **Extension packages:** builder extensions, old entry points removed, all text from the list
   above updated, generator snapshots re-approved.
3. **Test migration:** about 320 sites in about 90 files, plus `tools/Extrode.Jaunty.Fuzz` (Fable 8).
   - Mechanical rule: `JauntyConfig.X = v;` becomes `JauntyConfig.Reconfigure(c => c.X = v);`.
     `UseReflectionMapping()`/`JauntyNpgsql.Use()` move into `Reconfigure`.
     `RemoveTypeHandler<T>()` becomes `TypeHandlerRegistry.Remove<T>()`.
   - `Dispose` bodies doing `Reset(); TestInitializer.Initialize()`
     (`CrudSqlCacheConcurrentInvalidationTests.cs:38-39`) become
     `Reset(); Reconfigure(c => c.UseReflectionMapping())`.
   - **Workers must never introduce `Reset` plus `Configure`** (Fable 10).
   - The work is split by test project and run by `codegen-lite` workers in separate worktrees,
     2-3 at once.
   - Each project's own test run is the gate, not just the build, since a build won't catch a
     semantic change.
4. **Samples, AotSmoke, README.**
5. **Docs:** the decision record, the upgrade guide, CHANGELOG, page updates, docs site rebuild.
6. **Review:** two-lens `/code-review`, every finding fixed, then merge `--no-ff` into dev, the
   structure audit, and a cleanup script for the branch and worktrees.

## Verification

- `dotnet build Jaunty.slnx -c Release` with 0 errors. A removed setter is a compile error, so a
  clean build proves src, tests and samples are migrated.
- Full `dotnet test Jaunty.slnx`: net8, net10, net472.
- `scripts/verify-nativeaot.ps1`, the AotSmoke publish, `laws-check --run`, and a Fuzz build.
- A grep shows no `JauntyConfig.<frozen> =` outside `docs/archive`.
- The upgrade guide's examples compile in a scratch `tmp/` project, including a two-host
  `WebApplicationFactory`-style double `Configure`.
