using System.Data;
using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents a query with a JOIN that can be further filtered or executed.
/// </summary>
/// <typeparam name="TFrom">The primary (left) entity type.</typeparam>
/// <typeparam name="TJoin">The joined (right) entity type.</typeparam>
public interface IJoinedQuery<TFrom, TJoin> where TFrom : new() where TJoin : new()
{
    // --- Additional Joins ---

    /// <summary>
    /// Adds an INNER JOIN to another table.
    /// </summary>
    IJoinClause<TFrom, TJoin, T3> InnerJoin<T3>(string? alias = null) where T3 : new();

    /// <summary>
    /// Adds a LEFT JOIN to another table.
    /// </summary>
    IJoinClause<TFrom, TJoin, T3> LeftJoin<T3>(string? alias = null) where T3 : new();

    // --- WHERE Clauses ---

    /// <summary>
    /// Adds a WHERE clause using a predicate expression.
    /// </summary>
    IJoinedQuery<TFrom, TJoin> Where(Expression<Func<TFrom, TJoin, bool>> predicate);

    /// <summary>
    /// Adds a WHERE clause using a raw SQL condition.
    /// </summary>
    IJoinedQuery<TFrom, TJoin> Where(string condition);

    /// <summary>
    /// Adds a WHERE clause using column name and value.
    /// </summary>
    IJoinedQuery<TFrom, TJoin> Where(string column, object value);

    // --- AND/OR ---

    /// <summary>
    /// Adds an AND condition using a predicate expression.
    /// </summary>
    IJoinedQuery<TFrom, TJoin> And(Expression<Func<TFrom, TJoin, bool>> predicate);

    /// <summary>
    /// Adds an OR condition using a predicate expression.
    /// </summary>
    IJoinedQuery<TFrom, TJoin> Or(Expression<Func<TFrom, TJoin, bool>> predicate);

    // --- ORDER BY ---

    /// <summary>
    /// Adds an ORDER BY clause (ascending).
    /// </summary>
    IJoinedQuery<TFrom, TJoin> OrderBy<TKey>(Expression<Func<TFrom, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (ascending) for the joined entity.
    /// </summary>
    IJoinedQuery<TFrom, TJoin> OrderByJoined<TKey>(Expression<Func<TJoin, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (descending).
    /// </summary>
    IJoinedQuery<TFrom, TJoin> OrderByDescending<TKey>(Expression<Func<TFrom, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (descending) for the joined entity.
    /// </summary>
    IJoinedQuery<TFrom, TJoin> OrderByJoinedDescending<TKey>(Expression<Func<TJoin, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (ascending) for the primary entity.
    /// </summary>
    IJoinedQuery<TFrom, TJoin> ThenBy<TKey>(Expression<Func<TFrom, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (ascending) for the joined entity.
    /// </summary>
    IJoinedQuery<TFrom, TJoin> ThenByJoined<TKey>(Expression<Func<TJoin, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (descending) for the primary entity.
    /// </summary>
    IJoinedQuery<TFrom, TJoin> ThenByDescending<TKey>(Expression<Func<TFrom, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (descending) for the joined entity.
    /// </summary>
    IJoinedQuery<TFrom, TJoin> ThenByJoinedDescending<TKey>(Expression<Func<TJoin, TKey>> keySelector);

    // --- SELECT Operations ---

    /// <summary>
    /// Executes the query and returns the primary (From) entity.
    /// Equivalent to <c>Select&lt;TFrom&gt;()</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// var products = db.From&lt;Product&gt;()
    ///     .InnerJoin&lt;Category&gt;()
    ///     .On(p =&gt; p.CategoryId, c =&gt; c.CategoryId)
    ///     .Select();  // Returns List&lt;Product&gt;
    /// </code>
    /// </example>
    List<TFrom> Select();

    /// <summary>
    /// Executes the query and returns the specified entity type.
    /// Uses IMapped&lt;T&gt; if implemented, otherwise maps columns to properties (strict mode - all properties must match).
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <example>
    /// <code>
    /// // Select products
    /// var products = db.From&lt;Product&gt;()
    ///     .InnerJoin&lt;Category&gt;()
    ///     .On(p =&gt; p.CategoryId, c =&gt; c.CategoryId)
    ///     .Select&lt;Product&gt;();
    ///
    /// // Select custom entity
    /// var results = db.From&lt;Product&gt;()
    ///     .InnerJoin&lt;Category&gt;()
    ///     .On(p =&gt; p.CategoryId, c =&gt; c.CategoryId)
    ///     .Select&lt;ProductCategoryDto&gt;();
    /// </code>
    /// </example>
    List<T> Select<T>() where T : new();

    /// <summary>
    /// Executes the query and returns the specified entity type using a custom mapper.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    /// <param name="mapper">Function to map IDataReader to the result type.</param>
    List<T> Select<T>(Func<IDataReader, T> mapper);

    /// <summary>
    /// Executes the query and returns both entities as tuples.
    /// </summary>
    List<(TFrom From, TJoin Joined)> SelectBoth();

    /// <summary>
    /// Executes the query and returns both entities as tuples.
    /// </summary>
    /// <typeparam name="T1">Must be <typeparamref name="TFrom"/>.</typeparam>
    /// <typeparam name="T2">Must be <typeparamref name="TJoin"/>.</typeparam>
    /// <example>
    /// <code>
    /// var results = db.From&lt;Product&gt;()
    ///     .InnerJoin&lt;Category&gt;()
    ///     .On(p =&gt; p.CategoryId, c =&gt; c.CategoryId)
    ///     .Select&lt;Product, Category&gt;();  // Returns List&lt;(Product, Category)&gt;
    /// </code>
    /// </example>
    List<(T1, T2)> Select<T1, T2>() where T1 : new() where T2 : new();



    /// <summary>
    /// Returns the first result of the specified type or throws if empty.
    /// Uses IMapped&lt;T&gt; if implemented, otherwise maps columns to properties (strict mode).
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    T SelectFirst<T>() where T : new();

    /// <summary>
    /// Returns the first result of the specified type using a custom mapper or throws if empty.
    /// </summary>
    T SelectFirst<T>(Func<IDataReader, T> mapper);

    /// <summary>
    /// Returns the first result of the specified type, or default if empty.
    /// Uses IMapped&lt;T&gt; if implemented, otherwise maps columns to properties (strict mode).
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    T? SelectFirstOrDefault<T>() where T : new();

    /// <summary>
    /// Returns the first result of the specified type using a custom mapper, or default if empty.
    /// </summary>
    T? SelectFirstOrDefault<T>(Func<IDataReader, T> mapper);

    /// <summary>
    /// Returns the first result of the primary entity or throws if empty.
    /// Equivalent to <c>SelectFirst&lt;TFrom&gt;()</c>.
    /// </summary>
    TFrom SelectFirst();

    /// <summary>
    /// Returns the first result of the primary entity, or default if empty.
    /// Equivalent to <c>SelectFirstOrDefault&lt;TFrom&gt;()</c>.
    /// </summary>
    TFrom? SelectFirstOrDefault();

    /// <summary>
    /// Returns the first result as a tuple or throws if empty.
    /// </summary>
    (TFrom From, TJoin Joined) SelectFirstBoth();

    /// <summary>
    /// Returns the count of rows.
    /// </summary>
    int Count();

    /// <summary>
    /// Returns the count of rows as long.
    /// </summary>
    long LongCount();

    /// <summary>
    /// Returns the generated SQL for debugging purposes.
    /// </summary>
    string ToSql();

    // --- Async Operations ---

    /// <summary>
    /// Executes the query asynchronously and returns the primary (From) entity.
    /// Equivalent to <c>SelectAsync&lt;TFrom&gt;()</c>.
    /// </summary>
    Task<List<TFrom>> SelectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query asynchronously and returns the specified entity type.
    /// Uses IMapped&lt;T&gt; if implemented, otherwise maps columns to properties (strict mode).
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    Task<List<T>> SelectAsync<T>(CancellationToken cancellationToken = default) where T : new();

    /// <summary>
    /// Executes the query asynchronously and returns the specified entity type using a custom mapper.
    /// </summary>
    Task<List<T>> SelectAsync<T>(Func<IDataReader, T> mapper, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query asynchronously and returns both entities as tuples.
    /// </summary>
    /// <typeparam name="T1">Must be <typeparamref name="TFrom"/>.</typeparam>
    /// <typeparam name="T2">Must be <typeparamref name="TJoin"/>.</typeparam>
    Task<List<(T1, T2)>> SelectAsync<T1, T2>(CancellationToken cancellationToken = default) where T1 : new() where T2 : new();

    /// <summary>
    /// Returns the first result of the specified type asynchronously or throws if empty.
    /// Uses IMapped&lt;T&gt; if implemented, otherwise maps columns to properties (strict mode).
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    Task<T> SelectFirstAsync<T>(CancellationToken cancellationToken = default) where T : new();

    /// <summary>
    /// Returns the first result asynchronously using a custom mapper or throws if empty.
    /// </summary>
    Task<T> SelectFirstAsync<T>(Func<IDataReader, T> mapper, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result of the specified type asynchronously, or default if empty.
    /// Uses IMapped&lt;T&gt; if implemented, otherwise maps columns to properties (strict mode).
    /// </summary>
    /// <typeparam name="T">The entity type to map results to.</typeparam>
    Task<T?> SelectFirstOrDefaultAsync<T>(CancellationToken cancellationToken = default) where T : new();

    /// <summary>
    /// Returns the first result asynchronously using a custom mapper, or default if empty.
    /// </summary>
    Task<T?> SelectFirstOrDefaultAsync<T>(Func<IDataReader, T> mapper, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result of the primary entity asynchronously or throws if empty.
    /// Equivalent to <c>SelectFirstAsync&lt;TFrom&gt;()</c>.
    /// </summary>
    Task<TFrom> SelectFirstAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result of the primary entity asynchronously, or default if empty.
    /// Equivalent to <c>SelectFirstOrDefaultAsync&lt;TFrom&gt;()</c>.
    /// </summary>
    Task<TFrom?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result as a tuple asynchronously or throws if empty.
    /// </summary>
    Task<(TFrom From, TJoin Joined)> SelectFirstBothAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows asynchronously.
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows as long asynchronously.
    /// </summary>
    Task<long> LongCountAsync(CancellationToken cancellationToken = default);

    // --- SELECT PARTIAL Operations (Projection) ---

    /// <summary>
    /// Executes the query selecting only the specified columns and returns dynamic results.
    /// </summary>
    /// <param name="columns">Comma-delimited column names (e.g., "p.product_id, p.product_name, c.category_name").</param>
    /// <example>
    /// <code>
    /// var results = db.From&lt;Product&gt;("p")
    ///     .InnerJoin&lt;Category&gt;("c")
    ///     .On(p =&gt; p.CategoryId, c =&gt; c.CategoryId)
    ///     .SelectPartial("p.product_id, p.product_name, c.category_name");
    /// </code>
    /// </example>
    List<IDictionary<string, object?>> SelectPartial(string columns);

    /// <summary>
    /// Executes the query selecting only the specified columns with a custom mapper.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="columns">Comma-delimited column names.</param>
    /// <param name="mapper">Function to map IDataReader to the result type.</param>
    List<T> SelectPartial<T>(string columns, Func<IDataReader, T> mapper);

    /// <summary>
    /// Returns the first partial result or throws if empty.
    /// </summary>
    IDictionary<string, object?> SelectPartialFirst(string columns);

    /// <summary>
    /// Returns the first partial result with a custom mapper or throws if empty.
    /// </summary>
    T SelectPartialFirst<T>(string columns, Func<IDataReader, T> mapper);

    /// <summary>
    /// Returns the first partial result, or null if empty.
    /// </summary>
    IDictionary<string, object?>? SelectPartialFirstOrDefault(string columns);

    /// <summary>
    /// Returns the first partial result with a custom mapper, or default if empty.
    /// </summary>
    T? SelectPartialFirstOrDefault<T>(string columns, Func<IDataReader, T> mapper);

    /// <summary>
    /// Returns the single partial result or throws if empty or more than one.
    /// </summary>
    IDictionary<string, object?> SelectPartialSingle(string columns);

    /// <summary>
    /// Returns the single partial result with a custom mapper or throws if empty or more than one.
    /// </summary>
    T SelectPartialSingle<T>(string columns, Func<IDataReader, T> mapper);

    /// <summary>
    /// Returns the single partial result, or null if empty. Throws if more than one.
    /// </summary>
    IDictionary<string, object?>? SelectPartialSingleOrDefault(string columns);

    /// <summary>
    /// Returns the single partial result with a custom mapper, or default if empty. Throws if more than one.
    /// </summary>
    T? SelectPartialSingleOrDefault<T>(string columns, Func<IDataReader, T> mapper);

    // --- SELECT PARTIAL Async Operations ---

    /// <summary>
    /// Executes the query asynchronously selecting only the specified columns.
    /// </summary>
    Task<List<IDictionary<string, object?>>> SelectPartialAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query asynchronously selecting only the specified columns with a custom mapper.
    /// </summary>
    Task<List<T>> SelectPartialAsync<T>(string columns, Func<IDataReader, T> mapper, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first partial result asynchronously or throws if empty.
    /// </summary>
    Task<IDictionary<string, object?>> SelectPartialFirstAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first partial result asynchronously with a custom mapper or throws if empty.
    /// </summary>
    Task<T> SelectPartialFirstAsync<T>(string columns, Func<IDataReader, T> mapper, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first partial result asynchronously, or null if empty.
    /// </summary>
    Task<IDictionary<string, object?>?> SelectPartialFirstOrDefaultAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first partial result asynchronously with a custom mapper, or default if empty.
    /// </summary>
    Task<T?> SelectPartialFirstOrDefaultAsync<T>(string columns, Func<IDataReader, T> mapper, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single partial result asynchronously or throws if empty or more than one.
    /// </summary>
    Task<IDictionary<string, object?>> SelectPartialSingleAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single partial result asynchronously with a custom mapper or throws if empty or more than one.
    /// </summary>
    Task<T> SelectPartialSingleAsync<T>(string columns, Func<IDataReader, T> mapper, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single partial result asynchronously, or null if empty. Throws if more than one.
    /// </summary>
    Task<IDictionary<string, object?>?> SelectPartialSingleOrDefaultAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single partial result asynchronously with a custom mapper, or default if empty. Throws if more than one.
    /// </summary>
    Task<T?> SelectPartialSingleOrDefaultAsync<T>(string columns, Func<IDataReader, T> mapper, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a JOIN clause for a third table.
/// </summary>
public interface IJoinClause<T1, T2, T3> where T1 : new() where T2 : new() where T3 : new()
{
    /// <summary>
    /// Specifies the join condition using key expressions from T1.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> On<TLeftKey, TRightKey>(Expression<Func<T1, TLeftKey>> leftKey, Expression<Func<T3, TRightKey>> rightKey);

    /// <summary>
    /// Specifies the join condition using key expressions from T2.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> OnFromSecond<TLeftKey, TRightKey>(Expression<Func<T2, TLeftKey>> leftKey, Expression<Func<T3, TRightKey>> rightKey);

    /// <summary>
    /// Specifies the join condition using column names.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> On(string leftColumn, string rightColumn);

    /// <summary>
    /// Specifies the join condition using raw SQL.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> On(string condition);
}

/// <summary>
/// Represents a query with three joined tables.
/// </summary>
public interface IJoinedQuery3<T1, T2, T3> where T1 : new() where T2 : new() where T3 : new()
{
    /// <summary>
    /// Adds a WHERE clause using a predicate expression.
    /// </summary>
    /// <param name="predicate">Expression predicate for the WHERE condition.</param>
    IJoinedQuery3<T1, T2, T3> Where(Expression<Func<T1, T2, T3, bool>> predicate);

    /// <summary>
    /// Adds a WHERE clause using a raw SQL condition.
    /// </summary>
    /// <param name="condition">The raw SQL condition.</param>
    IJoinedQuery3<T1, T2, T3> Where(string condition);

    /// <summary>
    /// Executes the query and returns the primary (first) entity.
    /// </summary>
    List<T1> Select();

    /// <summary>
    /// Executes the query and returns all three entities as tuples.
    /// </summary>
    List<(T1, T2, T3)> SelectAll();

    /// <summary>
    /// Returns the generated SQL for debugging purposes.
    /// </summary>
    string ToSql();
}