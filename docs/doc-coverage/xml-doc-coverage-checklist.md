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
| `QueryPartialFirstOrDefault.cs` | 4 overloads | no | TODO |
| `QueryPartialSingle.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryPartialSingleOrDefault.cs` | 4 overloads | yes | Complete with examples, remarks, exceptions |
| `QueryScalar.cs` | 4 overloads | no | TODO |
| `ExecuteScalar.cs` | 4 overloads | no | TODO |
| `QueryAsync.cs` | 4 overloads | no | TODO |
| `QueryFirstAsync.cs` | 4 overloads | no | TODO |
| `QueryFirstOrDefaultAsync.cs` | 4 overloads | no | TODO |
| `QuerySingleAsync.cs` | 4 overloads | no | TODO |
| `QuerySingleOrDefaultAsync.cs` | 4 overloads | no | TODO |
| `QueryPartialAsync.cs` | 4 overloads | no | TODO |
| `QueryPartialFirstAsync.cs` | 4 overloads | no | TODO |
| `QueryPartialFirstOrDefaultAsync.cs` | 4 overloads | no | TODO |
| `QueryPartialSingleAsync.cs` | 4 overloads | no | TODO |
| `QueryPartialSingleOrDefaultAsync.cs` | 4 overloads | no | TODO |
| `QueryScalarAsync.cs` | 4 overloads | no | TODO |
| `ExecuteScalarAsync.cs` | 4 overloads | no | TODO |
| `QueryMultiEntity.cs` | 8 overloads | no | TODO |
| `QueryMultiEntityAsync.cs` | 8 overloads | no | TODO |

**Progress**: 24/104 methods (23%)

---

## Write Methods (Write/)

| File | Methods | Status | Notes |
|------|---------|--------|-------|
| `Insert.cs` | `Insert<T>(entity)`, `Insert<T>(entity, options)` | yes | Complete with examples, remarks, exceptions |
| `InsertAsync.cs` | 2 overloads | no | TODO |
| `Update.cs` | `Update<T>(entity)`, `Update<T>(entity, options)` | yes | Complete with examples, remarks, exceptions |
| `UpdateAsync.cs` | 2 overloads | no | TODO |
| `Delete.cs` | 6 overloads | yes | Complete with examples, remarks, exceptions |
| `DeleteAsync.cs` | 6 overloads | no | TODO |
| `BulkInsert.cs` | 4 overloads | no | TODO |
| `BulkInsertAsync.cs` | 4 overloads | no | TODO |
| `BulkUpdate.cs` | 4 overloads | no | TODO |
| `BulkUpdateAsync.cs` | 4 overloads | no | TODO |
| `BulkDelete.cs` | 4 overloads | no | TODO |
| `BulkDeleteAsync.cs` | 4 overloads | no | TODO |
| `Upsert.cs` | 2 overloads | no | TODO |
| `UpsertAsync.cs` | 2 overloads | no | TODO |

**Progress**: 10/46 methods (22%)

---

## Multiple Result Sets (Multiple/)

| File | Methods | Status | Notes |
|------|---------|--------|-------|
| `QueryMultiple.cs` | 6 overloads | no | TODO |
| `QueryMultipleAsync.cs` | 6 overloads | no | TODO |

**Progress**: 0/12 methods (0%)

---

## Streaming Methods (Streaming/)

| File | Methods | Status | Notes |
|------|---------|--------|-------|
| `QueryStream.cs` | 4 overloads | no | TODO |
| `QueryStreamAsync.cs` | 4 overloads | no | TODO |
| `QueryPartialStream.cs` | 4 overloads | no | TODO |
| `QueryPartialStreamAsync.cs` | 4 overloads | no | TODO |
| `QueryPartialUnbuffered.cs` | 4 overloads | no | TODO |
| `QueryPartialUnbufferedAsync.cs` | 4 overloads | no | TODO |

**Progress**: 0/24 methods (0%)

---

## Stored Procedures (StoredProcedure/)

| File | Methods | Status | Notes |
|------|---------|--------|-------|
| `StoredProcedure.cs` | 4 overloads | no | TODO |
| `StoredProcedureAsync.cs` | 4 overloads | no | TODO |
| `ExecuteStoredProcedureWithOutput.cs` | 4 overloads | no | TODO |
| `ExecuteStoredProcedureWithOutputAsync.cs` | 4 overloads | no | TODO |
| `SpParameters.cs` | Helper class | no | TODO |

**Progress**: 0/16 methods (0%)

---

## Attributes (Attributes/)

| File | Type | Status | Notes |
|------|------|--------|-------|
| `TableAttribute.cs` | Class | no | TODO |
| `ColumnAttribute.cs` | Class | no | TODO |
| `KeyAttribute.cs` | Class | no | TODO |
| `IgnoreAttribute.cs` | Class | no | TODO |
| `DatabaseGeneratedAttribute.cs` | Class | no | TODO |
| `DatabaseGeneratedOptions.cs` | Enum | no | TODO |
| `AttributeHelper.cs` | Internal | caution | Internal - low priority |

**Progress**: 0/6 types (0%)

---

## Core Types (Core/)

| File | Type | Status | Notes |
|------|------|--------|-------|
| `CommandOptions.cs` | Struct | no | TODO |
| `GridReader.cs` | Class | no | TODO |

**Progress**: 0/2 types (0%)

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
| Query Methods | 36 | 104 | 35% |
| Write Methods | 10 | 46 | 22% |
| Multiple Result Sets | 0 | 12 | 0% |
| Streaming | 0 | 24 | 0% |
| Stored Procedures | 0 | 16 | 0% |
| Attributes | 0 | 6 | 0% |
| Core Types | 0 | 2 | 0% |
| Configuration | 0 | 1 | 0% |
| Interfaces | 0 | 2 | 0% |
| **TOTAL** | **46** | **213** | **22%** |

---

## Priority Order

1. **DONE**: `Query<T>()` and `QueryFirst<T>()` - Most commonly used
2. **DONE**: `Insert<T>()` - Core CRUD operation
3. **DONE**: `QueryFirstOrDefault<T>()`, `QuerySingle<T>()`, `QuerySingleOrDefault<T>()` - Common query methods
4. **DONE**: `Update<T>()`, `Delete<T>()` - Core CRUD operations
5. **DONE**: `QueryPartial<T>()` - Partial mapping support
6. **DONE**: `QueryPartialFirst<T>()`, `QueryPartialSingle<T>()`, `QueryPartialSingleOrDefault<T>()` - Partial variants
7. **NEXT**: `QueryPartialFirstOrDefault<T>()` - Last partial variant
8. **NEXT**: All async variants - Async/await support
9. **LATER**: `QueryScalar<T>()`, `ExecuteScalar<T>()` - Scalar queries
10. **LATER**: `QueryMultiple<T>()` - Multiple result sets
11. **LATER**: `Bulk*` operations - Bulk operations
12. **LATER**: `Upsert<T>()` - Upsert operations
13. **LATER**: Streaming methods - Advanced scenarios
14. **LATER**: Stored procedures - Specialized use cases
15. **LATER**: Attributes, Core types, Interfaces - Reference documentation

---

*Last Updated: 2026-02-19*
