# IJoinedQuery3 and IJoinedQuery4 API Extension Spec

**Created**: 2026-03-09
**Status**: Proposed
**Related**: `work/archive/2026-03-10-fluent-test-plan.md`

---

## Summary

Extend `IJoinedQuery3<T1, T2, T3>` and `IJoinedQuery4<T1, T2, T3, T4>` interfaces with additional query methods to match the functionality available in `IJoinedQuery<TFrom, TJoin>` (2-table joins).

---

## Current API (Minimal)

Both interfaces currently only support:
- `Where(Expression)` / `Where(string)`
- `Select()` - returns `List<T1>`
- `SelectAll()` - returns `List<(T1, T2, T3)>` or `List<(T1, T2, T3, T4)>`
- `ToSql()`

---

## Proposed API Extensions

### ORDER BY Methods

```csharp
// IJoinedQuery3
IJoinedQuery3<T1, T2, T3> OrderBy<TKey>(Expression<Func<T1, TKey>> keySelector);
IJoinedQuery3<T1, T2, T3> OrderByDescending<TKey>(Expression<Func<T1, TKey>> keySelector);
IJoinedQuery3<T1, T2, T3> OrderByJoined<TKey>(Expression<Func<T2, TKey>> keySelector);
IJoinedQuery3<T1, T2, T3> OrderByJoined<TKey>(Expression<Func<T3, TKey>> keySelector);
IJoinedQuery3<T1, T2, T3> ThenBy<TKey>(Expression<Func<T1, TKey>> keySelector);
IJoinedQuery3<T1, T2, T3> ThenByDescending<TKey>(Expression<Func<T1, TKey>> keySelector);
IJoinedQuery3<T1, T2, T3> ThenByJoined<TKey>(Expression<Func<T2, TKey>> keySelector);
IJoinedQuery3<T1, T2, T3> ThenByJoined<TKey>(Expression<Func<T3, TKey>> keySelector);

// IJoinedQuery4 - same pattern plus T4 variants
IJoinedQuery4<T1, T2, T3, T4> OrderByJoined<TKey>(Expression<Func<T4, TKey>> keySelector);
IJoinedQuery4<T1, T2, T3, T4> ThenByJoined<TKey>(Expression<Func<T4, TKey>> keySelector);
```

### Select First/Single Variants

```csharp
// IJoinedQuery3
T1 SelectFirst();
T1? SelectFirstOrDefault();
T1 SelectSingle();
T1? SelectSingleOrDefault();

// IJoinedQuery4 - same pattern
T1 SelectFirst();
T1? SelectFirstOrDefault();
T1 SelectSingle();
T1? SelectSingleOrDefault();
```

### SelectPartial Methods

```csharp
// IJoinedQuery3
List<dynamic> SelectPartial(string columns);
dynamic? SelectPartialFirstOrDefault(string columns);
List<IDictionary<string, object?>> SelectPartialAsDictionary(string columns);

// IJoinedQuery4 - same pattern
```

### Count Methods

```csharp
// IJoinedQuery3
int Count();
long LongCount();
Task<int> CountAsync(CancellationToken ct = default);
Task<long> LongCountAsync(CancellationToken ct = default);

// IJoinedQuery4 - same pattern
```

### Async Select Methods

```csharp
// IJoinedQuery3
Task<List<T1>> SelectAsync(CancellationToken ct = default);
Task<List<(T1, T2, T3)>> SelectAllAsync(CancellationToken ct = default);
Task<T1> SelectFirstAsync(CancellationToken ct = default);
Task<T1?> SelectFirstOrDefaultAsync(CancellationToken ct = default);

// IJoinedQuery4 - same pattern
```

### AND/OR Conditions

```csharp
// IJoinedQuery3
IJoinedQuery3<T1, T2, T3> And(Expression<Func<T1, T2, T3, bool>> predicate);
IJoinedQuery3<T1, T2, T3> Or(Expression<Func<T1, T2, T3, bool>> predicate);

// IJoinedQuery4 - same pattern
```

---

## Implementation Files

### New/Modified Files

1. **Interfaces** (`src/Jaunty.Fluent/Interfaces/IJoinedQuery.cs`):
   - Extend `IJoinedQuery3<T1, T2, T3>` interface
   - Extend `IJoinedQuery4<T1, T2, T3, T4>` interface

2. **Query Builders**:
   - `src/Jaunty.Fluent/JoinedQueryBuilder3.cs` - Implement IJoinedQuery3 methods
   - `src/Jaunty.Fluent/JoinedQueryBuilder4.cs` - Implement IJoinedQuery4 methods

3. **Async Extensions** (if needed):
   - `src/Jaunty.Fluent/JoinedQueryBuilder3SelectAsync.cs`
   - `src/Jaunty.Fluent/JoinedQueryBuilder4SelectAsync.cs`

4. **OrderBy Support**:
   - `src/Jaunty.Fluent/JoinedQueryBuilder3OrderBy.cs`
   - `src/Jaunty.Fluent/JoinedQueryBuilder4OrderBy.cs`

---

## Test Coverage

Tests will be added to `FluentMultiTableJoinTests.cs` covering:
- OrderBy/OrderByJoined/ThenBy variants
- SelectFirst/SelectFirstOrDefault/SelectSingle/SelectSingleOrDefault
- SelectPartial variants
- Count/LongCount (sync + async)
- SelectAsync/SelectAllAsync variants
- And/Or conditions

---

## Priority

Following Jaunty priorities:
1. **Performance** - Avoid LINQ in hot paths, use span-based SQL building
2. **NativeAOT** - No runtime reflection, use compile-time generics
3. **Tests** - Write tests before implementation
4. **Spec-First** - This spec approved before coding begins

---

## Notes

- The 4-table join tests in the original plan require Order entity with ProductId, which doesn't exist in Northwind. Either:
  - Add OrderDetail entity to test entities
  - Use a different 4-table relationship (e.g., Order → Customer → ...)
  - Test 4-table joins with string-based joins only

- Async methods may hit SQLite DataReader limitations. Use `[SkipSQLiteAsyncFact]` where needed.
