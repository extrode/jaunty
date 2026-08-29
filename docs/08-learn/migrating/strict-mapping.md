# Strict mapping: the one thing that will surprise you

**The rule:** `Query<T>` requires that every public writable property on `T` has a matching column in
the result set. If one does not, it throws `InvalidOperationException` at the call site.
`QueryPartial<T>` maps whatever matches and ignores the rest.

Both are first-class. Neither is an escape hatch. **Which one you want depends on what the type is
for**, and getting that choice right up front removes essentially all of the friction people hit in
their first hour with Jaunty.

## Why it works this way

Every other micro-ORM maps leniently: columns that match get mapped, properties that do not match
keep their default value. That is convenient, and it fails silently in one specific case that costs
real money.

Someone renames a column. Someone edits a `SELECT` and drops a field. Someone adds a property to an
entity and forgets one of the six queries that returns it. The lenient ORM hands you an object where
that property is `0`, `null` or `false` - and `0` is a plausible value for a discount, a balance or a
quantity. **Nothing throws. The bug is now in your data**, and it surfaces weeks later in a report
nobody can reconcile.

Jaunty turns that into an exception on the line that ran the query, with the property named in the
message.

## The choice, in one table

| Your type is... | Use | Because |
|---|---|---|
| An entity mirroring a table, selected in full | `Query<T>` | Every property should be populated. A missing column is a bug |
| A DTO shaped to a specific query | `Query<T>` | Same reasoning. The DTO exists to hold *that* result set exactly |
| A projection - a few columns off a bigger entity | `QueryPartial<T>` | You are deliberately not selecting everything |
| A reporting or ad-hoc query against a wide entity | `QueryPartial<T>` | The shape changes per query and the type is shared |
| A type with computed or client-only properties | `Query<T>` **plus** `[Ignore]` | See below - the property should never have been a mapping target |

**The DTO-per-query habit is the one that pays off.** If a type describes exactly one result set,
strict mapping is free correctness and costs nothing. If a type is shared across queries that select
different subsets of it, `QueryPartial<T>` is the honest answer and you should use it without
feeling like you are working around something.

## The three things people hit

### 1. `SELECT *` against an entity with extra properties

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string DisplayName => $"{Name} ({Price:C})";   // fine: no setter, not a mapping target
    public List<Review> Reviews { get; set; } = new();    // problem: writable, no column
}
```

`Reviews` is a public writable property with no matching column, so `Query<Product>` throws. It
should never have been a mapping target in the first place:

```csharp
[Ignore]
public List<Review> Reviews { get; set; } = new();
```

**Read-only properties are not affected.** Computed values with no setter are not mapping targets and
never need `[Ignore]`.

### 2. Column names that do not match property names

Snake case, prefixes, legacy names. Map them once on the property rather than aliasing in every
query:

```csharp
[Column("product_name")]
public string Name { get; set; } = "";
```

### 3. A non-nullable property receiving NULL

`Query<T>` also throws when the column exists but the value is `NULL` and the property cannot hold
`NULL`. This is the same bug class: lenient mapping would have given you `0` or `""` and let it
through. Fix it by making the property nullable if the column is nullable:

```csharp
public decimal? Discount { get; set; }   // the column allows NULL, so the property must too
```

Where the column is genuinely non-nullable and the `NULL` comes from an outer join, that is a query
shape question - the property belongs on a projection type, not on the entity.

## Porting an existing codebase

**The fast path is a two-step.**

1. **Change every call to `QueryPartial<T>` first**, mechanically. `Query` to `QueryPartial`,
   `QueryFirstOrDefault` to `QueryPartialFirstOrDefault`, and so on. This reproduces your current
   ORM's behaviour exactly, so the port compiles and your tests pass with no semantic change.
2. **Then promote back to `Query<T>` where the type is a full entity or a purpose-built DTO**, one
   file at a time, and fix what it tells you about. Every exception you get in this step is a
   mismatch that was already in your codebase and was already silently producing a default value.

Doing it in that order means you are never debugging a port and a behaviour change at the same time.

**Expect the second step to find something.** In a codebase of any age it usually does, and that
finding is the entire argument for the feature.

## The full pair list

| Strict | Lenient |
|---|---|
| `Query<T>` | `QueryPartial<T>` |
| `QueryFirst<T>` | `QueryPartialFirst<T>` |
| `QueryFirstOrDefault<T>` | `QueryPartialFirstOrDefault<T>` |
| `QuerySingle<T>` | `QueryPartialSingle<T>` |
| `QuerySingleOrDefault<T>` | `QueryPartialSingleOrDefault<T>` |

Each has an `...Async` counterpart with the same semantics.

`MappingMode.Strict` and `MappingMode.Projection` in `Jaunty.Configuration` are the same distinction
where the mode is passed rather than chosen by method name.
