# Jaunty NativeAOT Compatibility Plan

## Current Status (Updated 2026-02-22)

Jaunty is **NativeAOT-ready**. The verification script (`scripts/Verify-NativeAOT.ps1`) reports **PASS** with zero issues. All reflection-based code has been moved to `Jaunty.Extensions.Reflection` (opt-in) and the core library uses source-generated mappers exclusively.

### Original Blockers (All Resolved)
1.  ~~**Runtime Reflection**: `MetadataBuilder` and `AttributeHelper` use reflection~~ → Moved to `Jaunty.Extensions.Reflection`
2.  ~~**Dynamic Code Generation**: `WriteParameterCache<T>` uses `Expression.Compile()`~~ → Replaced with source-generated `BindInsert`/`BindUpdate`/`BindDelete`
3.  ~~**Generic Instantiation**: `MetadataCache` uses `MakeGenericMethod`~~ → Moved to `Jaunty.Extensions.Reflection`
4.  ~~**Provider-Specific Hacks**: `BulkInsertAsync` uses reflection~~ → Isolated in extension assembly

---

## Roadmap to 100% Compatibility

### Phase 1: Annotation & Mitigation COMPLETE
Goal: Fix the most obvious warnings using AOT-friendly attributes.

- [x] **Annotate Type Parameters**: `[DynamicallyAccessedMembers]` added to all generic parameters in `Jaunty.Extensions.Reflection`
- [x] **Mark Dangerous APIs**: `[RequiresUnreferencedCode]` on `UseReflectionMapping()`
- [x] **Safe Provider Access**: Special type handling moved to `Jaunty.Extensions.Reflection`

### Phase 2: Source Generators COMPLETE
Goal: Eliminate runtime reflection entirely by moving metadata resolution to compile-time.

- [x] **Implement `Jaunty.SourceGenerator`**:
    - [x] **Metadata Generation**: Generates `EntityMetadata` at compile-time for `[Table]` classes
    - [x] **Static Mappers**: Generates `ReadEntity()` and `BindInsert/Update/Delete()` methods
    - [x] **SQL Pre-building**: CRUD SQL cached via `CrudSqlCache` at runtime (dialect-dependent)
- [x] **AOT-Safe Dispatcher**: `DrDispatcher` uses `IMapped<T>` source-generated mappers; `MappedCache` resolves them

### Phase 3: AOT-First Architecture (In Progress)
Goal: Ensure 0 warnings and verified runtime stability.

- [ ] **Enable `IsAotCompatible`**: Set the property in `Jaunty.csproj` for net8.0 target
- [ ] **Automated AOT Testing**: Integrate `build-aot.ps1` into CI pipeline
- [ ] **Zero-Allocation Hot Path**: Leverage the Source Generator to achieve a truly zero-allocation mapping path

---

## Action Items
1.  Set `IsAotCompatible=true` in `Jaunty.csproj` (net8.0 target) and verify zero AOT warnings
2.  Run `build-aot.ps1` end-to-end and document binary size
3.  Create NativeAOT sample projects (`samples/NativeAOT-Basic`, etc.)
4.  Integrate `build-aot.ps1` into CI pipeline
