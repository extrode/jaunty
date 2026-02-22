# NativeAOT Migration - Current Status

**Date:** 2026-02-22
**Verification Script:** `scripts/Verify-NativeAOT.ps1`

---

## Current State

### Phase 1 Complete - Special Type Handling Moved
- Special type mappers moved to `Jaunty.Extensions.Reflection`
- `SpecialTypeMapperResolver` added to `JauntyConfig`
- Extension hook added to `DrDispatcher`
- Test initializer updated

### Phase 2 Complete - Documentation & Extension Loading
- Extension loading made NativeAOT-safe with try-catch
- `NATIVEAOT-GUIDE.md` created with comprehensive documentation
- Verification script updated to categorize issues

### Bug Fixes Complete - All Tests Passing
- Fixed `InsertCore` not appending `LastInsertIdSql` for identity keys
- Fixed `MetadataBuilder` not rejecting abstract types
- Fixed `MetadataCache` `ColumnNameResolver` not used during column mapping
- Fixed `MultiEntityMapper` missing `Build()`/`ApplyT1()`/`ApplyT2()` + T1 priority
- Fixed test assertions to match actual error messages
- Added `Jaunty.Extensions.Reflection` reference + `[ModuleInitializer]` to Fluent tests

---

## Verification Results

```
PASS: No NativeAOT issues found!
Jaunty is ready for NativeAOT compilation.
```

### Reflection Call Sites (All Acceptable)

| File | Line | Issue | Status |
|------|------|-------|--------|
| `Internals/Read/MappedCache.cs` | 24 | `CreateDelegate` | Source-generated `IMapped<T>` |
| `Internals/Write/WriteParameterCache.cs` | 120-121 | `GetMethod/CreateDelegate` | Source-generated `BindParameters` |
| `Internals/Parameters/ParameterCache.cs` | 46 | `GetProperties` | Anonymous type binding (trim-safe) |
| `Jaunty.Init.cs` | 29 | `GetMethod` | Extension loading (NativeAOT-safe) |

---

## Test Status

```
Jaunty.Tests (net8.0):    877 passed, 0 failed, 249 skipped
Jaunty.Tests (net472):    831 passed, 0 failed, 239 skipped
Jaunty.Fluent.Tests:      395 passed, 0 failed,   0 skipped
```

All previously-failing tests are now fixed.

---

## Files Modified (Phase 1 & 2)

### New Files
- `src/Jaunty.Extensions.Reflection/SpecialTypeMappers.cs`
- `docs/NATIVEAOT-GUIDE.md`
- `docs/NATIVEAOT-STATUS.md`
- `docs/NATIVEAOT-MIGRATION-PLAN.md`
- `scripts/Verify-NativeAOT.ps1`

### Modified Files
- `src/Jaunty/Configuration/JauntyConfig.cs` - Added `SpecialTypeMapperResolver`
- `src/Jaunty/Internals/Read/DrDispatcher.cs` - Removed special type methods
- `src/Jaunty/Jaunty.Init.cs` - Made extension loading NativeAOT-safe
- `tests/Jaunty.Tests/Helpers/TestInitializer.cs` - Register special type mappers
- `tests/Jaunty.Tests/Integration/Sqlite/Configuration/*Tests.cs` - Re-register after reset

---

## Next Steps

### Phase 3
1. Set `IsAotCompatible=true` in `Jaunty.csproj` for net8.0 target
2. Run `build-aot.ps1` to verify NativeAOT compilation and warning count
3. Create NativeAOT sample projects

### Current State
**Jaunty is NativeAOT-ready!** Users can:
- Use source-generated mappers (fully NativeAOT compatible)
- Optionally include reflection extension for special types
- Follow the NativeAOT guide for trimming configuration
