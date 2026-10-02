# 014 — Mapping configuration is set once, at startup, through `JauntyConfig.Configure`

**Date:** 2026-10-02
**Status:** Accepted

## Decision

**Names, mapping hooks, enum storage and type handlers are set in one `JauntyConfig.Configure`
call and cannot change afterwards. Their public setters are removed (option "F3 with R").** This
is a breaking change, made before 1.0 because a break costs least now. The upgrade steps are in
[upgrading-to-configure.md](../06-releases/upgrading-to-configure.md).

```csharp
JauntyConfig.Configure(c =>
{
    c.TableNameResolver  = t => Snake(t.Name);
    c.ColumnNameResolver = Snake;
    c.DefaultEnumStorage = EnumStorage.String;
    c.RegisterTypeHandler(new MoneyHandler());
    c.UseNpgsqlCopy();          // Extrode.Jaunty.Extensions.Npgsql
});
```

| Moved into `Configure` (setter removed) | Still settable at runtime |
|---|---|
| `SchemaNameResolver`, `TableNameResolver`, `ColumnNameResolver` | `Logger` |
| the 7 `Reflection*Resolver` hooks, `SpecialTypeMapperResolver` | `ParameterParsingCapacity`, `QueryResultCapacity`, `CsvFieldCapacity` |
| `CopyImportFactory`, `DefaultEnumStorage` | interceptors (`AddInterceptor` and friends) |
| `RegisterTypeHandler` (both overloads); `RemoveTypeHandler` is gone | `BulkCopyConfiguration`, `SqlDialectFactory.RegisterDialect` |

The right-hand column is read once per use, so no operation can see two values of one setting.
`SqlDialectFactory.RegisterDialect` has to stay open: `DuckDb.cs:82` registers its dialect at
runtime.

## The problem

One operation reads the configuration more than once. For a source-generated entity, one
`Insert` reads it three times (`src/Extrode.Jaunty/Internals/Write/InsertCore.cs:18-21`):

```csharp
Action<IDbCommand, T> binder = WriteParameterCache<T>.InsertBinder;   // read 1: names for the values
CachedCrudSql cached = CrudSqlCache.GetSql<T>(connection);            // read 2: names for the SQL
// read 3: the generated binder reads the column names again
```

Two parties are involved. The user's code calls `Insert` on one thread, and the user's code on
another thread changes a setting. Jaunty does everything in between. If the change lands between
reads 1 and 2, the SQL says `@unit_price` while the values arrive named `@UnitPrice`.

The configuration was already a process-wide singleton, so "make it a singleton" did not help:
the issue is the number of reads per operation, not the number of copies.

### What a mismatch does: the SQLite probe

Measured 2026-10-02 (`tmp/sqlite-probe`, scratch, not kept), INSERT, UPDATE and a mismatched key
name on both SQLite drivers:

| Driver | Result |
|---|---|
| System.Data.SQLite 1.0.119 | throws `SQLiteException: Insufficient parameters supplied to the command`; row unchanged |
| Microsoft.Data.Sqlite 10.0.12 | throws `InvalidOperationException: Must add values for the following parameters: @unit_price`; row unchanged |

On SQLite the worst case is one confusing failure, not lost or corrupted data. SQL Server,
PostgreSQL, MySQL and DuckDB were not probed.

## Options considered

| | A: document it | F1: opt-in `Freeze()` | F2: auto-freeze on first query | **F3: `Configure` builder** | B: settings copy per call |
|---|---|---|---|---|---|
| What it is | Keep "configure at startup" in the docs | The app calls `Freeze()` after setup; later changes throw | Jaunty freezes the settings itself when the first query runs | One `Configure(c => ...)` call; no setters | Each call takes the settings once and uses that copy throughout |
| Protects | Nobody, beyond the docs | Apps that opt in | Every app | Every app | Every call, even with runtime changes |
| Breaking for users | No | No | Yes, at runtime | Yes, at compile time | Users must rebuild |
| When a mistake shows | A confusing database error, rarely | At the setter, at runtime | At the setter, at runtime | At compile time | Never |
| Main trap | None | `Freeze()` must run Jaunty's own setup first | Jaunty writes 7 settings during the first query (`Jaunty.Init.cs`); late setup throws from an unrelated place | A new API; per-request settings still can't be expressed | Work added to every call; per-request naming still impossible |
| `src` edits | 0 | about 15 | about 15, plus exemptions | about 30 lines in 10 files, 4 error messages, 1 diagnostic | SQL building, binding, readers, generated code |
| Test edits | 1 test | a handful | about 90 files | about 320 sites in about 90 files | a new suite |
| Docs and samples | wording | 1 section | 25 files, 6 samples | 76 sites in 25 files, 6 samples | 1 section |
| Ranking (Claude and Fable) | 1 | 2 | 5 | 4 | 3 |

Fable (a second model, consulted with the full code context) ranked A first: an app that
configures at startup is already safe, and F1 only turns a rare confusing database error into a
clear error at the setter. It ranked F2 last because it breaks apps at runtime with no build-time
warning, and the first trap it springs is Jaunty's own auto-reflection setup.

The owner chose F3 anyway: **misuse should fail at compile time, for every app**, and a pre-1.0
release is the cheapest moment to break the API. F3 is the same freeze as F1 and F2, with the
mistake caught by the compiler instead of at runtime.

### Removing the setters (R) versus keeping them obsolete for one release (O)

| | R: remove | O: obsolete for one release |
|---|---|---|
| Upgrade day | Build fails until fixed (CS0200, CS0117) | Builds, with CS0618 warnings |
| Startup-only apps | Must edit before shipping | Keep working; edit later |
| Apps changing settings at runtime | Build fails | Throw at runtime, at the setter |
| Our cost | One breaking release | Two releases, and the old setters to maintain and test in between |
| Fit with a 1.0 release candidate | Good: breaking changes are still allowed | Mostly protects users who already depend on the API |

**R was chosen.** O also reintroduces F2's "throws after the first query" trap for one release.

## Design details

- **Reads are unchanged.** The getters stay on `JauntyConfig` and read one immutable snapshot held
  in a volatile field, so code the source generator wrote for users' entities does not change and
  needs no rebuild for this.
- **What `Configure` does, in order:** start from defaults; switch on reflection mapping if
  Extrode.Jaunty.Extensions.Reflection is present; run the caller's callback, which wins; swap
  the snapshot in one write, register the type handlers, and bump `ConfigurationGeneration` once.
- **Once only.** A compare-and-swap on a state field means two threads calling `Configure` at once
  cannot both win.
- **A second call with the same settings does nothing; with different settings it throws.**
  Delegates compare with `Delegate.Equals` (same method, same target): true for method groups and
  for lambdas that capture nothing. This keeps `WebApplicationFactory` and Aspire tests working,
  where `Program.cs` runs twice in one process. A callback capturing a value is a new delegate each
  time, so `JauntyConfig.TryConfigure` is the explicit form for a call that can repeat.
- **`Configure` after first use throws.** Every frozen getter, `ConfigurationGeneration.Current`
  (which every derived cache and every generated static reads) and the type handler lookups set a
  "settings read" flag, with a check before the write so the steady state is one plain read.
- **When `Configure` is never called,** `Jaunty`'s static constructor still switches on reflection
  mapping, filling only hooks that are still unset (AUD-R35-099). It bypasses the once-only rule
  and the read flag, because generated statics and Fluent can read the settings before the
  `Jaunty` class is first touched; refusing there would leave every query failing with "No mapper
  found". It leaves `IsConfigured` false.
- **`Reset()` stays public, for tests only.** It restores the defaults and clears both flags.
  `Reset` then `Configure` races any test running in parallel, so it belongs in a serialized test
  collection.
- **An internal `Reconfigure`** changes the snapshot at any time, for Extrode.Jaunty's own tests,
  which set one resolver at a time in a process that has long since run queries.

## Plan review (Fable, same session)

The plan was reviewed before implementation. Its must-fix items were folded in: InternalsVisibleTo
for the three test projects that set settings mid-suite; all three auto-reflection paths defined;
multi-host tests (resolved by the same-settings no-op plus `TryConfigure`); and a compare-and-swap
rather than a check-then-set for once-only. Should-fix items also adopted: the read flag covers
`DefaultEnumStorage` and type handler lookups, which bypass the generation counter; Jaunty's own
tests never pair `Reset` with `Configure`; the upgrade guide covers type handler adapter packages;
stale error messages and remarks were reworded.

## Code review (two lenses, same session)

A correctness and a consequence review of the implementation found, and the branch fixed:

- **A first read racing `Configure` could still mix settings.** The read flag was checked only
  before the callback ran. `Configure` now re-checks it after publishing, with a full barrier on
  both sides, and on a hit restores the previous settings and throws.
- **`new MyHandler()` made a repeated `Configure` throw.** Type handlers now compare by type.
- **`if (!IsConfigured) Configure(...)` is check-then-act,** so parallel test hosts raced.
  `TryConfigure` is the atomic form: the first call applies, later calls skip without running.
- **A failing `Configure` could leave reflection mapping uninstalled** when `Jaunty`'s static
  constructor ran during it; the install is now retried. `Reset` waits for a `Configure` in
  flight, and an older Extensions.Reflection package is recorded as an error rather than absent.

Kept by design: a broken Extensions.Reflection assembly (`FileLoadException`,
`BadImageFormatException`) makes `Configure` throw rather than record and carry on, since it is a
deployment fault the caller should see at startup.

## Residual

- **`Reset()` is an escape hatch.** A test can still change the settings after use. It is
  documented as test-only and not atomic.
- **Per-tenant or per-request naming is not supported.** It would need settings scoped to a
  connection, which is a different feature.
- Changing `CopyImportFactory` bumps the generation although nothing derived from it is cached.
  Harmless, since it now happens once at startup.
