# Jaunty.Fluent Design Decisions

**Created**: 2026-03-09  
**Last Updated**: 2026-03-09

---

## Naming Conventions

### Fluent API Uses "Select*" Not "Query*"

**Decision**: Jaunty.Fluent uses `Select*` naming (e.g., `SelectFirst`, `SelectPartial`, `SelectSingle`) instead of matching core Jaunty's `Query*` naming (e.g., `QueryFirst`, `QueryPartial`).

**Rationale**: 
- "SELECT" is standard SQL/database terminology
- More intuitive for users writing SQL queries
- Distinguishes Fluent API from core Jaunty raw SQL API

**Examples**:
```csharp
// Fluent API (SELECT-based naming)
db.From<Product>().Select();
db.From<Product>().SelectFirst();
db.From<Product>().SelectPartial(p => p.ProductName);

// Core Jaunty (QUERY-based naming)
connection.Query<Product>(sql);
connection.QueryFirst<Product>(sql);
connection.QueryPartial<Product>(sql);
```

**Status**: Intentional design choice - no changes needed

---

## API Design Principles

### 1. Type Safety First
- Use expression trees for compile-time checking
- Avoid string-based APIs where possible
- Provide string-based overloads for dynamic scenarios

### 2. Fluent Chaining
- Each method returns interface for further chaining
- Terminal operations clearly distinguished (Select, Count, etc.)
- Builder pattern for complex operations (INSERT, UPDATE)

### 3. Dialect Awareness
- Generate appropriate SQL for each database
- Abstract dialect differences from users
- Support SQLite, SQL Server, PostgreSQL, MySQL

### 4. NativeAOT Compatible
- No runtime reflection in core paths
- Use compile-time generics
- Source generation for mapping code

### 5. Performance Conscious
- No LINQ in hot paths
- Span-based SQL building where beneficial
- SQL caching for repeated queries

---

## Implementation Patterns

### Marker Methods for SQL Generation

Methods that serve as expression tree markers throw at runtime:

```csharp
public static T Coalesce<T>(T? value, T defaultValue)
{
    throw new InvalidOperationException(
        "Sql.Coalesce is a marker method for SQL generation and cannot be called directly.");
}
```

**Rationale**: Clear error message if used outside expression trees

---

### Builder Classes Are Internal

Implementation classes are `internal`, only interfaces are public:

```csharp
internal sealed class QueryBuilder<T> : IFromClause<T>, IWhereClause<T>, ...
public interface IFromClause<T> { ... }
```

**Rationale**: 
- Hide implementation details
- Allow refactoring without breaking changes
- Guide users to fluent API pattern

---

## Documentation Standards

### XML Comments Required

All public APIs must have:
- Summary description
- Type parameter documentation (`<typeparam>`)
- Parameter documentation (`<param>`)
- Return value documentation (`<returns>`)
- Usage examples (`<example>`) for complex methods

### README Examples

User-facing documentation includes:
- Quick start examples
- Feature-specific examples
- Migration guide from core Jaunty
- Performance tips

---

## Testing Standards

### Test Coverage Requirements

- **Minimum 80%** code coverage
- **Unit tests** for expression visitors
- **Integration tests** for database operations
- **SQL generation tests** using `ToSql()`

### Test Naming Convention

```
Method_Scenario_ExpectedResult
```

Examples:
- `GroupBy_SingleKey_WithCount_ReturnsGroupedResults`
- `Cte_WithOrderBy_ReturnsOrderedResults`
- `Delete_WithWhereCondition_DeletesMatchingRows`

---

## Decisions Pending

None currently.

---

## References

- [Jaunty.Fluent README](../src/Jaunty.Fluent/README.md)
- [API Improvement Tasklist](../../docs/tasks/jaunty-fluent-improvement-tasklist.md)
- [Prioritized Tasklist](../../docs/tasks/jaunty-fluent-prioritized.md)
