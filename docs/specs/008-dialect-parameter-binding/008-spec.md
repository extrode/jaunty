# Dialect-Aware Parameter Binding — Feature Specification

> spec.md — The "what" and "why". No technical implementation details.
> Evidence, site inventory and provider probes live in `008-research.md`.

Status: draft · Created: 2026-07-29 · Origin: AUD-R25-030 (audit finding B4-3)

---

## 1. Problem Statement

`ISqlDialect.ParameterPrefix` is a public extension point whose stated purpose is
provider-specific placeholder syntax. `Jaunty.Fluent` honours it across ~20 call sites.
**Jaunty core does not.** `CrudSqlCache`, `MultiRowInsertCache`, `Upsert`, `DeleteCore`,
`GetCore` and `WriteParameterHelper` emit a literal `"@"`, and so do both entity binders —
`JauntyGenerator`'s generated `BindInsert`/`BindUpdate`/`BindDelete` and
`Jaunty.Extensions.Reflection`'s equivalents.

The consequence is not cosmetic. A dialect that returns anything other than `"@"` produces
SQL its own provider will not accept:

```
duckDbConnection.Insert(entity)
  → DuckDBException: Binder Error: Referenced column "CustomerId" not found in FROM clause!
```

DuckDB reads the `@CustomerId` core emits as a *column reference*, not a placeholder.
`DuckDbDialect.ParameterPrefix` returns `"$"` and `DuckDb`'s constructor registers that
dialect for `DuckDBConnection` by default, so this is the out-of-the-box behaviour of a
shipped Jaunty package, not an exotic configuration.

**Jaunty.Fluent is not a workaround.** It honours the prefix, so its SQL parses — and then
fails at bind time instead:

```
duckDbConnection.Into<CustomerProfile>().Values(entity).Insert()
  → DuckDBException: Invalid Input Error: Values were not provided for the following
    prepared statement parameters: CustomerId, Name, Email, CreatedAt
```

So writes and by-id lookups against a non-`"@"` dialect work through **neither** path today,
for **two different reasons**. Fixing the SQL alone would not make them work. That second
failure is the part that makes this a spec rather than a patch: it means there is a second,
undiscovered axis of provider variation, and the fix has to name it.

## 2. The Axes

The core insight, established by direct measurement (`008-research.md`, §2). Axes A and B were
the spec's original scope; axis C was discovered in the round-26 fix phase (AUD-R26-050) and is
recorded here because it is the same question about a different property of the parameter:

| | Axis A — placeholder syntax | Axis B — parameter name | Axis C — parameter value |
|---|---|---|---|
| **Where it appears** | The SQL text: `INSERT ... VALUES ($id)` | `IDbDataParameter.ParameterName` | `IDbDataParameter.Value` |
| **Modelled today by** | `ISqlDialect.ParameterPrefix` | *nothing* — always `"@" + column` | `IDecimalBindingDialect` (Fluent joined/grouped only) |
| **Honoured by** | Fluent only | neither | two Fluent builders only |
| **Varies by** | SQL grammar of the engine | the ADO.NET provider's name-matching | the provider's CLR-to-storage mapping |

These are **independent**. DuckDB needs `$id` in the SQL *and* the bare name `id` on the
parameter object — supplying `$id` as the name fails, which is exactly the second error
above. Microsoft.Data.Sqlite accepts the bare name too, with either placeholder sigil.

Jaunty currently models axis A and ignores it in core, and does not model axis B at all.
This feature closes both.

**Axis C**, added 2026-07-29. Both SQLite providers bind a CLR `decimal` as storage class
TEXT. SQLite converts a TEXT operand only where it has affinity to apply — against a
numeric-affinity column it does, so `WHERE price = @p` matches; against a bare expression it
does not, and TEXT sorts above every number, so `HAVING SUM(price) > @p` matches nothing and
`< @p` matches everything, at any magnitude. AUD-R26-050 moved the existing unconditional
`decimal`→`double` coercion behind `IDecimalBindingDialect` so it applies to SQLite alone;
measured against live PostgreSQL 16, SQL Server 2022 and MySQL 8, the unconditional form had
been returning **wrong rows** on all three.

That fix is deliberately narrow and leaves this spec three things:

1. **The same core/Fluent split as axis A.** `ParameterCollection.BindTo` and core
   `ParameterBinder` do not consult the dialect about values, so on SQLite the answer depends
   on which terminal you call. Measured on a column holding `9007199254740993`, same builder,
   same `Where`, plain column equality: `.Select()` returns the row, `.SelectBoth()` does not.
   Axis A's problem statement is "core ignores the dialect, Fluent honours it"; axis C is the
   same sentence with two Fluent builders on one side and everything else on the other.
2. **A lossless alternative that changes SQL rather than values.** `SUM(price) > CAST(@p AS
   NUMERIC)` compares correctly against the TEXT-bound decimal *and* keeps exact integers past
   2^53, where the `(double)` conversion does not. If axis C is solved in the SQL text it
   belongs with axis A; if in the bound value, with axis B. That choice is this spec's.
3. **A capability-discovery problem shared with every optional interface** — see US-6.

## 3. Vision

> "A dialect declares how its engine spells parameters, and every Jaunty write path —
> core CRUD, by-id reads, upsert, multi-row insert, and Fluent — obeys it."

A developer who registers a dialect, or writes their own, gets working reads *and* writes
without knowing which of Jaunty's internal caches happens to build the SQL.

## 4. Target Users

- **Developers using `Jaunty.FlatFiles.DuckDB`** for anything other than reads — today they
  hit an error message that names a column and gives no hint that the cause is a placeholder
  sigil.
- **Authors of third-party dialects** for engines Jaunty does not ship (Oracle `:name`,
  Firebird, ODBC positional `?`) — `ParameterPrefix` currently reads as a supported
  extension point and silently is not one on the write path.
- **Every existing user**, indirectly: the fix must be a provable no-op for the four core
  dialects, all of which return `"@"`.

## 5. User Stories

### US-1: Core CRUD honours the dialect
> As a developer using a non-`@` dialect, I want `connection.Insert/Update/Delete(entity)`
> to produce SQL my provider accepts.

**Acceptance Criteria:**
- `Insert`, `Update`, `Delete`, `Upsert`, multi-row insert, `Delete(id)` and `Get<T>(id)`
  all emit placeholders using the resolved dialect's prefix
- Round-trip against DuckDB: insert an entity, read it back, assert every column
- Generated SQL for `SQLiteDialect`, `SqlServerDialect`, `PostgreSqlDialect` and
  `MySqlDialect` is **byte-identical** to today's

### US-2: Parameter names match what the provider expects
> As a developer, I want the parameter objects Jaunty creates to bind successfully on my
> provider, not just to produce parseable SQL.

**Acceptance Criteria:**
- A dialect can declare how parameter *names* are spelled, independently of the placeholder
  sigil in the SQL
- The declared behaviour is verified against every provider Jaunty ships a dialect for
  (see §9 — this is currently verified for two of six)
- Fluent's `ParameterCollection.BindTo` uses the same rule as core, from the same source

### US-3: Both write paths work against DuckDB
> As a developer, I want DuckDB writes to work through core CRUD *and* through
> `Jaunty.Fluent`, since both are shipped as supported.

**Acceptance Criteria:**
- `_connection.Insert(entity)` round-trips
- `_connection.Into<T>().Values(entity).Insert()` round-trips
- `_connection.Get<T>(id)` round-trips
- `ParameterPrefixLimitationTests` — the four tests that today deliberately assert failure —
  are replaced by these round-trip assertions

### US-4: Custom dialects are a real extension point
> As an author of a third-party dialect, I want the write path to respect what my dialect
> declares.

**Acceptance Criteria:**
- A test-only dialect declaring a prefix no shipped dialect uses (e.g. `":"`) drives core
  CRUD end to end against a provider that accepts it
- `ISqlDialect`'s XML documentation no longer carries the "Honored by `Jaunty.Fluent` only"
  caveat, because it is no longer true

### US-6: A custom dialect inherits capabilities it cannot know about
> As an author of a third-party dialect, I want to opt into Jaunty's behaviour for an engine
> without reimplementing it — and without silently losing behaviour added after I wrote it.

Added 2026-07-29 from AUD-R26-050, which introduced this gap in its narrowest form and could
not close it from where it sat.

`ISqlDialect` is deliberately extended by *optional* companion interfaces, because it is public
and this assembly targets netstandard2.0 where a default interface implementation would break
existing implementers. `ISubstringToEndDialect` documents that trade. The cost is that a dialect
which does not implement one is indistinguishable from a dialect for an engine that does not
need it, and the failure is silent in both known cases: `ISubstringToEndDialect` falls back to a
sentinel length that truncates past 8,000 characters, `IDecimalBindingDialect` returns wrong
rows from a HAVING clause.

For Jaunty's own dialects this is handled — the four wrappers re-declare, and drift tests pin
it. A caller's dialect cannot be: **there is nothing to delegate to.** `SQLiteDialect` and its
siblings are `internal`, so a dialect registered through `SqlDialectFactory.RegisterDialect` for
a connection Jaunty does not recognise must hand-write ~40 members, and gets no capability it
was not written to know about. AUD-R26-050 made that concrete — such a dialect over a SQLite
connection used to get the `decimal` conversion, because the old coercion was unconditional, and
now cannot.

The affected population is small, and deliberately left so rather than patched: `GetDialect`
already looks through decorators by conventional property *and* private field, so the common
wrapped-connection cases — MiniProfiler, OpenTelemetry, DI proxies — resolve to the built-in
dialect without any registration. What remains needs an opaque wrapper *and* a hand-written
dialect *and* the affected clause. The reason it belongs here rather than in a patch is that the
remedy is an extension-point design decision this spec already owns.

**Acceptance Criteria:**
- A caller can obtain Jaunty's behaviour for a known engine without reimplementing
  `ISqlDialect` — whether by exposing the built-in dialects, a capability bag, per-capability
  defaults, or delegation is this spec's call
- A dialect built that way gains a capability added in a later version without recompiling
  against it, or the release notes state plainly that it will not
- The choice is applied to `ISubstringToEndDialect` and `IDecimalBindingDialect` together;
  solving it for one is solving it for neither

### US-5: Upgrading does not silently break generated code
> As a developer upgrading Jaunty, I want a clear failure — not wrong SQL — if my
> pre-generated entity binders predate the new contract.

**Acceptance Criteria:**
- An entity whose binder carries the old signature still works against a `"@"` dialect
- The same entity against a non-`"@"` dialect raises a diagnostic naming the entity, the
  dialect and the required action — it does not emit SQL that will fail downstream with a
  message about a missing column
- The `Jaunty.Extensions.Reflection` binder-resolver hooks on `JauntyConfig` change
  compatibly, or their replacement is documented with a migration note

## 6. Scope

**In scope**

- The 19 literal-`"@"` sites in `Jaunty` core listed in `008-research.md` §3 — 9 that build
  SQL text (axis A) and 10 that name parameter objects (axis B)
- The generated binder contract emitted by `JauntyGenerator`
- The reflection binder contract in `Jaunty.Extensions.Reflection` and its three
  `JauntyConfig` resolver hooks
- `WriteParameterCache<T>`, whose static-generic shape currently admits no dialect dimension
- `Jaunty.Fluent`'s `ParameterCollection.BindTo`, for axis B — and for axis C, since it is one
  of the two sides of the split that axis describes
- The `ISqlDialect` surface: whatever new member axis B requires, and the documentation
  rewrite on `ParameterPrefix`
- Axis C: whether `IDecimalBindingDialect` stays a value-side conversion, becomes a SQL-side
  `CAST`, or is subsumed by axis B's member; and the capability-inheritance decision in US-6,
  which applies equally to `ISubstringToEndDialect`

**Out of scope**

- **Stored procedures.** `ExecuteStoredProcedure` hardcodes `@` at one site. Stored-procedure
  support is engine-specific well beyond parameter naming, and no shipped non-`@` dialect
  supports them. Left as-is, with a note.
- **Positional parameters.** `?`-style binding (ODBC, and DuckDB's own positional mode) is a
  different model — order-dependent, not name-matched — and would change more than the
  spelling of a name. A dialect declaring positional binding is a later feature.
- **New dialects.** No engine gains support here; the four core dialects plus DuckDB are the
  full set.
- **The `SqlParameterParser` / `ParameterBinder` prefix *detection* logic**, which infers a
  prefix from user-supplied SQL. That is a separate mechanism and is already sigil-aware.

## 7. Compatibility

This is the reason the finding was deferred rather than fixed inside round 25.

Three contracts bake `"@"` in at a point where **no connection, and therefore no dialect,
exists**:

1. **The generated binder** — `public static void BindInsert(IDbCommand, T)`, emitted into
   the consumer's own assembly and discovered by name and signature. It is generated code,
   but it is *public API of the consumer's assembly*, and a downstream assembly may carry
   binders generated by an older Jaunty.
2. **`JauntyConfig.Reflection{Insert,Update,Delete}BinderResolver`** — public settable
   properties of type `Func<Type, Action<IDbCommand, object>>`. Changing the delegate type is
   source- and binary-breaking for anyone who set them.
3. **`WriteParameterCache<T>`** — a static generic. One binder set per entity type, with no
   connection dimension, so it cannot cache per-dialect at all. (`CrudSqlCache`, by contrast,
   is already keyed `(entityType, connectionType)` and resolves the dialect itself — the SQL
   half of this work has somewhere to stand.)

The spec does not choose the mechanism; it constrains it:

- **No silent wrong SQL.** Any path that cannot honour the dialect must fail loudly with a
  message naming the entity and the dialect, per US-5.
- **`"@"` dialects see no behavioural change**, provable by byte-comparison of generated SQL
  and of the generator's emitted output — the same method used to verify AUD-R25-031.
- **A breaking change is acceptable if it is the honest option**, and is then recorded with a
  migration note. Jaunty is pre-1.0.

## 8. Success Metrics

- All five user stories pass automated tests
- DuckDB `Insert` / `Update` / `Delete` / `Upsert` / `Get<T>(id)` round-trip, through core
  and through Fluent
- Generated SQL for all four core dialects byte-identical before and after
- `JauntyGenerator`'s emitted output byte-identical for `@`-dialect consumers, verified with
  `EmitCompilerGeneratedFiles` + `diff -r`
- No new allocations on the write hot path (benchmark before/after, per constitution)
- `ParameterPrefixLimitationTests` deleted, its four assertions replaced by round-trips

## 9. Open Questions

These block `/plan`, not this spec.

1. **Is "always name parameters without a sigil" universal?** Verified true for
   Microsoft.Data.Sqlite and DuckDB.NET 1.3.0 (`008-research.md` §2). **Unverified for
   SqlClient, Npgsql and MySqlConnector.** If universal, axis B needs no new `ISqlDialect`
   member at all — it becomes one rule applied everywhere, which is a far smaller change.
   If not, axis B needs a dialect member.

   **Evidence added 2026-07-29** (round 26, batch 3, `ParameterBinder.cs`, left open and
   deferred here rather than fixed): Jaunty *already* names parameters both ways, and the
   split is internal, not dialectal. The user-SQL path names them **bare** —
   `CommandTemplate.CreateTemplates` takes the name from `SqlParameterParser`, which strips the
   sigil, and `BindDynamic`, `BindScalar`, `BindFromDictionary` and `BindAllFromObject` all do
   the same. The entity-CRUD path names them with a literal `@` — `WriteParameterHelper`,
   `GetCore`, `DeleteCore`, `Upsert`, and the generated and reflection binders.

   That matters because the bare-naming path is `Query`/`Execute` with parameters: the
   most-exercised API in the library, covered against SQL Server by CI. So bare naming is
   demonstrably fine on SqlClient and Microsoft.Data.Sqlite today, in production code, with no
   dialect member involved. Two of the four unverified providers are answered by code that has
   been shipping. That does not prove universality — Npgsql and MySqlConnector are still
   open — but it shifts the burden: the question is now whether any provider *rejects* bare
   naming, not whether any accepts it.

   `008-research.md` §3.3 lists `ParameterBinder.cs` only as "detects a prefix … already
   sigil-aware" and does not record that it also *names* parameters, bare, at five sites. That
   omission should be corrected when this spec is planned.
2. **How is that verified?** ~~CI provides SQL Server only. PostgreSQL and MySQL have no CI
   service and no Testcontainers dependency in the repo. Adding either is a real cost and
   should be decided deliberately~~ — the constitution requires integration tests against real
   databases, not fakes.

   **Revised 2026-07-29, measured.** The premise was that this needs new test infrastructure.
   It does not — the infrastructure is already there and only wants a server.

   `Jaunty.Tests` integration tests are dialect-parameterised theories that skip themselves
   when no connection string is configured, reading `JAUNTY_TEST_{SQLSERVER, POSTGRESQL,
   MYSQL, MARIADB}` or `ConnectionStrings:*`. A local run on 2026-07-29 skipped **815 tests
   for want of `JAUNTY_TEST_POSTGRESQL` and 811 for want of `JAUNTY_TEST_SQLSERVER`** — the
   same assertions the SQLite dialect runs today, already written, already wired, waiting on a
   connection string.

   `.github/workflows/ci.yml` confirms the other half: one `mssql` service, one
   `JAUNTY_TEST_SQLSERVER` env var. Adding PostgreSQL and MySQL is therefore a `services:`
   block and one environment variable each, not a Testcontainers dependency and not new test
   code. That is a much smaller decision than this question assumed, and it unblocks question
   1 directly: run the existing suite against Npgsql and MySqlConnector and read the answer
   off the results.

   What it does *not* settle is CI cost and flakiness on shared services, which is still a
   deliberate call — but it is a cost question, not a feasibility one.
3. **Does `ParameterPrefix` survive, or is it replaced?** If axis B needs a member, two
   near-identical prefix concepts on one interface is a trap. A single member describing both
   axes may be better than two.
4. **Binder signature vs. cache key.** Passing the prefix as a binder argument, or keying
   `WriteParameterCache` by `(T, connectionType)` as `CrudSqlCache` already is. The first
   changes public generated API; the second does not, but multiplies a per-type static cache
   by connection type.

## 10. Related

- `audit/findings-registry.md` → `AUD-R25-030` (the finding), `AUD-R25-031` (the generator
  refactor that made the generated-output diff technique routine)
- `ParameterPrefixLimitationTests` — the executable record of today's behaviour
- `src/Jaunty/Dialects/ISqlDialect.cs` — the `ParameterPrefix` remarks, to be rewritten here
- `audit/findings-registry.md` → `AUD-R26-050` (axis C: the measurements, the three-engine
  wrong-rows result, the terminal-dependent divergence past 2^53, and the `CAST` alternative)
- `src/Jaunty/Dialects/IDecimalBindingDialect.cs` — axis C as it stands today
- `src/Jaunty/Dialects/ISubstringToEndDialect.cs` — the optional-interface precedent, and the
  other capability US-6 has to cover
