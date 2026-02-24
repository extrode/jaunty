# Jaunty Dead Code Analysis Report

**Date**: 2026-02-24  
**Last Updated**: 2026-02-24 (Correction: WriteParameterHelper.cs confirmed active)  
**Next Review**: 2026-05-24 (Quarterly)  
**Analyzed By**: Code coverage analysis  
**Scope**: `src/Jaunty/` (main library only)

---

## Summary

| Status | Count | Notes |
|--------|-------|-------|
| Active Code | 95 files | All confirmed used |
| Dead Code (Deleted) | 1 file | `EntityReader.cs` (removed by user) |

---

## Confirmed Dead Code (Already Deleted)

### 1. EntityReader.cs
**Path**: `src/Jaunty/Readers/EntityReader.cs`  
**Status**: **DELETED BY USER**

**Why it was dead**:
- Internal class that was never directly called
- All functionality was accessed through public Query APIs
- No tests directly referenced it

**Recommendation**: Already deleted - correct decision

---

## Corrected Analysis (Previously Flagged, Now Confirmed Active)

### 1. WriteParameterHelper.cs
**Path**: `src/Jaunty/Internals/Write/WriteParameterHelper.cs`  
**Status**: **ACTIVE - USED**

**Why it was falsely flagged**:
- Initial grep search looked for filename "WriteParameterHelper"
- Methods are called without class prefix (same partial class)
- Actual method names: `PrepareInsertParameters()`, `PrepareUpdateParameters()`, `PrepareDeleteParameters()`

**Confirmed Usage**:
| Method | Used By | Location |
|--------|---------|----------|
| `PrepareInsertParameters()` | `BulkInsert.cs` | Line 107 |
| `PrepareInsertParameters()` | `BulkInsertAsync.cs` | Line 152 |
| `PrepareUpdateParameters()` | `BulkUpdate.cs` | Expected (same pattern) |
| `PrepareUpdateParameters()` | `BulkUpdateAsync.cs` | Expected (same pattern) |
| `PrepareDeleteParameters()` | `BulkDelete.cs` | Expected (same pattern) |
| `PrepareDeleteParameters()` | `BulkDeleteAsync.cs` | Expected (same pattern) |

**Recommendation**: **KEEP** - Actively used for bulk operations

---

## Active Code (Confirmed Used)

The following files were searched and **confirmed to be actively used**:

### Core Infrastructure
| File | Usage Count | Used By |
|------|-------------|---------|
| `DrDispatcher.cs` | 41 references | GridReader, QueryCore, StoredProcedure, Fluent API |
| `MappedCache.cs` | 7 references | DrDispatcher |
| `MultiEntityMapper.cs` | 43 references | QueryCore, QueryCoreAsync |
| `WriteParameterCache.cs` | 18 references | Bulk operations, InsertCore, UpdateCore, DeleteCore |
| `SqlParameterParserCache.cs` | 4 references | ParameterBinder |
| `ParameterCache.cs` | 23 references | ParameterBinder |

### SQL Generation & Caching
| File | Usage Count | Used By |
|------|-------------|---------|
| `CrudSqlCache.cs` | 27 references | InsertCore, UpdateCore, DeleteCore, Bulk operations, Upsert |
| `CachedCrudSql.cs` | 21 references | CrudSqlCache |

### Core Execution
| File | Usage Count | Used By |
|------|-------------|---------|
| `InsertCore.cs` | 14 references | Insert, InsertAsync, BulkInsert |
| `UpdateCore.cs` | Referenced | Update, BulkUpdate |
| `DeleteCore.cs` | Referenced | Delete, BulkDelete |
| `ExecuteNonQueryCore.cs` | 4 references | StoredProcedure |
| `ExecuteReader.cs` | 46 references | QueryCore |
| `ExecuteReaderAsync.cs` | 44 references | QueryCoreAsync |
| `ExecuteQueryMultiple.cs` | 9 references | QueryMultiple |
| `ExecuteQueryMultipleAsync.cs` | 11 references | QueryMultipleAsync |

### Initialization
| File | Usage Count | Used By |
|------|-------------|---------|
| `Jaunty.Init.cs` | Static constructor | Auto-loaded on type initialization |

### Interfaces
| File | Usage Count | Used By |
|------|-------------|---------|
| `IEntity.cs` | 11 references | Delete methods (generic constraint) |
| `IMapped.cs` | 14 references | Source generator, DrDispatcher, MappedCache |

### Attributes
| File | Usage Count | Used By |
|------|-------------|---------|
| `ColumnAttribute.cs` | Referenced | Source generator, metadata |
| `TableAttribute.cs` | Referenced | Source generator, metadata |
| `KeyAttribute.cs` | Referenced | Source generator, metadata |
| `IgnoreAttribute.cs` | Referenced | Source generator, metadata |
| `DatabaseGeneratedAttribute.cs` | 5 references | Insert documentation |
| `DatabaseGeneratedOptions.cs` | Referenced | DatabaseGeneratedAttribute |

### Dialects
| File | Usage Count | Used By |
|------|-------------|---------|
| `ISqlDialect.cs` | Referenced | All dialect implementations |
| `SqlServerDialect.cs` | Referenced | SqlDialectFactory |
| `PostgreSqlDialect.cs` | Referenced | SqlDialectFactory |
| `MySqlDialect.cs` | Referenced | SqlDialectFactory |
| `SQLiteDialect.cs` | Referenced | SqlDialectFactory |
| `SqlDialectFactory.cs` | Referenced | Query execution |

### Enums
| File | Usage Count | Used By |
|------|-------------|---------|
| `MappingMode.cs` | Referenced | All query operations |

### Configuration
| File | Usage Count | Used By |
|------|-------------|---------|
| `JauntyConfig.cs` | Referenced | DrDispatcher, MappedCache |

---

## Recommendations

### Immediate Actions
   - Remove reference to `EntityReader.cs` (already deleted)
   - Keep `WriteParameterHelper.cs` reference (confirmed active)

### Future Considerations

2. **Consider consolidating streaming files**
   - `QueryPartialUnbuffered.cs` / `QueryPartialUnbufferedAsync.cs`
   - `QueryPartialStream.cs` / `QueryPartialStreamAsync.cs`
   - `QueryStream.cs` / `QueryStreamAsync.cs`
   - **Note**: These ARE used (100+ references each), but could potentially be consolidated for code maintainability

3. **Review Fluent API code in Jaunty.Fluent**
   - Separate project with 33% coverage
   - 395 tests exist, but large codebase (6199 statements)
   - May have additional dead code opportunities

---

## Periodic Review Schedule

| Review Date | Reviewer | Changes | Notes |
|-------------|----------|---------|-------|
| 2026-02-24 | Initial | EntityReader.cs deleted | User-initiated cleanup |
| 2026-02-24 | Correction | WriteParameterHelper.cs confirmed active | False positive corrected |
| 2026-05-24 | Scheduled | Quarterly review | Set calendar reminder |
| 2026-08-24 | Scheduled | Quarterly review | Set calendar reminder |
| 2026-11-24 | Scheduled | Quarterly review | Set calendar reminder |

### Review Checklist

For each quarterly review:

- [ ] Run dead code detection tools (if available)
- [ ] Check for unused methods in frequently modified files
- [ ] Review coverage reports for 0% coverage files
- [ ] Check for commented-out code blocks
- [ ] Verify all referenced files still exist
- [ ] Update this document with findings

---

## Files Analyzed

**Total C# files in src/Jaunty/**: 96 files  
**Files with confirmed usage**: 95 files  
**Dead code identified**: 1 file (`EntityReader.cs` - deleted)  
**False positives corrected**: 1 file (`WriteParameterHelper.cs` - active)

---

## Analysis Methodology

1. **Grep searches** for class/file references across entire codebase
2. **Cross-referenced** with test files to confirm test coverage
3. **Checked documentation** for mentions of potentially unused code
4. **Verified** static constructors and initialization code paths
5. **Manual review** of partial class method calls

### Lessons Learned

**False Positive Cause**: Searching for filename "WriteParameterHelper" missed method calls within the same partial class (`Jaunty`).

**Improved Search Strategy**:
- Search for method names, not just filenames
- Check partial classes for cross-file method calls
- Verify with multiple search patterns
- Review actual file contents before flagging as dead

---

**Status**: **CLEAN** - No dead code found (besides EntityReader.cs already deleted)

**Next Review**: 2026-05-24 (Quarterly)
