# Jaunty XML Documentation Style Guide

This guide captures the XML documentation standards, patterns, and best practices established during the comprehensive documentation of the Jaunty micro-ORM codebase. Use this guide to maintain consistent, high-quality documentation across all Jaunty projects.

---

## Table of Contents

1. [Overview](#overview)
2. [Required Documentation Elements](#required-documentation-elements)
3. [XML Tag Reference](#xml-tag-reference)
4. [Documentation Structure](#documentation-structure)
5. [Writing Style Guidelines](#writing-style-guidelines)
6. [Code Examples](#code-examples)
7. [Method-Specific Patterns](#method-specific-patterns)
8. [Special Characters and Escaping](#special-characters-and-escaping)
9. [Cross-References](#cross-references)
10. [Exception Documentation](#exception-documentation)
11. [Templates](#templates)
12. [Common Mistakes to Avoid](#common-mistakes-to-avoid)

---

## Overview

### Purpose

XML documentation in Jaunty serves multiple purposes:
- **IntelliSense support** in IDEs for real-time developer assistance
- **API reference documentation** generation for external consumption
- **Code clarity** for maintainers and contributors
- **Consistency** across the codebase for professional quality

### Documentation Standards

All documentation follows **Microsoft's XML Documentation Guidelines** with Jaunty-specific conventions:
- Use standard XML tags: `<summary>`, `<typeparam>`, `<param>`, `<returns>`, `<remarks>`, `<example>`, `<exception>`, `<seealso>`
- Write in clear, concise English
- Include practical code examples for all public APIs
- Document all exception conditions
- Cross-reference related methods

---

## Required Documentation Elements

### For Public Methods

Every public method must have:

```csharp
/// <summary>
/// [One-sentence description of what the method does]
/// </summary>
/// <typeparam name="T">[Description of type parameter if generic]</typeparam>
/// <param name="parameterName">[Description of each parameter]</param>
/// <returns>[Description of return value]</returns>
/// <remarks>
/// [Additional details, behavior notes, important information]
/// </remarks>
/// <example>
/// [Practical code example showing typical usage]
/// </example>
/// <exception cref="ExceptionType">[When this exception is thrown]</exception>
/// <seealso cref="RelatedMethod"/>
```

### For Classes/Types

```csharp
/// <summary>
/// [One-sentence description of what the type represents]
/// </summary>
/// <typeparam name="T">[Description if generic]</typeparam>
/// <remarks>
/// [Additional details about usage, behavior, design intent]
/// </remarks>
/// <example>
/// [Code example showing how to use the type]
/// </example>
/// <seealso cref="RelatedType"/>
```

### For Properties

```csharp
/// <summary>
/// [Description of what the property represents or returns]
/// </summary>
/// <remarks>
/// [Additional details about behavior, default values, etc.]
/// </remarks>
/// <example>
/// [Code example if non-obvious usage]
/// </example>
```

---

## XML Tag Reference

### Core Tags

| Tag | Purpose | Required For |
|-----|---------|--------------|
| `<summary>` | Brief description of purpose | All public members |
| `<typeparam>` | Describe generic type parameters | Generic types/methods |
| `<param>` | Describe method parameters | Methods with parameters |
| `<returns>` | Describe return value | Methods with return values |
| `<remarks>` | Additional details, notes | All public members |
| `<example>` | Code usage examples | All public members |
| `<exception>` | Document thrown exceptions | Methods that throw |
| `<seealso>` | Cross-reference related members | When related APIs exist |

### Tag Usage Examples

#### `<summary>`
```csharp
/// <summary>
/// Executes a SQL query asynchronously and returns the single result mapped to an entity.
/// </summary>
```

**Guidelines:**
- Start with a strong verb (Executes, Returns, Gets, Sets, Creates, etc.)
- Keep to 1-2 sentences
- Describe WHAT it does, not HOW

#### `<typeparam>`
```csharp
/// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
/// <typeparam name="TResult">The type of the result returned by the callback function.</typeparam>
```

**Guidelines:**
- Include constraint requirements (e.g., "Must have a parameterless constructor")
- Describe the role of the type parameter

#### `<param>`
```csharp
/// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
/// <param name="sql">The SQL query to execute.</param>
/// <param name="parameters">An anonymous object or dictionary containing parameter values.</param>
/// <param name="cancellationToken">A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.</param>
```

**Guidelines:**
- Describe the purpose and expected format
- Include type requirements or constraints
- Mention default values if applicable

#### `<returns>`
```csharp
/// <returns>A task containing the single entity mapped from the query results.</returns>
/// <returns>The number of rows affected by the update. Typically <c>1</c> if the entity was found and updated.</returns>
```

**Guidelines:**
- Describe what the return value represents
- Include typical values or ranges when helpful

#### `<remarks>`
```csharp
/// <remarks>
/// <para>
/// This method uses <strong>strict mapping mode</strong>. All public writable properties 
/// on <typeparamref name="T"/> must have matching columns in the result set.
/// </para>
/// <para>
/// <strong>Throws <see cref="InvalidOperationException"/> if:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>The query returns no results</description></item>
/// <item><description>The query returns more than one result</description></item>
/// </list>
/// </remarks>
```

**Guidelines:**
- Use `<para>` tags to separate logical sections
- Use `<strong>` for emphasis on important notes
- Use `<list>` for multiple conditions or options
- Include behavior details not obvious from signature

#### `<example>`
```csharp
/// <example>
/// <code>
/// public class Product
/// {
///     public int Id { get; set; }
///     public string Name { get; set; }
///     public decimal Price { get; set; }
/// }
/// 
/// // Get single product by ID (expects exactly one match)
/// var product = await connection.QuerySingleAsync&lt;Product&gt;(
///     "SELECT * FROM products WHERE id = @Id", 
///     new { Id = 1 });
/// </code>
/// </example>
```

**Guidelines:**
- Show complete, compilable code when possible
- Include context (class definitions) if needed
- Add comments explaining key parts
- Use realistic variable names and values

#### `<exception>`
```csharp
/// <exception cref="InvalidOperationException">
/// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns no results or more than one result.
/// </exception>
/// <exception cref="ArgumentException">
/// Thrown when parameter count doesn't match the SQL.
/// </exception>
```

**Guidelines:**
- Document ALL exceptions that can be thrown
- Describe the specific conditions that trigger each exception
- Use `cref` to reference exception types

#### `<seealso>`
```csharp
/// <seealso cref="QuerySingleOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>
/// <seealso cref="QuerySingle{T}(IDbConnection, string)"/>
/// <seealso cref="CommandOptions{T}"/>
```

**Guidelines:**
- Reference methods that are alternatives or related
- Reference types that are commonly used together
- Include full signature for overloaded methods

---

## Documentation Structure

### Standard Order

Always document in this order:

1. `<summary>` - What it does
2. `<typeparam>` - Type parameters (if generic)
3. `<param>` - Method parameters
4. `<returns>` - Return value
5. `<remarks>` - Additional details
6. `<example>` - Usage example
7. `<exception>` - Exception conditions
8. `<seealso>` - Related members

### Example Structure

```csharp
/// <summary>
/// Executes a SQL query with parameters asynchronously and returns the first result.
/// </summary>
/// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
/// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
/// <param name="sql">The SQL query to execute.</param>
/// <param name="parameters">An anonymous object or dictionary containing parameter values.</param>
/// <param name="cancellationToken">A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.</param>
/// <returns>A task containing the first entity mapped from the query results.</returns>
/// <remarks>
/// <para>
/// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
/// </para>
/// <para>
/// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var product = await connection.QueryFirstAsync&lt;Product&gt;(
///     "SELECT * FROM products WHERE category_id = @CategoryId",
///     new { CategoryId = 5 });
/// </code>
/// </example>
/// <exception cref="InvalidOperationException">
/// Thrown when the connection is not a <see cref="DbConnection"/> or when the query returns no results.
/// </exception>
/// <exception cref="ArgumentException">
/// Thrown when parameter count doesn't match the SQL.
/// </exception>
/// <seealso cref="QueryFirstAsync{T}(IDbConnection, string, CancellationToken)"/>
/// <seealso cref="QueryFirstOrDefaultAsync{T}(IDbConnection, string, object, CancellationToken)"/>
```

---

## Writing Style Guidelines

### Tone and Voice

- **Direct and concise**: Get to the point quickly
- **Active voice**: "Executes a query" not "A query is executed"
- **Present tense**: "Returns the first entity" not "Will return the first entity"
- **Professional but accessible**: Technical accuracy without unnecessary jargon

### Sentence Structure

**Good:**
```csharp
/// <summary>
/// Executes a SQL query and returns the first result mapped to an entity.
/// </summary>
```

**Avoid:**
```csharp
/// <summary>
/// This method is used for executing a SQL query and it will return the first result 
/// that gets mapped to an entity of the specified type.
/// </summary>
```

### Parameter Descriptions

**Good:**
```csharp
/// <param name="connection">The database connection to execute the query against.</param>
/// <param name="sql">The SQL query to execute.</param>
```

**Avoid:**
```csharp
/// <param name="connection">This is the connection parameter that you pass in.</param>
/// <param name="sql">This is the SQL string.</param>
```

### Type References

Always use `<see cref=""/>` or `<typeparamref name=""/>` for type references:

**Good:**
```csharp
/// <param name="connection">Must be a <see cref="DbConnection"/>.</param>
/// <returns>A task of type <typeparamref name="T"/>.</returns>
```

**Avoid:**
```csharp
/// <param name="connection">Must be a DbConnection.</param>
/// <returns>A task of type T.</returns>
```

### Emphasis and Formatting

Use `<strong>` for important warnings or key information:

```csharp
/// <remarks>
/// <para>
/// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
/// </para>
/// <para>
/// <strong>Important:</strong> The connection remains open until enumeration completes.
/// </para>
/// </remarks>
```

Use `<c>` for inline code references:

```csharp
/// <remarks>
/// Returns <c>1</c> for insert operations, <c>0</c> if no rows matched.
/// </remarks>
```

Use <see langword=""/> for language keywords:

```csharp
/// <returns>The entity, or <see langword="null"/> if no results are found.</returns>
```

---

## Code Examples

### Example Structure

Every example should:

1. **Show context** - Include necessary class definitions
2. **Be complete** - Show all required setup
3. **Be realistic** - Use meaningful names and values
4. **Include comments** - Explain non-obvious parts

### Basic Example Pattern

```csharp
/// <example>
/// <code>
/// // [Comment explaining what this example shows]
/// var result = connection.Method&lt;Type&gt;("query", new { Param = value });
/// </code>
/// </example>
```

### Complete Example Pattern

```csharp
/// <example>
/// <code>
/// public class Product
/// {
///     public int Id { get; set; }
///     public string Name { get; set; }
///     public decimal Price { get; set; }
/// }
/// 
/// // Get single product by ID (expects exactly one match)
/// var product = await connection.QuerySingleAsync&lt;Product&gt;(
///     "SELECT * FROM products WHERE id = @Id", 
///     new { Id = 1 });
/// 
/// Console.WriteLine($"{product.Id}: {product.Name} - ${product.Price}");
/// </code>
/// </example>
```

### Multiple Example Pattern

For methods with multiple common use cases:

```csharp
/// <example>
/// <code>
/// // Basic usage
/// var products = await connection.QueryAsync&lt;Product&gt;("SELECT * FROM products");
/// 
/// // With parameters
/// var products = await connection.QueryAsync&lt;Product&gt;(
///     "SELECT * FROM products WHERE category_id = @CategoryId",
///     new { CategoryId = 5 });
/// 
/// // With cancellation token
/// using var cts = new CancellationTokenSource();
/// var products = await connection.QueryAsync&lt;Product&gt;(
///     "SELECT * FROM products", 
///     cts.Token);
/// </code>
/// </example>
```

### Escaping in Examples

**CRITICAL**: Always escape angle brackets in code examples:

| Character | Escape Sequence |
|-----------|-----------------|
| `<` | `&lt;` |
| `>` | `&gt;` |

**Good:**
```csharp
/// <example>
/// <code>
/// var product = await connection.QueryAsync&lt;Product&gt;("SELECT * FROM products");
/// </code>
/// </example>
```

**Wrong (will break build):**
```csharp
/// <example>
/// <code>
/// var product = await connection.QueryAsync<Product>("SELECT * FROM products");
/// </code>
/// </example>
```

---

## Method-Specific Patterns

### Query Methods (Read Operations)

```csharp
/// <summary>
/// Executes a SQL query and returns the results as a list of entities of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
/// <param name="connection">The database connection to execute the query against.</param>
/// <param name="sql">The SQL query to execute.</param>
/// <returns>A list of entities of type <typeparamref name="T"/>.</returns>
/// <remarks>
/// <para>
/// This method uses <strong>strict mapping mode</strong>. All public writable properties 
/// on <typeparamref name="T"/> must have matching columns in the result set.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var products = connection.Query&lt;Product&gt;("SELECT * FROM products");
/// </code>
/// </example>
```

### Write Methods (Insert/Update/Delete)

```csharp
/// <summary>
/// Inserts an entity into the database and returns the generated identity value.
/// </summary>
/// <typeparam name="T">The entity type to insert. Must be a class with a parameterless constructor.</typeparam>
/// <param name="connection">The database connection to execute the insert against.</param>
/// <param name="entity">The entity instance to insert.</param>
/// <returns>The generated identity value, or <c>1</c> for non-identity inserts.</returns>
/// <remarks>
/// <para>
/// This method automatically detects the primary key property and populates it after the insert.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var product = new Product { Name = "Widget", Price = 19.99m };
/// var id = connection.Insert(product);
/// // product.Id is now populated with the generated identity value
/// </code>
/// </example>
```

### Async Methods

```csharp
/// <summary>
/// Asynchronously executes a SQL query and returns the results as a list of entities.
/// </summary>
/// <typeparam name="T">The entity type to map results to.</typeparam>
/// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
/// <param name="sql">The SQL query to execute.</param>
/// <param name="cancellationToken">
/// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
/// </param>
/// <returns>A task containing a list of entities of type <typeparamref name="T"/>.</returns>
/// <remarks>
/// <para>
/// Use this method for non-blocking database operations in async/await contexts.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var products = await connection.QueryAsync&lt;Product&gt;("SELECT * FROM products");
/// </code>
/// </example>
```

### Methods with Options

```csharp
/// <summary>
/// Executes a query with command options and returns the results.
/// </summary>
/// <typeparam name="T">The entity type to map results to.</typeparam>
/// <param name="connection">The database connection.</param>
/// <param name="sql">The SQL query to execute.</param>
/// <param name="options">
/// Command options for configuring the query execution. Use 
/// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
/// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
/// </param>
/// <returns>A list of entities of type <typeparamref name="T"/>.</returns>
/// <example>
/// <code>
/// // With transaction
/// using var tx = connection.BeginTransaction();
/// var products = connection.Query(
///     "SELECT * FROM products",
///     CommandOptions.WithTransaction(tx));
/// 
/// // With timeout
/// var products = connection.Query(
///     "SELECT * FROM products",
///     CommandOptions.WithTimeout(30));
/// </code>
/// </example>
```

---

## Special Characters and Escaping

### Required Escaping

| Character | Context | Escape |
|-----------|---------|--------|
| `<` | In `<code>` blocks | `&lt;` |
| `>` | In `<code>` blocks | `&gt;` |
| `&` | Anywhere | `&amp;` |
| `"` | In attribute values | `&quot;` |
| `'` | In attribute values | `&apos;` |

### Common Patterns

```csharp
// Generic type in code example
QueryAsync&lt;Product&gt;

// Lambda expression
.Where(x => x.Id == id)  // No escaping needed outside <code>

// Comparison operators in code
if (value &gt; 0) { }

// SQL with special characters
"SELECT * FROM products WHERE price &gt; @MinPrice"
```

---

## Cross-References

### Method References

Reference related methods using `<seealso>`:

```csharp
/// <seealso cref="QueryAsync{T}(IDbConnection, string, CancellationToken)"/>
/// <seealso cref="QueryFirstAsync{T}(IDbConnection, string, object, CancellationToken)"/>
```

### Type References

Reference related types:

```csharp
/// <seealso cref="CommandOptions{T}"/>
/// <seealso cref="DbConnection"/>
```

### In-Text References

Use `<see cref=""/>` for inline references:

```csharp
/// <param name="connection">Must be a <see cref="DbConnection"/>.</param>
/// <returns>The value, or <see langword="null"/> if not found.</returns>
```

### Type Parameter References

Use `<typeparamref name=""/>` for type parameters:

```csharp
/// <returns>A list of entities of type <typeparamref name="T"/>.</returns>
/// <para>Properties on <typeparamref name="T"/> must have matching columns.</para>
```

---

## Exception Documentation

### When to Document

Document exceptions that are:
- Thrown by the method itself (not by called methods unless re-thrown)
- Common and likely to be encountered
- Part of the method's contract

### Exception Format

```csharp
/// <exception cref="InvalidOperationException">
/// Thrown when the connection is not a <see cref="DbConnection"/> or when the query returns no results.
/// </exception>
/// <exception cref="ArgumentException">
/// Thrown when parameter count doesn't match the SQL.
/// </exception>
```

### Common Exception Patterns

#### InvalidOperationException
```csharp
/// <exception cref="InvalidOperationException">
/// Thrown when the connection is not a <see cref="DbConnection"/>.
/// </exception>
```

#### ArgumentException
```csharp
/// <exception cref="ArgumentException">
/// Thrown when parameter count doesn't match the SQL.
/// </exception>
```

#### ArgumentNullException
```csharp
/// <exception cref="ArgumentNullException">
/// Thrown when <paramref name="connection"/> is <see langword="null"/>.
/// </exception>
```

---

## Templates

### Method Template

```csharp
/// <summary>
/// [One-sentence description starting with a strong verb]
/// </summary>
/// <typeparam name="T">[Description of type parameter]</typeparam>
/// <param name="parameterName">[Description of parameter]</param>
/// <returns>[Description of return value]</returns>
/// <remarks>
/// <para>
/// [Additional details about behavior, mode, requirements]
/// </para>
/// <para>
/// <strong>[Important notes or warnings]</strong>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Complete, practical code example]
/// </code>
/// </example>
/// <exception cref="ExceptionType">[When this exception is thrown]</exception>
/// <seealso cref="RelatedMethod"/>
```

### Class Template

```csharp
/// <summary>
/// [One-sentence description of what the class represents]
/// </summary>
/// <typeparam name="T">[Description if generic]</typeparam>
/// <remarks>
/// <para>
/// [Description of purpose, typical usage scenarios]
/// </para>
/// <para>
/// <strong>[Important notes about usage or behavior]</strong>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Code example showing typical usage]
/// </code>
/// </example>
/// <seealso cref="RelatedClass"/>
```

### Property Template

```csharp
/// <summary>
/// [Description of what the property represents or returns]
/// </summary>
/// <remarks>
/// <para>
/// [Additional details about behavior, default values]
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Example if non-obvious usage]
/// </code>
/// </example>
```

### Interface Template

```csharp
/// <summary>
/// [Description of what the interface represents]
/// </summary>
/// <typeparam name="T">[Description of type parameter]</typeparam>
/// <remarks>
/// <para>
/// [Description of purpose, when to implement]
/// </para>
/// <para>
/// <strong>[Important implementation notes]</strong>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Example implementation]
/// </code>
/// </example>
/// <seealso cref="RelatedInterface"/>
```

---

## Common Mistakes to Avoid

### 1. Missing Angle Bracket Escaping

**Wrong:**
```csharp
/// <example>
/// <code>
/// var x = connection.Query<Product>("SQL");  // BREAKS BUILD
/// </code>
/// </example>
```

**Correct:**
```csharp
/// <example>
/// <code>
/// var x = connection.Query&lt;Product&gt;("SQL");
/// </code>
/// </example>
```

### 2. Vague Parameter Descriptions

**Wrong:**
```csharp
/// <param name="sql">The SQL string.</param>
```

**Correct:**
```csharp
/// <param name="sql">The SQL query to execute. Use parameter placeholders like @Id for parameters.</param>
```

### 3. Missing Exception Documentation

**Wrong:**
```csharp
/// No exception documentation
```

**Correct:**
```csharp
/// <exception cref="InvalidOperationException">
/// Thrown when the connection is not open.
/// </exception>
```

### 4. Incomplete Examples

**Wrong:**
```csharp
/// <example>
/// <code>
/// connection.Query(sql);  // What is sql? What does this return?
/// </code>
/// </example>
```

**Correct:**
```csharp
/// <example>
/// <code>
/// var products = connection.Query&lt;Product&gt;(
///     "SELECT * FROM products WHERE category_id = @CategoryId",
///     new { CategoryId = 5 });
/// </code>
/// </example>
```

### 5. Wrong Tag Order

**Wrong:**
```csharp
/// <param name="x">Description</param>
/// <summary>Description</summary>  // Should be first!
/// <returns>Description</returns>
```

**Correct:**
```csharp
/// <summary>Description</summary>
/// <param name="x">Description</param>
/// <returns>Description</returns>
```

### 6. Missing Type References

**Wrong:**
```csharp
/// <param name="connection">Must be DbConnection.</param>
```

**Correct:**
```csharp
/// <param name="connection">Must be a <see cref="DbConnection"/>.</param>
```

---

## Quick Reference Card

### Must-Have Checklist

For every public method:
- [ ] `<summary>` with strong verb
- [ ] `<typeparam>` for each type parameter
- [ ] `<param>` for each parameter
- [ ] `<returns>` if method returns value
- [ ] `<remarks>` with behavior details
- [ ] `<example>` with practical code
- [ ] `<exception>` for thrown exceptions
- [ ] `<seealso>` for related methods

### Escaping Checklist

In `<code>` blocks:
- [ ] `<` → `&lt;`
- [ ] `>` → `&gt;`
- [ ] `&` → `&amp;`

### Style Checklist

- [ ] Active voice ("Executes" not "Is executed")
- [ ] Present tense ("Returns" not "Will return")
- [ ] Type references with `<see cref=""/>`
- [ ] Type parameters with `<typeparamref name=""/>`
- [ ] Null keyword with `<see langword="null"/>`
- [ ] Important notes with `<strong>`

---

## Resources

- [Microsoft XML Documentation Comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/)
- [C# XML Documentation Tags](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/)
- [Documentation Best Practices](https://docs.microsoft.com/en-us/dotnet/csharp/coding-conventions)

---

*Last Updated: 2026-02-19*
*Version: 1.0*
