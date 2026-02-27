# Jaunty NativeAOT Compatibility Plan

## Current Status (Updated 2026-02-27)

Jaunty is **NativeAOT-ready**. The verification script (`scripts/Verify-NativeAOT.ps1`) reports **PASS** with zero issues. All reflection-based code has been moved to `Jaunty.Extensions.Reflection` (opt-in) and the core library uses source-generated mappers exclusively.

**Build Configuration Verified:**
- `IsAotCompatible=true` set in `Jaunty.csproj` (net8.0) and `Directory.Build.props`
- `IsTrimmable=true` set globally via `Directory.Build.props`
- Source generator (`Jaunty.SourceGenerator`) integrated as analyzer
- 4 acceptable reflection sites in core, all suppressed with `[UnconditionalSuppressMessage]`

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

- [x] **Enable `IsAotCompatible`**: Set in `Jaunty.csproj` (net8.0) and `Directory.Build.props`
- [x] **Automated AOT Testing**: GitHub Actions CI pipeline (`.github/workflows/ci.yml`) with `verify-aot` and `aot-publish` jobs
- [ ] **Zero-Allocation Hot Path**: Leverage the Source Generator to achieve a truly zero-allocation mapping path

---

## Remaining Action Items (Priority Order)

### P0 - Critical COMPLETE
1.  ~~Create CI/CD pipeline (`.github/workflows`) with AOT verification step (`Verify-NativeAOT.ps1`)~~
2.  ~~Add NativeAOT publish step to CI (`build-aot.ps1`)~~

### P1 - High COMPLETE
3.  ~~Run `build-aot.ps1` end-to-end and document binary size and warning count~~ (zero Jaunty warnings)
4.  ~~Create NativeAOT sample projects~~ (`samples/NativeAOT-Basic`, `NativeAOT-WithReflection`, `NativeAOT-CustomMapper`)
5.  ~~Investigate 224 failing tests~~ (not AOT-related - require unconfigured database servers)

### P2 - Medium
6.  Zero-allocation hot path via source generator for value type mapping
7.  Add `PublishAot=true` natively in `Jaunty.Scaffolding.Cli.csproj`

### Completed
- ~~Set `IsAotCompatible=true` in `Jaunty.csproj` (net8.0 target) and verify zero AOT warnings~~
