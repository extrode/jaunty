# Jaunty NativeAOT Migration Plan

**Goal:** Make Jaunty fully NativeAOT compatible by moving all reflection-based code to a separate extension assembly.

**Current Status:** 
- Core query operations working with source-generated mappers
- `DynamicallyAccessedMembers` annotations removed from main assembly
- 14 reflection call sites remain in Jaunty assembly
- ~400 tests failing (need investigation)

---

## Reflection Usage Analysis

### Category 1: Source-Generated Mapper Support (KEEP in Jaunty)
These are acceptable for NativeAOT because they only activate when `IMapped<T>` is implemented:

| File | Method | Reflection Usage | Action |
|------|--------|------------------|--------|
| `Internals/Read/MappedCache.cs:21-24` | `ResolveMapper()` | `GetMethod()`, `CreateDelegate()` for static `ReadEntity` | **KEEP** - Source-gen path |
| `Internals/Read/MappedCache.cs:28-35` | `ResolveMapper()` | `GetMethod()` for instance `ReadEntity` | **KEEP** - Fallback for manual IMapped |
| `Internals/Write/WriteParameterCache.cs:120-121` | `CreateFastBinder()` | `GetMethod()`, `CreateDelegate()` for `BindParameters` | **KEEP** - Source-gen path |

**Rationale:** These only use reflection when the type explicitly implements `IMapped<T>` with a `ReadEntity` method or has a `BindParameters` static method. For source-generated code, the reflection succeeds because the methods exist. For NativeAOT, users can add `DynamicDependency` attributes if needed.

---

### Category 2: Special Type Handling (MOVE to Extensions)
These handle Dictionary, KeyValuePair, ValueTuple, ExpandoObject - all require reflection:

| File | Method | Reflection Usage | Action |
|------|--------|------------------|--------|
| `Internals/Read/DrDispatcher.cs:125` | `CreateKeyValuePairMapper` | `Activator.CreateInstance(typeof(T), key, value)` | **MOVE** |
| `Internals/Read/DrDispatcher.cs:143` | `CreateValueTupleMapper` | `Activator.CreateInstance(typeof(T), values)` | **MOVE** |
| `Internals/Read/DrDispatcher.cs:150` | `GetDefault` | `Activator.CreateInstance(type)` | **MOVE** |
| `Internals/Read/DrDispatcher.cs:190-240` | `CreateDictionaryMapper` | `MakeGenericType`, `GetMethod("Add")`, `Activator.CreateInstance` | **MOVE** |
| `Internals/Read/DrDispatcher.cs:168-185` | `TryResolveSpecialType` | Type checks for Dictionary/KeyValuePair/ValueTuple | **MOVE** |

**Action:** Move entire `TryResolveSpecialType` and related mapper creation methods to `Jaunty.Extensions.Reflection`.

---

### Category 3: Parameter Binding (PARTIAL MOVE)

| File | Method | Reflection Usage | Action |
|------|--------|------------------|--------|
| `Internals/Parameters/ParameterCache.cs:46` | `BuildMetadata` | `GetProperties()` | **PARTIAL** - Keep cache structure, move property inspection |

**Analysis:** `ParameterCache` is used for binding anonymous type parameters (e.g., `new { Id = 1 }`). This is tricky because:
- Anonymous types are compiler-generated, can't use source-gen
- But they're trim-safe (properties exist)

**Solution:** Keep `ParameterCache` but add `DynamicDependency` attribute for trimming.

---

### Category 4: Extension Loading (KEEP with Fallback)

| File | Method | Reflection Usage | Action |
|------|--------|------------------|--------|
| `Jaunty.Init.cs:19` | `TryLoadReflectionExtensions` | `Assembly.GetType()`, `GetMethod()` | **MODIFY** - Make trim-safe with fallback |

**Current Code:**
```csharp
var type = Assembly.Load("Jaunty.Extensions.Reflection").GetType("...");
var method = type?.GetMethod("UseReflectionMapping");
```

**Issue:** Assembly loading fails in NativeAOT if the extension isn't linked.

**Solution:** Wrap in try-catch, provide manual initialization path.

---

## Files to Modify

### 1. Move Special Type Handling to Extensions

**File:** `src/Jaunty/Internals/Read/DrDispatcher.cs`

**Remove:**
- `TryResolveSpecialType<T>()` method
- `CreateKeyValuePairMapper<T>()` method  
- `CreateValueTupleMapper<T>()` method
- `CreateDictionaryMapper<T>()` method
- `GetDefault(Type)` method
- `ConvertValue(object, Type)` method

**Add:**
```csharp
// Fallback hook for special type handling
internal static Func<IDataReader, T>? TryResolveSpecialTypeFromExtensions<T>(IDataReader reader) where T : new()
{
    return JauntyConfig.SpecialTypeMapperResolver?.Invoke(typeof(T), reader) as Func<IDataReader, T>;
}
```

---

### 2. Add New Configuration Hook

**File:** `src/Jaunty/Configuration/JauntyConfig.cs`

**Add:**
```csharp
/// <summary>
/// Optional resolver for special types (Dictionary, KeyValuePair, ValueTuple, ExpandoObject).
/// Provided by Jaunty.Extensions.Reflection for reflection-based special type mapping.
/// </summary>
public static Func<Type, IDataReader, object>? SpecialTypeMapperResolver { get; set; }
```

---

### 3. Update DrDispatcher to Use Extension Hook

**File:** `src/Jaunty/Internals/Read/DrDispatcher.cs`

**Change:**
```csharp
// Old: Direct special type handling
var specialMapper = TryResolveSpecialType<T>(reader);

// New: Extension hook
var specialMapper = TryResolveSpecialTypeFromExtensions<T>(reader);
```

---

### 4. Create Extension Implementation

**File:** `src/Jaunty.Extensions.Reflection/SpecialTypeMappers.cs` (NEW)

```csharp
public static class SpecialTypeMappers
{
    public static void Register()
    {
        JauntyConfig.SpecialTypeMapperResolver = ResolveSpecialTypeMapper;
    }
    
    private static object ResolveSpecialTypeMapper(Type type, IDataReader reader)
    {
        // Dictionary handling
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            return CreateDictionaryMapper(type, reader);
        }
        
        // KeyValuePair handling
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            return CreateKeyValuePairMapper(type, reader);
        }
        
        // ValueTuple handling
        if (type.IsValueType && type.FullName?.StartsWith("System.ValueTuple`") == true)
        {
            return CreateValueTupleMapper(type, reader);
        }
        
        // ExpandoObject handling
        if (type == typeof(object))
        {
            return CreateExpandoMapper(reader);
        }
        
        return null;
    }
    
    // ... implementation of Create*Mapper methods
}
```

---

### 5. Update Test Initializer

**File:** `tests/Jaunty.Tests/Helpers/TestInitializer.cs`

**Add:**
```csharp
[ModuleInitializer]
public static void Initialize()
{
    JauntyReflectionExtensions.UseReflectionMapping();
    SpecialTypeMappers.Register();
}
```

---

## NativeAOT Verification Script

**File:** `scripts/Verify-NativeAOT.ps1` (NEW)

```powershell
#!/usr/bin/env pwsh

# NativeAOT Compatibility Verification Script
# Scans Jaunty assembly for reflection patterns that break NativeAOT

$ErrorPatterns = @(
    'GetMethod\(',
    'GetProperties\(',
    'GetFields\(',
    'Activator\.CreateInstance\(',
    'MakeGenericType\(',
    'RequiresUnreferencedCode',
    'RequiresDynamicCode'
)

$KeepPatterns = @(
    'IMapped<',
    'ReadEntity',
    'BindParameters'
)

Write-Host "=== NativeAOT Compatibility Check ===" -ForegroundColor Cyan

$files = Get-ChildItem -Path "src/Jaunty" -Recurse -Filter "*.cs" | 
         Where-Object { $_.FullName -notmatch 'Extensions' }

$issues = @()

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    
    foreach ($pattern in $ErrorPatterns) {
        $matches = [regex]::Matches($content, $pattern)
        foreach ($match in $matches) {
            # Check if this is in an allowed context
            $lineNum = ($content.Substring(0, $match.Index) -split "`n").Count
            $context = $content.Substring([Math]::Max(0, $match.Index - 100), [Math]::Min(200, $content.Length - $match.Index + 100))
            
            $isAllowed = $false
            foreach ($keepPattern in $KeepPatterns) {
                if ($context -match $keepPattern) {
                    $isAllowed = $true
                    break
                }
            }
            
            if (-not $isAllowed) {
                $issues += [PSCustomObject]@{
                    File = $file.FullName.Replace((Get-Location).Path, '')
                    Line = $lineNum
                    Pattern = $pattern
                    Context = $context.Trim() -replace "`r?`n", ' '
                }
            }
        }
    }
}

if ($issues.Count -eq 0) {
    Write-Host " No NativeAOT issues found!" -ForegroundColor Green
} else {
    Write-Host " Found $($issues.Count) potential NativeAOT issues:" -ForegroundColor Red
    $issues | Format-Table -AutoSize | Out-String | Write-Host
}

exit $issues.Count
```

---

## Migration Checklist

### Phase 1: Move Special Type Handling (Week 1)
- [ ] Create `SpecialTypeMappers.cs` in Extensions assembly
- [ ] Move `TryResolveSpecialType` and related methods
- [ ] Add `SpecialTypeMapperResolver` to `JauntyConfig`
- [ ] Update `DrDispatcher` to use extension hook
- [ ] Update test initializer
- [ ] Run verification script - should show 0 issues for special types
- [ ] Run tests - verify no new failures

### Phase 2: Handle Parameter Cache (Week 2)
- [ ] Add `DynamicDependency` attribute to `ParameterCache`
- [ ] Document that anonymous types require trimming preservation
- [ ] Add NativeAOT documentation for users
- [ ] Run verification script
- [ ] Run tests

### Phase 3: Extension Loading (Week 2)
- [ ] Update `Jaunty.Init.cs` with try-catch for assembly loading
- [ ] Add manual initialization documentation
- [ ] Create NativeAOT sample project
- [ ] Run verification script
- [ ] Run tests

### Phase 4: Testing & Documentation (Week 3)
- [ ] Create NativeAOT test project
- [ ] Publish NativeAOT executable
- [ ] Verify all functionality works
- [ ] Update README with NativeAOT instructions
- [ ] Create migration guide for users

---

## Expected Outcome

After migration:
- **Jaunty.dll**: 100% NativeAOT compatible (no reflection except source-gen paths)
- **Jaunty.Extensions.Reflection.dll**: Contains all reflection-based code, optional for NativeAOT
- **Verification script**: Shows 0 issues
- **Tests**: 100% passing (excluding MultiEntity/Bulk which are separate work)

---

## Notes

1. **Source-Generated Mappers**: The `IMapped<T>` pattern with `ReadEntity` methods is NativeAOT-safe because the source generator creates the methods at compile time.

2. **Anonymous Types**: Parameter binding for anonymous types (`new { Id = 1 }`) uses reflection but is trim-safe because the properties exist. Users need to add `DynamicDependency` if trimming removes them.

3. **Dictionary/KeyValuePair/ValueTuple**: These are convenience features that require reflection. Moving them to Extensions allows NativeAOT users to opt-in or use alternatives.

4. **Testing Strategy**: Run verification script after each change to ensure we're making progress.
