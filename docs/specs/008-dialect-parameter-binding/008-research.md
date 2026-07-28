# Dialect-Aware Parameter Binding — Research

> research.md — Evidence gathered before planning. Everything here was measured, not assumed.
> The "what" and "why" live in `008-spec.md`.

Status: draft · Created: 2026-07-29

---

## 1. Where This Came From

Audit round 25, finding B4-3, registered as `AUD-R25-030`. The finding observed that
`ISqlDialect.ParameterPrefix` is honoured by `Jaunty.Fluent` and ignored by core, and
*suspected* the write path was broken. Round 25 confirmed the suspicion, documented it on
`ISqlDialect` rather than fixing it, and pinned the behaviour with
`ParameterPrefixLimitationTests` — four tests that deliberately assert failures. It is the one
round-25 finding left open.

## 2. Provider Probes

The decisive question for scoping is whether the sigil that appears in the **SQL** and the
sigil that appears in **`IDbDataParameter.ParameterName`** are the same thing. They are not.
Both probes below were run standalone against the exact package versions the repo references.

### 2.1 DuckDB.NET.Data.Full 1.3.0

| SQL placeholder | `ParameterName` | Result |
|---|---|---|
| `$id` | `$id` | `Invalid Input Error: Values were not provided ... : id, name` |
| `$id` | `id` | yes |
| `@id` | `@id` | `Binder Error: Referenced column "id" not found in FROM clause!` |
| `@id` | `id` | `Binder Error: Referenced column "id" not found in FROM clause!` |
| `?`, `?` | any | `Invalid Input Error: Values were not provided ... : 1, 2` |
| `$id` | `id`, added out of order | yes |

Reading:

- DuckDB requires `$name` in the SQL — this is axis A, and is what `ParameterPrefix` already
  declares.
- DuckDB requires the **bare** name on the parameter object — this is axis B, which Jaunty
  does not model anywhere.
- The two are independent: getting axis A right and axis B wrong produces the *second* error,
  which is precisely what `Jaunty.Fluent` does today (`ParameterCollection.BindTo` sets
  `param.ParameterName = name` with the sigil still attached).
- Binding is by name, not position — order of `Parameters.Add` is irrelevant.
- Positional `?` is not reachable through `IDbCommand.Parameters` at all with this provider,
  which is one reason positional binding is out of scope for this feature.

### 2.2 Microsoft.Data.Sqlite 10.0.3 (SQLitePCLRaw.bundle_e_sqlite3 3.0.3)

| SQL placeholder | `ParameterName` | Result |
|---|---|---|
| `@id` | `@id` | yes |
| `@id` | `id` | yes |
| `$id` | `id` | yes |

Reading: Microsoft.Data.Sqlite normalises. The bare name works for both sigils, so it does
**not** constrain the choice — it is compatible with a universal "name parameters without a
sigil" rule.

### 2.3 Unverified

**SqlClient, Npgsql, MySqlConnector are not measured.** All three are believed to normalise
parameter names the way Microsoft.Data.Sqlite does, but "believed" is what produced this
finding in the first place. This is open question §9.1 in the spec, and it decides the size of
the whole feature:

- If the bare name works everywhere, axis B needs **no new `ISqlDialect` member** — it is one
  rule, applied at every site that sets `ParameterName`.
- If any provider rejects it, axis B needs a dialect member and the `ISqlDialect` surface
  grows.

Verifying costs something. CI (`.github/workflows/ci.yml`) provisions **SQL Server only** —
there is no PostgreSQL or MySQL service, and no `Testcontainers` package reference anywhere in
the repo. The constitution requires integration tests against real databases rather than
fakes, so this cannot be settled with a mock.

## 3. Site Inventory

Exhaustive as of `dev` @ `f47dfc4`.

### 3.1 Axis A — building SQL text (9 sites)

| File | Lines | What |
|---|---|---|
| `src/Jaunty/Internals/Write/CrudSqlCache.cs` | 121 | `BuildInsertSql` — VALUES list |
| | 150 | `BuildUpdateSql` — SET clause |
| | 186 | `BuildDeleteByIdSql` — WHERE |
| | 209, 217, 225 | `BuildUpsertSql` — insert/update/key param arrays |
| | 246 | `AppendWhereClause` — shared by UPDATE and DELETE |
| | 272 | `BuildSelectByIdSql` — WHERE |
| `src/Jaunty/Internals/Write/MultiRowInsertCache.cs` | 129 | multi-row VALUES tuples |

Both types already receive an `ISqlDialect` — `CrudSqlCache.GetSql<T>` resolves it from the
connection and is keyed `(entityType, connectionType)`. **Axis A is mechanical.** For the four
core dialects, all of which return `"@"`, the output is unchanged by construction.

### 3.2 Axis B — naming parameter objects (11 sites)

| File | Lines |
|---|---|
| `src/Jaunty/Internals/Write/WriteParameterHelper.cs` | 18, 33, 41, 54 |
| `src/Jaunty/Internals/Read/GetCore.cs` | 177, 333 |
| `src/Jaunty/Internals/Write/DeleteCore.cs` | 399, 408 |
| `src/Jaunty/Write/Upsert.cs` | 200, 214 |
| `src/Jaunty.Extensions.Reflection/JauntyReflectionExtensions.cs` | 316 (`BuildColumnConverters`) |

Plus four emission statements in `src/Jaunty.SourceGenerator/JauntyGenerator.cs` — lines 627
(`BindInsert`), 637 and 641 (`BindUpdate`: non-key columns, then keys), and 651
(`BindDelete`) — each emitting `AddParam(command, p, "@<column>", ...)`.

And `src/Jaunty.Fluent/Internals/ParameterCollection.cs:52`, which sets `ParameterName` to a
name that already carries whatever sigil the builder attached — the source of the DuckDB
Fluent failure.

### 3.3 Deliberately excluded

| File | Line | Why |
|---|---|---|
| `src/Jaunty/StoredProcedure/ExecuteStoredProcedure.cs` | 545 | stored procedures — out of scope, see spec §6 |
| `src/Jaunty/Internals/Parameters/ParameterBinder.cs` | 458 | *detects* a prefix in user-supplied SQL; already sigil-aware |
| `src/Jaunty/Internals/Parameters/SqlParameterParser.cs` | 67, 90, 230, 248 | same — parsing, not emission |
| `src/Jaunty/Interceptors/LoggingInterceptor.cs` | 216 | already handles `@ : ? $` |
| `src/Jaunty/Dialects/*.cs` | — | the dialects' own `ParameterPrefix` returns |

## 4. The Three Contracts

Why this was deferred rather than fixed inline. Each bakes `"@"` in where no dialect exists.

### 4.1 The generated binder

`JauntyGenerator` emits, into the consumer's assembly:

```csharp
public static void BindInsert(IDbCommand command, Customer entity)
```

`WriteParameterCache<T>` finds it by reflection on exactly that name and signature
(`src/Jaunty/Internals/Write/WriteParameterCache.cs:154-158`), falling back to the reflection
resolver when absent. The signature is public API of the *consumer's* assembly, and a
downstream assembly may carry binders emitted by an older Jaunty — so changing it is breaking
in a way an internal refactor is not.

### 4.2 The reflection resolver hooks

```csharp
public static Func<Type, Action<IDbCommand, object>>? ReflectionInsertBinderResolver { get; set; }
```

Three of these on `JauntyConfig` (lines 127–148). Public settable properties; changing the
delegate type is source- and binary-breaking. In-repo, only `Jaunty.Extensions.Reflection`
sets them.

### 4.3 `WriteParameterCache<T>`

A `static` generic with `static readonly` binder fields initialised in a static constructor.
**One binder set per entity type, with no connection dimension** — so it structurally cannot
cache a per-dialect binder. This is the actual architectural constraint, and it is why "just
pass the dialect in" is not a one-line change.

Two shapes resolve it, and the spec deliberately does not choose:

1. **Prefix as a call-time argument.** The binder delegate gains a parameter; the cache stays
   keyed by `T`. Changes the generated public signature (4.1).
2. **Key the cache by `(T, connectionType)`.** No public signature change; multiplies a
   per-type static cache by connection type, and the generated binder would still need the
   prefix from somewhere.

`CrudSqlCache` already demonstrates that shape 2 works for SQL — it is keyed
`(Type, connectionType)` and resolves the dialect itself.

## 5. Verification Techniques Available

Both were established during round 25 and apply directly here.

- **Byte-identical generated SQL.** The four core dialects all return `"@"`, so any correct
  axis-A change is a provable no-op for them.
- **Byte-identical generator output.** Build with `-p:EmitCompilerGeneratedFiles=true
  -p:CompilerGeneratedFilesOutputPath=<dir>` before and after, then `diff -r`. This is how
  `AUD-R25-031` proved a full generator rewrite changed no emitted code.
- **Negative verification.** Revert the production change, confirm the new tests fail. Round
  25 applied this to every fix cluster.

## 6. Probe Sources

Both probes are standalone console projects, kept out of the repo. Reproduce with
`dotnet new console`, the package reference named in §2, and:

```csharp
void Try(string label, string sql, params (string name, object val)[] ps)
{
    try
    {
        using IDbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in ps)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = n;
            p.Value = v;
            cmd.Parameters.Add(p);
        }
        Console.WriteLine($"OK    | {label} | rows={cmd.ExecuteNonQuery()}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL  | {label} | {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
    }
}
```

against `CREATE TABLE t (id INTEGER, name VARCHAR)`, varying the SQL placeholder sigil and the
`ParameterName` sigil independently. When the SqlClient / Npgsql / MySqlConnector rows of §2.3
are filled in, they should be filled in the same way and the table above extended.
