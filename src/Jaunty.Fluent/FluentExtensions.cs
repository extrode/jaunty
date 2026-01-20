using System.Data;

namespace Jaunty.Fluent;

/// <summary>
/// Entry point extension methods for the Jaunty Fluent API.
/// </summary>
public static class FluentExtensions
{
    /// <summary>
    /// Starts a fluent query builder for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type to query.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="alias">Optional alias for the table (used in string-based join conditions).</param>
    /// <returns>A fluent query builder for chaining WHERE, ORDER BY, JOIN, and SELECT operations.</returns>
    /// <example>
    /// <code>
    /// // Select all columns (strict mapping)
    /// var products = db.From&lt;Product&gt;().Select();
    ///
    /// // Select specific columns (partial mapping)
    /// var products = db.From&lt;Product&gt;().SelectPartial("Id", "ProductName", "CategoryId");
    /// var products = db.From&lt;Product&gt;().SelectPartial(p => p.Id, p => p.ProductName, p => p.CategoryId);
    ///
    /// // With WHERE clause
    /// var products = db.From&lt;Product&gt;()
    ///     .Where(p => p.CategoryId == 1)
    ///     .SelectPartial("Id", "ProductName");
    ///
    /// // With ORDER BY
    /// var products = db.From&lt;Product&gt;()
    ///     .Where("CategoryId", 1)
    ///     .OrderBy(p => p.ProductName)
    ///     .Select();
    ///
    /// // With JOIN (expression-based)
    /// var products = db.From&lt;Product&gt;()
    ///     .InnerJoin&lt;Category&gt;()
    ///     .On(p => p.CategoryId, c => c.Id)
    ///     .Select();
    ///
    /// // With JOIN (string-based with aliases)
    /// var products = db.From&lt;Product&gt;("p")
    ///     .InnerJoin&lt;Category&gt;("c")
    ///     .On("p.category_id", "c.id")
    ///     .Select();
    /// </code>
    /// </example>
    public static IFromClause<T> From<T>(this IDbConnection connection, string? alias = null) where T : new()
    {
        return new QueryBuilder<T>(connection, alias);
    }
}
