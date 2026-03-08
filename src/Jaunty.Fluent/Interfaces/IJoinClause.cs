using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Represents a JOIN clause that needs an ON condition.
/// </summary>
/// <typeparam name="TFrom">The primary (left) entity type.</typeparam>
/// <typeparam name="TJoin">The joined (right) entity type.</typeparam>
public interface IJoinClause<TFrom, TJoin> where TFrom : new() where TJoin : new()
{
    /// <summary>
    /// Specifies the join condition using key expressions (equality join).
    /// </summary>
    /// <typeparam name="TLeftKey">The left key type.</typeparam>
    /// <typeparam name="TRightKey">The right key type.</typeparam>
    /// <param name="leftKey">Expression selecting the key from the left entity.</param>
    /// <param name="rightKey">Expression selecting the key from the right entity.</param>
    /// <returns>A joined query builder for further operations.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Product&gt;()
    ///     .InnerJoin&lt;Category&gt;()
    ///     .On(p =&gt; p.CategoryId, c =&gt; c.Id)
    ///     .Select();
    /// </code>
    /// </example>
    IJoinedQuery<TFrom, TJoin> On<TLeftKey, TRightKey>(Expression<Func<TFrom, TLeftKey>> leftKey, Expression<Func<TJoin, TRightKey>> rightKey);

    /// <summary>
    /// Specifies the join condition using a predicate expression.
    /// </summary>
    /// <param name="predicate">Expression defining the join condition.</param>
    /// <returns>A joined query builder for further operations.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Product&gt;()
    ///     .InnerJoin&lt;Category&gt;()
    ///     .On((p, c) =&gt; p.CategoryId == c.Id)
    ///     .Select();
    /// </code>
    /// </example>
    IJoinedQuery<TFrom, TJoin> On(Expression<Func<TFrom, TJoin, bool>> predicate);

    /// <summary>
    /// Specifies the join condition using column names (equality join).
    /// </summary>
    /// <param name="leftColumn">The left column (e.g., "p.category_id").</param>
    /// <param name="rightColumn">The right column (e.g., "c.id").</param>
    /// <returns>A joined query builder for further operations.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Product&gt;("p")
    ///     .InnerJoin&lt;Category&gt;("c")
    ///     .On("p.category_id", "c.id")
    ///     .Select();
    /// </code>
    /// </example>
    IJoinedQuery<TFrom, TJoin> On(string leftColumn, string rightColumn);

    /// <summary>
    /// Specifies the join condition using a raw SQL condition.
    /// </summary>
    /// <param name="condition">The raw SQL join condition.</param>
    /// <returns>A joined query builder for further operations.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Product&gt;("p")
    ///     .InnerJoin&lt;Category&gt;("c")
    ///     .On("p.category_id = c.id AND p.active = 1")
    ///     .Select();
    /// </code>
    /// </example>
    IJoinedQuery<TFrom, TJoin> On(string condition);

    /// <summary>
    /// Specifies the join condition using a raw SQL condition with a typed parameter.
    /// </summary>
    /// <typeparam name="TValue">The type of the parameter value.</typeparam>
    /// <param name="condition">The raw SQL join condition with a @value placeholder.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>A joined query builder for further operations.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Product&gt;("p")
    ///     .InnerJoin&lt;Category&gt;("c")
    ///     .On&lt;int&gt;("p.category_id = c.id AND p.active = @value", 1)
    ///     .Select();
    /// </code>
    /// </example>
    IJoinedQuery<TFrom, TJoin> On<TValue>(string condition, TValue value);
}