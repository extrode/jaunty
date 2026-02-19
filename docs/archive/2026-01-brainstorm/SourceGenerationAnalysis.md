# Source Generation in Jaunty: Analysis

## Current Architecture

Jaunty already achieves excellent performance through:
- **Expression tree compilation** cached permanently in `MetadataCache<T>`
- **Static generic caching** - one-time initialization per type via static constructors
- **FrozenDictionary** column lookup on .NET 8+
- **Compiled delegates** for property setters - no reflection during query execution

The key insight: **Jaunty already eliminates runtime reflection costs** via compiled expression trees. The question becomes: what would source generation add?

---

## Benefits of Source Generation

### 1. Startup Performance
- **Current**: First query for a type triggers static constructor → reflection → expression tree compilation → IL emit
- **With SG**: Mapper code exists at compile time. Zero runtime code generation cost.
- **Impact**: Meaningful for serverless/cold-start scenarios (Azure Functions, AWS Lambda)

### 2. AOT Compilation Compatibility
- **Current**: Expression tree compilation is **not AOT-compatible**. Native AOT throws `PlatformNotSupportedException` for `Expression.Compile()`
- **With SG**: Generated code compiles directly. **Full AOT support**.
- **Impact**: Critical for .NET 8+ Native AOT, iOS/Android, WASM/Blazor

### 3. Debuggability
- **Current**: Compiled delegates are opaque black boxes in debugger
- **With SG**: Generated `.g.cs` files are readable, debuggable, breakpoint-able
- **Impact**: Easier troubleshooting of mapping issues

### 4. Trimming Compatibility
- **Current**: Reflection-based metadata building triggers trimmer warnings, may break
- **With SG**: No reflection needed for mapper code. **Fully trimmable**.
- **Impact**: Smaller deployment size, works with `PublishTrimmed`

### 5. Compile-Time Errors
- **Current**: Missing property setters discovered at runtime
- **With SG**: Generator can emit warnings/errors for mapping issues
- **Impact**: Earlier feedback during development

---

## Pitfalls & Challenges

### 1. Two Mapping Systems to Maintain
The brainstorm doc's approach requires `[DataReaderMapper]` attribute. But Jaunty's value proposition is **zero boilerplate** - users just call `Query<T>()` on any POCO.

**Problem**: You'd need to either:
- Require users to add attributes (breaks simplicity)
- Auto-detect types used with Jaunty (complex, fragile)
- Maintain **both** expression tree AND source generator paths

### 2. Incremental Complexity
Source generators must be:
- Incremental (to not slow compilation)
- Deterministic (same input → same output)
- Properly cached

**Reality**: Jaunty would need a separate `Jaunty.Generators` NuGet package targeting `netstandard2.0` (for Roslyn compatibility), adding deployment/versioning complexity.

### 3. Runtime Configuration Doesn't Work
Jaunty supports runtime configuration:
```csharp
JauntyConfig.ColumnNameResolver = NamingConvention.ToSnakeCase;
```

**Problem**: Source generators run at compile time. They can't know runtime configuration. You'd need:
- Compile-time attributes only (`[Column("snake_case_name")]`)
- Or generate multiple variants (explosively complex)

### 4. Dynamic Queries Break
Consider:
```csharp
var results = connection.Query<T>(dynamicSql); // T determined at runtime
```

Source generators can't help with generic type parameters resolved at runtime.

### 5. Generic Type Parameters
Source generators analyze syntax, not semantics for open generics. Code like:
```csharp
public T FetchOne<T>(string sql) where T : new()
    => connection.QueryFirst<T>(sql);
```
Can't have generated mappers - T is unknown at compile time.

### 6. Column Name Matching is Runtime
Jaunty matches result set columns to properties **at runtime** because SQL is a string. The generator can create the setter code, but column ordinal resolution still happens at execution.

---

## Honest Comparison

| Aspect | Expression Trees (Current) | Source Generation |
|--------|---------------------------|-------------------|
| **Startup cost** | First query ~1-2ms per type | Near zero |
| **Steady-state perf** | Identical | Identical |
| **AOT support** | Not compatible | Full support |
| **Trimming** | Requires annotations | Fully trimmable |
| **Debugging** | Opaque delegates | Readable code |
| **Zero config** | Works on any POCO | Requires attribute or detection |
| **Runtime config** | Full support | Compile-time only |
| **Maintenance** | Single code path | Two systems |
| **Dependency** | None | Separate analyzer package |

---

## Pragmatic Assessment

### When Source Generation Makes Sense
1. **Native AOT is required** - No other option
2. **Serverless cold starts are critical** - Every millisecond counts
3. **Trimmed deployments** - Need minimal binary size
4. **iOS/Android/Blazor WASM** - AOT platforms

### When Current Approach is Better
1. **General .NET development** - Expression trees work great
2. **Runtime configuration needed** - Naming conventions, etc.
3. **Generic repository patterns** - Open generics at call sites
4. **Simplicity is paramount** - No extra packages/attributes

---

## What Implementation Would Actually Require

If you pursued this:

### 1. New Package Structure
```
Jaunty/
├── src/Jaunty/                    # Runtime library (existing)
├── src/Jaunty.Generators/         # Source generator (NEW)
│   ├── DataReaderMapperGenerator.cs
│   └── Jaunty.Generators.csproj   # netstandard2.0, analyzer package
```

### 2. Attribute for Opt-In
```csharp
[GenerateMapper]  // Triggers source generation
public class Product { ... }
```

### 3. Generated Code Pattern
```csharp
// Product.g.cs (generated)
public static partial class JauntyMappers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Product MapProduct(IDataReader reader, int[] ordinals)
    {
        return new Product
        {
            Id = reader.IsDBNull(ordinals[0]) ? 0 : reader.GetInt32(ordinals[0]),
            Name = reader.IsDBNull(ordinals[1]) ? null : reader.GetString(ordinals[1]),
            // ...
        };
    }
}
```

### 4. Runtime Detection
`DrDispatcher` would need to check for generated mapper:
```csharp
// Check if generated mapper exists via static field/method
if (JauntyMappers.HasMapperFor<T>())
    return JauntyMappers.GetMapper<T>(reader);
// Fall back to expression trees
```

### 5. Ordinal Resolution Still Required
Column-to-ordinal mapping must happen at runtime (result set structure isn't known at compile time). So even generated mappers need `GetOrdinal()` calls.

---

## Recommendation

Given Jaunty's current architecture and goals:

**Don't implement source generation as the primary path.** The expression tree approach already achieves steady-state performance parity, and maintaining two systems adds significant complexity.

**Consider source generation as an opt-in feature if**:
1. You want to support Native AOT (growing .NET 8+ use case)
2. Users explicitly request it for cold-start optimization
3. You're willing to maintain a separate `Jaunty.Generators` package

**Alternative worth considering**: The `IMapped<T>` interface already exists and lets users provide hand-written mappers. This could be documented as the "AOT-compatible path" without adding generator complexity.

---

## Related Documents
- [HighPerformanceMapperImplementation.md](./HighPerformanceMapperImplementation.md) - Original brainstorm document
