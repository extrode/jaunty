# NativeAOT Migration - Current Status

**Date:** 2026-02-20
**Verification Script:** `scripts/Verify-NativeAOT.ps1`

---

## Current State

### Completed
1. Reflection-based mapping moved to `Jaunty.Extensions.Reflection`
2. `DynamicallyAccessedMembers` annotations removed from Jaunty assembly
3. Verification script created and working
4. Core query operations (Query, QueryPartial, QueryFirst, etc.) working
5. 875 out of 877 non-excluded tests passing

### Remaining Work (12 reflection call sites)

```
File: src\Jaunty\Jaunty.Init.cs
   Line 19: GetMethod - Extension loading

File: src\Jaunty\Internals\Parameters\ParameterCache.cs
   Line 46: GetProperties - Anonymous type parameter binding

File: src\Jaunty\Internals\Read\DrDispatcher.cs
   Line 125, 143, 150, 220, 230: Activator.CreateInstance - Special types
   Line 214: MakeGenericType - Dictionary mapping
   Line 215: GetMethod - Dictionary mapping

File: src\Jaunty\Internals\Read\MappedCache.cs
   Line 24: CreateDelegate - Source-generated mapper (ACCEPTABLE)

File: src\Jaunty\Internals\Write\WriteParameterCache.cs
   Line 120, 121: GetMethod, CreateDelegate - Source-generated binder (ACCEPTABLE)
```

### Acceptable (4 call sites - source-generated paths)
- `MappedCache.cs:24` - `CreateDelegate` for `IMapped<T>.ReadEntity`
- `WriteParameterCache.cs:120-121` - `GetMethod/CreateDelegate` for `BindParameters`

These are NativeAOT-safe because they're used by source-generated code.

### To Move (8 call sites - special type handling)
All in `DrDispatcher.cs`:
- `CreateKeyValuePairMapper` - Line 125
- `CreateValueTupleMapper` - Line 143
- `GetDefault` - Line 150
- `CreateDictionaryMapper` - Lines 214, 215, 220, 230

### To Document (2 call sites)
- `ParameterCache.cs:46` - Anonymous type binding (trim-safe with documentation)
- `Jaunty.Init.cs:19` - Extension loading (needs try-catch for NativeAOT)

---

## Next Steps (In Order)

### Step 1: Move Special Type Handling
**Files to modify:**
- `src/Jaunty/Internals/Read/DrDispatcher.cs` - Remove special type methods
- `src/Jaunty/Configuration/JauntyConfig.cs` - Add `SpecialTypeMapperResolver`
- `src/Jaunty.Extensions.Reflection/SpecialTypeMappers.cs` - Create new file

**Expected outcome:** 6 fewer reflection call sites in Jaunty

### Step 2: Fix Extension Loading
**Files to modify:**
- `src/Jaunty/Jaunty.Init.cs` - Add try-catch for assembly loading

**Expected outcome:** NativeAOT-safe extension loading

### Step 3: Add Documentation
**Files to create:**
- `docs/NATIVEAOT-GUIDE.md` - User guide for NativeAOT usage

**Expected outcome:** Users know how to use Jaunty with NativeAOT

### Step 4: Verify and Test
**Commands:**
```powershell
# Run verification script
./scripts/Verify-NativeAOT.ps1

# Expected output: 6 issues (all acceptable - source-generated paths)

# Run tests
dotnet test --framework net8.0

# Expected: All tests passing
```

---

## Test Status

```
Passed:  875
Failed:    2 (test isolation issues - ConfigResolverTests, EdgeCaseTests)
Skipped: 249 (MultiEntity, Bulk operations - set aside)
Total:  1126
```

The 2 failing tests are:
1. `ConfigResolverTests.Query_WithSnakeCaseColumnResolver_MapsCorrectly` - Test isolation
2. `EdgeCaseTests.DeleteById_WithCompositePrimaryKey_ThrowsInformativeException` - Unimplemented feature

---

## Verification Script Usage

```powershell
# Basic check
./scripts/Verify-NativeAOT.ps1

# With verbose output (shows acceptable reflection too)
./scripts/Verify-NativeAOT.ps1 -Verbose

# With fix suggestions (shows code context)
./scripts/Verify-NativeAOT.ps1 -FixSuggestions
```

---

## Target State

After migration:
- **Jaunty.dll**: 6 reflection call sites (all source-generated - NativeAOT safe)
- **Jaunty.Extensions.Reflection.dll**: Contains all special type handling
- **Verification script**: Shows 0 issues (or 6 with -Verbose for acceptable)
- **Tests**: 100% passing
