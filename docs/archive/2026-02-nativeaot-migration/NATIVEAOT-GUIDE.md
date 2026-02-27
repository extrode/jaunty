# Jaunty NativeAOT Usage Guide

**Last Updated:** 2026-02-27
**Jaunty Version:** 2026.01.01+

---

## Overview

Jaunty is designed to be **NativeAOT-compatible**. The core library uses source-generated mappers for optimal performance and trim safety. Optional reflection-based features are provided in a separate assembly that can be included or excluded based on your needs.

---

## Architecture

```
Jaunty (NativeAOT-safe)
├── Source-generated mappers (IMapped<T>)
├── Compiled expression trees
└── Zero reflection for entity mapping

Jaunty.Extensions.Reflection (Optional)
├── Reflection-based entity mapping
├── Special type support (Dictionary, KeyValuePair, ValueTuple)
└── NOT NativeAOT-safe (requires trimming configuration)
```

---

## Quick Start for NativeAOT

### Option 1: Source-Generated Only (Recommended)

For best NativeAOT compatibility, use source-generated mappers exclusively:

```csharp
using Jaunty;

[Table("products")]
public class Product
{
    [Column("product_id")]
    public int Id { get; set; }
    
    [Column("product_name")]
    public string Name { get; set; } = string.Empty;
    
    [Column("unit_price")]
    public decimal Price { get; set; }
}

// Usage - fully NativeAOT compatible
var products = connection.Query<Product>("SELECT * FROM products");
```

**Project file:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <TrimMode>full</TrimMode>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Beparey.Jaunty" Version="2026.01.01" />
    <!-- Do NOT include Jaunty.Extensions.Reflection -->
  </ItemGroup>
</Project>
```

### Option 2: With Reflection Support

If you need special types (Dictionary, KeyValuePair, ValueTuple) or dynamic mapping:

```csharp
using Jaunty;
using Jaunty.Extensions.Reflection;

// At application startup
SpecialTypeMappers.Register();

// Now you can use special types
var dictResults = connection.Query<Dictionary<string, object>>(
    "SELECT product_id, product_name, unit_price FROM products");
```

**Project file:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <TrimMode>full</TrimMode>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Beparey.Jaunty" Version="2026.01.01" />
    <PackageReference Include="Beparey.Jaunty.Extensions.Reflection" Version="2026.01.01" />
  </ItemGroup>
  
  <!-- Prevent trimming of reflection extension -->
  <ItemGroup>
    <TrimmerRootAssembly Include="Jaunty.Extensions.Reflection" />
  </ItemGroup>
</Project>
```

---

## Supported Features (NativeAOT)

### Fully Supported

| Feature | Description |
|---------|-------------|
| `Query<T>()` | Source-generated entity mapping |
| `QueryPartial<T>()` | Partial entity mapping |
| `QueryFirst<T>()`, `QuerySingle<T>()` | Single entity queries |
| `QueryScalar<T>()` | Scalar value queries |
| `Insert<T>()`, `Update<T>()`, `Delete<T>()` | CRUD operations |
| `BulkInsert<T>()`, `BulkUpdate<T>()`, `BulkDelete<T>()` | Bulk operations |
| `Upsert<T>()` | Insert or update |
| `QueryMultiple()` | Multiple result sets |
| Anonymous type parameters | `new { Id = 1 }` |
| CommandOptions | Transactions, timeouts, custom mappers |

### Requires Reflection Extension

| Feature | Description |
|---------|-------------|
| `Query<Dictionary<string, T>>()` | Dictionary mapping |
| `Query<KeyValuePair<TKey, TValue>>()` | Key-value pair mapping |
| `Query<ValueTuple<...>>()` | ValueTuple mapping |
| `Query<dynamic>()` | Dynamic/ExpandoObject mapping |
| `QueryPartial<T>()` without `[Table]` | Reflection-based partial mapping |

---

## Trimming Configuration

### For Source-Generated Only

No special configuration needed. Jaunty is fully trim-safe.

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <TrimMode>full</TrimMode>
</PropertyGroup>
```

### With Reflection Extension

Exclude the reflection extension from trimming:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <TrimMode>full</TrimMode>
</PropertyGroup>

<ItemGroup>
  <!-- Keep reflection extension types -->
  <TrimmerRootAssembly Include="Jaunty.Extensions.Reflection" />
  
  <!-- Or use DynamicDependency for fine-grained control -->
  <DynamicDependency Include="Jaunty.Extensions.Reflection.SpecialTypeMappers" />
</ItemGroup>
```

---

## Manual Initialization (Advanced)

For full control over initialization in NativeAOT:

```csharp
using Jaunty;
using Jaunty.Extensions.Reflection;

public class Program
{
    public static void Main()
    {
        // Manually initialize reflection mapping
        // This avoids the automatic Assembly.Load which may fail in NativeAOT
        JauntyReflectionExtensions.UseReflectionMapping();
        SpecialTypeMappers.Register();
        
        // Now use Jaunty normally
        var connection = new SqliteConnection("Data Source=test.db");
        var products = connection.Query<Product>("SELECT * FROM products");
    }
}
```

---

## Common Issues and Solutions

### Issue: "No mapper found for type"

**Cause:** Using a type without `[Table]` attribute without the reflection extension.

**Solution:**
1. Add `[Table("table_name")]` to your entity class, OR
2. Include Jaunty.Extensions.Reflection and call `UseReflectionMapping()`

### Issue: "Dictionary mapping not working in NativeAOT"

**Cause:** Dictionary mapping requires reflection.

**Solution:**
1. Include Jaunty.Extensions.Reflection
2. Call `SpecialTypeMappers.Register()` at startup
3. Add `<TrimmerRootAssembly Include="Jaunty.Extensions.Reflection" />` to project file

### Issue: "Anonymous type parameters not working"

**Cause:** Anonymous types use reflection for property binding.

**Solution:**
This is trim-safe! Anonymous types preserve their properties. If issues occur, add:
```xml
<DynamicDependency Include="System.Reflection.PropertyInfo" />
```

---

## Performance Considerations

| Scenario | Performance | NativeAOT |
|----------|-------------|-----------|
| Source-generated mapper | Fastest | Full support |
| Reflection-based mapper | Slower | Requires config |
| Special types | Slower | Requires config |
| Anonymous parameters | Fast | Trim-safe |

---

## Migration from Dapper

If migrating from Dapper to Jaunty for NativeAOT:

1. **Entity classes:** Add `[Table]` and `[Column]` attributes
2. **Queries:** Replace `connection.Query<T>(sql)` with same Jaunty API
3. **Special types:** Use source-generated entities instead of Dictionary when possible
4. **Parameters:** Anonymous objects work the same way

```csharp
// Dapper
var product = connection.QueryFirstOrDefault<Product>(
    "SELECT * FROM products WHERE id = @Id", new { Id = 1 });

// Jaunty (same API)
var product = connection.QueryFirstOrDefault<Product>(
    "SELECT * FROM products WHERE product_id = @Id", new { Id = 1 });
```

---

## Verification

Use the included verification script to check NativeAOT compatibility:

```powershell
# From project root
./scripts/Verify-NativeAOT.ps1
```

Expected output for source-generated only:
```
PASS: No NativeAOT issues found!
Jaunty is ready for NativeAOT compilation.
```

---

## Sample Projects

> **Note:** Sample projects are planned but not yet created. See [NATIVE-AOT-PLAN.md](NATIVE-AOT-PLAN.md) for status.

Planned samples:

- `samples/NativeAOT-Basic` - Basic NativeAOT with source-generated mappers
- `samples/NativeAOT-WithReflection` - NativeAOT with reflection extension
- `samples/NativeAOT-CustomMapper` - NativeAOT with custom manual mappers

---

## Support

For issues or questions:
1. Check the [GitHub Issues](https://github.com/beparey/jaunty/issues)
2. Review the [Migration Plan](NATIVEAOT-MIGRATION-PLAN.md)
3. Check current [Status](NATIVEAOT-STATUS.md)
