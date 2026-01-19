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
    /// <returns>A fluent query builder for chaining WHERE, ORDER BY, and SELECT operations.</returns>
    /// <example>
    /// <code>
    /// // Select all columns (strict mapping)
    /// var products = db.From<Product>().Select();
    ///
    /// // Select specific columns (partial mapping)
    /// var products = db.From<Product>().SelectPartial("Id", "ProductName", "CategoryId");
    /// var products = db.From<Product>().SelectPartial(p => p.Id, p => p.ProductName, p => p.CategoryId);
    ///
    /// // With WHERE clause
    /// var products = db.From<Product>()
    ///     .Where(p => p.CategoryId == 1)
    ///     .SelectPartial("Id", "ProductName");
    ///
    /// // With ORDER BY
    /// var products = db.From<Product>()
    ///     .Where("CategoryId", 1)
    ///     .OrderBy(p => p.ProductName)
    ///     .Select();
    /// </code>
    /// </example>
    public static IFromClause<T> From<T>(this IDbConnection connection) where T : new()
    {
        return new QueryBuilder<T>(connection);
    }
}
