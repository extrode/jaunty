# Parameter Binding Specification

**Version**: 2026.02.19  
**Status**: Active

---

## Overview

Jaunty's parameter binding system extracts parameter names from SQL, validates counts, and binds object properties to `DbCommand.Parameters`.

---

## Components

### 1. SqlParameterParser

**Purpose**: State machine that extracts `@param` names from SQL strings.

**Location**: `src/Jaunty/Internals/Parameters/SqlParameterParser.cs`

**State Machine States**:

| State | Trigger | Action |
|-------|---------|--------|
| `Normal` | `@` | Start parameter name |
| `Normal` | `--` | Enter `SingleLineComment` |
| `Normal` | `/*` | Enter `BlockComment` |
| `Normal` | `'` | Enter `StringLiteral` |
| `Normal` | `"` | Enter `QuotedIdentifier` |
| `Normal` | `[` | Enter `QuotedIdentifier` |
| `SingleLineComment` | `\n` | Return to `Normal` |
| `BlockComment` | `*/` | Return to `Normal` |
| `StringLiteral` | `'` (unescaped) | Return to `Normal` |
| `QuotedIdentifier` | `"` or `]` | Return to `Normal` |

**Implementation**:

```csharp
internal static class SqlParameterParser
{
    public static string[] ExtractParameterNames(string sql)
    {
        var parameters = new HashSet<string>();
        var state = ParserState.Normal;
        var i = 0;
        
        while (i < sql.Length)
        {
            var c = sql[i];
            
            switch (state)
            {
                case ParserState.Normal:
                    if (c == '@' && i + 1 < sql.Length && char.IsLetterOrDigit(sql[i + 1]))
                    {
                        // Extract parameter name
                        var start = i + 1;
                        while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_'))
                            i++;
                        
                        var paramName = sql.Substring(start, i - start);
                        parameters.Add(paramName);
                        continue;
                    }
                    else if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
                    {
                        state = ParserState.SingleLineComment;
                        i += 2;
                        continue;
                    }
                    else if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                    {
                        state = ParserState.BlockComment;
                        i += 2;
                        continue;
                    }
                    else if (c == '\'')
                    {
                        state = ParserState.StringLiteral;
                        i++;
                        continue;
                    }
                    else if (c == '"' || c == '[')
                    {
                        state = ParserState.QuotedIdentifier;
                        i++;
                        continue;
                    }
                    break;
                    
                case ParserState.SingleLineComment:
                    if (c == '\n')
                        state = ParserState.Normal;
                    break;
                    
                case ParserState.BlockComment:
                    if (c == '*' && i + 1 < sql.Length && sql[i + 1] == '/')
                    {
                        state = ParserState.Normal;
                        i++;
                    }
                    break;
                    
                case ParserState.StringLiteral:
                    if (c == '\'' && (i == 0 || sql[i - 1] != '\\'))
                        state = ParserState.Normal;
                    break;
                    
                case ParserState.QuotedIdentifier:
                    if ((c == '"' || c == ']'))
                        state = ParserState.Normal;
                    break;
            }
            
            i++;
        }
        
        return parameters.ToArray();
    }
}
```

**Examples**:

```csharp
// Simple parameter
ExtractParameterNames("SELECT * FROM products WHERE id = @Id")
// Returns: ["Id"]

// Multiple parameters
ExtractParameterNames("SELECT * FROM products WHERE category_id = @CategoryId AND price > @MinPrice")
// Returns: ["CategoryId", "MinPrice"]

// Duplicate parameters (deduplicated)
ExtractParameterNames("SELECT * FROM products WHERE category_id = @Id OR supplier_id = @Id")
// Returns: ["Id"]

// Comments ignored
ExtractParameterNames("SELECT * FROM products WHERE id = @Id -- @NotAParam")
// Returns: ["Id"]

// String literals ignored
ExtractParameterNames("SELECT * FROM products WHERE name = '@NotAParam'")
// Returns: []

// Quoted identifiers ignored
ExtractParameterNames("SELECT * FROM [products] WHERE [id] = @Id")
// Returns: ["Id"]
```

---

### 2. SqlParameterParserCache

**Purpose**: Caches parsed parameter names per SQL string.

**Location**: `src/Jaunty/Internals/Parameters/SqlParameterParserCache.cs`

```csharp
internal static class SqlParameterParserCache
{
    // Size-capped: callers that embed literals or build SQL dynamically would otherwise leak
    // memory through an ever-growing set of distinct SQL-text keys.
    private static readonly BoundedCache<string, string[]> Cache = new(StringComparer.Ordinal);

    // AUD-R34-014: the same SQL text parses differently under MySQL/MariaDB, where a backslash
    // escapes the next character inside a string literal. Two caches rather than one composite
    // key, because the flag is fixed per engine.
    private static readonly BoundedCache<string, string[]> BackslashEscapedCache = new(StringComparer.Ordinal);

    public static string[] GetOrAdd(string sql, bool backslashEscapes = false)
    {
        return backslashEscapes
            ? BackslashEscapedCache.GetOrAdd(sql, static s => SqlParameterParser.ExtractParameterNames(s, backslashEscapes: true))
            : Cache.GetOrAdd(sql, static s => SqlParameterParser.ExtractParameterNames(s));
    }
}
```

**Cache Key**: Raw SQL string (case-sensitive).

**Eviction**: None (application lifetime).

---

### 3. ParameterBinder

**Purpose**: Binds object properties to `DbCommand.Parameters`.

**Location**: `src/Jaunty/Internals/Parameters/ParameterBinder.cs`

**Named Parameter Binding**:

```csharp
internal static class ParameterBinder
{
    public static void Bind(DbCommand command, object parameters)
    {
        if (parameters is null) return;
        
        var type = parameters.GetType();
        var properties = ParameterCache.GetProperties(type);
        
        foreach (var prop in properties)
        {
            var value = prop.GetValue(parameters);
            var dbParam = command.CreateParameter();
            dbParam.ParameterName = prop.Name;
            dbParam.Value = value ?? DBNull.Value;
            command.Parameters.Add(dbParam);
        }
    }
}
```

**Positional Parameter Binding**:

```csharp
public static void BindPositional(DbCommand command, object[] parameters, string[] parameterNames)
{
    if (parameters.Length != parameterNames.Length)
    {
        throw new ArgumentException(
            $"Parameter count mismatch: SQL contains {parameterNames.Length} unique parameter(s), " +
            $"but {parameters.Length} value(s) provided.");
    }
    
    for (var i = 0; i < parameters.Length; i++)
    {
        var dbParam = command.CreateParameter();
        dbParam.ParameterName = parameterNames[i];
        dbParam.Value = parameters[i] ?? DBNull.Value;
        command.Parameters.Add(dbParam);
    }
}
```

**Type Handling**:

| CLR Type | DbType | Notes |
|----------|--------|-------|
| `string` | `String` | NULL → `DBNull.Value` |
| `int` | `Int32` | - |
| `long` | `Int64` | - |
| `decimal` | `Decimal` | - |
| `double` | `Double` | - |
| `float` | `Single` | - |
| `bool` | `Boolean` | - |
| `DateTime` | `DateTime` | - |
| `Guid` | `Guid` | - |
| `byte[]` | `Binary` | - |
| `enum` | underlying | Converted to underlying type |

---

### 4. ParameterCache<T>

**Purpose**: Caches property info for parameter types.

**Location**: `src/Jaunty/Internals/Parameters/ParameterCache.cs`

```csharp
internal static class ParameterCache<T>
{
    public static readonly PropertyInfo[] Properties = typeof(T)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanRead)
        .ToArray();
}

internal static class ParameterCache
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _cache = new();
    
    public static PropertyInfo[] GetProperties(Type type)
    {
        return _cache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToArray());
    }
}
```

---

## Validation

### Parameter Count Validation

**Purpose**: Catch parameter mismatches early with clear error messages.

```csharp
public static void ValidateParameterCount(string sql, object? parameters)
{
    if (parameters is null) return;
    
    var expectedParameters = SqlParameterParserCache.GetOrAdd(sql);
    
    if (parameters is not IEnumerable<object> paramArray)
    {
        // Single object - check property count matches
        var properties = ParameterCache.GetProperties(parameters.GetType());
        
        // Handle duplicate parameters in SQL
        var uniqueSqlParams = expectedParameters.Length;
        var providedParams = properties.Length;
        
        if (uniqueSqlParams != providedParams)
        {
            throw new ArgumentException(
                $"Parameter count mismatch: SQL contains {uniqueSqlParams} unique parameter(s), " +
                $"but {providedParams} property/properties provided.");
        }
    }
    else
    {
        // Array - check element count matches
        var paramList = paramArray.ToList();
        if (expectedParameters.Length != paramList.Count)
        {
            throw new ArgumentException(
                $"Parameter count mismatch: SQL contains {expectedParameters.Length} unique parameter(s), " +
                $"but {paramList.Count} value(s) provided.");
        }
    }
}
```

**Error Messages**:

```
Parameter count mismatch: SQL contains 2 unique parameter(s), but 3 value(s) provided.
SQL parameters: [@CategoryId, @MinPrice]. Provided: [1, 100, 200].

Parameter count mismatch: SQL contains 3 unique parameter(s), but 2 property/properties provided.
SQL parameters: [@CategoryId, @MinPrice, @MaxPrice]. Provided: [CategoryId, MinPrice].
```

---

## Collection Parameter Expansion

**Purpose**: Support `WHERE id IN @Ids` syntax.

**Location**: `src/Jaunty/Internals/Parameters/ParameterBinder.cs`

```csharp
public static void BindWithCollectionExpansion(DbCommand command, object parameters)
{
    var type = parameters.GetType();
    var properties = ParameterCache.GetProperties(type);
    
    foreach (var prop in properties)
    {
        var value = prop.GetValue(parameters);
        
        if (value is IEnumerable enumerable && value is not string)
        {
            // Expand collection into individual parameters
            var items = enumerable.Cast<object>().ToList();
            
            if (items.Count == 0)
            {
                // Empty collection - use sentinel value
                var dbParam = command.CreateParameter();
                dbParam.ParameterName = prop.Name;
                dbParam.Value = DBNull.Value;
                command.Parameters.Add(dbParam);
            }
            else
            {
                // Create individual parameters @Ids_0, @Ids_1, etc.
                for (var i = 0; i < items.Count; i++)
                {
                    var dbParam = command.CreateParameter();
                    dbParam.ParameterName = $"{prop.Name}_{i}";
                    dbParam.Value = items[i] ?? DBNull.Value;
                    command.Parameters.Add(dbParam);
                }
            }
        }
        else
        {
            // Single value
            var dbParam = command.CreateParameter();
            dbParam.ParameterName = prop.Name;
            dbParam.Value = value ?? DBNull.Value;
            command.Parameters.Add(dbParam);
        }
    }
}
```

**SQL Rewriting**:

The SQL must be rewritten to match expanded parameters:

```csharp
// Original SQL
WHERE category_id IN @CategoryIds

// Rewritten SQL
WHERE category_id IN (@CategoryIds_0, @CategoryIds_1, @CategoryIds_2)
```

---

## Performance Characteristics

### Parsing Cost (One-Time per SQL)

| Operation | Complexity | Allocations |
|-----------|------------|-------------|
| `ExtractParameterNames` | O(s) where s = SQL length | string[], HashSet |
| Cache lookup | O(1) | None (after warm) |

**Typical cost**: ~1-10μs for 100-1000 character SQL.

### Binding Cost (Per-Query)

| Operation | Complexity | Allocations |
|-----------|------------|-------------|
| Property lookup | O(1) | None (cached) |
| Property value read | O(1) | Boxed value (if value type) |
| DbParameter creation | O(1) | DbParameter instance |

**Typical cost**: ~100-500ns per parameter.

---

## Error Handling

### NULL Handling

```csharp
// Reference types: NULL → null
// Value types: NULL → throws
// Nullable<T>: NULL → null

if (value is null)
{
    dbParam.Value = DBNull.Value;
}
```

### Type Conversion Errors

```csharp
try
{
    dbParam.Value = value;
}
catch (InvalidCastException ex)
{
    throw new ArgumentException(
        $"Cannot convert parameter '{prop.Name}' of type '{value?.GetType().Name ?? "null"}' " +
        $"to database type '{dbParam.DbType}'.", ex);
}
```

---

## See Also

- [`architecture-specification.md`](architecture-specification.md) - Full architecture
- [`../01-api-reference/query-methods.md`](../01-api-reference/query-methods.md) - Query method API
- [`../03-development/adding-new-methods.md`](../03-development/adding-new-methods.md) - Adding methods
