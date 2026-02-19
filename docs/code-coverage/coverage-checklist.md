# Jaunty Code Coverage Checklist

**Target**: 100% Code Coverage
**Created**: 2026-02-19
**Last Updated**: 2026-02-19

This document tracks test coverage for the **Jaunty micro-ORM** codebase. Each item includes the class/method and its test status.

---

## Legend

- = Covered by tests
- = Not covered (needs tests)
- = Partially covered (needs more tests)

---

## 1. Read Methods (`src/Jaunty/Read/`)

### Query (Strict Mapping)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `Query<T>(sql)` | yes | QueryTests.cs | All columns, strict mapping |
| `Query<T>(sql, parameters)` | yes | QueryTests.cs | Named and positional params |
| `Query<T>(sql, options)` | yes | QueryCommandOptionsTests.cs | CommandOptions overload |
| `Query<T>(sql, parameters, options)` | yes | QueryTests.cs | Full overload |
| `QueryAsync<T>(sql)` | yes | QueryAsyncTests.cs | Async variant |
| `QueryAsync<T>(sql, parameters)` | yes | QueryAsyncTests.cs | Async with params |
| `QueryAsync<T>(sql, options)` | yes | QueryAsyncTests.cs | Async with options |
| `QueryAsync<T>(sql, parameters, options)` | yes | QueryAsyncTests.cs | Full async overload |

### QueryPartial (Partial Mapping)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryPartial<T>(sql)` | yes | QueryTests.cs | Partial mapping mode |
| `QueryPartial<T>(sql, parameters)` | yes | QueryPartialAsyncTests.cs | Via async tests |
| `QueryPartial<T>(sql, options)` | - | QueryPartialAsyncTests.cs | Implicit via other tests |
| `QueryPartial<T>(sql, parameters, options)` | - | QueryPartialAsyncTests.cs | Implicit via other tests |
| `QueryPartialAsync<T>(sql)` | yes | QueryPartialAsyncTests.cs | Async variant |
| `QueryPartialAsync<T>(sql, parameters)` | yes | QueryPartialAsyncTests.cs | Async with params |
| `QueryPartialAsync<T>(sql, options)` | - | QueryPartialAsyncTests.cs | Needs explicit test |
| `QueryPartialAsync<T>(sql, parameters, options)` | - | QueryPartialAsyncTests.cs | Needs explicit test |

### QueryFirst (Strict)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryFirst<T>(sql)` | yes | QueryFirstTests.cs | Returns first, throws if empty |
| `QueryFirst<T>(sql, parameters)` | yes | QueryFirstTests.cs | With parameters |
| `QueryFirst<T>(sql, options)` | yes | QueryFirstTests.cs | With CommandOptions |
| `QueryFirst<T>(sql, parameters, options)` | yes | QueryFirstTests.cs | Full overload |
| `QueryFirstAsync<T>(sql)` | yes | QueryFirstAsyncTests.cs | Async variant |
| `QueryFirstAsync<T>(sql, parameters)` | yes | QueryFirstAsyncTests.cs | Async with params |
| `QueryFirstAsync<T>(sql, options)` | yes | QueryFirstAsyncTests.cs | Async with options |
| `QueryFirstAsync<T>(sql, parameters, options)` | yes | QueryFirstAsyncTests.cs | Full async overload |

### QueryFirstOrDefault (Strict)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryFirstOrDefault<T>(sql)` | yes | QueryFirstOrDefaultTests.cs | Returns null if empty |
| `QueryFirstOrDefault<T>(sql, parameters)` | yes | QueryFirstOrDefaultTests.cs | With parameters |
| `QueryFirstOrDefault<T>(sql, options)` | yes | QueryFirstOrDefaultTests.cs | With options |
| `QueryFirstOrDefault<T>(sql, parameters, options)` | yes | QueryFirstOrDefaultTests.cs | Full overload |
| `QueryFirstOrDefaultAsync<T>(sql)` | yes | QueryFirstOrDefaultAsyncTests.cs | Async variant |
| `QueryFirstOrDefaultAsync<T>(sql, parameters)` | yes | QueryFirstOrDefaultAsyncTests.cs | Async with params |
| `QueryFirstOrDefaultAsync<T>(sql, options)` | yes | QueryFirstOrDefaultAsyncTests.cs | Async with options |
| `QueryFirstOrDefaultAsync<T>(sql, parameters, options)` | yes | QueryFirstOrDefaultAsyncTests.cs | Full async overload |

### QuerySingle (Strict)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QuerySingle<T>(sql)` | yes | QuerySingleTests.cs | Returns single, throws if != 1 |
| `QuerySingle<T>(sql, parameters)` | yes | QuerySingleTests.cs | With parameters |
| `QuerySingle<T>(sql, options)` | yes | QuerySingleTests.cs | With options |
| `QuerySingle<T>(sql, parameters, options)` | yes | QuerySingleTests.cs | Full overload |
| `QuerySingleAsync<T>(sql)` | yes | QuerySingleAsyncTests.cs | Async variant |
| `QuerySingleAsync<T>(sql, parameters)` | yes | QuerySingleAsyncTests.cs | Async with params |
| `QuerySingleAsync<T>(sql, options)` | yes | QuerySingleAsyncTests.cs | Async with options |
| `QuerySingleAsync<T>(sql, parameters, options)` | yes | QuerySingleAsyncTests.cs | Full async overload |

### QuerySingleOrDefault (Strict)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QuerySingleOrDefault<T>(sql)` | yes | QuerySingleOrDefaultTests.cs | Returns null if empty |
| `QuerySingleOrDefault<T>(sql, parameters)` | yes | QuerySingleOrDefaultTests.cs | With parameters |
| `QuerySingleOrDefault<T>(sql, options)` | yes | QuerySingleOrDefaultTests.cs | With options |
| `QuerySingleOrDefault<T>(sql, parameters, options)` | yes | QuerySingleOrDefaultTests.cs | Full overload |
| `QuerySingleOrDefaultAsync<T>(sql)` | yes | QuerySingleOrDefaultAsyncTests.cs | Async variant |
| `QuerySingleOrDefaultAsync<T>(sql, parameters)` | yes | QuerySingleOrDefaultAsyncTests.cs | Async with params |
| `QuerySingleOrDefaultAsync<T>(sql, options)` | yes | QuerySingleOrDefaultAsyncTests.cs | Async with options |
| `QuerySingleOrDefaultAsync<T>(sql, parameters, options)` | yes | QuerySingleOrDefaultAsyncTests.cs | Full async overload |

### QueryPartialFirst (Partial)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryPartialFirst<T>(sql)` | todo | — | Zero coverage |
| `QueryPartialFirst<T>(sql, parameters)` | todo | — | Zero coverage |
| `QueryPartialFirst<T>(sql, options)` | todo | — | Zero coverage |
| `QueryPartialFirst<T>(sql, parameters, options)` | todo | — | Zero coverage |
| `QueryPartialFirstAsync<T>(sql)` | todo | — | Zero coverage |
| `QueryPartialFirstAsync<T>(sql, parameters)` | todo | — | Zero coverage |
| `QueryPartialFirstAsync<T>(sql, options)` | todo | — | Zero coverage |
| `QueryPartialFirstAsync<T>(sql, parameters, options)` | todo | — | Zero coverage |

### QueryPartialFirstOrDefault (Partial)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryPartialFirstOrDefault<T>(sql)` | todo | — | Zero coverage |
| `QueryPartialFirstOrDefault<T>(sql, parameters)` | todo | — | Zero coverage |
| `QueryPartialFirstOrDefault<T>(sql, options)` | todo | — | Zero coverage |
| `QueryPartialFirstOrDefault<T>(sql, parameters, options)` | todo | — | Zero coverage |
| `QueryPartialFirstOrDefaultAsync<T>(sql)` | todo | — | Zero coverage |
| `QueryPartialFirstOrDefaultAsync<T>(sql, parameters)` | todo | — | Zero coverage |
| `QueryPartialFirstOrDefaultAsync<T>(sql, options)` | todo | — | Zero coverage |
| `QueryPartialFirstOrDefaultAsync<T>(sql, parameters, options)` | todo | — | Zero coverage |

### QueryPartialSingle (Partial)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryPartialSingle<T>(sql)` | todo | — | Zero coverage |
| `QueryPartialSingle<T>(sql, parameters)` | todo | — | Zero coverage |
| `QueryPartialSingle<T>(sql, options)` | todo | — | Zero coverage |
| `QueryPartialSingle<T>(sql, parameters, options)` | todo | — | Zero coverage |
| `QueryPartialSingleAsync<T>(sql)` | todo | — | Zero coverage |
| `QueryPartialSingleAsync<T>(sql, parameters)` | todo | — | Zero coverage |
| `QueryPartialSingleAsync<T>(sql, options)` | todo | — | Zero coverage |
| `QueryPartialSingleAsync<T>(sql, parameters, options)` | todo | — | Zero coverage |

### QueryPartialSingleOrDefault (Partial)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryPartialSingleOrDefault<T>(sql)` | todo | — | Zero coverage |
| `QueryPartialSingleOrDefault<T>(sql, parameters)` | todo | — | Zero coverage |
| `QueryPartialSingleOrDefault<T>(sql, options)` | todo | — | Zero coverage |
| `QueryPartialSingleOrDefault<T>(sql, parameters, options)` | todo | — | Zero coverage |
| `QueryPartialSingleOrDefaultAsync<T>(sql)` | todo | — | Zero coverage |
| `QueryPartialSingleOrDefaultAsync<T>(sql, parameters)` | todo | — | Zero coverage |
| `QueryPartialSingleOrDefaultAsync<T>(sql, options)` | todo | — | Zero coverage |
| `QueryPartialSingleOrDefaultAsync<T>(sql, parameters, options)` | todo | — | Zero coverage |

### QueryScalar

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryScalar<T>(sql)` | yes | QueryScalarTests.cs | Scalar value query |
| `QueryScalar<T>(sql, parameters)` | yes | QueryScalarTests.cs | With parameters |
| `QueryScalar<T>(sql, options)` | yes | QueryScalarTests.cs | With options |
| `QueryScalar<T>(sql, parameters, options)` | yes | QueryScalarTests.cs | Full overload |
| `QueryScalarAsync<T>(sql)` | yes | QueryScalarAsyncTests.cs | Async variant |
| `QueryScalarAsync<T>(sql, parameters)` | yes | QueryScalarAsyncTests.cs | Async with params |
| `QueryScalarAsync<T>(sql, options)` | yes | QueryScalarAsyncTests.cs | Async with options |
| `QueryScalarAsync<T>(sql, parameters, options)` | yes | QueryScalarAsyncTests.cs | Full async overload |

### ExecuteScalar

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `ExecuteScalar<T>(sql)` | todo | — | Zero coverage, separate from QueryScalar |
| `ExecuteScalar<T>(sql, parameters)` | todo | — | Zero coverage |
| `ExecuteScalar<T>(sql, options)` | todo | — | Zero coverage |
| `ExecuteScalar<T>(sql, parameters, options)` | todo | — | Zero coverage |
| `ExecuteScalarAsync<T>(sql)` | todo | — | Zero coverage |
| `ExecuteScalarAsync<T>(sql, parameters)` | todo | — | Zero coverage |
| `ExecuteScalarAsync<T>(sql, options)` | todo | — | Zero coverage |
| `ExecuteScalarAsync<T>(sql, parameters, options)` | todo | — | Zero coverage |

### QueryMultiEntity (Multi-Table Mapping)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `Query<T1, T2>(sql)` | yes | QueryMultiEntityTests.cs | Tuple mapping |
| `Query<T1, T2>(sql, parameters)` | yes | QueryMultiEntityTests.cs | With params |
| `Query<T1, T2>(sql, options)` | yes | QueryMultiEntityTests.cs | With options |
| `Query<T1, T2>(sql, parameters, options)` | yes | QueryMultiEntityTests.cs | Full overload |
| `QueryFirst<T1, T2>(sql)` | yes | QueryMultiEntityTests.cs | First tuple |
| `QueryFirst<T1, T2>(sql, parameters)` | yes | QueryMultiEntityTests.cs | With params |
| `QueryFirstOrDefault<T1, T2>(sql)` | yes | QueryMultiEntityTests.cs | First or null |
| `QuerySingle<T1, T2>(sql)` | yes | QueryMultiEntityTests.cs | Single tuple |
| `QuerySingleOrDefault<T1, T2>(sql)` | yes | QueryMultiEntityTests.cs | Single or null |
| `QueryStream<T1, T2>(sql)` | yes | QueryMultiEntityTests.cs | Streaming tuples |
| Async variants (all) | yes | QueryMultiEntityTests.cs | Async counterparts |
| `[Obsolete]` overloads | - | — | Legacy overloads, low priority |

### Special Type Queries

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `Query<Dictionary<string, object>>` | yes | QueryDictionaryTests.cs | Dictionary mapping |
| `Query<ExpandoObject>` (dynamic) | yes | QueryDynamicTests.cs | Dynamic mapping |
| `Query<KeyValuePair<K,V>>` | yes | QueryKeyValuePairTests.cs | KeyValuePair mapping |
| `Query<ValueTuple<...>>` | yes | QueryValueTupleTests.cs | ValueTuple mapping |

---

## 2. Streaming Methods (`src/Jaunty/Streaming/`)

### QueryStream (Strict)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryStream<T>(sql)` | yes | QueryStreamTests.cs | Lazy IEnumerable |
| `QueryStream<T>(sql, parameters)` | yes | QueryStreamTests.cs | With params |
| `QueryStream<T>(sql, options)` | yes | QueryStreamTests.cs | With options |
| `QueryStream<T>(sql, parameters, options)` | yes | QueryStreamTests.cs | Full overload |
| `QueryStreamAsync<T>(sql)` | yes | QueryStreamAsyncTests.cs | Async variant |
| `QueryStreamAsync<T>(sql, parameters)` | yes | QueryStreamAsyncTests.cs | Async with params |
| `QueryStreamAsync<T>(sql, options)` | yes | QueryStreamAsyncTests.cs | Async with options |
| `QueryStreamAsync<T>(sql, parameters, options)` | yes | QueryStreamAsyncTests.cs | Full async overload |

### QueryPartialStream (Partial)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryPartialStream<T>(sql)` | yes | QueryPartialStreamTests.cs | Partial lazy enumeration |
| `QueryPartialStream<T>(sql, parameters)` | yes | QueryPartialStreamTests.cs | With params |
| `QueryPartialStream<T>(sql, options)` | yes | QueryPartialStreamTests.cs | With options |
| `QueryPartialStream<T>(sql, parameters, options)` | yes | QueryPartialStreamTests.cs | Full overload |
| `QueryPartialStreamAsync<T>(sql)` | yes | QueryPartialStreamAsyncTests.cs | Async variant |
| `QueryPartialStreamAsync<T>(sql, parameters)` | yes | QueryPartialStreamAsyncTests.cs | Async with params |
| `QueryPartialStreamAsync<T>(sql, options)` | yes | QueryPartialStreamAsyncTests.cs | Async with options |
| `QueryPartialStreamAsync<T>(sql, parameters, options)` | yes | QueryPartialStreamAsyncTests.cs | Full async overload |

### QueryPartialUnbuffered

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryPartialUnbuffered<T>(sql)` | todo | — | Zero coverage |
| `QueryPartialUnbuffered<T>(sql, parameters)` | todo | — | Zero coverage |
| `QueryPartialUnbuffered<T>(sql, options)` | todo | — | Zero coverage |
| `QueryPartialUnbuffered<T>(sql, parameters, options)` | todo | — | Zero coverage |
| `QueryPartialUnbufferedAsync<T>(sql)` | todo | — | Zero coverage |
| `QueryPartialUnbufferedAsync<T>(sql, parameters)` | todo | — | Zero coverage |
| `QueryPartialUnbufferedAsync<T>(sql, options)` | todo | — | Zero coverage |
| `QueryPartialUnbufferedAsync<T>(sql, parameters, options)` | todo | — | Zero coverage |

---

## 3. Write Methods (`src/Jaunty/Write/`)

### Insert (Individual)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `Insert<T>(entity)` | todo | — | Individual insert, zero coverage |
| `Insert<T>(entity, options)` | todo | — | With CommandOptions |
| `InsertAsync<T>(entity)` | todo | — | Async variant |
| `InsertAsync<T>(entity, options)` | todo | — | Async with options |

### Update (Individual)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `Update<T>(entity)` | todo | — | Individual update, zero coverage |
| `Update<T>(entity, options)` | todo | — | With CommandOptions |
| `UpdateAsync<T>(entity)` | todo | — | Async variant |
| `UpdateAsync<T>(entity, options)` | todo | — | Async with options |

### Delete (Individual)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `Delete<T>(entity)` | todo | — | Delete by entity |
| `Delete<T>(entity, options)` | todo | — | With CommandOptions |
| `Delete<T>(id)` | todo | — | Delete by object ID |
| `Delete<T>(id, options)` | todo | — | With CommandOptions |
| `Delete<T, TId>(id)` | todo | — | Delete by typed ID (IEntity<TId>) |
| `Delete<T, TId>(id, options)` | todo | — | With CommandOptions |
| `DeleteAsync<T>(entity)` | todo | — | Async variants |
| `DeleteAsync<T>(entity, options)` | todo | — | |
| `DeleteAsync<T>(id)` | todo | — | |
| `DeleteAsync<T>(id, options)` | todo | — | |
| `DeleteAsync<T, TId>(id)` | todo | — | |
| `DeleteAsync<T, TId>(id, options)` | todo | — | |

### BulkInsert

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `BulkInsert<T>(entities)` | yes | BulkOperationsTests.cs | Bulk insert |
| `BulkInsert<T>(entities, options)` | yes | BulkOperationsTests.cs | With options |
| `BulkInsertIgnoreConstraints<T>(entities)` | - | BulkOperationsTests.cs | Needs explicit test |
| `BulkInsertIgnoreConstraints<T>(entities, options)` | - | BulkOperationsTests.cs | Needs explicit test |
| `BulkInsertAsync<T>(entities)` | yes | BulkOperationsAsyncTests.cs | Async variant |
| `BulkInsertAsync<T>(entities, options)` | yes | BulkOperationsAsyncTests.cs | Async with options |
| `BulkInsertIgnoreConstraintsAsync<T>(...)` | - | BulkOperationsAsyncTests.cs | Needs explicit test |

### BulkUpdate

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `BulkUpdate<T>(entities)` | yes | BulkOperationsTests.cs | Bulk update |
| `BulkUpdate<T>(entities, options)` | yes | BulkOperationsTests.cs | With options |
| `BulkUpdateIgnoreConstraints<T>(entities)` | - | BulkOperationsTests.cs | Needs explicit test |
| `BulkUpdateIgnoreConstraints<T>(entities, options)` | - | BulkOperationsTests.cs | Needs explicit test |
| `BulkUpdateAsync<T>(entities)` | yes | BulkOperationsAsyncTests.cs | Async variant |
| `BulkUpdateAsync<T>(entities, options)` | yes | BulkOperationsAsyncTests.cs | Async with options |
| `BulkUpdateIgnoreConstraintsAsync<T>(...)` | - | BulkOperationsAsyncTests.cs | Needs explicit test |

### BulkDelete

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `BulkDelete<T>(entities)` | yes | BulkOperationsTests.cs | Bulk delete |
| `BulkDelete<T>(entities, options)` | yes | BulkOperationsTests.cs | With options |
| `BulkDeleteIgnoreConstraints<T>(entities)` | - | BulkOperationsTests.cs | Needs explicit test |
| `BulkDeleteIgnoreConstraints<T>(entities, options)` | - | BulkOperationsTests.cs | Needs explicit test |
| `BulkDeleteAsync<T>(entities)` | yes | BulkOperationsAsyncTests.cs | Async variant |
| `BulkDeleteAsync<T>(entities, options)` | yes | BulkOperationsAsyncTests.cs | Async with options |
| `BulkDeleteIgnoreConstraintsAsync<T>(...)` | - | BulkOperationsAsyncTests.cs | Needs explicit test |

### Upsert

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `Upsert<T>(entity)` | yes | UpsertTests.cs | Insert or update |
| `Upsert<T>(entity, options)` | yes | UpsertTests.cs | With options |
| `UpsertAsync<T>(entity)` | yes | UpsertTests.cs | Async variant |
| `UpsertAsync<T>(entity, options)` | yes | UpsertTests.cs | Async with options |

---

## 4. Multiple Result Sets (`src/Jaunty/Multiple/`)

### QueryMultiple

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryMultiple(sql)` | yes | QueryMultipleTests.cs | Returns GridReader |
| `QueryMultiple(sql, parameters)` | yes | QueryMultipleTests.cs | With params |
| `QueryMultiple(sql, options)` | yes | QueryMultipleTests.cs | With options |
| `QueryMultiple(sql, parameters, options)` | yes | QueryMultipleTests.cs | Full overload |
| `QueryMultiple(sql, Action<GridReader>)` | yes | QueryMultipleTests.cs | Callback variant |
| `QueryMultiple<TResult>(sql, Func<GridReader, TResult>)` | yes | QueryMultipleTests.cs | Func variant |
| `QueryMultipleAsync(sql)` | yes | QueryMultipleAsyncTests.cs | Async variant |
| `QueryMultipleAsync(sql, parameters)` | yes | QueryMultipleAsyncTests.cs | Async with params |
| `QueryMultipleAsync(sql, options)` | yes | QueryMultipleAsyncTests.cs | Async with options |
| `QueryMultipleAsync(sql, parameters, options)` | yes | QueryMultipleAsyncTests.cs | Full async overload |
| `QueryMultipleAsync(sql, Action<GridReader>)` | yes | QueryMultipleAsyncTests.cs | Async callback |
| `QueryMultipleAsync(sql, Func<GridReader, Task>)` | yes | QueryMultipleAsyncTests.cs | Async func |
| `QueryMultipleAsync<TResult>(sql, Func<...>)` | yes | QueryMultipleAsyncTests.cs | Async typed func |

### GridReader

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `Read<T>()` | yes | GridReaderTests.cs | Read strict result set |
| `ReadPartial<T>()` | yes | GridReaderTests.cs | Read partial result set |
| `ReadFirst<T>()` | yes | GridReaderTests.cs | Read first row strict |
| `ReadFirstOrDefault<T>()` | yes | GridReaderTests.cs | Read first or null |
| `ReadSingle<T>()` | yes | GridReaderTests.cs | Read single row strict |
| `ReadSingleOrDefault<T>()` | yes | GridReaderTests.cs | Read single or null |
| `ReadPartialFirst<T>()` | - | GridReaderTests.cs | May need explicit test |
| `ReadPartialFirstOrDefault<T>()` | - | GridReaderTests.cs | May need explicit test |
| `ReadPartialSingle<T>()` | - | GridReaderTests.cs | May need explicit test |
| `ReadPartialSingleOrDefault<T>()` | - | GridReaderTests.cs | May need explicit test |
| `ReadScalar<T>()` | yes | GridReaderTests.cs | Read scalar value |
| `Dispose()` | yes | GridReaderTests.cs | Resource cleanup |
| Async variants (ReadAsync, etc.) | yes | GridReaderAsyncTests.cs | All async counterparts |

---

## 5. Stored Procedures (`src/Jaunty/StoredProcedure/`)

> **Note**: SQLite does not support stored procedures. These methods require SQL Server or PostgreSQL for testing.

### ExecuteStoredProcedure (Sync)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `ExecuteStoredProcedure<T>(procedureName)` | todo | — | Requires SQL Server/PostgreSQL |
| `ExecuteStoredProcedure<T>(procedureName, parameters)` | todo | — | |
| `ExecuteStoredProcedure<T>(procedureName, parameters, options)` | todo | — | |
| `ExecuteStoredProcedureFirst<T>(procedureName)` | todo | — | |
| `ExecuteStoredProcedureFirst<T>(procedureName, parameters)` | todo | — | |
| `ExecuteStoredProcedureFirst<T>(procedureName, parameters, options)` | todo | — | |
| `ExecuteStoredProcedureFirstOrDefault<T>(procedureName)` | todo | — | |
| `ExecuteStoredProcedureFirstOrDefault<T>(procedureName, parameters)` | todo | — | |
| `ExecuteStoredProcedureFirstOrDefault<T>(procedureName, parameters, options)` | todo | — | |
| `ExecuteStoredProcedureScalar<T>(procedureName)` | todo | — | |
| `ExecuteStoredProcedureScalar<T>(procedureName, parameters)` | todo | — | |
| `ExecuteStoredProcedureScalar<T>(procedureName, parameters, options)` | todo | — | |
| `ExecuteStoredProcedureNonQuery(procedureName)` | todo | — | |
| `ExecuteStoredProcedureNonQuery(procedureName, parameters)` | todo | — | |
| `ExecuteStoredProcedureNonQuery(procedureName, parameters, options)` | todo | — | |

### ExecuteStoredProcedureAsync

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `ExecuteStoredProcedureAsync<T>(procedureName)` | todo | — | Requires SQL Server/PostgreSQL |
| `ExecuteStoredProcedureAsync<T>(procedureName, parameters)` | todo | — | |
| `ExecuteStoredProcedureAsync<T>(procedureName, parameters, options)` | todo | — | |
| `ExecuteStoredProcedureFirstAsync<T>(procedureName)` | todo | — | |
| `ExecuteStoredProcedureFirstAsync<T>(procedureName, parameters)` | todo | — | |
| `ExecuteStoredProcedureFirstAsync<T>(procedureName, parameters, options)` | todo | — | |
| `ExecuteStoredProcedureFirstOrDefaultAsync<T>(procedureName)` | todo | — | |
| `ExecuteStoredProcedureFirstOrDefaultAsync<T>(procedureName, parameters)` | todo | — | |
| `ExecuteStoredProcedureFirstOrDefaultAsync<T>(procedureName, parameters, options)` | todo | — | |
| `ExecuteStoredProcedureScalarAsync<T>(procedureName)` | todo | — | |
| `ExecuteStoredProcedureScalarAsync<T>(procedureName, parameters)` | todo | — | |
| `ExecuteStoredProcedureScalarAsync<T>(procedureName, parameters, options)` | todo | — | |
| `ExecuteStoredProcedureNonQueryAsync(procedureName)` | todo | — | |
| `ExecuteStoredProcedureNonQueryAsync(procedureName, parameters)` | todo | — | |
| `ExecuteStoredProcedureNonQueryAsync(procedureName, parameters, options)` | todo | — | |

---

## 6. Core Types (`src/Jaunty/Core/`)

### CommandOptions / CommandOptions<T>

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| Default (null values) | yes | CommandOptionsTests.cs | Unit test |
| Constructor sets values | yes | CommandOptionsTests.cs | Unit test |
| `WithTimeout()` | yes | CommandOptionsTests.cs | Static factory |
| `WithTransaction()` | yes | CommandOptionsTests.cs | Static factory |
| `With()` | yes | CommandOptionsTests.cs | Combined factory |
| `WithMapper()` | yes | QueryCommandOptionsTests.cs | Integration test |
| Is readonly struct | yes | CommandOptionsTests.cs | Type constraint test |

---

## 7. Configuration (`src/Jaunty/Configuration/`)

### JauntyConfig

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `ColumnNameResolver` | yes | ConfigurationTests.cs | Custom naming conventions |
| `TableNameResolver` | yes | ConfigurationTests.cs | Table name conventions |
| `SchemaNameResolver` | yes | ConfigResolverTests.cs | Schema resolution |
| `NamingConvention.ToSnakeCase` | yes | ConfigResolverTests.cs | Built-in converter |
| `NamingConvention.SnakeCasePluralTable` | yes | ConfigResolverTests.cs | Built-in converter |
| `Reset()` | yes | ConfigurationTests.cs | Reset to defaults |

---

## 8. Attributes (`src/Jaunty/Attributes/`)

| Attribute | Status | Test File | Notes |
|-----------|--------|-----------|-------|
| `[Table("name")]` | yes | QueryAttributeMappingTests.cs | Table name override |
| `[Column("name")]` | yes | QueryAttributeMappingTests.cs | Column name override |
| `[Key]` | yes | QueryAttributeMappingTests.cs | Primary key marker |
| `[Ignore]` | yes | QueryAttributeMappingTests.cs | Exclude property from mapping |
| `[DatabaseGenerated]` | - | BulkOperationsTests.cs | Tested via insert identity |
| `DatabaseGeneratedOptions` enum | - | BulkOperationsTests.cs | Identity, Computed, None |

---

## 9. Interfaces (`src/Jaunty/Interfaces/`)

| Interface | Status | Test File | Notes |
|-----------|--------|-----------|-------|
| `IMapped<T>` | - | — | Tested indirectly via MappedCache; needs dedicated test |
| `IEntity<T>` | todo | — | Used by Delete<T, TId>; zero explicit coverage |
| `IEntity` (non-generic) | todo | — | Zero explicit coverage |

---

## 10. Internals (`src/Jaunty/Internals/`)

### SqlParameterParser

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `ExtractParameterNames` - single param | yes | SqlParameterParserTests.cs | Unit test (28 test methods) |
| Single-line comment handling | yes | SqlParameterParserTests.cs | `-- comment` |
| Block comment handling | yes | SqlParameterParserTests.cs | `/* ... */` |
| String literal handling | yes | SqlParameterParserTests.cs | `'...'` with escapes |
| Quoted identifier handling | yes | SqlParameterParserTests.cs | `"..."` and `[...]` |
| Edge cases (empty, @-only, unterminated) | yes | SqlParameterParserTests.cs | Graceful handling |

### ParameterBinder

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| Named parameter binding | yes | ParameterBinderTests.cs | Unit test (50+ methods) |
| Positional parameter binding | yes | PositionalParameterBindingTests.cs | Integration test |
| Type binding (int, string, bool, etc.) | yes | ParameterBinderTests.cs | All primitive types |
| Null/nullable handling | yes | ParameterBinderTests.cs | DbNull conversion |
| Collection expansion (`IN @ids`) | yes | ParameterBinderTests.cs + CollectionParameterTests.cs | Array/List expansion |
| Empty collection handling | yes | ParameterBinderTests.cs | No-match subquery |
| Error cases (mismatch, too few, too many) | yes | ParameterBinderTests.cs | ArgumentException |
| Case-insensitive matching | yes | ParameterBinderTests.cs | CaseInsensitiveMatch |

### MetadataBuilder

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `Build<T>()` | - | — | Tested only via integration tests (Query, Insert, etc.) |
| Column name resolution | - | ConfigResolverTests.cs | Via JauntyConfig integration |
| Attribute reading | - | QueryAttributeMappingTests.cs | Via attribute mapping tests |
| Dedicated unit tests | todo | — | No isolated unit tests |

### MetadataCache<T>

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| Static constructor caching | - | — | Tested only indirectly |
| `GetSetters()` | - | — | Tested via Query* integration |
| `CreateSetter()` (expression tree) | - | — | Tested via Query* integration |
| Dedicated unit tests | todo | — | No isolated unit tests |

### DrDispatcher

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `Resolve<T>()` dispatcher | - | — | Tested via integration tests |
| Dictionary mapper | yes | QueryDictionaryTests.cs | Via integration test |
| ExpandoObject mapper | yes | QueryDynamicTests.cs | Via integration test |
| KeyValuePair mapper | yes | QueryKeyValuePairTests.cs | Via integration test |
| ValueTuple mapper | yes | QueryValueTupleTests.cs | Via integration test |
| IMapped<T> mapper resolution | - | — | Needs dedicated test |
| Dedicated unit tests | todo | — | No isolated unit tests |

### MappedCache<T>

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `Mapper` resolution | - | — | Tested indirectly via DrDispatcher |
| `ResolveMapper()` | - | — | Tested indirectly |
| Dedicated unit tests | todo | — | No isolated unit tests |

### CrudSqlCache

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `GetSql<T>()` | - | — | Tested via Insert/Update/Delete integration |
| `BuildInsertSql` | - | — | Tested via BulkInsert integration |
| `BuildUpdateSql` | - | — | Tested via BulkUpdate integration |
| `BuildDeleteSql` | - | — | Tested via BulkDelete integration |
| `BuildDeleteByIdSql` | todo | — | Not tested (Delete by ID not tested) |
| `BuildUpsertSql` | - | — | Tested via Upsert integration |
| Dedicated unit tests | todo | — | No isolated unit tests |

### Dialects (ISqlDialect implementations)

| Dialect | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `ISqlDialect` interface | - | — | Tested indirectly via SQLite |
| `SQLiteDialect` | - | — | Tested indirectly (all SQLite tests) |
| `SqlServerDialect` | todo | — | Zero coverage, no SQL Server test infra |
| `MySqlDialect` | todo | — | Zero coverage, no MySQL test infra |
| `PostgreSqlDialect` | todo | — | Zero coverage, no PostgreSQL test infra |
| `SqlDialectFactory.GetDialect()` | - | — | Only SQLite path tested |
| Dedicated unit tests | todo | — | No isolated unit tests |

### MultiEntityMapper

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `Build(reader)` | yes | QueryMultiEntityTests.cs | Via multi-entity integration |
| `ApplyT1()` / `ApplyT2()` | yes | QueryMultiEntityTests.cs | Via multi-entity integration |
| Dedicated unit tests | todo | — | No isolated unit tests |

### QueryCore / QueryCoreAsync (Private)

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `QueryCore<T>()` | yes | — | Tested via all Query* integration tests |
| `QueryFirstCore<T>()` | yes | — | Tested via QueryFirst* integration |
| `QuerySingleCore<T>()` | yes | — | Tested via QuerySingle* integration |
| `QueryStreamCore<T>()` | yes | — | Tested via QueryStream* integration |
| `QueryScalarCore<T>()` | yes | — | Tested via QueryScalar* integration |
| `ExecuteReader()` | yes | — | Tested via all read integration tests |
| Async equivalents | yes | — | Tested via all async integration tests |

### ExecuteNonQueryCore (Private)

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `ExecuteNonQueryCore()` | - | — | Tested via BulkInsert/Update/Delete; not via StoredProcedure |
| `ExecuteNonQueryCoreAsync()` | - | — | Tested via async bulk ops |
| `CommandType.StoredProcedure` path | todo | — | Not tested (stored procs untested) |

---

## 11. Cross-Cutting Integration Tests

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| Strict mapping mode | yes | QueryMappingModeTests.cs | Missing column throws |
| Partial mapping mode | yes | QueryMappingModeTests.cs | Extra columns ignored |
| Attribute mapping | yes | QueryAttributeMappingTests.cs | [Column], [Ignore], [Key] |
| Case sensitivity | yes | QueryCaseSensitivityTests.cs | Column name matching |
| Connection state management | yes | QueryConnectionStateTests.cs | Open/closed handling |
| Transaction support | yes | QueryTransactionTests.cs | Commit/rollback |
| Parameter binding errors | yes | QueryParameterBindingErrorTests.cs | Mismatch errors |
| Null handling | yes | QueryNullHandlingTests.cs | NULL values in results |
| Type conversion | yes | QueryTypeConversionTests.cs | Cross-type mapping |
| Empty results | yes | QueryEmptyResultsTests.cs | Empty result sets |
| SQL error handling | yes | QuerySqlErrorTests.cs | Syntax errors |
| Special characters | yes | QuerySpecialCharacterTests.cs | Unicode, quotes, etc. |
| Large result sets | yes | QueryLargeResultSetTests.cs | Performance edge cases |
| Named parameter binding | yes | NamedParameterBindingTests.cs | @param style |
| Positional parameter binding | yes | PositionalParameterBindingTests.cs | Ordered params |
| Collection parameters | yes | CollectionParameterTests.cs | IN @ids expansion |
| Argument validation | yes | QueryArgumentValidationTests.cs | Null checks, empty SQL |
| Database connection | yes | QueryDatabaseConnectionTests.cs | Infrastructure test |

---

## 12. Jaunty.Fluent.Tests

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| SELECT clause | yes | FluentSelectTests.cs | Column selection |
| WHERE clause | yes | FluentWhereTests.cs | Filtering |
| WHERE IN / BETWEEN | yes | FluentWhereInBetweenTests.cs | Range/set filters |
| ORDER BY | yes | FluentOrderByTests.cs | Sorting |
| TAKE / SKIP | yes | FluentTakeSkipTests.cs | Pagination |
| DISTINCT | yes | FluentDistinctTests.cs | Deduplication |
| GROUP BY | yes | FluentGroupByTests.cs | Grouping with aggregates |
| JOIN | yes | FluentJoinTests.cs | INNER, LEFT, etc. |
| Aggregate functions | yes | FluentAggregateTests.cs | COUNT, SUM, AVG, MIN, MAX |
| Set operations | yes | FluentSetOperationsTests.cs | UNION, INTERSECT, EXCEPT |
| Subqueries | yes | FluentSubqueryTests.cs | Nested queries |
| Window functions | yes | FluentWindowFunctionTests.cs | ROW_NUMBER, RANK, etc. |
| CTEs | yes | FluentCteTests.cs | WITH clauses |
| EXISTS | yes | FluentExistsTests.cs | EXISTS subqueries |
| Date functions | yes | FluentDateFunctionsTests.cs | Date/time operations |
| String functions | yes | FluentStringFunctionsTests.cs | String operations |
| SQL functions | yes | FluentSqlFunctionsTests.cs | COALESCE, CASE, etc. |
| CASE WHEN | yes | FluentCaseWhenTests.cs | Conditional expressions |
| Write operations | yes | FluentWriteOperationsTests.cs | INSERT, UPDATE, DELETE |

---

## 13. Jaunty.Scaffolding.Tests

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| Entity code generation | yes | EntityCodeGeneratorTests.cs | 18 test methods |
| Naming helpers | yes | NamingHelperTests.cs | PascalCase, camelCase, singularize |
| SQLite type mapper | yes | SQLiteTypeMapperTests.cs | 11 test methods |
| SQL Server type mapper | yes | SqlServerTypeMapperTests.cs | 11 test methods |
| Scaffolder integration | yes | ScaffolderIntegrationTests.cs | 10 integration tests |
| SQLite schema reader | yes | SQLiteSchemaReaderTests.cs | 9 integration tests |

---

## Summary

### Coverage by Category

| Category | Covered | Partial | Uncovered | Total | % Covered |
|----------|---------|---------|-----------|-------|-----------|
| Read (Strict) | 48 | 0 | 0 | 48 | 100% |
| Read (Partial) | 4 | 4 | 32 | 40 | 10% |
| Read (Scalar) | 8 | 0 | 8 | 16 | 50% |
| Read (MultiEntity) | 20+ | 2 | 0 | 22+ | ~90% |
| Read (Special Types) | 4 | 0 | 0 | 4 | 100% |
| Streaming | 16 | 0 | 8 | 24 | 67% |
| Write (Individual) | 0 | 0 | 20 | 20 | 0% |
| Write (Bulk) | 12 | 8 | 0 | 20 | 60% |
| Write (Upsert) | 4 | 0 | 0 | 4 | 100% |
| Multiple | 13+ | 4 | 0 | 17+ | ~75% |
| Stored Procedures | 0 | 0 | 30 | 30 | 0% |
| Core/Config | 12 | 0 | 0 | 12 | 100% |
| Attributes | 4 | 2 | 0 | 6 | 67% |
| Interfaces | 0 | 1 | 2 | 3 | 0% |
| Internals | 5 | 12 | 8 | 25 | 20% |
| Cross-cutting | 17 | 0 | 0 | 17 | 100% |
| Fluent API | 19 | 0 | 0 | 19 | 100% |
| Scaffolding | 6 | 0 | 0 | 6 | 100% |

### Priority Areas

#### 1. High Priority (Zero Coverage - Public API)
- [ ] **QueryPartialFirst** / QueryPartialFirstOrDefault (8 sync + 8 async = 16 methods)
- [ ] **QueryPartialSingle** / QueryPartialSingleOrDefault (8 sync + 8 async = 16 methods)
- [ ] **QueryPartialUnbuffered** / QueryPartialUnbufferedAsync (4 sync + 4 async = 8 methods)
- [ ] **ExecuteScalar** / ExecuteScalarAsync (4 sync + 4 async = 8 methods)
- [ ] **Individual Insert** / InsertAsync (2 sync + 2 async = 4 methods)
- [ ] **Individual Update** / UpdateAsync (2 sync + 2 async = 4 methods)
- [ ] **Individual Delete** / DeleteAsync (6 sync + 6 async = 12 methods)
- [ ] **StoredProcedure** methods (15 sync + 15 async = 30 methods)

#### 2. Medium Priority (Partial Coverage)
- [ ] **GridReader** partial methods (ReadPartialFirst, ReadPartialSingle, etc.)
- [ ] **BulkInsertIgnoreConstraints** / BulkUpdateIgnoreConstraints / BulkDeleteIgnoreConstraints
- [ ] **MetadataBuilder** dedicated unit tests
- [ ] **MappedCache** dedicated unit tests
- [ ] **CrudSqlCache** dedicated unit tests
- [ ] **DrDispatcher** dedicated unit tests
- [ ] **IMapped<T>** interface coverage
- [ ] **[DatabaseGenerated]** attribute explicit tests

#### 3. Lower Priority (Edge Cases / Non-SQLite)
- [ ] SQL dialect unit tests (SqlServerDialect, MySqlDialect, PostgreSqlDialect)
- [ ] `[Obsolete]` multi-entity overloads
- [ ] **IEntity / IEntity<T>** explicit interface coverage
- [ ] MetadataCache<T> dedicated unit tests
- [ ] MultiEntityMapper dedicated unit tests

---

## Test File Locations

| Test File | Location |
|-----------|----------|
| QueryTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryFirstTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryFirstOrDefaultTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QuerySingleTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QuerySingleOrDefaultTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryScalarTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryMultiEntityTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryDictionaryTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryDynamicTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryKeyValuePairTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryValueTupleTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryPartialAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryFirstAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryFirstOrDefaultAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QuerySingleAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QuerySingleOrDefaultAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryScalarAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryMappingModeTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryAttributeMappingTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryCaseSensitivityTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryConnectionStateTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryCommandOptionsTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryTransactionTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryParameterBindingErrorTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryNullHandlingTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryTypeConversionTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryEmptyResultsTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QuerySqlErrorTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QuerySpecialCharacterTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryLargeResultSetTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| NamedParameterBindingTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| PositionalParameterBindingTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryPositionalParameterTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| CollectionParameterTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryArgumentValidationTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryStreamTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Streaming/` |
| QueryStreamAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Streaming/` |
| QueryPartialStreamTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Streaming/` |
| QueryPartialStreamAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Streaming/` |
| QueryMultipleTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Multiple/` |
| QueryMultipleAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Multiple/` |
| GridReaderTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Multiple/` |
| GridReaderAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Multiple/` |
| BulkOperationsTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| BulkOperationsAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| UpsertTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| ConfigurationTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Configuration/` |
| ConfigResolverTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Configuration/` |
| QueryDatabaseConnectionTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Infrastructure/` |
| CommandOptionsTests.cs | `tests/Jaunty.Tests/Unit/Read/` |
| SqlParameterParserTests.cs | `tests/Jaunty.Tests/Unit/Read/` |
| ParameterBinderTests.cs | `tests/Jaunty.Tests/Unit/Read/` |

---

## How to Use This Document

1. **Pick an uncovered item** () from the list above
2. **Read existing test files** in the same area to match patterns
3. **Write tests** in the appropriate test project/directory
4. **Run tests**: `dotnet test`
5. **Mark as covered** () once tests pass
6. **Update the Summary section** percentages
7. **Commit**: `git commit -m "test: add tests for [ClassName]"`
