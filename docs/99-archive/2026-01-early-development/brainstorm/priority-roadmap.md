# Jaunty Priority Roadmap

## Overview

This document provides a prioritized analysis of features Jaunty should implement, ordered by impact on competitiveness and developer experience.

---

## Tier 1: Critical (Blocking Competitiveness)

| Priority | Feature | Explanation |
|----------|---------|-------------|
| **1** | **Collection Parameter Expansion** | `WHERE id IN @ids` is used in almost every real application. Without it, users must manually build SQL strings, defeating the purpose of parameterized queries. Dapper has this. This is the #1 missing feature that makes Jaunty feel incomplete. |
| **2** | **Dynamic Object Support** | `Query<dynamic>` enables ad-hoc queries without creating DTOs. Essential for exploratory queries, admin dashboards, and reporting. Every competitor has this. |
| **3** | **Dictionary Support** | `Query<Dictionary<string, object>>` is the strongly-typed alternative to dynamic. Critical for scenarios where column names are runtime-determined. |

**Why Tier 1 is critical:** These three features are table-stakes for any data access library. Their absence forces developers to work around Jaunty rather than with it.

---

## Tier 2: High (Significant Developer Experience)

| Priority | Feature | Explanation |
|----------|---------|-------------|
| **4** | **Database Scaffolding Tool** | Generate entity classes from existing databases. Zero-to-productive in minutes. Differentiating feature vs Dapper (which has no scaffolder). |
| **5** | **Multi-Mapping** | `Query<Order, Customer, OrderWithCustomer>()` with `splitOn` parameter. Essential for JOIN queries that return related entities. Dapper's killer feature. |
| **6** | **Bulk Operations** | `InsertRange`, `UpdateRange`, `DeleteRange`. Basic CRUD is done, but inserting 1000 records one-by-one is unacceptable. Batch operations are expected. |

**Why Tier 2 matters:** These features determine whether Jaunty can handle real-world complexity. Without them, it's a toy for simple cases only.

---

## Tier 3: Medium (Completeness)

| Priority | Feature | Explanation |
|----------|---------|-------------|
| **7** | **TVP Support** | Table-Valued Parameters for SQL Server. The proper way to pass collections for bulk operations. High value for SQL Server users specifically. |
| **8** | **Upsert Operations** | `Upsert<T>()` / `InsertOrUpdate<T>()`. Common pattern that currently requires manual SQL. |
| **9** | **Advanced Parameter Binding** | Nested object parameters, complex type handling, automatic type inference. Current binding is basic. |
| **10** | **Execute Methods** | `Execute(sql)` for non-query statements (DDL, stored procs with no return). Currently missing explicit non-query execution. |

---

## Tier 4: Enhancement (Nice-to-Have)

| Priority | Feature | Explanation |
|----------|---------|-------------|
| **11** | **Connection Resilience** | Automatic retry for transient failures. Important for cloud databases (Azure SQL, etc.). |
| **12** | **Query Interceptors/Hooks** | Before/after query execution hooks for logging, metrics, caching. |
| **13** | **Read-Only Mode** | Configuration to prevent accidental writes. Safety feature for reporting connections. |
| **14** | **Custom Type Handlers** | Register custom converters for types like `JsonDocument`, spatial types, etc. |

---

## Tier 5: Deliberate Non-Goals

These are features Jaunty should **not** implement to maintain its identity:

| Feature | Reason to Skip |
|---------|----------------|
| **LINQ Support** | Jaunty's philosophy is "your SQL, your control." LINQ translation adds complexity and magic. |
| **Change Tracking** | Belongs in full ORMs like EF. Jaunty is a micro-ORM. |
| **Migrations** | Database schema management is a separate concern. |
| **Lazy Loading** | Leads to N+1 problems. Explicit is better. |
| **Caching** | Application-level concern, not ORM concern. |

---

## Immediate Recommendations

Based on effort-to-impact ratio:

### Quick Wins (Do First)
1. **Collection Parameter Expansion** - Moderate effort, massive impact
2. **Dynamic Support** - Moderate effort, high impact
3. **Dictionary Support** - Low effort, high impact (similar pattern to dynamic)

### Strategic Investment
4. **Database Scaffolding** - Higher effort, but differentiating feature that no Dapper user has today

### Deferred
5. **Multi-Mapping** - Complex implementation, can live without initially
6. **Bulk Operations** - Users can work around with loops for now

---

## Summary

```
Must Do Now:     Collection Expansion → Dynamic → Dictionary
Should Do Next:  Scaffolding → Multi-Mapping → Bulk Ops
Could Do Later:  TVP → Upsert → Advanced Binding
Skip Entirely:   LINQ, Change Tracking, Migrations, Caching
```

The first three items (Collection Expansion, Dynamic, Dictionary) would take Jaunty from "interesting experiment" to "viable Dapper alternative." The scaffolding tool would make it stand out. Everything else is incremental improvement.

---

## Related Documents

- [DatabaseScaffoldingAnalysis.md](./DatabaseScaffoldingAnalysis.md) - Scaffolding tool analysis
- [ScaffoldPhase1Plan.md](./ScaffoldPhase1Plan.md) - Scaffolding implementation plan
- [jaunty-api-ranking.md](../jaunty-api-ranking.md) - Detailed API ranking
- [jaunty-missing-apis.md](../jaunty-missing-apis.md) - Missing APIs analysis
