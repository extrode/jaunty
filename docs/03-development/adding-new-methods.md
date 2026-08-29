# Adding a New Method

How a new public method gets from an idea to a merged, tested, documented part of Jaunty's API
surface. The shape below is not a style preference; it is what the 680 existing `IDbConnection` extension
methods in `src/Jaunty` already do, and a method that departs from it reads as an exception
forever.

Before starting, read [`api-design-guidelines.md`](api-design-guidelines.md). The constitution
(`docs/constitution.md`) is binding: no LINQ in hot paths, no runtime reflection in core, tests
before implementation.

---

## 1. Decide whether it belongs in core

`src/Jaunty` has **no package references on net8.0 or net10.0** - not one, nothing transitive -
and `PackageDependencyTests` asserts it. On netstandard2.0 it carries exactly two backports of
types that are in-box on modern .NET. If your method needs a dependency, it belongs in an
extension package, not in core:

| Package | For |
|---|---|
| `Jaunty.Extensions.Reflection` | anything that reflects at runtime |
| `Jaunty.Extensions.Logging` | `ILogger`, DI registration |
| `Jaunty.Extensions.Npgsql` | PostgreSQL-specific surface |
| `Jaunty.Fluent` | the expression-tree query builder |
| `Jaunty.FlatFiles` | CSV, Parquet, DuckDB |

Core stays AOT- and trim-clean. `IsTrimmable` and `IsAotCompatible` are set for every net8.0+
target in `src/Directory.Build.props`, so a new reflection call in core is a build warning before
it is a review comment.

## 2. Put the public method where its family lives

One file per method family, named for the method:

```
src/Jaunty/Read/     Query.cs  QueryAsync.cs  QueryPartialFirst.cs  QueryScalar.cs  ...
src/Jaunty/Write/    Insert.cs  BulkInsertAsync.cs  Upsert.cs  ...
src/Jaunty/Execute/  Multiple/  Streaming/  StoredProcedure/
```

Every one of these files is `public static partial class Jaunty`, so the whole surface is one
class spread across files. A new `QuerySomething` goes in `src/Jaunty/Read/QuerySomething.cs`, and
its async twin in `QuerySomethingAsync.cs` - never both in one file.

## 3. Keep the public method a thin wrapper

Public methods guard their arguments and delegate. They contain no logic:

```csharp
public static List<T> QueryPartial<T>(this IDbConnection connection, string sql) where T : new()
{
#if NET8_0_OR_GREATER
    ArgumentNullException.ThrowIfNull(connection);
    ArgumentNullException.ThrowIfNull(sql);
    ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
    if (connection is null) throw new ArgumentNullException(nameof(connection));
    if (sql is null) throw new ArgumentNullException(nameof(sql));
    if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
    return QueryCore<T>(connection, sql, null, default, MappingMode.Projection);
}
```

The work lives in `src/Jaunty/Internals/`, in a `*Core` method that every overload of the family
funnels into. That is what makes the overload count affordable: `QueryPartial<T>` has four
synchronous arities and all four are four lines each.

See [`multi-targeting.md`](multi-targeting.md) for why the guard is written twice.

## 4. Ship the whole overload family, not a subset

The families are uniform on purpose, and a partial family is the thing reviewers push back on
hardest. For a read method that means eight methods:

| | no options | with options |
|---|---|---|
| **sync** | `(sql)` | `(sql, CommandOptions<T> options)` |
| **sync + params** | `(sql, object parameters)` | `(sql, object parameters, CommandOptions<T> options)` |
| **async** | `(sql, CancellationToken = default)` | `(sql, CommandOptions<T> options, CancellationToken = default)` |
| **async + params** | `(sql, object parameters, CancellationToken = default)` | `(sql, object parameters, CommandOptions<T> options, CancellationToken = default)` |

Rules that fall out of this:

- Async methods return `ValueTask<T>`, not `Task<T>`, and take the `CancellationToken` **last**
  with a `default`.
- A method that maps entities takes `CommandOptions<T>`. One that does not takes the non-generic
  `CommandOptions`. Never the other way round: the implicit conversion silently drops `Mapper` and
  `ExpectedRowCount` (see [`../01-api-reference/command-options.md`](../01-api-reference/command-options.md#the-implicit-conversion-loses-two-fields)).
- Do not add an overload that creates ambiguity between the strict and partial variants of a
  family. This is the one rule the module's coding notes calls out by name.

## 5. Write the tests first, in both suites

| Suite | Scope | Frameworks |
|---|---|---|
| `tests/Jaunty.UnitTests` | isolated internals, no database | net8.0, net10.0, net472 |
| `tests/Jaunty.Tests` | observable API behaviour, live engines | net8.0, net10.0, net472 |

Unit tests go under a directory mirroring the source (`Unit/Read/`, `Unit/Write/`). Integration
tests go under `Integration/` and should be dialect-parameterized where the behaviour is
SQL-generating rather than provider-neutral.

Two rules that are enforced rather than advisory:

- **A new test that passes on its first run is not yet trusted.** Change the input, or break the
  code under it, and confirm the test fails. A test that passes because the code never executed
  asserts nothing.
- **A new test project must be added to `Jaunty.slnx` and named by both CI framework legs.**
  `SolutionLayoutTests` fails the build otherwise - being in the solution only gets a suite
  compiled; a workflow step has to invoke it or it never runs.

## 6. Document it in the same branch

Three places, and the link checker gates the last one:

1. **XML doc comments** on every public overload - summary, every `typeparam` and `param`,
   `returns`, `exception` for each one the method can throw, and at least one `example`. The house
   style is [`xml-documentation-style.md`](xml-documentation-style.md).
2. **`docs/01-api-reference/`** - add the method to the family page, and to the table in
   [`README.md`](../01-api-reference/README.md) and
   [`api-summary.md`](../01-api-reference/api-summary.md).
3. Run `node scripts/check-doc-links.mjs`. It exits non-zero on any broken relative link.

## 7. Before opening the merge

```bash
dotnet build Jaunty.slnx -c Release            # 0 warnings; analyzers are errors here
dotnet test  Jaunty.slnx -c Release            # all frameworks
pwsh scripts/Verify-NativeAOT.ps1              # markers-only reflection scan over src/
node scripts/check-doc-links.mjs
```

`Verify-NativeAOT.ps1` fails on any reflection call in `src/` that is not annotated with an
`AOT-SAFE:` marker in the contiguous comment block immediately above it. The marker has to say
*why* the call is safe. Putting it anywhere else is silently ignored - it reads as an ordinary
failure with no hint that a marker was present.

If the method touches a database, reset the fixtures afterwards:
`pwsh scripts/reset-test-databases.ps1 -e`.

---

## See Also

- [`api-design-guidelines.md`](api-design-guidelines.md) - the API surface rules in full
- [`xml-documentation-style.md`](xml-documentation-style.md) - doc comment house style
- [`multi-targeting.md`](multi-targeting.md) - netstandard2.0, net8.0, net10.0, net472
- [`code-review-checklist.md`](code-review-checklist.md) - what a reviewer will look for
- [`../02-architecture/parameter-binding-spec.md`](../02-architecture/parameter-binding-spec.md) - how parameters reach the command
- [`../01-api-reference/README.md`](../01-api-reference/README.md) - the surface you are adding to
