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

    /// <summary>
    /// Starts a fluent INSERT builder for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type to insert.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <returns>A fluent insert builder for specifying values and executing the insert.</returns>
    /// <example>
    /// <code>
    /// // Insert with entity
    /// var id = db.Into&lt;Product&gt;()
    ///     .Values(new Product { ProductName = "Widget", UnitPrice = 9.99m })
    ///     .Insert();
    ///
    /// // Insert with anonymous object
    /// var id = db.Into&lt;Product&gt;()
    ///     .Values(new { ProductName = "Widget", UnitPrice = 9.99m })
    ///     .Insert();
    ///
    /// // Insert with individual values
    /// var id = db.Into&lt;Product&gt;()
    ///     .Value(p => p.ProductName, "Widget")
    ///     .Value(p => p.UnitPrice, 9.99m)
    ///     .Insert();
    /// </code>
    /// </example>
    public static IIntoClause<T> Into<T>(this IDbConnection connection) where T : new()
    {
        return new InsertBuilder<T>(connection);
    }

    /// <summary>
    /// Starts a fluent CTE (Common Table Expression) builder for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type for the CTE.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="cteName">The name of the CTE.</param>
    /// <returns>A fluent CTE builder for defining and querying the CTE.</returns>
    /// <example>
    /// <code>
    /// // Simple CTE: query expensive products
    /// var expensiveProducts = db.Cte&lt;Product&gt;("ExpensiveProducts")
    ///     .As(q => q.Where(p => p.UnitPrice > 100))
    ///     .Select();
    /// // SQL: WITH ExpensiveProducts AS (SELECT * FROM products WHERE unit_price > 100)
    /// //      SELECT * FROM ExpensiveProducts
    ///
    /// // CTE with additional filtering on results
    /// var results = db.Cte&lt;Product&gt;("ExpensiveProducts")
    ///     .As(q => q.Where(p => p.UnitPrice > 100))
    ///     .Where(p => p.CategoryId == 1)
    ///     .OrderByDescending(p => p.UnitPrice)
    ///     .Take(10)
    ///     .Select();
    /// // SQL: WITH ExpensiveProducts AS (SELECT * FROM products WHERE unit_price > 100)
    /// //      SELECT * FROM ExpensiveProducts WHERE category_id = 1 ORDER BY unit_price DESC LIMIT 10
    /// </code>
    /// </example>
    public static ICteClause<T> Cte<T>(this IDbConnection connection, string cteName) where T : new()
    {
        return new CteBuilder<T>(connection, cteName);
    }
}
