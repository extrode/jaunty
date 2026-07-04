# Jaunty Production Readiness Report (2026-03-03)

## Scope
- Reviewed core library (`src/Jaunty`), extension packages, tests, benchmark artifacts, and documentation.
- Compared API/functionality positioning with Dapper, EF Core, and RepoDb.
- Validation type: source/document review. Local `dotnet build/test` was not fully executable in this environment due package restore network limits.

## Functionality Coverage

| Area | Status | Notes |
|---|---|---|
| Query APIs | Complete | Strict + partial, first/single/default, scalar, streaming, async |
| Multiple result sets | Complete | `QueryMultiple` + `GridReader` |
| Stored procedures | Complete | Includes output parameter flow |
| CRUD | Complete | Insert/update/delete + id variants |
| Bulk operations | Complete | Bulk insert/update/delete, native bulk copy integration |
| Upsert | Complete | Dialect-aware |
| Parameter binding | Strong | Named + positional + collection expansion |
| NativeAOT/source generation | Implemented | Generator path exists, requires hardening for shape-safe ordinal cache |
| Fluent query builder | Broad, not fully uniform | Some multi-join/async consistency gaps |
| Scaffolding | Broad | Navigation property generation still missing |

## ORM Comparison Snapshot

| Capability | Jaunty | Dapper | EF Core | RepoDb |
|---|---|---|---|---|
| Micro-ORM performance focus | Strong | Strong | Medium | Strong |
| Strict mapping by default | Yes (differentiator) | No | N/A | Partial |
| Built-in CRUD + bulk + upsert | Yes | Mostly external/manual | Yes | Yes |
| Fluent SQL builder package | Yes | No (external libs) | LINQ model | Limited |
| AOT-oriented mapping strategy | Yes | Limited | Improving | Limited |
| Change tracking/unit of work | No | No | Yes | Limited |

## Key Findings

### P0
1. Source-generated mapper ordinal cache is vulnerable to stale mappings when result shape changes.

### P1
2. Async API uses `DbConnection` while sync API uses `IDbConnection`.
3. Core assembly still contains reflection paths that weaken AOT guarantees.
4. Sync/async query allocation behavior is inconsistent (`ExpectedRowCount` parity gap).
5. Benchmark narratives and artifacts need normalization before external publication.
6. Documentation has stale/conflicting status reports in multiple areas.

### P2
7. Fluent 3-way join parity and CTE async method gaps.
8. Scaffolding lacks generated navigation properties.
9. Missing first-class interception/observability/retry abstractions.

## Production Readiness Verdict

Jaunty is close to production-ready for controlled workloads and experienced teams, but requires a short hardening phase before broad adoption claims:
- Correctness hardening for generated mapping.
- API consistency cleanup.
- Documentation and benchmark trustworthiness cleanup.
- CI-quality proof points for cross-provider behavior.

## Next Document
- Execution plan: [../tasklists/production-readiness-tasklist.md](../tasklists/production-readiness-tasklist.md)
