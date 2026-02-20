# Jaunty NativeAOT Compatibility Plan

## Current Status
Jaunty successfully builds as a NativeAOT application (verified via `Jaunty.Scaffolding.Cli`), but it produces **40+ trim/AOT warnings**. These warnings indicate that several core features will fail at runtime because the AOT compiler cannot statically analyze the dynamic code paths.

### Primary Blockers
1.  **Runtime Reflection**: `MetadataBuilder` and `AttributeHelper` use reflection to scan types at runtime. AOT may trim the properties or attributes needed.
2.  **Dynamic Code Generation**: `WriteParameterCache<T>` uses `Expression.Compile()`, which is not supported in AOT (it falls back to a slow interpreter or fails).
3.  **Generic Instantiation**: `MetadataCache` uses `MakeGenericMethod`, which prevents the AOT compiler from pre-generating the necessary machine code for specific entity types.
4.  **Provider-Specific Hacks**: `BulkInsertAsync` uses reflection to access internal provider types (like `SqlBulkCopy`), which is extremely brittle in AOT/Trimmed environments.

---

## Roadmap to 100% Compatibility

### Phase 1: Annotation & Mitigation (Short Term)
Goal: Fix the most obvious warnings using AOT-friendly attributes.

- [ ] **Annotate Type Parameters**: Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]` to all generic type parameters `T` where reflection is used.
- [ ] **Mark Dangerous APIs**: Annotate reflection-heavy methods with `[RequiresUnreferencedCode]` to inform users of the risks.
- [ ] **Safe Provider Access**: Replace `BulkInsert` reflection-based provider detection with explicit provider-specific packages or safe type-checking.

### Phase 2: Source Generators (Long Term - Recommended)
Goal: Eliminate runtime reflection entirely by moving metadata resolution to compile-time.

- [ ] **Implement `Jaunty.SourceGenerator`**:
    - [ ] **Metadata Generation**: Generate `EntityMetadata` at compile-time for classes marked with `[Table]`.
    - [ ] **Static Mappers**: Generate static `Read(DbDataReader)` and `Bind(DbCommand, T)` methods for each entity type, replacing expression trees.
    - [ ] **SQL Pre-building**: Generate CRUD SQL strings during compilation.
- [ ] **AOT-Safe Dispatcher**: Update `DrDispatcher` to use generated mappers instead of resolving them at runtime.

### Phase 3: AOT-First Architecture
Goal: Ensure 0 warnings and verified runtime stability.

- [ ] **Enable `IsAotCompatible`**: Set the property in all project files once warnings are resolved.
- [ ] **Automated AOT Testing**: Integrate the `build-aot.ps1` script into the CI pipeline to prevent regressions.
- [ ] **Zero-Allocation Hot Path**: Leverage the Source Generator to achieve a truly zero-allocation mapping path.

---

## Action Items for Next Session
1.  Begin annotating `MetadataCache<T>` and `MetadataBuilder` with `DynamicallyAccessedMembers`.
2.  Investigate `System.Runtime.CompilerServices.InterceptsLocation` (Interceptors) for high-performance AOT-safe command dispatching.
3.  Audit `BulkInsertAsync.cs` to remove unsafe `Assembly.GetType` calls.
