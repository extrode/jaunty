# XML Documentation Coverage Tracker

Tracks XML documentation coverage for all public API methods in Jaunty.

**Legend**: = Documented | = Not Documented | = Partially Documented

---

## Query Methods (Read/)

| File | Methods | Status | Notes |
|------|---------|--------|-------|
| `Query.cs` | `Query<T>(sql)`, `Query<T>(sql, params)`, `Query<T>(sql, options)`, `Query<T>(sql, params, options)` | yes | Complete with examples, remarks, exceptions |
| `QueryFirst.cs` | `QueryFirst<T>(sql)`, `QueryFirst<T>(sql, params)`, `QueryFirst<T>(sql, options)`, `QueryFirst<T>(sql, params, options)` | yes | Complete with examples, remarks, exceptions |
| `QueryFirstOrDefault.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QuerySingle.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QuerySingleOrDefault.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartial.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialFirst.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialFirstOrDefault.cs` | 4 overloads | caution | Basic docs present, needs examples |
| `QueryPartialSingle.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialSingleOrDefault.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryScalar.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `ExecuteScalar.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryFirstAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryFirstOrDefaultAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QuerySingleAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QuerySingleOrDefaultAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialFirstAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialFirstOrDefaultAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialSingleAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialSingleOrDefaultAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryScalarAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `ExecuteScalarAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryMultiEntity.cs` | 26 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryMultiEntityAsync.cs` | 26 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryMultiple.cs` | 6 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryMultipleAsync.cs` | 8 overloads | yes | Complete with examples, remarks, exceptions |

**Progress**: 154/172 methods (89%)

---

## Write Methods (Write/)

| File | Methods | Status | Notes |
|------|---------|--------|-------|
| `Insert.cs` | `Insert<T>(entity)`, `Insert<T>(entity, options)` | yes | Complete with examples, remarks, exceptions |
| `InsertAsync.cs` | 2 overloads | yes | Complete with examples, remarks, exceptions |
| `Update.cs` | `Update<T>(entity)`, `Update<T>(entity, options)` | yes | Complete with examples, remarks, exceptions |
| `UpdateAsync.cs` | 2 overloads | yes | Complete with examples, remarks, exceptions |
| `Delete.cs` | 6 overloads | yes | Complete with examples, remarks, exceptions |
| `DeleteAsync.cs` | 6 overloads | yes | Complete with examples, remarks, exceptions |
| `BulkInsert.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `BulkInsertAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `BulkUpdate.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `BulkUpdateAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `BulkDelete.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `BulkDeleteAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `Upsert.cs` | 2 overloads | yes | Complete with examples, remarks, exceptions |
| `UpsertAsync.cs` | 2 overloads | yes | Complete with examples, remarks, exceptions |

**Progress**: 46/46 methods (100%)

---

## Multiple Result Sets (Multiple/)

| File | Methods | Status | Notes |
|------|---------|--------|-------|
| `QueryMultiple.cs` | 6 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryMultipleAsync.cs` | 8 overloads | yes | Complete with examples, remarks, exceptions |

**Progress**: 14/14 methods (100%)

---

## Streaming Methods (Streaming/)

| File | Methods | Status | Notes |
|------|---------|--------|-------|
| `QueryStream.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryStreamAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialStream.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialStreamAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialUnbuffered.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialUnbufferedAsync.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |

**Progress**: 24/24 methods (100%)

---

## Stored Procedures (StoredProcedure/)

| File | Methods | Status | Notes |
|------|---------|--------|-------|
| `StoredProcedure.cs` | 16 overloads | yes | Complete with examples, remarks, exceptions |
| `StoredProcedureAsync.cs` | 16 overloads | yes | Complete with examples, remarks, exceptions |
| `ExecuteStoredProcedureWithOutput.cs` | 5 overloads | yes | Complete with examples, remarks, exceptions |
| `ExecuteStoredProcedureWithOutputAsync.cs` | 5 overloads | yes | Complete with examples, remarks, exceptions |
| `SpParameters.cs` | Helper class | yes | Complete with examples, remarks |

**Progress**: 32/32 methods (100%)

---

## Attributes (Attributes/)

| File | Type | Status | Notes |
|------|------|--------|-------|
| `TableAttribute.cs` | Class | yes | Complete with examples, remarks |
| `ColumnAttribute.cs` | Class | yes | Complete with examples, remarks |
| `KeyAttribute.cs` | Class | yes | Complete with examples, remarks |
| `IgnoreAttribute.cs` | Class | yes | Complete with examples, remarks |
| `DatabaseGeneratedAttribute.cs` | Class | yes | Complete with examples, remarks |
| `DatabaseGeneratedOptions.cs` | Enum | yes | Complete with examples, remarks |
| `AttributeHelper.cs` | Internal | yes | Complete (internal) |

**Progress**: 7/7 types (100%)

---

## Core Types (Core/)

| File | Type | Status | Notes |
|------|------|--------|-------|
| `CommandOptions.cs` | Struct | yes | Complete with examples, remarks |
| `GridReader.cs` | Class | yes | Complete with examples, remarks, exceptions |

**Progress**: 2/2 types (100%)

---

## Configuration (Configuration/)

| File | Type | Status | Notes |
|------|------|--------|-------|
| `JauntyConfig.cs` | Static class | no | TODO |

**Progress**: 0/1 types (0%)

---

## Interfaces (Interfaces/)

| File | Type | Status | Notes |
|------|------|--------|-------|
| `IEntity.cs` | Interface | no | TODO |
| `IMapped.cs` | Interface | no | TODO |

**Progress**: 0/2 types (0%)

---

## Overall Progress

| Category | Documented | Total | Percentage |
|----------|------------|-------|------------|
| Query Methods | 140 | 156 | 90% |
| Write Methods | 46 | 46 | 100% |
| Multiple Result Sets | 14 | 14 | 100% |
| Streaming | 24 | 24 | 100% |
| Stored Procedures | 32 | 32 | 100% |
| Attributes | 7 | 7 | 100% |
| Core Types | 2 | 2 | 100% |
| Configuration | 0 | 1 | 0% |
| Interfaces | 0 | 2 | 0% |
| **TOTAL** | **265** | **284** | **93%** |

---

## Priority Order

1. **DONE**: `Query<T>()` and `QueryFirst<T>()` - Most commonly used
2. **DONE**: `Insert<T>()` - Core CRUD operation
3. **DONE**: `QueryFirstOrDefault<T>()`, `QuerySingle<T>()`, `QuerySingleOrDefault<T>()` - Common query methods
4. **DONE**: `Update<T>()`, `Delete<T>()` - Core CRUD operations
5. **DONE**: `QueryPartial<T>()` - Partial mapping support
6. **DONE**: `QueryPartialFirst<T>()`, `QueryPartialSingle<T>()`, `QueryPartialSingleOrDefault<T>()` - Partial variants
7. **DONE**: `QuerySingleAsync<T>()` - Async single variant
8. **DONE**: `QuerySingleOrDefaultAsync<T>()` - Async single variant (returns null)
9. **DONE**: `QueryPartialFirstAsync<T>()` - Async partial first variant
10. **DONE**: `QueryPartialSingleAsync<T>()`, `QueryPartialSingleOrDefaultAsync<T>()` - Async partial single variants
11. **DONE**: `QueryPartialAsync<T>()`, `QueryPartialFirstOrDefaultAsync<T>()` - Async partial variants
12. **DONE**: `QueryAsync<T>()`, `QueryFirstAsync<T>()`, `QueryFirstOrDefaultAsync<T>()` - Async query variants
13. **DONE**: `QueryScalar<T>()`, `ExecuteScalar<T>()` - Scalar queries (sync and async)
14. **DONE**: `QueryMultiple<T>()` - Multiple result sets (100% complete)
15. **DONE**: `QueryMultiEntity<T1, T2>()` - Multi-entity mapping (sync and async)
16. **DONE**: Async Write operations (Insert, Update, Delete)
17. **DONE**: `Bulk*` operations - Bulk operations (Insert, Update, Delete - sync and async)
18. **DONE**: `Upsert<T>()` - Upsert operations (sync and async)
19. **DONE**: Streaming methods - All streaming variants (100% complete)
20. **DONE**: Stored procedures - All variants (100% complete)
21. **DONE**: Attributes - All attribute types (100% complete)
22. **DONE**: Core Types - CommandOptions, GridReader (100% complete)
23. **NEXT**: Configuration - `JauntyConfig.cs`
24. **LATER**: Interfaces - `IEntity`, `IMapped`

---

*Last Updated: 2026-02-19*
