# Jaunty Code Coverage Checklist

**Target**: 100% Code Coverage  
**Created**: 2026-02-19  
**Last Updated**: 2026-02-24 (Coverage plan created)

**Current Coverage**: 75% overall
- Jaunty (net8.0): 76%
- Jaunty (netstandard2.0): 69%
- Jaunty.Extensions.Reflection: 95%
- Jaunty.Fluent: 33%
- Jaunty.Scaffolding: 49%

> **See**: [`COVERAGE-PLAN-2026-02-24.md`](COVERAGE-PLAN-2026-02-24.md) for the prioritized 100% coverage roadmap.

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
| `QueryPartial<T>(sql, options)` | yes | QueryTests.cs | Explicit test with CommandOptions |
| `QueryPartial<T>(sql, parameters, options)` | yes | QueryTests.cs | Explicit test with params + options |
| `QueryPartialAsync<T>(sql)` | yes | QueryPartialAsyncTests.cs | Async variant |
| `QueryPartialAsync<T>(sql, parameters)` | yes | QueryPartialAsyncTests.cs | Async with params |
| `QueryPartialAsync<T>(sql, options)` | yes | QueryPartialAsyncTests.cs | Explicit test with CommandOptions |
| `QueryPartialAsync<T>(sql, parameters, options)` | yes | QueryPartialAsyncTests.cs | Explicit test with params + options |

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
| `QueryPartialFirst<T>(sql)` | yes | QueryPartialFirstTests.cs | Partial mapping, returns first |
| `QueryPartialFirst<T>(sql, parameters)` | yes | QueryPartialFirstTests.cs | With parameters |
| `QueryPartialFirst<T>(sql, options)` | yes | QueryPartialFirstTests.cs | With options |
| `QueryPartialFirst<T>(sql, parameters, options)` | yes | QueryPartialFirstTests.cs | Full overload |
| `QueryPartialFirstAsync<T>(sql)` | yes | QueryPartialFirstAsyncTests.cs | Async (skipped on SQLite) |
| `QueryPartialFirstAsync<T>(sql, parameters)` | yes | QueryPartialFirstAsyncTests.cs | Async with params |
| `QueryPartialFirstAsync<T>(sql, options)` | yes | QueryPartialFirstAsyncTests.cs | Async with options |
| `QueryPartialFirstAsync<T>(sql, parameters, options)` | yes | QueryPartialFirstAsyncTests.cs | Full async overload |

### QueryPartialFirstOrDefault (Partial)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryPartialFirstOrDefault<T>(sql)` | yes | QueryPartialFirstOrDefaultTests.cs | Returns null if empty |
| `QueryPartialFirstOrDefault<T>(sql, parameters)` | yes | QueryPartialFirstOrDefaultTests.cs | With parameters |
| `QueryPartialFirstOrDefault<T>(sql, options)` | yes | QueryPartialFirstOrDefaultTests.cs | With options |
| `QueryPartialFirstOrDefault<T>(sql, parameters, options)` | yes | QueryPartialFirstOrDefaultTests.cs | Full overload |
| `QueryPartialFirstOrDefaultAsync<T>(sql)` | yes | QueryPartialFirstOrDefaultAsyncTests.cs | Async (skipped on SQLite) |
| `QueryPartialFirstOrDefaultAsync<T>(sql, parameters)` | yes | QueryPartialFirstOrDefaultAsyncTests.cs | Async with params |
| `QueryPartialFirstOrDefaultAsync<T>(sql, options)` | yes | QueryPartialFirstOrDefaultAsyncTests.cs | Async with options |
| `QueryPartialFirstOrDefaultAsync<T>(sql, parameters, options)` | yes | QueryPartialFirstOrDefaultAsyncTests.cs | Full async overload |

### QueryPartialSingle (Partial)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryPartialSingle<T>(sql)` | yes | QueryPartialSingleTests.cs | Throws if != 1 |
| `QueryPartialSingle<T>(sql, parameters)` | yes | QueryPartialSingleTests.cs | With parameters |
| `QueryPartialSingle<T>(sql, options)` | yes | QueryPartialSingleTests.cs | With options |
| `QueryPartialSingle<T>(sql, parameters, options)` | yes | QueryPartialSingleTests.cs | Full overload |
| `QueryPartialSingleAsync<T>(sql)` | yes | QueryPartialSingleAsyncTests.cs | Async (skipped on SQLite) |
| `QueryPartialSingleAsync<T>(sql, parameters)` | yes | QueryPartialSingleAsyncTests.cs | Async with params |
| `QueryPartialSingleAsync<T>(sql, options)` | yes | QueryPartialSingleAsyncTests.cs | Async with options |
| `QueryPartialSingleAsync<T>(sql, parameters, options)` | yes | QueryPartialSingleAsyncTests.cs | Full async overload |

### QueryPartialSingleOrDefault (Partial)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `QueryPartialSingleOrDefault<T>(sql)` | yes | QueryPartialSingleOrDefaultTests.cs | Returns null if empty |
| `QueryPartialSingleOrDefault<T>(sql, parameters)` | yes | QueryPartialSingleOrDefaultTests.cs | With parameters |
| `QueryPartialSingleOrDefault<T>(sql, options)` | yes | QueryPartialSingleOrDefaultTests.cs | With options |
| `QueryPartialSingleOrDefault<T>(sql, parameters, options)` | yes | QueryPartialSingleOrDefaultTests.cs | Full overload |
| `QueryPartialSingleOrDefaultAsync<T>(sql)` | yes | QueryPartialSingleOrDefaultAsyncTests.cs | Async (skipped on SQLite) |
| `QueryPartialSingleOrDefaultAsync<T>(sql, parameters)` | yes | QueryPartialSingleOrDefaultAsyncTests.cs | Async with params |
| `QueryPartialSingleOrDefaultAsync<T>(sql, options)` | yes | QueryPartialSingleOrDefaultAsyncTests.cs | Async with options |
| `QueryPartialSingleOrDefaultAsync<T>(sql, parameters, options)` | yes | QueryPartialSingleOrDefaultAsyncTests.cs | Full async overload |

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
| `ExecuteScalar<T>(sql)` | yes | ExecuteScalarTests.cs | COUNT, MAX, string |
| `ExecuteScalar<T>(sql, parameters)` | yes | ExecuteScalarTests.cs | With parameters |
| `ExecuteScalar<T>(sql, options)` | yes | ExecuteScalarTests.cs | With options |
| `ExecuteScalar<T>(sql, parameters, options)` | yes | ExecuteScalarTests.cs | Full overload |
| `ExecuteScalarAsync<T>(sql)` | yes | ExecuteScalarAsyncTests.cs | Async (skipped on SQLite) |
| `ExecuteScalarAsync<T>(sql, parameters)` | yes | ExecuteScalarAsyncTests.cs | Async with params |
| `ExecuteScalarAsync<T>(sql, options)` | yes | ExecuteScalarAsyncTests.cs | Async with options |
| `ExecuteScalarAsync<T>(sql, parameters, options)` | yes | ExecuteScalarAsyncTests.cs | Full async overload |

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
| `[Obsolete]` overloads (sync) | yes | ObsoleteMultiEntityTests.cs | 14 sync tests (Query, QueryFirst, QuerySingle, QueryStream, combiner) |
| `[Obsolete]` overloads (async) | yes | ObsoleteMultiEntityTests.cs | 7 async tests |

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
| `QueryPartialUnbuffered<T>(sql)` | yes | QueryPartialUnbufferedTests.cs | Lazy IEnumerable partial |
| `QueryPartialUnbuffered<T>(sql, parameters)` | yes | QueryPartialUnbufferedTests.cs | With params |
| `QueryPartialUnbuffered<T>(sql, options)` | yes | QueryPartialUnbufferedTests.cs | With options |
| `QueryPartialUnbuffered<T>(sql, parameters, options)` | yes | QueryPartialUnbufferedTests.cs | Full overload |
| `QueryPartialUnbufferedAsync<T>(sql)` | yes | QueryPartialUnbufferedAsyncTests.cs | Async (skipped on SQLite) |
| `QueryPartialUnbufferedAsync<T>(sql, parameters)` | yes | QueryPartialUnbufferedAsyncTests.cs | Async with params |
| `QueryPartialUnbufferedAsync<T>(sql, options)` | yes | QueryPartialUnbufferedAsyncTests.cs | Async with options |
| `QueryPartialUnbufferedAsync<T>(sql, parameters, options)` | yes | QueryPartialUnbufferedAsyncTests.cs | Full async overload |

---

## 3. Write Methods (`src/Jaunty/Write/`)

### Insert (Individual)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `Insert<T>(entity)` | yes | InsertTests.cs | Returns identity, in-memory SQLite |
| `Insert<T>(entity, options)` | yes | InsertTests.cs | With transaction |
| `InsertAsync<T>(entity)` | yes | InsertAsyncTests.cs | Async insert |
| `InsertAsync<T>(entity, options)` | yes | InsertAsyncTests.cs | Async with transaction |

### Update (Individual)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `Update<T>(entity)` | yes | UpdateTests.cs | Returns rows affected |
| `Update<T>(entity, options)` | yes | UpdateTests.cs | With transaction |
| `UpdateAsync<T>(entity)` | yes | UpdateAsyncTests.cs | Async update |
| `UpdateAsync<T>(entity, options)` | yes | UpdateAsyncTests.cs | Async with transaction |

### Delete (Individual)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `Delete<T>(entity)` | yes | DeleteTests.cs | Delete by entity |
| `Delete<T>(entity, options)` | yes | DeleteTests.cs | With transaction |
| `Delete<T>(id)` | yes | DeleteTests.cs | Delete by object ID |
| `Delete<T>(id, options)` | yes | DeleteTests.cs | With transaction |
| `Delete<T, TId>(id)` | yes | DeleteByEntityIdTests.cs | IEntity<TId> via EntityTestEntity |
| `Delete<T, TId>(id, options)` | yes | DeleteByEntityIdTests.cs | With transaction |
| `DeleteAsync<T>(entity)` | yes | DeleteAsyncTests.cs | Async delete by entity |
| `DeleteAsync<T>(entity, options)` | yes | DeleteAsyncTests.cs | Async with transaction |
| `DeleteAsync<T>(id)` | yes | DeleteAsyncTests.cs | Async delete by ID |
| `DeleteAsync<T>(id, options)` | yes | DeleteAsyncTests.cs | Async with transaction |
| `DeleteAsync<T, TId>(id)` | yes | DeleteByEntityIdTests.cs | Async IEntity<TId> |
| `DeleteAsync<T, TId>(id, options)` | yes | DeleteByEntityIdTests.cs | Async with transaction |

### BulkInsert

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `BulkInsert<T>(entities)` | yes | BulkOperationsTests.cs | Bulk insert |
| `BulkInsert<T>(entities, options)` | yes | BulkOperationsTests.cs | With options |
| `BulkInsertIgnoreConstraints<T>(entities)` | yes | BulkOperationsTests.cs | With empty collection test |
| `BulkInsertIgnoreConstraints<T>(entities, options)` | yes | BulkOperationsTests.cs | With transaction |
| `BulkInsertAsync<T>(entities)` | yes | BulkOperationsAsyncTests.cs | Async variant |
| `BulkInsertAsync<T>(entities, options)` | yes | BulkOperationsAsyncTests.cs | Async with options |
| `BulkInsertIgnoreConstraintsAsync<T>(entities)` | yes | BulkOperationsAsyncTests.cs | Basic overload |
| `BulkInsertIgnoreConstraintsAsync<T>(entities, options)` | yes | BulkOperationsAsyncTests.cs | With transaction |

### BulkUpdate

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `BulkUpdate<T>(entities)` | yes | BulkOperationsTests.cs | Bulk update |
| `BulkUpdate<T>(entities, options)` | yes | BulkOperationsTests.cs | With options |
| `BulkUpdateIgnoreConstraints<T>(entities)` | yes | BulkOperationsTests.cs | With empty collection test |
| `BulkUpdateIgnoreConstraints<T>(entities, options)` | yes | BulkOperationsTests.cs | With transaction, verifies values |
| `BulkUpdateAsync<T>(entities)` | yes | BulkOperationsAsyncTests.cs | Async variant |
| `BulkUpdateAsync<T>(entities, options)` | yes | BulkOperationsAsyncTests.cs | Async with options |
| `BulkUpdateIgnoreConstraintsAsync<T>(entities)` | yes | BulkOperationsAsyncTests.cs | Basic overload |
| `BulkUpdateIgnoreConstraintsAsync<T>(entities, options)` | yes | BulkOperationsAsyncTests.cs | With transaction, verifies values |

### BulkDelete

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `BulkDelete<T>(entities)` | yes | BulkOperationsTests.cs | Bulk delete |
| `BulkDelete<T>(entities, options)` | yes | BulkOperationsTests.cs | With options |
| `BulkDeleteIgnoreConstraints<T>(entities)` | yes | BulkOperationsTests.cs | With empty collection test |
| `BulkDeleteIgnoreConstraints<T>(entities, options)` | yes | BulkOperationsTests.cs | With transaction |
| `BulkDeleteAsync<T>(entities)` | yes | BulkOperationsAsyncTests.cs | Async variant |
| `BulkDeleteAsync<T>(entities, options)` | yes | BulkOperationsAsyncTests.cs | Async with options |
| `BulkDeleteIgnoreConstraintsAsync<T>(entities)` | yes | BulkOperationsAsyncTests.cs | Basic overload |
| `BulkDeleteIgnoreConstraintsAsync<T>(entities, options)` | yes | BulkOperationsAsyncTests.cs | With transaction |

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
| `ReadPartialFirst<T>()` | yes | GridReaderTests.cs | Happy path + no results throws |
| `ReadPartialFirstOrDefault<T>()` | yes | GridReaderTests.cs | Happy path + no results returns null |
| `ReadPartialSingle<T>()` | yes | GridReaderTests.cs | Happy path + multiple/no results throws |
| `ReadPartialSingleOrDefault<T>()` | yes | GridReaderTests.cs | Happy path + no results returns null |
| `ReadScalar<T>()` | yes | GridReaderTests.cs | Read scalar value |
| `Dispose()` | yes | GridReaderTests.cs | Resource cleanup |
| Async variants (ReadAsync, etc.) | yes | GridReaderAsyncTests.cs, MicrosoftSqliteGridReaderAsyncTests.cs | All async counterparts (19 async tests via Microsoft.Data.Sqlite) |

---

## 5. Stored Procedures (`src/Jaunty/StoredProcedure/`)

> **Note**: SQLite does not support stored procedures. Real implementations exist for SQL Server and PostgreSQL.
> Tests auto-skip when the database is not configured. Configure via environment variables or `appsettings.json`.
> See `data/sqlserver/create-stored-procedures.sql` and `data/postgres/create-stored-procedures.sql` for setup.

### ExecuteStoredProcedure (Sync)

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `ExecuteStoredProcedure<T>(procedureName)` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | Auto-skips without DB |
| `ExecuteStoredProcedure<T>(procedureName, parameters)` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | With parameter binding |
| `ExecuteStoredProcedure<T>(procedureName, parameters, options)` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | With transaction |
| `ExecuteStoredProcedureFirst<T>(procedureName, parameters)` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | Returns first |
| `ExecuteStoredProcedureFirst<T>(procedureName, parameters, options)` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | With transaction |
| `ExecuteStoredProcedureFirst<T> - no results` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | Throws InvalidOperationException |
| `ExecuteStoredProcedureFirstOrDefault<T>(procedureName, parameters)` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | Returns first or null |
| `ExecuteStoredProcedureFirstOrDefault<T> - no results` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | Returns null |
| `ExecuteStoredProcedureFirstOrDefault<T>(procedureName, parameters, options)` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | With transaction |
| `ExecuteStoredProcedureScalar<T>(procedureName)` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | Scalar count |
| `ExecuteStoredProcedureScalar<T>(procedureName, parameters)` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | Scalar with params |
| `ExecuteStoredProcedureScalar<T>(procedureName, parameters, options)` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | With transaction |
| `ExecuteStoredProcedureNonQuery(procedureName, parameters, options)` | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | Update with rollback |
| `SpParameters` output parameter binding | yes | SqlServerStoredProcedureTests.cs, PostgresStoredProcedureTests.cs | AddOutput/AddInputOutput + Get |

### ExecuteStoredProcedureAsync

| Method | Status | Test File | Notes |
|--------|--------|-----------|-------|
| `ExecuteStoredProcedureAsync<T>(procedureName)` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Async list |
| `ExecuteStoredProcedureAsync<T>(procedureName, parameters)` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Async with params |
| `ExecuteStoredProcedureAsync<T>(procedureName, parameters, options)` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Async with transaction |
| `ExecuteStoredProcedureFirstAsync<T>(procedureName, parameters)` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Async first |
| `ExecuteStoredProcedureFirstAsync<T>(procedureName, parameters, options)` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Async with transaction |
| `ExecuteStoredProcedureFirstAsync<T> - no results` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Throws async |
| `ExecuteStoredProcedureFirstOrDefaultAsync<T>(procedureName, parameters)` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Async first or null |
| `ExecuteStoredProcedureFirstOrDefaultAsync<T> - no results` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Returns null async |
| `ExecuteStoredProcedureFirstOrDefaultAsync<T>(procedureName, parameters, options)` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Async with transaction |
| `ExecuteStoredProcedureScalarAsync<T>(procedureName)` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Async scalar |
| `ExecuteStoredProcedureScalarAsync<T>(procedureName, parameters)` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Async scalar with params |
| `ExecuteStoredProcedureScalarAsync<T>(procedureName, parameters, options)` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Async with transaction |
| `ExecuteStoredProcedureNonQueryAsync(procedureName, parameters, options)` | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Async update with rollback |
| `SpParameters` output parameter binding (async) | yes | SqlServerStoredProcedureAsyncTests.cs, PostgresStoredProcedureAsyncTests.cs | Async output param |

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
| `[DatabaseGenerated]` | yes | DatabaseGeneratedTests.cs | Identity auto-increment, ignores provided ID |
| `DatabaseGeneratedOptions` enum | yes | DatabaseGeneratedTests.cs | Identity tested explicitly |

---

## 9. Interfaces (`src/Jaunty/Interfaces/`)

| Interface | Status | Test File | Notes |
|-----------|--------|-----------|-------|
| `IMapped<T>` | yes | IMappedTests.cs | Query, QueryFirst, QuerySingle, QueryStream with Product entity. MappedCache bug fixed — now uses interface enumeration to detect `IMapped<T>` implementations. |
| `IEntity<T>` | yes | DeleteByEntityIdTests.cs | Delete<T,TId> + DeleteAsync<T,TId> via EntityTestEntity |
| `IEntity` (non-generic) | yes | InsertTests.cs | Insert sets Id via IEntity cast in WriteParameterCache |

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
| `Build<T>()` | yes | MetadataBuilderTests.cs | 18 dedicated unit tests |
| Table name from type name | yes | MetadataBuilderTests.cs | Default convention |
| Table name from `[Table]` | yes | MetadataBuilderTests.cs | Attribute override |
| Schema from `[Table]` | yes | MetadataBuilderTests.cs | Schema parameter |
| Column name from `[Column]` | yes | MetadataBuilderTests.cs | Column attribute |
| Column name default | yes | MetadataBuilderTests.cs | Uses property name |
| `[Key]` attribute detection | yes | MetadataBuilderTests.cs | Key attribute |
| `Id` convention key detection | yes | MetadataBuilderTests.cs | Convention-based |
| `{Type}Id` convention key detection | yes | MetadataBuilderTests.cs | Convention-based |
| Composite keys | yes | MetadataBuilderTests.cs | Multiple keys |
| `[Ignore]` attribute | yes | MetadataBuilderTests.cs | Excluded from columns |
| `[DatabaseGenerated(Identity)]` | yes | MetadataBuilderTests.cs | IsIdentity flag |
| `[DatabaseGenerated(Computed)]` | yes | MetadataBuilderTests.cs | IsComputed flag |
| NonIdentityColumns filtering | yes | MetadataBuilderTests.cs | Identity excluded |
| NonPrimaryKeyColumns filtering | yes | MetadataBuilderTests.cs | Keys excluded |
| Read-only properties excluded | yes | MetadataBuilderTests.cs | CanWrite check |
| Indexer properties excluded | yes | MetadataBuilderTests.cs | GetIndexParameters check |
| Abstract type throws | yes | MetadataBuilderTests.cs | InvalidOperationException |

### MetadataCache<T>

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| Static constructor caching | yes | MetadataCacheTests.cs | Same reference verified |
| Metadata contains correct columns | yes | MetadataCacheTests.cs | Column count and names |
| Column attribute uses column names | yes | MetadataCacheTests.cs | [Column] name in metadata |
| `GetSetters()` strict - all match | yes | MetadataCacheTests.cs | Returns correct count |
| `GetSetters()` strict - missing column | yes | MetadataCacheTests.cs | Throws InvalidOperationException |
| `GetSetters()` strict - extra column | yes | MetadataCacheTests.cs | Throws InvalidOperationException |
| `GetSetters()` strict - [Column] mapping | yes | MetadataCacheTests.cs | Maps via attribute name |
| `GetSetters()` strict - case insensitive | yes | MetadataCacheTests.cs | UPPER column names match |
| `GetSetters()` projection - extra columns | yes | MetadataCacheTests.cs | Ignored silently |
| `GetSetters()` projection - missing columns | yes | MetadataCacheTests.cs | Partial setters returned |
| `GetSetters()` projection - subset entity | yes | MetadataCacheTests.cs | Extra columns ignored |
| `GetSetters()` empty result set | yes | MetadataCacheTests.cs | Still returns setters for field count |
| `PropertySetter.Set()` applies values | yes | MetadataCacheTests.cs | Int, string, all types |
| `PropertySetter.Set()` nullable null | yes | MetadataCacheTests.cs | NULL leaves property null |
| `PropertySetter.Set()` nullable non-null | yes | MetadataCacheTests.cs | Non-null sets value |
| `PropertySetter.Set()` non-nullable null throws | yes | MetadataCacheTests.cs | InvalidOperationException |
| `PropertySetter.Set()` column attribute | yes | MetadataCacheTests.cs | Maps via [Column] name |
| `PropertySetter.Set()` multiple rows | yes | MetadataCacheTests.cs | Reuses setters across rows |
| `CreateSetter()` int property | yes | MetadataCacheTests.cs | Expression tree compiled |
| `CreateSetter()` string property | yes | MetadataCacheTests.cs | Expression tree compiled |
| `CreateSetter()` nullable int property | yes | MetadataCacheTests.cs | Nullable expression tree |
| ColumnToIndex dual registration | yes | MetadataCacheTests.cs | Both column and property name |

### DrDispatcher

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `Resolve<T>()` dispatcher | yes | DrDispatcherTests.cs | 9 dedicated tests |
| User override mapper (priority 1) | yes | DrDispatcherTests.cs | CommandOptions.Mapper takes priority |
| Dictionary mapper (priority 2) | yes | DrDispatcherTests.cs + QueryDictionaryTests.cs | Special type resolution |
| KeyValuePair mapper | yes | DrDispatcherTests.cs + QueryKeyValuePairTests.cs | Two-column mapping |
| ValueTuple mapper | yes | DrDispatcherTests.cs + QueryValueTupleTests.cs | Positional mapping |
| ExpandoObject mapper | yes | QueryDynamicTests.cs | Via integration test |
| IMapped<T> mapper resolution | yes | DrDispatcherTests.cs | Fixed — MappedCache bug resolved, test un-skipped |
| Metadata reflection fallback (priority 4) | yes | DrDispatcherTests.cs | Plain entity mapping |
| KeyValuePair insufficient columns | yes | DrDispatcherTests.cs | Throws InvalidOperationException |
| ValueTuple insufficient columns | yes | DrDispatcherTests.cs | Throws InvalidOperationException |

### MappedCache<T>

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| Plain entity returns null mapper | yes | MappedCacheTests.cs | Non-IMapped type |
| IMapped entity mapper resolution | yes | MappedCacheTests.cs | Fixed — uses interface enumeration instead of IsAssignableFrom |
| Mapper caching (same reference) | yes | MappedCacheTests.cs | Verified same delegate reference |

### CrudSqlCache

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `GetSql<T>()` | yes | CrudSqlCacheTests.cs | 22 dedicated unit tests |
| `BuildInsertSql` | yes | CrudSqlCacheTests.cs | Simple, identity, computed entities |
| `BuildUpdateSql` | yes | CrudSqlCacheTests.cs | WHERE clause, key exclusion, composite keys |
| `BuildDeleteSql` | yes | CrudSqlCacheTests.cs | DELETE with WHERE, no-key returns empty |
| `BuildDeleteByIdSql` | yes | CrudSqlCacheTests.cs | Single key only, composite returns empty |
| `BuildUpsertSql` | yes | CrudSqlCacheTests.cs | ON CONFLICT / DO UPDATE SET |
| Identity column exclusion | yes | CrudSqlCacheTests.cs | Not in INSERT or UPDATE SET |
| Computed column exclusion | yes | CrudSqlCacheTests.cs | Not in INSERT |
| No-key entity (empty SQL) | yes | CrudSqlCacheTests.cs | Update/Delete return empty |
| Caching (same instance) | yes | CrudSqlCacheTests.cs | Same type returns cached |
| Caching (different types) | yes | CrudSqlCacheTests.cs | Different types differ |
| HasPrimaryKey / HasSinglePrimaryKey | yes | CrudSqlCacheTests.cs | Metadata properties |
| HasIdentityKey | yes | CrudSqlCacheTests.cs | Identity detection |
| LastInsertIdSql | yes | CrudSqlCacheTests.cs | SQLite dialect |

### Dialects (ISqlDialect implementations)

| Dialect | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `SQLiteDialect` | yes | SqlDialectTests.cs | 24 tests: escaping, paging, FK, upsert, functions, window |
| `SqlServerDialect` | yes | SqlDialectTests.cs | 17 tests: brackets, SCOPE_IDENTITY, MERGE, collations |
| `MySqlDialect` | yes | SqlDialectTests.cs | 14 tests: backticks, LAST_INSERT_ID, ON DUPLICATE KEY |
| `PostgreSqlDialect` | yes | SqlDialectTests.cs | 17 tests: double-quotes, RETURNING, ILIKE, EXTRACT |
| Cross-dialect comparisons | yes | SqlDialectTests.cs | 5 tests: COALESCE, NULLIF, window funcs, FK toggle |
| `SqlDialectFactory.GetDialect()` | yes | SqlDialectTests.cs | All 4 providers + unknown default tested |

### MultiEntityMapper

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| `Build(reader)` disjoint columns | yes | MultiEntityMapperTests.cs | 14 dedicated unit tests |
| `Build(reader)` multiple rows reuse | yes | MultiEntityMapperTests.cs | Same mapper across rows |
| `Build(reader)` T1 priority | yes | MultiEntityMapperTests.cs | Overlapping column goes to T1 |
| `Build(reader)` unmatched column ignored | yes | MultiEntityMapperTests.cs | Partial mapping behavior |
| `Build(reader)` [Column] attribute | yes | MultiEntityMapperTests.cs | Maps via column name |
| `Build(reader)` property name fallback | yes | MultiEntityMapperTests.cs | Maps via property name |
| `Build(reader)` case insensitive | yes | MultiEntityMapperTests.cs | UPPER names match |
| `ApplyT1()` / `ApplyT2()` values | yes | MultiEntityMapperTests.cs | Correct property assignment |
| `ApplyT1/T2` nullable null | yes | MultiEntityMapperTests.cs | NULL leaves property null |
| `ApplyT1/T2` nullable non-null | yes | MultiEntityMapperTests.cs | Sets value correctly |
| `ApplyT2` non-nullable null throws | yes | MultiEntityMapperTests.cs | InvalidOperationException |
| `Build` no matching columns | yes | MultiEntityMapperTests.cs | Empty setters, defaults remain |

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

## 11. IDbConnection Fallback Path Tests

> Tests using `IDbConnectionWrapper` (wraps a real connection but does NOT extend `DbConnection`), forcing all `if (connection is DbConnection)` checks to fail and exercising IDbConnection-only code paths.

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| Query via IDbConnection | yes | QueryFallbackTests.cs | IDataReader fallback |
| QueryFirst via IDbConnection | yes | QueryFallbackTests.cs | First row fallback |
| QuerySingle via IDbConnection | yes | QueryFallbackTests.cs | Single row fallback |
| QueryScalar via IDbConnection | yes | QueryFallbackTests.cs | Scalar fallback |
| QueryStream via IDbConnection | yes | QueryFallbackTests.cs | Streaming fallback |
| Insert via IDbConnection | yes | WriteFallbackTests.cs | Write fallback |
| Update via IDbConnection | yes | WriteFallbackTests.cs | Write fallback |
| Delete via IDbConnection | yes | WriteFallbackTests.cs | Write fallback |
| Multi-entity via IDbConnection | yes | MultiEntityFallbackTests.cs | Deprecated API fallback |
| Async write throws for non-DbConnection | yes | AsyncWriteFallbackTests.cs | Validates async requires DbConnection |

---

## 12. Microsoft.Data.Sqlite Provider Tests (net8.0 only)

| Feature | Status | Test File | Notes |
|---------|--------|-----------|-------|
| Query (sync + async) | yes | MicrosoftSqliteTests.cs | 14 tests, proper async support |
| Write operations | yes | MicrosoftSqliteTests.cs | Insert/Update/Delete |
| Streaming | yes | MicrosoftSqliteTests.cs | QueryStream + QueryPartialStream |
| GridReader async (all methods) | yes | MicrosoftSqliteGridReaderAsyncTests.cs | 19 async tests (ReadAsync, ReadPartialAsync, ReadFirstAsync, etc.) |
| Cross-provider parameter safety | yes | MicrosoftSqliteTests.cs | ParameterBinder type check fix verified |

---

## 13. Cross-Cutting Integration Tests

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

## 14. Jaunty.Fluent.Tests

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

## 15. Jaunty.Scaffolding.Tests

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
| Read (Partial) | 40 | 0 | 0 | 40 | 100% |
| Read (Scalar) | 16 | 0 | 0 | 16 | 100% |
| Read (MultiEntity) | 22+ | 0 | 0 | 22+ | 100% |
| Read (Special Types) | 4 | 0 | 0 | 4 | 100% |
| Streaming | 24 | 0 | 0 | 24 | 100% |
| Write (Individual) | 20 | 0 | 0 | 20 | 100% |
| Write (Bulk) | 24 | 0 | 0 | 24 | 100% |
| Write (Upsert) | 4 | 0 | 0 | 4 | 100% |
| Multiple | 17+ | 0 | 0 | 17+ | 100% |
| Stored Procedures | 28 | 0 | 0 | 28 | 100% (auto-skip without DB) |
| Core/Config | 12 | 0 | 0 | 12 | 100% |
| Attributes | 6 | 0 | 0 | 6 | 100% |
| Interfaces | 3 | 0 | 0 | 3 | 100% |
| Internals | 106 | 2 | 1 | 109 | 97% |
| IDbConnection Fallback | 10 | 0 | 0 | 10 | 100% |
| Microsoft.Data.Sqlite | 5 | 0 | 0 | 5 | 100% |
| Cross-cutting | 17 | 0 | 0 | 17 | 100% |
| Fluent API | 19 | 0 | 0 | 19 | 100% |
| Scaffolding | 6 | 0 | 0 | 6 | 100% |

### Priority Areas

#### 1. High Priority (Zero Coverage - Public API)
- [x] **QueryPartialFirst** / QueryPartialFirstOrDefault (8 sync + 8 async = 16 methods) - DONE
- [x] **QueryPartialSingle** / QueryPartialSingleOrDefault (8 sync + 8 async = 16 methods) - DONE
- [x] **QueryPartialUnbuffered** / QueryPartialUnbufferedAsync (4 sync + 4 async = 8 methods) - DONE
- [x] **ExecuteScalar** / ExecuteScalarAsync (4 sync + 4 async = 8 methods) - DONE
- [x] **Individual Insert** / InsertAsync (2 sync + 2 async = 4 methods) - DONE
- [x] **Individual Update** / UpdateAsync (2 sync + 2 async = 4 methods) - DONE
- [x] **Individual Delete** / DeleteAsync (all 12 methods) - DONE (includes Delete<T,TId>)
- [x] **StoredProcedure** methods - Skip-tests created (SQLite limitation)

#### 2. Medium Priority (Partial Coverage)
- [x] **GridReader** partial methods (ReadPartialFirst, ReadPartialSingle, etc.) - DONE
- [x] **BulkInsertIgnoreConstraints** / BulkUpdateIgnoreConstraints / BulkDeleteIgnoreConstraints - DONE
- [x] **MetadataBuilder** dedicated unit tests - DONE (18 tests)
- [x] **MappedCache** dedicated unit tests - DONE (3 tests, all passing — open generic bug fixed)
- [x] **CrudSqlCache** dedicated unit tests - DONE (22 tests)
- [x] **DrDispatcher** dedicated unit tests - DONE (9 tests, all passing — IMapped test un-skipped)
- [x] **IMapped<T>** interface coverage - DONE
- [x] **[DatabaseGenerated]** attribute explicit tests - DONE

#### 3. Lower Priority (Edge Cases / Non-SQLite)
- [x] SQL dialect unit tests (SqlServerDialect, MySqlDialect, PostgreSqlDialect) - DONE (77 tests across 5 nested classes)
- [x] `[Obsolete]` multi-entity overloads - DONE (21 tests: 14 sync + 7 async)
- [x] **IEntity<T>** explicit interface coverage - DONE (via DeleteByEntityIdTests)
- [x] MetadataCache<T> dedicated unit tests - DONE (22 tests)
- [x] MultiEntityMapper dedicated unit tests - DONE (14 tests)

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
| QueryPartialFirstTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryPartialFirstAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryPartialFirstOrDefaultTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryPartialFirstOrDefaultAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryPartialSingleTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryPartialSingleAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryPartialSingleOrDefaultTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryPartialSingleOrDefaultAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| ExecuteScalarTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| ExecuteScalarAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
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
| IMappedTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryArgumentValidationTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| QueryStreamTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Streaming/` |
| QueryStreamAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Streaming/` |
| QueryPartialStreamTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Streaming/` |
| QueryPartialStreamAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Streaming/` |
| QueryPartialUnbufferedTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Streaming/` |
| QueryPartialUnbufferedAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Streaming/` |
| QueryMultipleTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Multiple/` |
| QueryMultipleAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Multiple/` |
| GridReaderTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Multiple/` |
| GridReaderAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Multiple/` |
| BulkOperationsTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| BulkOperationsAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| InsertTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| InsertAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| UpdateTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| UpdateAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| DeleteTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| DeleteAsyncTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| DeleteByEntityIdTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| DatabaseGeneratedTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| UpsertTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Write/` |
| ConfigurationTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Configuration/` |
| ConfigResolverTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Configuration/` |
| QueryDatabaseConnectionTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Infrastructure/` |
| CommandOptionsTests.cs | `tests/Jaunty.Tests/Unit/Read/` |
| SqlParameterParserTests.cs | `tests/Jaunty.Tests/Unit/Read/` |
| ParameterBinderTests.cs | `tests/Jaunty.Tests/Unit/Read/` |
| MetadataBuilderTests.cs | `tests/Jaunty.Tests/Unit/Internals/` |
| MappedCacheTests.cs | `tests/Jaunty.Tests/Unit/Internals/` |
| CrudSqlCacheTests.cs | `tests/Jaunty.Tests/Unit/Internals/` |
| DrDispatcherTests.cs | `tests/Jaunty.Tests/Unit/Internals/` |
| SqlDialectTests.cs | `tests/Jaunty.Tests/Unit/Internals/` |
| MetadataCacheTests.cs | `tests/Jaunty.Tests/Unit/Internals/` |
| MultiEntityMapperTests.cs | `tests/Jaunty.Tests/Unit/Internals/` |
| ObsoleteMultiEntityTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| StoredProcedureTests.cs | `tests/Jaunty.Tests/Integration/Sqlite/Read/` |
| MicrosoftSqliteTests.cs | `tests/Jaunty.Tests/Integration/MicrosoftSqlite/` |
| MicrosoftSqliteGridReaderAsyncTests.cs | `tests/Jaunty.Tests/Integration/MicrosoftSqlite/` |
| QueryFallbackTests.cs | `tests/Jaunty.Tests/Integration/IDbConnectionFallback/` |
| WriteFallbackTests.cs | `tests/Jaunty.Tests/Integration/IDbConnectionFallback/` |
| MultiEntityFallbackTests.cs | `tests/Jaunty.Tests/Integration/IDbConnectionFallback/` |
| AsyncWriteFallbackTests.cs | `tests/Jaunty.Tests/Integration/IDbConnectionFallback/` |
| SqlServerStoredProcedureTests.cs | `tests/Jaunty.Tests/Integration/SqlServer/StoredProcedure/` |
| SqlServerStoredProcedureAsyncTests.cs | `tests/Jaunty.Tests/Integration/SqlServer/StoredProcedure/` |
| PostgresStoredProcedureTests.cs | `tests/Jaunty.Tests/Integration/Postgres/StoredProcedure/` |
| PostgresStoredProcedureAsyncTests.cs | `tests/Jaunty.Tests/Integration/Postgres/StoredProcedure/` |

---

## How to Use This Document

1. **Pick an uncovered item** () from the list above
2. **Read existing test files** in the same area to match patterns
3. **Write tests** in the appropriate test project/directory
4. **Run tests**: `dotnet test`
5. **Mark as covered** () once tests pass
6. **Update the Summary section** percentages
7. **Commit**: `git commit -m "test: add tests for [ClassName]"`
