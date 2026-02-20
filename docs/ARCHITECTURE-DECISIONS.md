# Architecture Decision Records (ADRs)

This directory contains Architecture Decision Records (ADRs) for Jaunty. Each ADR documents a significant architectural decision, its context, and consequences.

## What is an ADR?

An ADR is a document that captures:
- A significant architectural decision
- The context and problem being solved
- Options considered
- The decision made and rationale
- Consequences (both positive and negative)

## ADR Template

Copy this template for new ADRs:

```markdown
# ADR-NNN: [Title]

**Status:** [Proposed | Accepted | Deprecated | Superseded]  
**Date:** YYYY-MM-DD  
**Author:** [Name]

## Context

What is the issue that we're seeing that is motivating this decision or change?

## Problem Statement

What problem are we trying to solve?

## Options Considered

### Option 1: [Name]

**Description:** Brief description

**Pros:**
- Pro 1
- Pro 2

**Cons:**
- Con 1
- Con 2

### Option 2: [Name]

[Same format as Option 1]

## Decision

What is the change that we're proposing and/or doing?

## Rationale

Why did we choose this option over the others?

## Consequences

### Positive
- What becomes easier or better?
- What improvements do we expect?

### Negative
- What becomes harder or worse?
- What trade-offs are we making?

### Neutral
- What changes but has no significant impact?

## References

- Links to related issues, PRs, or documentation
- Links to external resources

## Status History

- YYYY-MM-DD: Proposed
- YYYY-MM-DD: Accepted
```

## ADR Index

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| ADR-001 | Use SQLite for Integration Testing | Accepted | 2025-01-15 |
| ADR-002 | Support Multiple Database Dialects | Accepted | 2025-01-20 |
| ADR-003 | Strict vs Partial Mapping Modes | Accepted | 2025-02-01 |
| ADR-004 | Compiled Delegates for Parameter Binding | Accepted | 2025-02-10 |
| ADR-005 | Return Type Design: long vs int | Accepted | 2025-02-15 |

---

## ADR-001: Use SQLite for Integration Testing

**Status:** Accepted  
**Date:** 2025-01-15  
**Author:** Jaunty Team

### Context

Integration tests require a real database to validate SQL generation and execution. We need a lightweight, portable solution that works across all development environments.

### Problem Statement

How do we run integration tests reliably across different development environments without requiring external database servers?

### Options Considered

#### Option 1: SQLite In-Memory

**Description:** Use SQLite in-memory database for all integration tests

**Pros:**
- No external dependencies
- Fast execution
- Portable across platforms
- Easy to set up and tear down

**Cons:**
- SQLite dialect differences from SQL Server/PostgreSQL
- Some SQL features not available

#### Option 2: TestContainers

**Description:** Use Docker containers for real database instances

**Pros:**
- Tests against real database engines
- Can test database-specific features

**Cons:**
- Requires Docker
- Slower test execution
- More complex setup

### Decision

Use SQLite in-memory database for integration tests.

### Rationale

SQLite provides the best balance of speed, portability, and simplicity for our use case. Most of our SQL generation is standard SQL that works across databases. Database-specific features are tested through dialect-specific unit tests.

### Consequences

#### Positive
- Fast test execution (< 2 seconds for full test suite)
- No external dependencies required
- Works on all developer machines without setup

#### Negative
- Cannot test database-specific SQL features directly
- Need separate dialect tests for database-specific behavior

---

## ADR-002: Support Multiple Database Dialects

**Status:** Accepted  
**Date:** 2025-01-20  
**Author:** Jaunty Team

### Context

Different databases have different SQL syntax, identifier escaping, and feature support. We need to support multiple databases without duplicating code.

### Problem Statement

How do we support multiple database dialects (SQL Server, PostgreSQL, MySQL, SQLite) while maintaining a single codebase?

### Options Considered

#### Option 1: ISqlDialect Interface

**Description:** Define interface with dialect-specific methods, implement per database

**Pros:**
- Clear separation of concerns
- Easy to add new dialects
- Type-safe

**Cons:**
- Interface may grow over time
- Need to implement all methods for each dialect

#### Option 2: Configuration-Based

**Description:** Use configuration files to define dialect behavior

**Pros:**
- No code changes to add dialects
- Flexible

**Cons:**
- Harder to validate correctness
- Less type-safe
- Complex logic hard to express in config

### Decision

Use ISqlDialect interface with concrete implementations per database.

### Rationale

The interface approach provides type safety, clear contracts, and makes it easy to add new dialects. Each dialect is self-contained and testable.

### Consequences

#### Positive
- Type-safe dialect implementations
- Easy to test each dialect independently
- Clear extension point for new databases

#### Negative
- Need to update all dialects when adding new interface methods
- Some code duplication in similar dialect implementations

---

## ADR-003: Strict vs Partial Mapping Modes

**Status:** Accepted  
**Date:** 2025-02-01  
**Author:** Jaunty Team

### Context

Entity mapping can be strict (all properties must match columns) or partial (only matching columns are mapped). Different use cases require different approaches.

### Problem Statement

How do we support both strict validation and flexible partial mapping for different use cases?

### Options Considered

#### Option 1: Single Mapping Mode

**Description:** Choose either strict or partial, apply everywhere

**Pros:**
- Simple, consistent behavior
- Less code to maintain

**Cons:**
- Doesn't support all use cases
- Forces users to adapt their entities

#### Option 2: Configurable Mapping Mode

**Description:** Support both modes, configurable per query

**Pros:**
- Supports all use cases
- Users can choose appropriate mode

**Cons:**
- More complex API
- Need to maintain both code paths

### Decision

Support both Strict and Partial mapping modes via `MappingMode` enum.

### Rationale

Different scenarios require different mapping modes:
- **Strict**: Production code where entity-schema alignment is critical
- **Partial**: DTOs, projections, queries that select subsets of columns

### Consequences

#### Positive
- Flexible mapping for different use cases
- Strict mode catches schema drift early
- Partial mode enables efficient projections

#### Negative
- Slightly more complex API
- Need to test both modes

---

## ADR-004: Compiled Delegates for Parameter Binding

**Status:** Accepted  
**Date:** 2025-02-10  
**Author:** Jaunty Team

### Context

Parameter binding from entity properties to SQL parameters happens on every query. We need efficient binding without reflection overhead.

### Problem Statement

How do we efficiently bind entity properties to SQL parameters without sacrificing performance?

### Options Considered

#### Option 1: Reflection on Every Call

**Description:** Use reflection to read property values for each binding

**Pros:**
- Simple implementation
- No caching complexity

**Cons:**
- Slow performance (reflection is expensive)
- Allocation overhead

#### Option 2: Compiled Expression Trees

**Description:** Build and cache compiled delegates for property access

**Pros:**
- Fast execution (near direct call performance)
- One-time compilation cost
- No per-call reflection

**Cons:**
- More complex implementation
- Cache management

### Decision

Use compiled expression trees cached per type.

### Rationale

The performance benefit of compiled delegates far outweighs the implementation complexity. Parameter binding is on the critical path for every query, so optimization is justified.

### Consequences

#### Positive
- Fast parameter binding (microseconds vs milliseconds)
- Scales well with high query volume
- No per-call reflection overhead

#### Negative
- More complex code in ParameterCache
- Need to handle cache invalidation (rarely needed)

---

## ADR-005: Return Type Design: long vs int

**Status:** Accepted  
**Date:** 2025-02-15  
**Author:** Jaunty Team

### Context

Different operations return different types: identity values can be large (bigint), row counts fit in int. We need to choose appropriate return types.

### Problem Statement

Should Insert return `long` (for identity values) or `int` (consistent with Update/Delete)?

### Options Considered

#### Option 1: Uniform int Return Type

**Description:** All write operations return `int`

**Pros:**
- Consistent API
- Simpler to use

**Cons:**
- Loses precision for large identity values
- May truncate bigint identities

#### Option 2: Semantic Return Types

**Description:** Insert returns `long` (identity), Update/Delete return `int` (row count)

**Pros:**
- Preserves full identity value range
- Semantically correct (identity vs count)

**Cons:**
- Inconsistent API surface
- Users need to handle different types

### Decision

Use semantic return types: `Insert` returns `long`, `Update`/`Delete` return `int`.

### Rationale

Identity columns can be `bigint` in some databases, supporting values beyond `int.MaxValue`. Row counts will never exceed `int.MaxValue` in practice. The semantic difference justifies different return types.

### Consequences

#### Positive
- Supports full range of identity values
- Semantically correct (identity value vs row count)
- Prevents silent truncation of large identities

#### Negative
- Slightly inconsistent API
- Users need to be aware of different return types

---

## Creating New ADRs

1. Copy the ADR template above
2. Fill in all sections
3. Use next sequential ADR number
4. Submit as PR for review
5. Update ADR Index when merged

## ADR Status Definitions

- **Proposed**: Under discussion, not yet accepted
- **Accepted**: Decision is made, implementation in progress or complete
- **Deprecated**: No longer recommended, superseded by newer ADR
- **Superseded**: Replaced by a newer ADR
