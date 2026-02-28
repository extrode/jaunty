# Jaunty Performance Improvement Roadmap

Actionable task list based on benchmark analysis. Items are prioritized by expected impact.

---

## Completed in This Session

- [x] **Reduce benchmark runtime** from 8h+ to ~3-4h by trimming QueryBenchmarks RowCount from 6 values to 3 (`1, 100, 10_000`)
- [x] **Fix positional parameter support** — added `IsScalarType` + `BindScalar` to `ParameterBinder` so scalar values (int, string, Guid, etc.) bind directly to SQL parameters
- [x] **Cache composed INSERT SQL** — moved `ComposeInsertCommandText` into `CachedCrudSql.InsertCommandText` (computed once at cache-build time, eliminates per-call string concatenation)
- [x] **Eliminate per-bind allocation in `CommandTemplate.Bind`** — replaced `command.CreateParameter().GetType()` with `command.GetType()` for provider type checking (saves one `IDbDataParameter` allocation per bind call)

---

## Priority 1: High Impact (Single Insert & QueryScalar)

### 1.1 Single Insert: Jaunty 1.17x slower than Dapper (warm)

**Root cause analysis:**
- Jaunty uses `ExecuteScalar` (to return identity) vs Dapper's `ExecuteNonQuery` — ~3us overhead per call
- `CrudSqlCache.GetSql<T>()` does a `ConcurrentDictionary` lookup on every call (key = `(Type, string dialect)`)
- `connection.State == ConnectionState.Closed` check + try/finally + `connection.Open()/Close()` lifecycle management adds overhead

**Actions:**
- [ ] **Benchmark with `ExecuteNonQuery` path**: When entity has no identity key, the insert already uses `ExecuteNonQuery`. Verify this path is on par with Dapper
- [ ] **Consider a `InsertNoReturn` API**: For fire-and-forget inserts where the identity value isn't needed, bypass `ExecuteScalar` entirely
- [ ] **Profile `CrudSqlCache.GetSql<T>`**: The dictionary lookup should be ~50ns but may be slower due to the tuple key hashing. Consider using a `ConditionalWeakTable<Type, ...>` or generic static cache pattern

### 1.2 QueryScalar: Jaunty 1.16x slower than Dapper (warm)

**Root cause analysis:**
- Jaunty's `QueryScalar<T>` goes through `QueryCore.QueryScalarCore<T>` which uses `dbReader.GetFieldValue<T>(0)` — zero-boxing, efficient
- The overhead is likely in the command creation path: `connection.CreateCommand()`, parameter binding overhead, `CommandText` assignment
- Dapper's `ExecuteScalar` is a thinner wrapper around `IDbCommand.ExecuteScalar()`

**Actions:**
- [ ] **Profile QueryScalar hot path**: Use a profiler to identify the exact overhead source
- [ ] **Compare command creation**: Dapper may reuse command objects or have a lighter creation path

---

## Priority 2: Medium Impact (Caching & Cold Start)

### 2.1 Named Param Cold/Warm Ratio (4.47x cold vs 1.34x warm advantage)

The 3.3x gap between cold and warm advantage over Dapper suggests Jaunty's caching warms up well but Dapper's IL.Emit caching produces tighter warm-run code.

**Dapper's approach (reference):**
- `Identity` cache key: SQL + ReturnType + CommandType + ConnectionString + ParamType
- IL-generated type deserializers via `Emit` (faster than `Expression.Compile()`)
- `ConcurrentDictionary` with 1000-item limit

**Actions:**
- [ ] **Evaluate IL.Emit for parameter binding**: Replace `Expression.Lambda<>.Compile()` in `ParameterCache.CreateGetter` with `DynamicMethod` + IL.Emit for ~10-15% faster delegates
- [ ] **Consider source-generating parameter binders**: Like `IMapped<T>` generates `ReadEntity`/`BindInsert`, source-generate parameter getters too — zero-reflection, zero-expression-compile cold start
- [ ] **Profile `TemplateCache` hit rate**: Ensure the cache key `(SQL, Type, CommandType)` is efficient and not causing excessive hash collisions

### 2.2 Query<T> Cold Start: 1.53x slower than Dapper at RowCount=1

**Root cause analysis:**
- At RowCount=1, the cold measurement is dominated by first-call overhead: type resolution, cache population, JIT
- `MappedCache<T>.Resolve()` uses reflection to find `ReadEntity` static method on first call
- `DrDispatcher.Resolve()` walks the mapper resolution chain: user mapper -> IMapped<T> -> special types -> reflection fallback
- At RowCount=100+, Jaunty is consistently faster than Dapper — the cold start penalty is amortized

**Actions:**
- [ ] **Pre-warm MappedCache in source generator**: Have the source generator emit a static constructor or module initializer that pre-registers the `ReadEntity` delegate, avoiding runtime reflection
- [ ] **Lazy-initialize DrDispatcher**: Defer expensive initialization until actually needed
- [ ] **Consider `[ModuleInitializer]` attribute**: Auto-register all IMapped<T> types at assembly load time (.NET 5+)

---

## Priority 3: Lower Impact (BulkInsert Optimization)

### 3.1 BulkInsert ValueSetter uses PropertyInfo.GetValue() reflection

**File:** `src/Jaunty/Internals/Write/WriteParameterCache.cs` lines 50-61

The `InsertValueSetter` delegate uses `col.Property.GetValue(entity)` — raw `PropertyInfo.GetValue()` reflection on every row in a bulk insert loop. While BulkInsert is already 2.4-2.9x faster than Dapper, this is leaving performance on the table.

**Actions:**
- [ ] **Replace `PropertyInfo.GetValue()` with compiled delegates**: Pre-compile `Func<T, object>` getters for each column using `Expression.Lambda`, similar to `ParameterCache.CreateGetter`. Cache in `WriteParameterCache<T>`
- [ ] **Source-generate value setters**: Extend the source generator to emit `SetInsertValues(IDataParameterCollection, T)` alongside `BindInsert` — zero-reflection bulk path
- [ ] **Benchmark before/after**: Expect 15-30% improvement on warm BulkInsert due to eliminating reflection in the hot loop

### 3.2 Source-generated ReadEntity calls GetOrdinal per property per row

The source-generated `ReadEntity` method calls `reader.GetOrdinal("column_name")` for each property on each row. This involves a string lookup per property per row.

**Actions:**
- [ ] **Cache ordinal indices**: On the first row, resolve all ordinals and store in a local array. Use indexed access for subsequent rows
- [ ] **Emit ordinal caching in source generator**: Have the generator produce code like:
  ```csharp
  int _ord0 = -1;
  if (_ord0 < 0) _ord0 = reader.GetOrdinal("product_id");
  ```

---

## Priority 4: Benchmark Infrastructure

### 4.1 Re-run benchmarks with all 4 databases

The saved report files only contain SQLite results. SQL Server, PostgreSQL, and MariaDB showed NA in the saved files (from before multi-DB fixes). Need to re-run all benchmarks to get multi-database data.

**Actions:**
- [ ] **Verify database connectivity**: Ensure SQL Server, PostgreSQL, and MariaDB are running and accessible
- [ ] **Run full benchmark suite**: `dotnet run -c Release -- --filter "*"`
- [ ] **Update BENCHMARK-RESULTS.md**: Add multi-database comparison tables
- [ ] **Update BENCHMARK-REPORT.html**: Add per-database charts

### 4.2 Fix remaining NA benchmarks

- [ ] **Fix MapperBenchmarks hand-written mapper**: Shows NA — investigate and fix the HandWrittenMapper benchmark
- [ ] **Fix QueryFirstBenchmarks RepoDb**: Shows NA on SQLite — RepoDb's Query for single-row may need different API usage
- [ ] **Add Dapper positional param benchmark**: Now that Jaunty supports scalar params, add Dapper's equivalent for fair comparison

### 4.3 Benchmark runtime further optimization

Current estimate: ~3-4 hours with reduced RowCount params. Further reductions possible:
- [ ] **Consider splitting by database**: Run `--filter "*" --job Cold` and `--job Warm` separately on different DBs
- [ ] **Add `--join` flag**: BDN's `--join` can reduce process overhead
- [ ] **Reduce BulkInsert batch sizes**: Current `(100, 1_000, 10_000)` — consider `(100, 1_000)` for faster iteration

---

## Performance Targets

| Benchmark | Current vs Dapper | Target |
|-----------|-------------------|--------|
| Query (warm) | 1.1-1.4x faster | Maintain |
| Query memory | 3.5x less | Maintain |
| QueryFirst | 1.29x faster | Maintain |
| QueryScalar | 1.16x slower | Parity (1.0x) |
| Single Insert | 1.17x slower | Parity (1.0x) |
| Bulk Insert | 2.4-2.9x faster | Maintain or improve |
| Named Params | 1.34x faster | 1.5x+ faster |
| Cold Start | Mixed | Consistently faster |

---

*Generated from benchmark analysis session. See `BENCHMARK-RESULTS.md` for raw data and `BENCHMARK-REPORT.html` for visual charts.*
