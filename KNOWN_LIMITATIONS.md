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

### 2. No `ThenBy` Chaining in Joined Queries
`IJoinedQuery<TFrom, TJoin>` supports `OrderBy()` and `OrderByDescending()` but cannot chain multiple sort columns with `ThenBy()`.

**Workaround:** Use a single `OrderBy()` on the primary sort column, or use raw SQL for multi-column sorting.

### 3. Missing `OnRaw(string, object)` Overload
`IJoinClause.OnRaw()` only accepts a raw SQL string with no way to pass parameters.

**Workaround:** Use `On()` with expression keys, or embed literal values in the raw SQL (not recommended for user input).

## Medium Priority

### 4. `OrderByDesc` vs `OrderByDescending` Naming
`WindowBuilder<T>` uses `OrderByDesc()` while all other APIs use `OrderByDescending()`.

**Impact:** Minor inconsistency, both work correctly.

### 5. `IDistinctClause<T>` Limited WHERE Methods
After `.Distinct()`, only basic `Where()` is available. Missing:
- `WhereIn()` / `WhereNotIn()`
- `WhereBetween()` / `WhereNotBetween()`
- `WhereExists()` / `WhereNotExists()`

**Workaround:** Apply these filters before `.Distinct()` in the query chain.

### 6. Missing Async in `ICteQueryClause<T>`
CTE queries have `SelectFirstAsync()` and `SelectFirstOrDefaultAsync()` missing.

**Workaround:** Use `SelectAsync()` and take the first element.

## Low Priority

### 7. `SelectCount()` Alias Redundancy
`IQueryTerminal<T>` has both `Count()` and `SelectCount()` which do the same thing. This pattern is not consistent across all aggregate methods.

**Note:** Both work identically. Use whichever you prefer.

### 8. SQLite `Count()` Returns Int64
The `Count()` method uses `QueryScalar<int>` but SQLite's `COUNT(*)` returns `Int64`, causing a cast exception.

**Workaround:** Use `LongCount()` instead when working with SQLite.

---

## Reporting Issues

If you encounter additional limitations or have feature requests, please open an issue at:
https://github.com/extrode/jaunty/issues
