# Elegant join SQL

**Status:** in progress · **Branch:** `feat/elegant-join-sql` · **Tier:** Standard

## Why

The fluent join builder emits SQL no one would write by hand:

```sql
SELECT products.product_id AS f_product_id, products.product_name AS f_product_name,
       products.category_id AS f_category_id, products.unit_price AS f_unit_price,
       products.units_in_stock AS f_units_in_stock, products.discontinued AS f_discontinued,
       categories.category_id AS j_category_id, categories.category_name AS j_category_name,
       categories.description AS j_description
FROM products
INNER JOIN categories ON products.category_id = categories.category_id
WHERE (products.category_id = @jp0)
```

Three defects, each with its own cause:

1. **No table alias is ever generated.** `GetPrefixedColumnsWithAlias` (`JoinedQueryBuilder.cs:471`)
   falls back to the full escaped table name when no alias is supplied, and nothing supplies one.
2. **`AS f_`/`AS j_` column aliases** (`:477`) exist only so `SelectBoth()` can map each entity out
   of a name-to-ordinal dictionary (`MapEntity`, `:556`). Arities 3 and 4 use `t1_`..`t4_`.
3. **`@jp0` parameter names** (`JoinExpressionVisitor.cs:53`) are a bare counter, unsearchable and
   inconsistent with the string-overload path, which already derives the name from the column.

Readable generated SQL is the library's stated argument against EF. This is the one place it does
not hold up. Fixed before the first public release, because changing generated SQL after
publication breaks anyone snapshot-testing it.

## Target

```sql
SELECT p.product_id, p.product_name, p.category_id, p.unit_price, p.units_in_stock, p.discontinued,
       c.category_id, c.category_name, c.description
FROM products p
INNER JOIN categories c ON p.category_id = c.category_id
WHERE (p.category_id = @p_category_id)
```

## Design

### 1. Table aliases inferred from lambda parameter names

The developer already typed the alias: `On(p => p.CategoryId, c => c.CategoryId)`. Roslyn lowers
that into `Expression.Parameter(typeof(Product), "p")`, where `"p"` is an `ldstr` literal in the IL
of the factory call — tree data, not metadata. Trimming and NativeAOT cannot remove it, and reading
it is a property access rather than reflection. The join path only visits trees and never
`Compile()`s them, so the AOT-hostile part of `System.Linq.Expressions` stays out.

Reviewed independently by three models (fable, glm-5.3, qwen3.8-max) on 2026-09-01; brief and
transcripts under `tmp/`. All three confirmed the mechanism and converged on the design below.

**Names never bind a table.** `JoinExpressionVisitor.cs:274-289` binds a member to a table by
`ParameterExpression` reference identity (`param == _param1`); arities 3 and 4 pass aliases
positionally. A name can only choose the qualifier *text*. The wrong-table hazard the design has to
guard is not name confusion but **alias capture**: an inferred alias equal to a real table name in
the same query, where `FROM products categories JOIN categories c` makes every `categories.`
reference mean products.

**Timing is forced.** `JoinClauseBuilder.cs:35` renders the ON condition to a string inside the
`On` call, before the query builder that handles `Where`/`OrderBy`/`Select` exists. So the alias is
decided at each join's `On`. First `On` wins; later `Where`/`OrderBy` lambda parameter names are
decorative and are ignored.

**Rules.**

| Case | Rule |
|---|---|
| Both sides named, valid, unique | Infer. `From<Product>().InnerJoin<Category>().On(p => …, c => …)` gives `p`, `c`. |
| Explicit `From<T>(alias)` / `InnerJoin<T>(alias)` | Wins. Inference fills nulls only. |
| Name null (hand-built tree), invalid identifier, dialect keyword | Inference off for the query. |
| Name collides with another alias or any table name in the query (`OrdinalIgnoreCase`) | Inference off for the query. |
| Same identifier both sides (`On(x => x.A, x => x.B)`) | Inference off for the query. |
| String-based `On` | Inference off for the query. |
| Self-join where inference is off | `t1`/`t2` scheme, not the table name. |
| Single-table query, no join | Never aliased. |

**Inference is all-or-nothing per query.** Either every alias-less table gets a valid unique
inferred alias, or none does and the query renders byte-identical to today. No new exceptions:
nothing that compiles today starts throwing. The cost is that one unusable lambda name silently
reverts the whole query to the verbose form, visible through `ToSql()`.

The self-join exception exists because rendering a self-join unaliased emits
`categories.parent_id = categories.category_id`, which is ambiguous SQL. That is a live bug today;
the `t1`/`t2` fallback fixes it.

**Rejected:** a fixed `t1`/`t2` scheme as the default (satisfies every constraint except
readability, which is the point); a first-letter-with-numbering scheme (invents a name the
developer did not write); a new `On<TFrom,TJoin>(p => …, c => …)` overload (redundant — the
existing overload already binds side by position and the type system guarantees it); throwing on
alias capture or on a `Where` lambda that reverses the established aliases (both invent failure
modes on code that compiles today).

### 2. Positional entity mapping, and the column aliases go

The builder writes the SELECT list, so each entity occupies a known contiguous ordinal range.
`MapEntity` reads `offset + i` instead of looking up `$"{prefix}{col.ColumnName}"`. The `f_`/`j_`
and `t1_`..`t4_` aliases then have no purpose and are dropped. Eight emit/map sites: arities 2, 3
and 4, sync and async.

Every column stays qualified with its table alias. Qualifying only the colliding ones was
considered and rejected: `EntityMetadata.Columns` holds mapped properties, not table columns, so an
unmapped colliding column (`[Ignore]`, or simply not declared) is invisible to the check and the
database raises `Ambiguous column name` on a column the developer never mentioned. Making the check
sound would need a live schema read per join query.

`GetPrefixedColumnsWithAlias`, the name-based `MapEntity` overload and `BuildOrdinalLookup` are
kept, not removed — they have direct unit tests and other callers.

### 3. Parameter names derived from the column

`ParameterCollection.CreateUniqueName` (`:85`) already derives, sanitizes (AUD-R22: non
letter/digit/underscore becomes `_`) and uniquifies a placeholder from column text. The
string-based `Where(string, object?)` overloads use it; the expression path does not. One library,
two answers.

The expression path routes through the same helper: `@p_category_id`, numbered only on a real
collision (`p.CategoryId == 1 || p.CategoryId == 2` → `@p_category_id`, `@p_category_id_2`). Four
sites: `JoinExpressionVisitor.cs:53`, `JoinExpressionVisitor3.cs:283`,
`JoinExpressionVisitor4.cs:294`, `JoinedGroupByExpressionVisitor.cs:162` (HAVING, `@jhp0`).

The string path keeps its unconditional `_<count>` suffix — its comment at `:70-74` explains it
separates `"p.category_id"` from `"p_category_id"`, which cannot arise when the name is built from
an alias and a column rather than free text.

## Acceptance criteria

1. The target SQL above is what `ToSql()` returns for the README's query, on all four dialects.
2. Self-join without explicit aliases emits distinct qualifiers and maps each tuple slot to its own
   table's row.
3. Every fallback case emits byte-identical SQL to `dev`.
4. Explicit aliases beat inferred ones.
5. Parameter names are derived from the column and unique within a query.
6. Full suite green on net8.0 and net10.0; databases reset via `scripts/reset-test-databases.ps1 -e`.
7. Every new test RED-phase checked against a broken target.
8. NativeAOT publish clean, with a golden-SQL case exercised under it.

## Files

- `src/Jaunty.Fluent/Builders/Join/` — `JoinClauseBuilder.cs`, `JoinedQueryBuilder.cs`,
  `JoinedQueryBuilderSelect{,Async}.cs`, `JoinedQueryBuilder3.cs`, `JoinedQueryBuilder4.cs`
- `src/Jaunty.Fluent/Builders/Query/QueryBuilder.cs` — from-alias settable before the first join
- `src/Jaunty.Fluent/Expressions/Join/JoinExpressionVisitor{,3,4}.cs`,
  `JoinedGroupByExpressionVisitor.cs` — parameter naming
- New: `src/Jaunty.Fluent/Internals/AliasInference.cs`
- `tests/Jaunty.Fluent.Tests/` — 8 files assert join SQL; new `Unit/Builders/Join/AliasInferenceTests.cs`
- `README.md`

---

## Outcome (2026-09-01)

Branch `feat/elegant-join-sql`, commit `70260f1d`.

| AC | Status | Evidence |
| --- | --- | --- |
| 1. Target SQL | met on SQLite and the bracket TestDialect | `FluentJoinSqlShapeTests.KeySelectorOn_InfersAliasesFromLambdaParameterNames` |
| 2. Self-join | met | `SelfJoin_WithoutUsableNames_TakesThePositionalScheme`, `SelfJoin_WithDistinctNames_UsesThem` |
| 3. Byte-identical fallback | met | `AliasEscapingFallbackTests`, `GroupedJoinPrefixAndHavingParameterTests` |
| 4. Explicit aliases win | met, after a fix | `ExplicitAliasesBeatInferredOnes`; arities 3 and 4 ignored `_alias` and were overwriting it |
| 5. Derived, unique parameters | met | `TwoFiltersOnOneColumn_NumberTheSecondParameter`, `FluentJoinPredicateOnTests` |
| 6. Full suite both TFMs | met | net10.0 and net8.0: 0 failed, 8,704 passed |
| 7. RED-phase check | met | three mutation rounds, below |
| 8. NativeAOT publish | partial | `samples/NativeAOT-FluentQuery` publishes clean on `net10.0`/`win-x64`; the only trim warnings are IL2026 from the sample's own `Program.cs:66,94` (`Expression.Bind`, `Expression.New`), and no Jaunty assembly warns. The published binary was not run or sized here - reading and executing under `bin/` is blocked in this environment, so the execution half belongs to the CI AOT leg. |

### RED-phase rounds

Each of the 12 new tests fails under at least one mutation.

| Round | Mutation | Tests it kills |
| --- | --- | --- |
| 1 | `ForJoin` returns `Fallback(...)` unconditionally; `JoinParameterNaming.Derive` gets an empty stem | 27, including 6 shape tests and every derived-name assertion |
| 2 | `Sanction` accepts every candidate; `MapEntity` drops the ordinal offset | 15, including all 4 fallback tests and `SelectBoth_MapsEachEntityFromItsOwnColumns` |
| 3 | `Infer` is passed null for both explicit aliases; the string `On` path forwards `"p"`/`"c"` | `ExplicitAliasesBeatInferredOnes`, `StringOn_DoesNotInfer` |

### Two source defects the suite found

- `AliasInference.ForAddedJoin` accepted a candidate equal to the table it aliases, emitting
  `JOIN suppliers suppliers`. It now declines, which is what leaves the unaliased form the
  caller's own column references expect.
- `JoinedQueryBuilder3.Infer` and `JoinedQueryBuilder4.Infer` inferred over the caller's explicit
  alias instead of returning it. Every existing test passed by coincidence, because each one had
  chosen an explicit alias equal to its lambda parameter name.

### Breaking change

A query that mixes a lambda `On` with a string column reference naming a table now fails at the
database: `On(p => p.CategoryId, c => c.CategoryId).Where("products.unit_price > 10")` renders
`FROM products p`, so `products.` no longer resolves. 21 tests across five files were written that
way and were updated to the alias. There is no way to keep both — an alias hides the table name —
so this is the cost of the elegant form.

### Not done

~~`JoinedGroupByExpressionVisitor.cs:162` still mints `@jhp0`.~~ Done 2026-09-02, see below.

`AddOnParameters` (the two-table `On(predicate)` path) still binds its names without uniquifying.
That is sound today because the ON is what creates the joined builder, so nothing has bound a name
yet - but a second `On` on the same clause builder re-adds them. That was true before this change
too, with `@jp0`, so it is pre-existing rather than introduced.

## Independent review (2026-09-01, fable `review-deep`)

Eight correctness questions put to a reviewer with no shell, given the test and AOT results as
facts. Three findings, one of them a defect in the merged change.

### Fixed: the positional fallback could emit a name the caller already owned

`AliasInference.cs:136` returned `Positional(slot)` without checking it against `taken`, which
`Sanction` checks for every other candidate. A caller who names the FROM alias `t3`, or writes a
lambda parameter `t3` in an earlier join, owns that name before the third join's step runs, and the
earlier ON is already a rendered string. Measured before the fix:

```sql
FROM products t3
INNER JOIN categories c ON (t3.category_id = c.category_id)
INNER JOIN products t3 ON (c.category_id = t3.category_id)
```

Two correlation names alike: SQLite rejects it, SQL Server reports "the correlation name 't3' is
specified multiple times in a FROM clause", and every `t3.` reference in the SELECT list is
ambiguous. The fallback now walks forward to the first free name (`t4` here). The two-table
`Fallback` is unaffected: it fires only when both sides are unaliased, where `t1`/`t2` cannot have
been claimed. `APositionalFallbackNameTheCallerAlreadyUsed_DoesNotProduceTwoT3s` covers it and
executes the query, so the engine's acceptance is part of the assertion.

### Recorded: the breaking change is wider than this plan first stated

The break is not confined to `Where(string)`. Every string-form API that names a table on a query
whose lambda `On` inferred aliases is affected, including a string `On` on a *later* join and
`SelectPartial`: `.On((p, c) => ...).InnerJoin<Supplier>().On("products.supplier_id = suppliers.supplier_id")`
now names nothing in the FROM clause. The `@jp0` to `@p_category_id` rename is also observable to
any interceptor or log parser matching parameter names.

### Open, unconfirmed: schema names are not in the collision set

`taken` is seeded with table names and aliases, never `SchemaName`. A query referencing an
unaliased table three-part (`[sales].[orders].[col]`) could acquire a later inferred alias equal to
the schema name. Engines differ on whether the reference still binds; the reviewer could not
confirm a failure and neither can this plan without a live SQL Server. Flagged, not fixed.

### No finding

All-or-nothing in `ForJoin`; case sensitivity (OrdinalIgnoreCase is the stricter test, so it costs
the verbose fallback and never wrong SQL); termination of `Derive`'s suffix loop; sanitize
collisions binding a value to another value's name; ordinal contiguity across partial, grouped and
self-joined selects.

Two stale artefacts noticed and left: the name-based `MapEntity(prefix, ordinals)` overload
(`JoinedQueryBuilder.cs:676`) now has no callers, and the `BuildOrdinalLookup` doc comment
(`JoinedQueryBuilder.cs:735`) still describes the removed `f_`/`j_` prefixes.

## The fourth site (2026-09-02)

`JoinedGroupByExpressionVisitor.cs:162` and its single-entity twin
`GroupedQueryBuilder.AddHavingParameter` now derive their names too, so no path is left on a
counter:

| before | after |
| --- | --- |
| `HAVING COUNT(*) > @jhp_0` | `HAVING COUNT(*) > @count` |
| `HAVING SUM(p.unit_price) > @jhp_0` | `HAVING SUM(p.unit_price) > @sum_p_unit_price` |
| `HAVING MIN(product_name) = @hp_0` | `HAVING MIN(product_name) = @min_product_name` |

The stem is the aggregate on the *other* side of the comparison, so `g.Count() > 3` and
`3 < g.Count()` both bind `@count`. It is built from the expression tree, never the rendered SQL:
AVG renders through `FractionalAverage`, whose CAST wrapper would otherwise reach the name as
`@avg_cast_p_unit_price_as_float`. The operator is left out, so widening `>` to `>=` does not
rename a parameter. A comparison with no aggregate on either side keeps the positional form.

### The deferral's stated blocker was wrong

The plan deferred this because a name derived from `COUNT(*)` "collides by construction" against
the five tests asserting two groupings off one builder get distinct names. That confuses
codebase-wide uniqueness with the scope a parameter name has, which is one query: the first
grouping takes `@count` and the second `@count_2`, and all five tests pass unmodified. Sameness
across queries is the feature - `HAVING COUNT(*) > @count` reads the same in every log line it
appears in.

### What the design removed

Names are minted against the query's own `ParameterCollection` inside the visitor, which is what
the single-entity path always did. `JoinedQueryBuilder.RegisterHavingParameters` and
`HavingParameterRenameRegexes` are gone with it. AUD-R35-016's collision is now prevented by
construction rather than repaired by a regex rewrite afterwards, and there is no longer a window in
which a value and its name are two separate strings that a rewrite could mismatch.

`ParameterCollection.CreateDerivedName` is the new primitive: `prefix + stem`, suffixed `_2`, `_3`
only on collision. It differs from `CreateUniqueName`'s unconditional `_<count>` because that
suffix exists to keep two distinct *caller-supplied* texts apart (AUD-R35-014), which does not
apply to a stem the builder rendered. Its taken test asks both `_names` and `_strippedNames`,
because `Add` throws on either (AUD-R35-201).

### Verification

11 new tests in `HavingParameterNamingTests`, all RED-phase checked:

| Mutation | Killed |
|---|---|
| joined `AggregateStem` always null | 9, incl. every joined naming assertion |
| `CreateDerivedName` never suffixes | 7, incl. all five AUD-R35-016 collision tests |
| single-entity stem drops its column | 2 |
| bound HAVING value shifted by 100 | `TheNamedOperandStillBindsItsValue` and 4 pre-existing |

Three pre-existing assertions changed with the behaviour, and no others:
`FluentGroupByJoinTests.cs:205`, `FluentGroupByTests.cs:409`,
`GroupedQueryBuilderParameterBindingTests.cs:38`.

Full solution net10.0: 8,720 passed, 0 failed.
