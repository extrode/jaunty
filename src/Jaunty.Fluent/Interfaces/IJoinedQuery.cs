using System.Data;
using System.Linq.Expressions;

using Jaunty.Core;

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

    /// <summary>
    /// Adds a RIGHT JOIN to another table.
    /// </summary>
    IJoinClause<TFrom, TJoin, T3> RightJoin<T3>(string? alias = null) where T3 : new();

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
    /// <remarks>
    /// AUD-R35-184. <paramref name="value"/> is nullable and a null means <c>IS NULL</c>, matching
    /// the single-table twin <c>QueryBuilder.Where(string, object?)</c>. The parameter used to be
    /// declared non-nullable while a null passed anyway - trivially reachable from a nullable
    /// property or a dictionary lookup - was bound as a parameter, producing <c>col = @p</c> with a
    /// null value. That is UNKNOWN in SQL, not false, so the filter silently matched nothing rather
    /// than the rows the caller meant.
    /// </remarks>
    IJoinedQuery<TFrom, TJoin> Where(string column, object? value);

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

    // --- GROUP BY Operations ---

    /// <summary>
    /// Groups the joined results by a key drawn from either entity, for HAVING/aggregate
    /// Select projections. See <see cref="IGroupedJoinedQuery{TFrom,TJoin,TKey}"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// db.From&lt;Film&gt;()
    ///     .InnerJoin&lt;Category&gt;().On(f =&gt; f.CategoryId, c =&gt; c.CategoryId)
    ///     .GroupBy((f, c) =&gt; c.Name)
    ///     .Select(g =&gt; new { Category = g.Key, Count = g.Count() });
    /// </code>
    /// </example>
    IGroupedJoinedQuery<TFrom, TJoin, TKey> GroupBy<TKey>(Expression<Func<TFrom, TJoin, TKey>> keySelector);

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
    /// Executes the query and returns the primary (From) entity, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// Equivalent to <c>Select&lt;TFrom&gt;()</c>.
    /// </summary>
    List<TFrom> Select(CommandOptions options);

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
    /// Returns the first result of the primary entity or throws if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// Equivalent to <c>SelectFirst&lt;TFrom&gt;()</c>.
    /// </summary>
    TFrom SelectFirst(CommandOptions options);

    /// <summary>
    /// Returns the first result of the primary entity, or default if empty.
    /// Equivalent to <c>SelectFirstOrDefault&lt;TFrom&gt;()</c>.
    /// </summary>
    TFrom? SelectFirstOrDefault();

    /// <summary>
    /// Returns the first result of the primary entity, or default if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// Equivalent to <c>SelectFirstOrDefault&lt;TFrom&gt;()</c>.
    /// </summary>
    TFrom? SelectFirstOrDefault(CommandOptions options);

    /// <summary>
    /// Returns the single result of the primary entity or throws if not exactly one.
    /// </summary>
    TFrom SelectSingle();

    /// <summary>
    /// Returns the single result of the primary entity or throws if not exactly one, using the
    /// specified <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    TFrom SelectSingle(CommandOptions options);

    /// <summary>
    /// Returns the single result of the primary entity, or default if empty. Throws if more than one.
    /// </summary>
    TFrom? SelectSingleOrDefault();

    /// <summary>
    /// Returns the single result of the primary entity, or default if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction). Throws if more
    /// than one.
    /// </summary>
    TFrom? SelectSingleOrDefault(CommandOptions options);

    /// <summary>
    /// Returns the first result as a tuple or throws if empty.
    /// </summary>
    (TFrom From, TJoin Joined) SelectFirstBoth();

    /// <summary>
    /// Returns the count of rows.
    /// </summary>
    int Count();

    /// <summary>
    /// Returns the count of rows, using the specified <see cref="CommandOptions"/> (e.g. to run
    /// within an explicit transaction).
    /// </summary>
    int Count(CommandOptions options);

    /// <summary>
    /// Returns the count of rows as long.
    /// </summary>
    long LongCount();

    /// <summary>
    /// Returns the count of rows as long, using the specified <see cref="CommandOptions"/> (e.g.
    /// to run within an explicit transaction).
    /// </summary>
    long LongCount(CommandOptions options);

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
    /// Executes the query asynchronously and returns the primary (From) entity, using the
    /// specified <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// Equivalent to <c>SelectAsync&lt;TFrom&gt;()</c>.
    /// </summary>
    Task<List<TFrom>> SelectAsync(CommandOptions options, CancellationToken cancellationToken = default);

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
    /// <remarks>
    /// AUD-R35-185. The async surface had no counterpart to the synchronous
    /// <see cref="SelectBoth"/> - the only async route to the tuple list was
    /// <see cref="SelectAsync{T1, T2}(CancellationToken)"/>, so translating a sync call meant
    /// changing its shape rather than appending <c>Async</c>, while the neighbouring
    /// <c>SelectFirstBoth</c> / <c>SelectFirstBothAsync</c> pair was already symmetric.
    /// </remarks>
    Task<List<(TFrom From, TJoin Joined)>> SelectBothAsync(CancellationToken cancellationToken = default);

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
    /// Returns the first result of the primary entity asynchronously or throws if empty, using
    /// the specified <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// Equivalent to <c>SelectFirstAsync&lt;TFrom&gt;()</c>.
    /// </summary>
    Task<TFrom> SelectFirstAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result of the primary entity asynchronously, or default if empty.
    /// Equivalent to <c>SelectFirstOrDefaultAsync&lt;TFrom&gt;()</c>.
    /// </summary>
    Task<TFrom?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result of the primary entity asynchronously, or default if empty, using
    /// the specified <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// Equivalent to <c>SelectFirstOrDefaultAsync&lt;TFrom&gt;()</c>.
    /// </summary>
    Task<TFrom?> SelectFirstOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result as a tuple asynchronously or throws if empty.
    /// </summary>
    Task<(TFrom From, TJoin Joined)> SelectFirstBothAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result of the primary entity asynchronously, or throws if the
    /// result set is empty or contains more than one row. Equivalent to <c>SelectSingle()</c>.
    /// </summary>
    Task<TFrom> SelectSingleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result of the primary entity asynchronously, or throws if the result
    /// set is empty or contains more than one row, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction). Equivalent to
    /// <c>SelectSingle()</c>.
    /// </summary>
    Task<TFrom> SelectSingleAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result of the primary entity asynchronously, or default if empty;
    /// throws if the result set contains more than one row. Equivalent to
    /// <c>SelectSingleOrDefault()</c>.
    /// </summary>
    Task<TFrom?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result of the primary entity asynchronously, or default if empty, using
    /// the specified <see cref="CommandOptions"/> (e.g. to run within an explicit transaction);
    /// throws if the result set contains more than one row. Equivalent to
    /// <c>SelectSingleOrDefault()</c>.
    /// </summary>
    Task<TFrom?> SelectSingleOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows asynchronously.
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows asynchronously, using the specified <see cref="CommandOptions"/>
    /// (e.g. to run within an explicit transaction).
    /// </summary>
    Task<int> CountAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows as long asynchronously.
    /// </summary>
    Task<long> LongCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows as long asynchronously, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    Task<long> LongCountAsync(CommandOptions options, CancellationToken cancellationToken = default);

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
    /// Specifies the join condition using a predicate expression over all three tables, for
    /// conditions the key-expression overloads cannot state - a composite key, a comparison
    /// against a literal, or a join back to the second table as well as the first.
    /// </summary>
    /// <param name="predicate">Expression defining the join condition.</param>
    /// <example>
    /// <code>
    /// db.From&lt;Order&gt;("o")
    ///     .InnerJoin&lt;Customer&gt;("c").On((o, c) =&gt; o.CustomerId == c.Id)
    ///     .InnerJoin&lt;OrderLine&gt;("l").On((o, c, l) =&gt; l.OrderId == o.Id &amp;&amp; l.Quantity &gt; 0)
    ///     .Select();
    /// </code>
    /// </example>
    IJoinedQuery3<T1, T2, T3> On(Expression<Func<T1, T2, T3, bool>> predicate);

    /// <summary>
    /// Specifies the join condition using column names.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> On(string leftColumn, string rightColumn);

    /// <summary>
    /// Specifies the join condition using raw SQL.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> On(string condition);

    /// <summary>
    /// Specifies the join condition using a raw SQL condition with a typed parameter.
    /// </summary>
    /// <typeparam name="TValue">The type of the parameter value.</typeparam>
    /// <param name="condition">
    /// The raw SQL join condition with a "value" placeholder using the connection's dialect
    /// parameter prefix (e.g. "@value" for SQL Server/PostgreSQL/MySQL/SQLite, "$value" for
    /// DuckDB).
    /// </param>
    /// <param name="value">The parameter value.</param>
    /// <remarks>
    /// The parameter is always named "value", so only one join in a chain can use this overload.
    /// For more than one raw-value join condition, use the overload that takes an explicit
    /// parameter name.
    /// </remarks>
    IJoinedQuery3<T1, T2, T3> On<TValue>(string condition, TValue value);

    /// <summary>
    /// Specifies the join condition using a raw SQL condition with a named typed parameter.
    /// Use this instead of <see cref="On{TValue}(string, TValue)"/> when a join chain has more
    /// than one raw-value condition: that overload always binds to "value", so a second use
    /// would collide on the same parameter name.
    /// </summary>
    /// <typeparam name="TValue">The type of the parameter value.</typeparam>
    /// <param name="condition">
    /// The raw SQL join condition referencing <paramref name="parameterName"/> with the
    /// connection's dialect parameter prefix (e.g. "@orderId", or "$orderId" for DuckDB).
    /// </param>
    /// <param name="parameterName">
    /// The parameter name, either bare ("orderId") or already prefixed ("@orderId"). Must be
    /// unique across the query's join conditions.
    /// </param>
    /// <param name="value">The parameter value.</param>
    IJoinedQuery3<T1, T2, T3> On<TValue>(string condition, string parameterName, TValue value);
}

/// <summary>
/// Builder for the fourth JOIN clause in a 4-table join.
/// </summary>
public interface IJoinClause<T1, T2, T3, T4>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    /// <summary>
    /// Specifies the join condition using key expressions from T1.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> On<TLeftKey, TRightKey>(
        Expression<Func<T1, TLeftKey>> leftKey,
        Expression<Func<T4, TRightKey>> rightKey);

    /// <summary>
    /// Specifies the join condition using key expressions from T2.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> OnFromSecond<TLeftKey, TRightKey>(
        Expression<Func<T2, TLeftKey>> leftKey,
        Expression<Func<T4, TRightKey>> rightKey);

    /// <summary>
    /// Specifies the join condition using key expressions from T3.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> OnFromThird<TLeftKey, TRightKey>(
        Expression<Func<T3, TLeftKey>> leftKey,
        Expression<Func<T4, TRightKey>> rightKey);

    /// <summary>
    /// Specifies the join condition using a predicate expression over all four tables, for
    /// conditions the key-expression overloads cannot state - a composite key, a comparison
    /// against a literal, or a join back to more than one earlier table.
    /// </summary>
    /// <param name="predicate">Expression defining the join condition.</param>
    /// <example>
    /// <code>
    /// db.From&lt;Order&gt;("o")
    ///     .InnerJoin&lt;Customer&gt;("c").On((o, c) =&gt; o.CustomerId == c.Id)
    ///     .InnerJoin&lt;OrderLine&gt;("l").On((o, c, l) =&gt; l.OrderId == o.Id)
    ///     .InnerJoin&lt;Product&gt;("p").On((o, c, l, p) =&gt; l.ProductId == p.Id &amp;&amp; p.Active)
    ///     .Select();
    /// </code>
    /// </example>
    IJoinedQuery4<T1, T2, T3, T4> On(Expression<Func<T1, T2, T3, T4, bool>> predicate);

    /// <summary>
    /// Specifies the join condition using column names.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> On(string leftColumn, string rightColumn);

    /// <summary>
    /// Specifies the join condition using raw SQL.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> On(string condition);

    /// <summary>
    /// Specifies the join condition using a raw SQL condition with a typed parameter.
    /// </summary>
    /// <typeparam name="TValue">The type of the parameter value.</typeparam>
    /// <param name="condition">
    /// The raw SQL join condition with a "value" placeholder using the connection's dialect
    /// parameter prefix (e.g. "@value" for SQL Server/PostgreSQL/MySQL/SQLite, "$value" for
    /// DuckDB).
    /// </param>
    /// <param name="value">The parameter value.</param>
    /// <remarks>
    /// The parameter is always named "value", so only one join in a chain can use this overload.
    /// For more than one raw-value join condition, use the overload that takes an explicit
    /// parameter name.
    /// </remarks>
    IJoinedQuery4<T1, T2, T3, T4> On<TValue>(string condition, TValue value);

    /// <summary>
    /// Specifies the join condition using a raw SQL condition with a named typed parameter.
    /// Use this instead of <see cref="On{TValue}(string, TValue)"/> when a join chain has more
    /// than one raw-value condition: that overload always binds to "value", so a second use
    /// would collide on the same parameter name.
    /// </summary>
    /// <typeparam name="TValue">The type of the parameter value.</typeparam>
    /// <param name="condition">
    /// The raw SQL join condition referencing <paramref name="parameterName"/> with the
    /// connection's dialect parameter prefix (e.g. "@orderId", or "$orderId" for DuckDB).
    /// </param>
    /// <param name="parameterName">
    /// The parameter name, either bare ("orderId") or already prefixed ("@orderId"). Must be
    /// unique across the query's join conditions.
    /// </param>
    /// <param name="value">The parameter value.</param>
    IJoinedQuery4<T1, T2, T3, T4> On<TValue>(string condition, string parameterName, TValue value);
}

/// <summary>
/// Represents a query with three joined tables.
/// </summary>
public interface IJoinedQuery3<T1, T2, T3> where T1 : new() where T2 : new() where T3 : new()
{
    // --- WHERE Clauses ---

    /// <summary>
    /// Adds a WHERE clause using a predicate expression.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> Where(Expression<Func<T1, T2, T3, bool>> predicate);

    /// <summary>
    /// Adds a WHERE clause using a raw SQL condition.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> Where(string condition);

    /// <summary>
    /// Adds a WHERE clause using column name and value.
    /// </summary>
    /// <remarks>
    /// AUD-R35-184. <paramref name="value"/> is nullable and a null means <c>IS NULL</c>, matching
    /// the single-table twin <c>QueryBuilder.Where(string, object?)</c>. The parameter used to be
    /// declared non-nullable while a null passed anyway - trivially reachable from a nullable
    /// property or a dictionary lookup - was bound as a parameter, producing <c>col = @p</c> with a
    /// null value. That is UNKNOWN in SQL, not false, so the filter silently matched nothing rather
    /// than the rows the caller meant.
    /// </remarks>
    IJoinedQuery3<T1, T2, T3> Where(string column, object? value);

    /// <summary>
    /// Adds an AND condition using a predicate expression.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> And(Expression<Func<T1, T2, T3, bool>> predicate);

    /// <summary>
    /// Adds an OR condition using a predicate expression.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> Or(Expression<Func<T1, T2, T3, bool>> predicate);

    // --- ORDER BY ---

    /// <summary>
    /// Adds an ORDER BY clause (ascending) for the primary entity.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> OrderBy<TKey>(Expression<Func<T1, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (descending) for the primary entity.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> OrderByDescending<TKey>(Expression<Func<T1, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (ascending) for the second entity.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> OrderByJoined<TKey>(Expression<Func<T2, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (descending) for the second entity.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> OrderByJoinedDescending<TKey>(Expression<Func<T2, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (ascending) for the third entity.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> OrderByJoined<TKey>(Expression<Func<T3, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (descending) for the third entity.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> OrderByJoinedDescending<TKey>(Expression<Func<T3, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (ascending) for the primary entity.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> ThenBy<TKey>(Expression<Func<T1, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (descending) for the primary entity.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> ThenByDescending<TKey>(Expression<Func<T1, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (ascending) for the second entity.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> ThenByJoined<TKey>(Expression<Func<T2, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (descending) for the second entity.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> ThenByJoinedDescending<TKey>(Expression<Func<T2, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (ascending) for the third entity.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> ThenByJoined<TKey>(Expression<Func<T3, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (descending) for the third entity.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> ThenByJoinedDescending<TKey>(Expression<Func<T3, TKey>> keySelector);

    // --- GROUP BY Operations ---

    /// <summary>
    /// Groups the joined results by a key drawn from any of the three entities, for
    /// HAVING/aggregate Select projections. See
    /// <see cref="IGroupedJoinedQuery3{T1,T2,T3,TKey}"/>.
    /// </summary>
    IGroupedJoinedQuery3<T1, T2, T3, TKey> GroupBy<TKey>(Expression<Func<T1, T2, T3, TKey>> keySelector);

    // --- SELECT Operations ---

    /// <summary>
    /// Executes the query and returns the primary entity.
    /// </summary>
    List<T1> Select();

    /// <summary>
    /// Executes the query and returns the primary entity, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    List<T1> Select(CommandOptions options);

    /// <summary>
    /// Executes the query and returns all three entities as tuples.
    /// </summary>
    List<(T1, T2, T3)> SelectAll();

    /// <summary>
    /// Executes the query and returns all three entities as tuples, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction). AUD-R34-016: this
    /// terminal builds its own command, and without this overload it could not be enlisted in the
    /// caller's transaction at all.
    /// </summary>
    List<(T1, T2, T3)> SelectAll(CommandOptions options);

    /// <summary>
    /// Returns the first result or throws if empty.
    /// </summary>
    T1 SelectFirst();

    /// <summary>
    /// Returns the first result or throws if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    T1 SelectFirst(CommandOptions options);

    /// <summary>
    /// Returns the first result, or default if empty.
    /// </summary>
    T1? SelectFirstOrDefault();

    /// <summary>
    /// Returns the first result, or default if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    T1? SelectFirstOrDefault(CommandOptions options);

    /// <summary>
    /// Returns the single result or throws if not exactly one.
    /// </summary>
    T1 SelectSingle();

    /// <summary>
    /// Returns the single result or throws if not exactly one, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    T1 SelectSingle(CommandOptions options);

    /// <summary>
    /// Returns the single result, or default if empty. Throws if more than one.
    /// </summary>
    T1? SelectSingleOrDefault();

    /// <summary>
    /// Returns the single result, or default if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction). Throws if more
    /// than one.
    /// </summary>
    T1? SelectSingleOrDefault(CommandOptions options);

    /// <summary>
    /// Returns the count of rows.
    /// </summary>
    int Count();

    /// <summary>
    /// Returns the count of rows, using the specified <see cref="CommandOptions"/> (e.g. to run
    /// within an explicit transaction).
    /// </summary>
    int Count(CommandOptions options);

    /// <summary>
    /// Returns the count of rows as long.
    /// </summary>
    long LongCount();

    /// <summary>
    /// Returns the count of rows as long, using the specified <see cref="CommandOptions"/> (e.g.
    /// to run within an explicit transaction).
    /// </summary>
    long LongCount(CommandOptions options);

    /// <summary>
    /// Returns the generated SQL for debugging purposes.
    /// </summary>
    string ToSql();

    // --- SELECT PARTIAL Operations ---

    /// <summary>
    /// Executes the query selecting only the specified columns.
    /// </summary>
    List<IDictionary<string, object?>> SelectPartial(string columns);

    /// <summary>
    /// Returns the first partial result or throws if empty.
    /// </summary>
    IDictionary<string, object?> SelectPartialFirst(string columns);

    /// <summary>
    /// Returns the first partial result, or null if empty.
    /// </summary>
    IDictionary<string, object?>? SelectPartialFirstOrDefault(string columns);

    /// <summary>
    /// Returns the single partial result or throws if not exactly one.
    /// </summary>
    IDictionary<string, object?> SelectPartialSingle(string columns);

    /// <summary>
    /// Returns the single partial result, or null if empty. Throws if more than one.
    /// </summary>
    IDictionary<string, object?>? SelectPartialSingleOrDefault(string columns);

    // --- Async Operations ---

    /// <summary>
    /// Executes the query asynchronously and returns the primary entity.
    /// </summary>
    Task<List<T1>> SelectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query asynchronously and returns the primary entity, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    Task<List<T1>> SelectAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query asynchronously and returns all three entities as tuples.
    /// </summary>
    Task<List<(T1, T2, T3)>> SelectAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query asynchronously and returns all three entities as tuples, using the
    /// specified <see cref="CommandOptions"/> (AUD-R34-016).
    /// </summary>
    Task<List<(T1, T2, T3)>> SelectAllAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously or throws if empty.
    /// </summary>
    Task<T1> SelectFirstAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously or throws if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    Task<T1> SelectFirstAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously, or default if empty.
    /// </summary>
    Task<T1?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously, or default if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    Task<T1?> SelectFirstOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously or throws if not exactly one.
    /// </summary>
    Task<T1> SelectSingleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously or throws if not exactly one, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    Task<T1> SelectSingleAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously, or default if empty. Throws if more than one.
    /// </summary>
    Task<T1?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously, or default if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction). Throws if more
    /// than one.
    /// </summary>
    Task<T1?> SelectSingleOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows asynchronously.
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows asynchronously, using the specified <see cref="CommandOptions"/>
    /// (e.g. to run within an explicit transaction).
    /// </summary>
    Task<int> CountAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows as long asynchronously.
    /// </summary>
    Task<long> LongCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows as long asynchronously, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    Task<long> LongCountAsync(CommandOptions options, CancellationToken cancellationToken = default);

    // --- SELECT PARTIAL Async Operations ---

    /// <summary>
    /// Executes the query asynchronously selecting only the specified columns.
    /// </summary>
    Task<List<IDictionary<string, object?>>> SelectPartialAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first partial result asynchronously or throws if empty.
    /// </summary>
    Task<IDictionary<string, object?>> SelectPartialFirstAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first partial result asynchronously, or null if empty.
    /// </summary>
    Task<IDictionary<string, object?>?> SelectPartialFirstOrDefaultAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single partial result asynchronously or throws if not exactly one.
    /// </summary>
    Task<IDictionary<string, object?>> SelectPartialSingleAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single partial result asynchronously, or null if empty. Throws if more than one.
    /// </summary>
    Task<IDictionary<string, object?>?> SelectPartialSingleOrDefaultAsync(string columns, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a query with four joined tables.
/// </summary>
public interface IJoinedQuery4<T1, T2, T3, T4>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    // --- WHERE Clauses ---

    /// <summary>
    /// Adds a WHERE clause using a predicate expression.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> Where(Expression<Func<T1, T2, T3, T4, bool>> predicate);

    /// <summary>
    /// Adds a WHERE clause using a raw SQL condition.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> Where(string condition);

    /// <summary>
    /// Adds a WHERE clause using column name and value.
    /// </summary>
    /// <remarks>
    /// AUD-R35-184. <paramref name="value"/> is nullable and a null means <c>IS NULL</c>, matching
    /// the single-table twin <c>QueryBuilder.Where(string, object?)</c>. The parameter used to be
    /// declared non-nullable while a null passed anyway - trivially reachable from a nullable
    /// property or a dictionary lookup - was bound as a parameter, producing <c>col = @p</c> with a
    /// null value. That is UNKNOWN in SQL, not false, so the filter silently matched nothing rather
    /// than the rows the caller meant.
    /// </remarks>
    IJoinedQuery4<T1, T2, T3, T4> Where(string column, object? value);

    /// <summary>
    /// Adds an AND condition using a predicate expression.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> And(Expression<Func<T1, T2, T3, T4, bool>> predicate);

    /// <summary>
    /// Adds an OR condition using a predicate expression.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> Or(Expression<Func<T1, T2, T3, T4, bool>> predicate);

    // --- ORDER BY ---

    /// <summary>
    /// Adds an ORDER BY clause (ascending) for the primary entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> OrderBy<TKey>(Expression<Func<T1, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (descending) for the primary entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> OrderByDescending<TKey>(Expression<Func<T1, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (ascending) for the second entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> OrderByJoined<TKey>(Expression<Func<T2, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (descending) for the second entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> OrderByJoinedDescending<TKey>(Expression<Func<T2, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (ascending) for the third entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> OrderByJoined<TKey>(Expression<Func<T3, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (descending) for the third entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> OrderByJoinedDescending<TKey>(Expression<Func<T3, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (ascending) for the fourth entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> OrderByJoined<TKey>(Expression<Func<T4, TKey>> keySelector);

    /// <summary>
    /// Adds an ORDER BY clause (descending) for the fourth entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> OrderByJoinedDescending<TKey>(Expression<Func<T4, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (ascending) for the primary entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> ThenBy<TKey>(Expression<Func<T1, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (descending) for the primary entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> ThenByDescending<TKey>(Expression<Func<T1, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (ascending) for the second entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> ThenByJoined<TKey>(Expression<Func<T2, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (descending) for the second entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> ThenByJoinedDescending<TKey>(Expression<Func<T2, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (ascending) for the third entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> ThenByJoined<TKey>(Expression<Func<T3, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (descending) for the third entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> ThenByJoinedDescending<TKey>(Expression<Func<T3, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (ascending) for the fourth entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> ThenByJoined<TKey>(Expression<Func<T4, TKey>> keySelector);

    /// <summary>
    /// Adds an additional ORDER BY column (descending) for the fourth entity.
    /// </summary>
    IJoinedQuery4<T1, T2, T3, T4> ThenByJoinedDescending<TKey>(Expression<Func<T4, TKey>> keySelector);

    // --- GROUP BY Operations ---

    /// <summary>
    /// Groups the joined results by a key drawn from any of the four entities, for
    /// HAVING/aggregate Select projections. See
    /// <see cref="IGroupedJoinedQuery4{T1,T2,T3,T4,TKey}"/>.
    /// </summary>
    IGroupedJoinedQuery4<T1, T2, T3, T4, TKey> GroupBy<TKey>(Expression<Func<T1, T2, T3, T4, TKey>> keySelector);

    // --- SELECT Operations ---

    /// <summary>
    /// Executes the query and returns the primary entity.
    /// </summary>
    List<T1> Select();

    /// <summary>
    /// Executes the query and returns the primary entity, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    List<T1> Select(CommandOptions options);

    /// <summary>
    /// Executes the query and returns all four entities as tuples.
    /// </summary>
    List<(T1, T2, T3, T4)> SelectAll();

    /// <summary>
    /// Executes the query and returns all four entities as tuples, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction). AUD-R34-016: this
    /// terminal builds its own command, and without this overload it could not be enlisted in the
    /// caller's transaction at all.
    /// </summary>
    List<(T1, T2, T3, T4)> SelectAll(CommandOptions options);

    /// <summary>
    /// Returns the first result or throws if empty.
    /// </summary>
    T1 SelectFirst();

    /// <summary>
    /// Returns the first result or throws if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    T1 SelectFirst(CommandOptions options);

    /// <summary>
    /// Returns the first result, or default if empty.
    /// </summary>
    T1? SelectFirstOrDefault();

    /// <summary>
    /// Returns the first result, or default if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    T1? SelectFirstOrDefault(CommandOptions options);

    /// <summary>
    /// Returns the single result or throws if not exactly one.
    /// </summary>
    T1 SelectSingle();

    /// <summary>
    /// Returns the single result or throws if not exactly one, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    T1 SelectSingle(CommandOptions options);

    /// <summary>
    /// Returns the single result, or default if empty. Throws if more than one.
    /// </summary>
    T1? SelectSingleOrDefault();

    /// <summary>
    /// Returns the single result, or default if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction). Throws if more
    /// than one.
    /// </summary>
    T1? SelectSingleOrDefault(CommandOptions options);

    /// <summary>
    /// Returns the count of rows.
    /// </summary>
    int Count();

    /// <summary>
    /// Returns the count of rows, using the specified <see cref="CommandOptions"/> (e.g. to run
    /// within an explicit transaction).
    /// </summary>
    int Count(CommandOptions options);

    /// <summary>
    /// Returns the count of rows as long.
    /// </summary>
    long LongCount();

    /// <summary>
    /// Returns the count of rows as long, using the specified <see cref="CommandOptions"/> (e.g.
    /// to run within an explicit transaction).
    /// </summary>
    long LongCount(CommandOptions options);

    /// <summary>
    /// Returns the generated SQL for debugging purposes.
    /// </summary>
    string ToSql();

    // --- SELECT PARTIAL Operations ---

    /// <summary>
    /// Executes the query selecting only the specified columns.
    /// </summary>
    List<IDictionary<string, object?>> SelectPartial(string columns);

    /// <summary>
    /// Returns the first partial result or throws if empty.
    /// </summary>
    IDictionary<string, object?> SelectPartialFirst(string columns);

    /// <summary>
    /// Returns the first partial result, or null if empty.
    /// </summary>
    IDictionary<string, object?>? SelectPartialFirstOrDefault(string columns);

    /// <summary>
    /// Returns the single partial result or throws if not exactly one.
    /// </summary>
    IDictionary<string, object?> SelectPartialSingle(string columns);

    /// <summary>
    /// Returns the single partial result, or null if empty. Throws if more than one.
    /// </summary>
    IDictionary<string, object?>? SelectPartialSingleOrDefault(string columns);

    // --- Async Operations ---

    /// <summary>
    /// Executes the query asynchronously and returns the primary entity.
    /// </summary>
    Task<List<T1>> SelectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query asynchronously and returns the primary entity, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    Task<List<T1>> SelectAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query asynchronously and returns all four entities as tuples.
    /// </summary>
    Task<List<(T1, T2, T3, T4)>> SelectAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query asynchronously and returns all four entities as tuples, using the
    /// specified <see cref="CommandOptions"/> (AUD-R34-016).
    /// </summary>
    Task<List<(T1, T2, T3, T4)>> SelectAllAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously or throws if empty.
    /// </summary>
    Task<T1> SelectFirstAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously or throws if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    Task<T1> SelectFirstAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously, or default if empty.
    /// </summary>
    Task<T1?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result asynchronously, or default if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    Task<T1?> SelectFirstOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously or throws if not exactly one.
    /// </summary>
    Task<T1> SelectSingleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously or throws if not exactly one, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    Task<T1> SelectSingleAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously, or default if empty. Throws if more than one.
    /// </summary>
    Task<T1?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single result asynchronously, or default if empty, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction). Throws if more
    /// than one.
    /// </summary>
    Task<T1?> SelectSingleOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows asynchronously.
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows asynchronously, using the specified <see cref="CommandOptions"/>
    /// (e.g. to run within an explicit transaction).
    /// </summary>
    Task<int> CountAsync(CommandOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows as long asynchronously.
    /// </summary>
    Task<long> LongCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of rows as long asynchronously, using the specified
    /// <see cref="CommandOptions"/> (e.g. to run within an explicit transaction).
    /// </summary>
    Task<long> LongCountAsync(CommandOptions options, CancellationToken cancellationToken = default);

    // --- SELECT PARTIAL Async Operations ---

    /// <summary>
    /// Executes the query asynchronously selecting only the specified columns.
    /// </summary>
    Task<List<IDictionary<string, object?>>> SelectPartialAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first partial result asynchronously or throws if empty.
    /// </summary>
    Task<IDictionary<string, object?>> SelectPartialFirstAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first partial result asynchronously, or null if empty.
    /// </summary>
    Task<IDictionary<string, object?>?> SelectPartialFirstOrDefaultAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single partial result asynchronously or throws if not exactly one.
    /// </summary>
    Task<IDictionary<string, object?>> SelectPartialSingleAsync(string columns, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single partial result asynchronously, or null if empty. Throws if more than one.
    /// </summary>
    Task<IDictionary<string, object?>?> SelectPartialSingleOrDefaultAsync(string columns, CancellationToken cancellationToken = default);
}