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
    /// db.From<Product>()
    ///     .InnerJoin<Category>()
    ///     .On(p => p.CategoryId, c => c.Id)
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
    /// db.From<Product>()
    ///     .InnerJoin<Category>()
    ///     .On((p, c) => p.CategoryId == c.Id)
    ///     .Select();
    /// </code>
    /// </example>
    IJoinedQuery<TFrom, TJoin> On(Expression<Func<TFrom, TJoin, bool>> predicate);

    /// <summary>
    /// Specifies the join condition using a raw SQL condition.
    /// </summary>
    /// <param name="condition">The raw SQL join condition.</param>
    /// <returns>A joined query builder for further operations.</returns>
    /// <example>
    /// <code>
    /// db.From<Product>("p")
    ///     .InnerJoin<Category>("c")
    ///     .On("p.category_id = c.id AND p.active = 1")
    ///     .Select();
    /// </code>
    /// </example>
    IJoinedQuery<TFrom, TJoin> On(string condition);

    /// <summary>
    /// Specifies the join condition using a raw SQL condition with parameters.
    /// </summary>
    /// <param name="condition">The raw SQL join condition with parameter placeholders.</param>
    /// <param name="parameters">An anonymous object containing parameter values.</param>
    /// <returns>A joined query builder for further operations.</returns>
    /// <example>
    /// <code>
    /// db.From<Product>("p")
    ///     .InnerJoin<Category>("c")
    ///     .On("p.category_id = c.id AND p.active = @active", new { active = 1 })
    ///     .Select();
    /// </code>
    /// </example>
    IJoinedQuery<TFrom, TJoin> On(string condition, object parameters);

    /// <summary>
    /// Specifies the join condition using column names (equality join).
    /// </summary>
    /// <param name="leftColumn">The left column (e.g., "p.category_id" or "products.category_id").</param>
    /// <param name="rightColumn">The right column (e.g., "c.id" or "categories.id").</param>
    /// <returns>A joined query builder for further operations.</returns>
    /// <example>
    /// <code>
    /// db.From<Product>("p")
    ///     .InnerJoin<Category>("c")
    ///     .OnColumns("p.category_id", "c.id")
    ///     .Select();
    /// </code>
    /// </example>
    IJoinedQuery<TFrom, TJoin> OnColumns(string leftColumn, string rightColumn);
}
