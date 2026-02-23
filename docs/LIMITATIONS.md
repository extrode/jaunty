# Jaunty.Fluent Known Limitations

This document lists known API limitations and inconsistencies that will be addressed in future versions.

## High Priority (v2 Roadmap)

### 1. `IJoinedQuery3<>` (3-Way Joins) Limited API
The 3-way join interface is significantly less capable than 2-way joins:
- No async variants
- No `OrderBy()` / `ThenBy()` support
- No `Take()` / `Skip()` pagination
- No `Count()` / `LongCount()`

**Workaround:** Use raw SQL for complex 3+ table joins, or chain 2-way joins.

## Medium Priority

### 2. `IDistinctClause<T>` Limited WHERE Methods
After `.Distinct()`, only basic `Where()` is available. Missing:
- `WhereIn()` / `WhereNotIn()`
- `WhereBetween()` / `WhereNotBetween()`
- `WhereExists()` / `WhereNotExists()`

**Workaround:** Apply these filters before `.Distinct()` in the query chain.

### 3. Missing Async in `ICteQueryClause<T>`
CTE queries have `SelectFirstAsync()` and `SelectFirstOrDefaultAsync()` missing.

**Workaround:** Use `SelectAsync()` and take the first element.

## Low Priority

### 4. `SelectCount()` Alias Redundancy
`IQueryTerminal<T>` has both `Count()` and `SelectCount()` which do the same thing. This pattern is not consistent across all aggregate methods.

**Note:** Both work identically. Use whichever you prefer.

### 5. `COUNT(*)` Return Type Varies by Provider
`COUNT(*)` does not return the same CLR type across providers:
- SQL Server commonly returns `Int32`
- PostgreSQL, MariaDB/MySQL, and SQLite commonly return `Int64`

If you call scalar methods with a mismatched type (for example `QueryScalar<int>` against a provider returning `Int64`), a cast/conversion error can occur.

**Workaround:** For cross-dialect code, prefer `QueryScalar<long>` / `ExecuteScalar<long>` (or fluent `LongCount()`).

### 6. `IMapped<T>.ReadEntity` Is Treated As Strict Mapping
`IMapped<T>` / `ReadEntity(IDataReader)` is intended for full-shape entity mapping. Partial/projection queries may omit columns, so relying on `ReadEntity` for those queries is unsafe unless your mapper explicitly handles missing columns.

**Workaround:** For partial/projection queries, use `QueryPartial*` with a projection-safe mapper via `CommandOptions<T>.WithMapper(...)` (or reflection-based partial mapping).

---

## Reporting Issues

If you encounter additional limitations or have feature requests, please open an issue at:
https://github.com/extrode/jaunty/issues
