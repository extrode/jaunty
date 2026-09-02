# Error Messages, Explained

Each entry is a message you can hit from ordinary code, the query that produces it, and the change
that fixes it. Messages quoted here were captured from a run, not paraphrased; the wording of a
database error varies by engine, so the SQLite form is given and the SQL Server equivalent noted
where it reads differently.

If a message you hit is not here, the ones that are should still show the shape of the answer:
Jaunty generates SQL you can read, so `ToSql()` on the query is the first move in every case.

## `no such column: products.supplier_id`

**SQL Server:** `The multi-part identifier "products.supplier_id" could not be bound.`

You wrote a lambda `On`, which aliases the table, and then a string that names the table.

```csharp
db.From<Product>()
  .InnerJoin<Category>().On((p, c) => p.CategoryId == c.CategoryId)
  .InnerJoin<Supplier>().On("products.supplier_id = suppliers.supplier_id");
```

```sql
FROM products p
INNER JOIN categories c ON (p.category_id = c.category_id)
INNER JOIN suppliers ON products.supplier_id = suppliers.supplier_id
                        ^^^^^^^^
```

Jaunty took `p` and `c` from the names you wrote in the lambda, so the FROM clause reads
`FROM products p`. In SQL, aliasing a table retires its name as a qualifier for the rest of the
statement — there is no dialect where `products.` and `p.` both work once `p` is declared. The
string was passed through untouched, so `products` refers to nothing.

**Fix:** use the alias the lambda established.

```csharp
.InnerJoin<Supplier>().On("p.supplier_id = suppliers.supplier_id");
```

The same applies to every string-form API on such a query — `Where("products.unit_price > 10")`,
`Where("products.x", value)`, `SelectPartial("products.unit_price, categories.category_name")`.

**Strings are not the problem; the qualifier is.** Once the alias exists, string conditions keep
working as long as they use it:

```csharp
.Where("p.unit_price > 20")                    // runs
```

A subquery inside the string is a separate scope, so naming the table *there* is still right — the
alias belongs to the outer statement only:

```csharp
.Where("p.product_id IN (SELECT product_id FROM products WHERE discontinued = 0)")   // runs
```

Both were executed against Jaunty's own SQLite test database rather than reasoned about, and both
returned rows.

**Why it fails rather than silently working.** It would be worse if it didn't. A query whose
qualifier resolves to the wrong table is well-formed SQL returning the wrong rows, and nothing
would tell you. The database rejecting the statement, naming the token to change, is the outcome
to want.

**How to see the alias:** call `ToSql()` before executing. The alias is the lambda parameter name
when Jaunty can use it: not a SQL keyword, not the name of a table already in the query, not an
alias an earlier join locked in, and valid as a plain identifier on every engine. When a join step
cannot use a name, that step keeps the full table names, with one exception: a self-join with
neither side aliased takes the positional `t1`/`t2` scheme, because two bare occurrences of one
table are ambiguous SQL.

## `No metadata found for type 'Product'.`

Full text:

> No metadata found for type 'Product'. Ensure the class has [Table] and is processed by the Jaunty
> source generator (the class must be declared 'partial'), or call Jaunty.Extensions.Reflection's
> UseReflectionMapping().

Jaunty reads entity metadata that its source generator emits at compile time — that is what keeps
it free of runtime reflection and NativeAOT-safe. The message means the generator produced nothing
for this type. Three causes, in the order they occur:

1. **The class is not `partial`.** The generator adds to your class, so it needs the keyword.
   `public partial class Product`.
2. **The generator is not referenced.** In a project that references `Jaunty` through a project
   reference rather than the NuGet package, the analyzer has to be wired up explicitly:
   ```xml
   <ProjectReference Include="..\..\src\Jaunty.SourceGenerator\Jaunty.SourceGenerator.csproj"
                     OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
   ```
   The NuGet package does this for you.
3. **The type has no `[Table]` attribute** and no convention picked it up.

If you want runtime reflection instead — for a prototype, or a type you cannot make `partial` —
`UseReflectionMapping()` from `Jaunty.Extensions.Reflection` supplies it, at the cost of the
NativeAOT guarantee.

## `InvalidOperationException` naming a property with no column

`Query<T>` maps strictly: every public writable property must have a column in the result set. A
property that does not is an error at the call site rather than a silently-null field.

This one has its own page, because it is the difference people meet first when porting from
another ORM: [strict mapping](migrating/strict-mapping.md). `QueryPartial<T>` is the opt-out when
you genuinely want a partial shape.

## Reading generated SQL

Every fluent query answers `ToSql()` without executing, which resolves most questions in this file
faster than the exception does:

```csharp
string sql = db.From<Product>()
    .InnerJoin<Category>().On((p, c) => p.CategoryId == c.CategoryId)
    .Where((p, c) => p.CategoryId == 1)
    .ToSql();
```

```sql
SELECT p.product_id, p.product_name, p.category_id, p.unit_price, p.units_in_stock, p.discontinued,
       c.category_id, c.category_name, c.description
FROM products p
INNER JOIN categories c ON (p.category_id = c.category_id)
WHERE (p.category_id = @p_category_id)
```

Parameters are named after the column they filter, and HAVING operands after the aggregate they are
compared to (`HAVING COUNT(*) > @count`), so a name in a command log can be searched for in the
query that produced it.
