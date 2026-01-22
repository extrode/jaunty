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
    /// </summary>
    /// <typeparam name="T">Must be either <typeparamref name="TFrom"/> or <typeparamref name="TJoin"/>.</typeparam>
    /// <example>
    /// <code>
    /// // Select only products (same as Select())
    /// var products = db.From&lt;Product&gt;()
    ///     .InnerJoin&lt;Category&gt;()
    ///     .On(p =&gt; p.CategoryId, c =&gt; c.CategoryId)
    ///     .Select&lt;Product&gt;();
    ///
    /// // Select only categories
    /// var categories = db.From&lt;Product&gt;()
    ///     .InnerJoin&lt;Category&gt;()
    ///     .On(p =&gt; p.CategoryId, c =&gt; c.CategoryId)
    ///     .Select&lt;Category&gt;();
    /// </code>
    /// </example>
    List<T> Select<T>() where T : new();

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
    /// Executes the query with a custom projection.
    /// </summary>
    List<TResult> Select<TResult>(Func<TFrom, TJoin, TResult> mapper);

    /// <summary>
    /// Returns the first result of the specified type or throws if empty.
    /// </summary>
    /// <typeparam name="T">Must be either <typeparamref name="TFrom"/> or <typeparamref name="TJoin"/>.</typeparam>
    T SelectFirst<T>() where T : new();

    /// <summary>
    /// Returns the first result of the specified type, or default if empty.
    /// </summary>
    /// <typeparam name="T">Must be either <typeparamref name="TFrom"/> or <typeparamref name="TJoin"/>.</typeparam>
    T? SelectFirstOrDefault<T>() where T : new();

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
    /// </summary>
    /// <typeparam name="T">Must be either <typeparamref name="TFrom"/> or <typeparamref name="TJoin"/>.</typeparam>
    Task<List<T>> SelectAsync<T>(CancellationToken cancellationToken = default) where T : new();

    /// <summary>
    /// Executes the query asynchronously and returns both entities as tuples.
    /// </summary>
    /// <typeparam name="T1">Must be <typeparamref name="TFrom"/>.</typeparam>
    /// <typeparam name="T2">Must be <typeparamref name="TJoin"/>.</typeparam>
    Task<List<(T1, T2)>> SelectAsync<T1, T2>(CancellationToken cancellationToken = default) where T1 : new() where T2 : new();

    /// <summary>
    /// Executes the query asynchronously with a custom projection.
    /// </summary>
    Task<List<TResult>> SelectAsync<TResult>(Func<TFrom, TJoin, TResult> mapper, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the first result of the specified type asynchronously or throws if empty.
    /// </summary>
    /// <typeparam name="T">Must be either <typeparamref name="TFrom"/> or <typeparamref name="TJoin"/>.</typeparam>
    Task<T> SelectFirstAsync<T>(CancellationToken cancellationToken = default) where T : new();

    /// <summary>
    /// Returns the first result of the specified type asynchronously, or default if empty.
    /// </summary>
    /// <typeparam name="T">Must be either <typeparamref name="TFrom"/> or <typeparamref name="TJoin"/>.</typeparam>
    Task<T?> SelectFirstOrDefaultAsync<T>(CancellationToken cancellationToken = default) where T : new();

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
}

/// <summary>
/// Represents a JOIN clause for a third table.
/// </summary>
public interface IJoinClause<T1, T2, T3> where T1 : new() where T2 : new() where T3 : new()
{
    /// <summary>
    /// Specifies the join condition using key expressions.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> On<TLeftKey, TRightKey>(Expression<Func<T1, TLeftKey>> leftKey, Expression<Func<T3, TRightKey>> rightKey);

    /// <summary>
    /// Specifies the join condition using key from T2.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> OnFromSecond<TLeftKey, TRightKey>(Expression<Func<T2, TLeftKey>> leftKey, Expression<Func<T3, TRightKey>> rightKey);

    /// <summary>
    /// Specifies the join condition using column names.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> OnColumns(string leftColumn, string rightColumn);

    /// <summary>
    /// Specifies the join condition using raw SQL.
    /// </summary>
    IJoinedQuery3<T1, T2, T3> OnRaw(string condition);
}

/// <summary>
/// Represents a query with three joined tables.
/// </summary>
public interface IJoinedQuery3<T1, T2, T3> where T1 : new() where T2 : new() where T3 : new()
{
    IJoinedQuery3<T1, T2, T3> Where(Expression<Func<T1, T2, T3, bool>> predicate);
    IJoinedQuery3<T1, T2, T3> Where(string condition);

    List<T1> Select();
    List<(T1, T2, T3)> SelectAll();
    List<TResult> Select<TResult>(Func<T1, T2, T3, TResult> mapper);

    string ToSql();
}
