using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Provides SQL function markers for use in fluent query expressions.
/// These methods are translated to their SQL equivalents during query building.
/// </summary>
/// <remarks>
/// Methods in this class are not meant to be executed directly - they serve as
/// markers that are detected and translated by expression visitors during SQL generation.
/// Using these methods outside of Jaunty expressions will throw exceptions.
/// </remarks>
[ExcludeFromCodeCoverage]
public static partial class Sql
{
    /// <summary>
    /// Returns the first non-null value from the provided arguments.
    /// Translates to SQL COALESCE function.
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    /// <param name="value">The primary value to check.</param>
    /// <param name="defaultValue">The fallback value if primary is null.</param>
    /// <returns>This method is a marker and should not be called directly.</returns>
    /// <example>
    /// <code>
    /// // In a WHERE clause
    /// db.From&lt;Product&gt;()
    ///   .Where(p => Sql.Coalesce(p.UnitsInStock, 0) > 10)
    ///   .Select();
    ///
    /// // Generates: WHERE COALESCE(units_in_stock, 0) > 10
    /// </code>
    /// </example>
    public static T Coalesce<T>(T? value, T defaultValue)
    {
        throw new InvalidOperationException(
            "Sql.Coalesce is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Returns the first non-null value from the provided arguments.
    /// Translates to SQL COALESCE function with multiple fallbacks.
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    /// <param name="value1">The primary value to check.</param>
    /// <param name="value2">The first fallback value.</param>
    /// <param name="value3">The second fallback value.</param>
    /// <returns>This method is a marker and should not be called directly.</returns>
    public static T Coalesce<T>(T? value1, T? value2, T value3)
    {
        throw new InvalidOperationException(
            "Sql.Coalesce is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Replaces null with a specified value.
    /// Translates to ISNULL (SQL Server) or IFNULL (SQLite/MySQL) or COALESCE (PostgreSQL).
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    /// <param name="value">The value to check for null.</param>
    /// <param name="defaultValue">The value to return if the first argument is null.</param>
    /// <returns>This method is a marker and should not be called directly.</returns>
    /// <example>
    /// <code>
    /// // In a WHERE clause
    /// db.From&lt;Product&gt;()
    ///   .Where(p => Sql.IsNull(p.UnitPrice, 0m) > 10m)
    ///   .Select();
    ///
    /// // SQL Server generates: WHERE ISNULL(unit_price, 0) > 10
    /// // SQLite generates: WHERE IFNULL(unit_price, 0) > 10
    /// </code>
    /// </example>
    public static T IsNull<T>(T? value, T defaultValue)
    {
        throw new InvalidOperationException(
            "Sql.IsNull is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Returns null if two values are equal, otherwise returns the first value.
    /// Translates to SQL NULLIF function.
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="compareValue">The value to compare against.</param>
    /// <returns>This method is a marker and should not be called directly.</returns>
    /// <example>
    /// <code>
    /// // Useful for avoiding division by zero
    /// db.From&lt;Product&gt;()
    ///   .Where(p => p.UnitPrice / Sql.NullIf(p.UnitsInStock, (short)0) > 10)
    ///   .Select();
    ///
    /// // Generates: WHERE unit_price / NULLIF(units_in_stock, 0) > 10
    /// </code>
    /// </example>
    public static T NullIf<T>(T value, T compareValue)
    {
        throw new InvalidOperationException(
            "Sql.NullIf is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    // ==========================================
    // String Functions
    // ==========================================

    /// <summary>
    /// Returns the length of a string.
    /// Translates to LEN (SQL Server) or LENGTH (SQLite/PostgreSQL/MySQL).
    /// </summary>
    /// <param name="value">The string value to measure.</param>
    /// <returns>This method is a marker and should not be called directly.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Product&gt;()
    ///   .Where(p => Sql.Length(p.ProductName) > 10)
    ///   .Select();
    /// // SQL Server: WHERE LEN(product_name) > 10
    /// // SQLite/PostgreSQL: WHERE LENGTH(product_name) > 10
    /// </code>
    /// </example>
    public static int Length(string? value)
    {
        throw new InvalidOperationException(
            "Sql.Length is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Converts a string to uppercase.
    /// Translates to SQL UPPER function.
    /// </summary>
    /// <param name="value">The string value to convert.</param>
    /// <returns>This method is a marker and should not be called directly.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Product&gt;()
    ///   .Where(p => Sql.Upper(p.ProductName) == "CHAI")
    ///   .Select();
    /// // Generates: WHERE UPPER(product_name) = 'CHAI'
    /// </code>
    /// </example>
    public static string Upper(string? value)
    {
        throw new InvalidOperationException(
            "Sql.Upper is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Converts a string to lowercase.
    /// Translates to SQL LOWER function.
    /// </summary>
    /// <param name="value">The string value to convert.</param>
    /// <returns>This method is a marker and should not be called directly.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Product&gt;()
    ///   .Where(p => Sql.Lower(p.ProductName) == "chai")
    ///   .Select();
    /// // Generates: WHERE LOWER(product_name) = 'chai'
    /// </code>
    /// </example>
    public static string Lower(string? value)
    {
        throw new InvalidOperationException(
            "Sql.Lower is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Removes leading and trailing whitespace from a string.
    /// Translates to TRIM (standard SQL) or LTRIM/RTRIM combination for older SQL Server.
    /// </summary>
    /// <param name="value">The string value to trim.</param>
    /// <returns>This method is a marker and should not be called directly.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Product&gt;()
    ///   .Where(p => Sql.Trim(p.ProductName) == "Chai")
    ///   .Select();
    /// // Generates: WHERE TRIM(product_name) = 'Chai'
    /// </code>
    /// </example>
    public static string Trim(string? value)
    {
        throw new InvalidOperationException(
            "Sql.Trim is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Extracts a substring from a string.
    /// Translates to SUBSTR (SQLite) or SUBSTRING (SQL Server/PostgreSQL/MySQL).
    /// </summary>
    /// <param name="value">The string value to extract from.</param>
    /// <param name="start">The 1-based starting position.</param>
    /// <param name="length">The number of characters to extract.</param>
    /// <returns>This method is a marker and should not be called directly.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Product&gt;()
    ///   .Where(p => Sql.Substring(p.ProductName, 1, 3) == "Cha")
    ///   .Select();
    /// // SQL Server: WHERE SUBSTRING(product_name, 1, 3) = 'Cha'
    /// // SQLite: WHERE SUBSTR(product_name, 1, 3) = 'Cha'
    /// </code>
    /// </example>
    public static string Substring(string? value, int start, int length)
    {
        throw new InvalidOperationException(
            "Sql.Substring is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    // ==========================================
    // Date Functions
    // ==========================================

    /// <summary>
    /// Extracts the year from a date/datetime value.
    /// Translates to YEAR (SQL Server/MySQL) or strftime (SQLite) or EXTRACT (PostgreSQL).
    /// </summary>
    /// <param name="value">The date/datetime value to extract from.</param>
    /// <returns>This method is a marker and should not be called directly.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Order&gt;()
    ///   .Where(o => Sql.Year(o.OrderDate) == 2024)
    ///   .Select();
    /// // SQL Server: WHERE YEAR(order_date) = 2024
    /// // SQLite: WHERE CAST(strftime('%Y', order_date) AS INTEGER) = 2024
    /// </code>
    /// </example>
    public static int Year(DateTime? value)
    {
        throw new InvalidOperationException(
            "Sql.Year is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Extracts the month from a date/datetime value.
    /// Translates to MONTH (SQL Server/MySQL) or strftime (SQLite) or EXTRACT (PostgreSQL).
    /// </summary>
    /// <param name="value">The date/datetime value to extract from.</param>
    /// <returns>This method is a marker and should not be called directly.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Order&gt;()
    ///   .Where(o => Sql.Month(o.OrderDate) == 12)
    ///   .Select();
    /// // SQL Server: WHERE MONTH(order_date) = 12
    /// </code>
    /// </example>
    public static int Month(DateTime? value)
    {
        throw new InvalidOperationException(
            "Sql.Month is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Extracts the day of month from a date/datetime value.
    /// Translates to DAY (SQL Server/MySQL) or strftime (SQLite) or EXTRACT (PostgreSQL).
    /// </summary>
    /// <param name="value">The date/datetime value to extract from.</param>
    /// <returns>This method is a marker and should not be called directly.</returns>
    /// <example>
    /// <code>
    /// db.From&lt;Order&gt;()
    ///   .Where(o => Sql.Day(o.OrderDate) == 15)
    ///   .Select();
    /// // SQL Server: WHERE DAY(order_date) = 15
    /// </code>
    /// </example>
    public static int Day(DateTime? value)
    {
        throw new InvalidOperationException(
            "Sql.Day is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    // ==========================================
    // CASE/WHEN Expressions
    // ==========================================

    /// <summary>
    /// Starts a CASE expression for conditional logic in SQL.
    /// Use .When() to add conditions and .Else() to complete the expression.
    /// </summary>
    /// <typeparam name="TFrom">The type of the entity being queried from.</typeparam>
    /// <typeparam name="TResult">The type of the CASE expression result.</typeparam>
    /// <returns>A CaseBuilder to chain When/Else clauses.</returns>
    /// <example>
    /// <code>
    /// // Categorize products by price in WHERE clause
    /// db.From&lt;Product&gt;()
    ///   .Where(p => Sql.Case&lt;Product, string&gt;()
    ///       .When(x => x.UnitPrice &lt; 10, "Budget")
    ///       .When(x => x.UnitPrice &lt; 50, "Standard")
    ///       .Else("Premium") == "Budget")
    ///   .Select();
    ///
    /// // SQL: WHERE CASE WHEN unit_price &lt; 10 THEN 'Budget'
    /// //           WHEN unit_price &lt; 50 THEN 'Standard'
    /// //           ELSE 'Premium' END = 'Budget'
    /// </code>
    /// </example>
    public static CaseBuilder<TFrom, TResult> Case<TFrom, TResult>()
    {
        throw new InvalidOperationException(
            "Sql.Case is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }
}

/// <summary>
/// Builder for SQL CASE expressions. Used as a marker pattern for expression translation.
/// </summary>
/// <typeparam name="TFrom">The type of the entity being queried from.</typeparam>
/// <typeparam name="TResult">The type of the CASE expression result.</typeparam>
[ExcludeFromCodeCoverage]
public sealed class CaseBuilder<TFrom, TResult>
{
    /// <summary>
    /// Adds a WHEN clause to the CASE expression.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="result">The result if the condition is true.</param>
    /// <returns>The CaseBuilder for chaining.</returns>
    public CaseBuilder<TFrom, TResult> When(Expression<Func<TFrom, bool>> condition, TResult result)
    {
        throw new InvalidOperationException(
            "CaseBuilder.When is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Adds the ELSE clause and completes the CASE expression.
    /// </summary>
    /// <param name="defaultResult">The result if no WHEN conditions match.</param>
    /// <returns>The final result type for comparison in WHERE clauses.</returns>
    public TResult Else(TResult defaultResult)
    {
        throw new InvalidOperationException(
            "CaseBuilder.Else is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Completes the CASE expression without an ELSE clause (result is NULL if no WHEN matches).
    /// </summary>
    /// <returns>The final result type for comparison in WHERE clauses.</returns>
    public TResult End()
    {
        throw new InvalidOperationException(
            "CaseBuilder.End is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }
}

// ==========================================
// Window Functions (partial class extension)
// ==========================================

public static partial class Sql
{
    // ==========================================
    // Window Ranking Functions
    // ==========================================

    /// <summary>
    /// Returns a sequential row number starting from 1 within a partition.
    /// Use with .PartitionBy() and .OrderBy() to define the window.
    /// Translates to SQL ROW_NUMBER() OVER(...) function.
    /// </summary>
    /// <returns>A WindowBuilder for chaining PARTITION BY and ORDER BY clauses.</returns>
    /// <example>
    /// <code>
    /// // Number products within each category by price
    /// var ranked = db.From&lt;Product&gt;()
    ///     .Select(p => new {
    ///         p.ProductName,
    ///         p.CategoryId,
    ///         RowNum = Sql.RowNumber&lt;Product&gt;()
    ///             .PartitionBy(p => p.CategoryId)
    ///             .OrderBy(p => p.UnitPrice)
    ///     });
    ///
    /// // SQL: SELECT product_name, category_id,
    /// //      ROW_NUMBER() OVER (PARTITION BY category_id ORDER BY unit_price) AS RowNum
    /// //      FROM products
    /// </code>
    /// </example>
    public static WindowBuilder<TFrom, long> RowNumber<TFrom>()
    {
        throw new InvalidOperationException(
            "Sql.RowNumber is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Returns the rank of a row within a partition, with gaps for ties.
    /// Use with .PartitionBy() and .OrderBy() to define the window.
    /// Translates to SQL RANK() OVER(...) function.
    /// </summary>
    /// <returns>A WindowBuilder for chaining PARTITION BY and ORDER BY clauses.</returns>
    /// <example>
    /// <code>
    /// // Rank products by price (ties get same rank, next rank is skipped)
    /// var ranked = db.From&lt;Product&gt;()
    ///     .Select(p => new {
    ///         p.ProductName,
    ///         Rank = Sql.Rank&lt;Product&gt;().OrderBy(p => p.UnitPrice)
    ///     });
    ///
    /// // SQL: SELECT product_name,
    /// //      RANK() OVER (ORDER BY unit_price) AS Rank
    /// //      FROM products
    /// </code>
    /// </example>
    public static WindowBuilder<TFrom, long> Rank<TFrom>()
    {
        throw new InvalidOperationException(
            "Sql.Rank is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Returns the rank of a row within a partition, without gaps for ties.
    /// Use with .PartitionBy() and .OrderBy() to define the window.
    /// Translates to SQL DENSE_RANK() OVER(...) function.
    /// </summary>
    /// <returns>A WindowBuilder for chaining PARTITION BY and ORDER BY clauses.</returns>
    /// <example>
    /// <code>
    /// // Dense rank products by price (ties get same rank, no gaps)
    /// var ranked = db.From&lt;Product&gt;()
    ///     .Select(p => new {
    ///         p.ProductName,
    ///         DenseRank = Sql.DenseRank&lt;Product&gt;().OrderByDescending(p => p.UnitPrice)
    ///     });
    ///
    /// // SQL: SELECT product_name,
    /// //      DENSE_RANK() OVER (ORDER BY unit_price DESC) AS DenseRank
    /// //      FROM products
    /// </code>
    /// </example>
    public static WindowBuilder<TFrom, long> DenseRank<TFrom>()
    {
        throw new InvalidOperationException(
            "Sql.DenseRank is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Distributes rows into a specified number of groups (buckets).
    /// Use with .PartitionBy() and .OrderBy() to define the window.
    /// Translates to SQL NTILE(n) OVER(...) function.
    /// </summary>
    /// <param name="buckets">The number of groups to distribute rows into.</param>
    /// <returns>A WindowBuilder for chaining PARTITION BY and ORDER BY clauses.</returns>
    /// <example>
    /// <code>
    /// // Divide products into 4 quartiles by price
    /// var quartiles = db.From&lt;Product&gt;()
    ///     .Select(p => new {
    ///         p.ProductName,
    ///         Quartile = Sql.NTile&lt;Product&gt;(4).OrderBy(p => p.UnitPrice)
    ///     });
    ///
    /// // SQL: SELECT product_name,
    /// //      NTILE(4) OVER (ORDER BY unit_price) AS Quartile
    /// //      FROM products
    /// </code>
    /// </example>
    public static WindowBuilder<TFrom, long> NTile<TFrom>(int buckets)
    {
        throw new InvalidOperationException(
            "Sql.NTile is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    // ==========================================
    // Window Aggregate Functions
    // ==========================================

    /// <summary>
    /// Creates a windowed SUM aggregate that can be used with OVER clause.
    /// Use .Over() to convert to a window function with PARTITION BY and ORDER BY.
    /// </summary>
    /// <typeparam name="T">The numeric type to sum.</typeparam>
    /// <param name="column">The column to sum.</param>
    /// <returns>A WindowAggregateBuilder for chaining .Over() clause.</returns>
    /// <example>
    /// <code>
    /// // Running total of freight by order date
    /// var running = db.From&lt;Order&gt;()
    ///     .Select(o => new {
    ///         o.OrderDate,
    ///         o.Freight,
    ///         RunningTotal = Sql.Sum&lt;Order, decimal?&gt;(o.Freight).Over().OrderBy(o => o.OrderDate)
    ///     });
    ///
    /// // SQL: SELECT order_date, freight,
    /// //      SUM(freight) OVER (ORDER BY order_date) AS RunningTotal
    /// //      FROM orders
    /// </code>
    /// </example>
    public static WindowAggregateBuilder<TFrom, T> Sum<TFrom, T>(T column)
    {
        throw new InvalidOperationException(
            "Sql.Sum is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Creates a windowed AVG aggregate that can be used with OVER clause.
    /// Use .Over() to convert to a window function with PARTITION BY and ORDER BY.
    /// </summary>
    /// <typeparam name="T">The numeric type to average.</typeparam>
    /// <param name="column">The column to average.</param>
    /// <returns>A WindowAggregateBuilder for chaining .Over() clause.</returns>
    /// <example>
    /// <code>
    /// // Moving average of freight within each category
    /// var moving = db.From&lt;Order&gt;()
    ///     .Select(o => new {
    ///         o.OrderDate,
    ///         MovingAvg = Sql.Avg&lt;Order, decimal?&gt;(o.Freight).Over()
    ///             .PartitionBy(o => o.ShipCountry)
    ///             .OrderBy(o => o.OrderDate)
    ///     });
    ///
    /// // SQL: SELECT order_date,
    /// //      AVG(freight) OVER (PARTITION BY ship_country ORDER BY order_date) AS MovingAvg
    /// //      FROM orders
    /// </code>
    /// </example>
    public static WindowAggregateBuilder<TFrom, T> Avg<TFrom, T>(T column)
    {
        throw new InvalidOperationException(
            "Sql.Avg is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Creates a windowed COUNT aggregate that can be used with OVER clause.
    /// Use .Over() to convert to a window function with PARTITION BY and ORDER BY.
    /// </summary>
    /// <returns>A WindowAggregateBuilder for chaining .Over() clause.</returns>
    /// <example>
    /// <code>
    /// // Count of orders per customer
    /// var counts = db.From&lt;Order&gt;()
    ///     .Select(o => new {
    ///         o.OrderId,
    ///         o.CustomerId,
    ///         CustomerOrderCount = Sql.Count&lt;Order&gt;().Over().PartitionBy(o => o.CustomerId)
    ///     });
    ///
    /// // SQL: SELECT order_id, customer_id,
    /// //      COUNT(*) OVER (PARTITION BY customer_id) AS CustomerOrderCount
    /// //      FROM orders
    /// </code>
    /// </example>
    public static WindowAggregateBuilder<TFrom, long> Count<TFrom>()
    {
        throw new InvalidOperationException(
            "Sql.Count is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Creates a windowed MIN aggregate that can be used with OVER clause.
    /// Use .Over() to convert to a window function with PARTITION BY and ORDER BY.
    /// </summary>
    /// <typeparam name="T">The type of the column.</typeparam>
    /// <param name="column">The column to find minimum of.</param>
    /// <returns>A WindowAggregateBuilder for chaining .Over() clause.</returns>
    /// <example>
    /// <code>
    /// // Minimum price in each category
    /// var mins = db.From&lt;Product&gt;()
    ///     .Select(p => new {
    ///         p.ProductName,
    ///         p.UnitPrice,
    ///         CategoryMinPrice = Sql.Min&lt;Product, decimal?&gt;(p.UnitPrice).Over().PartitionBy(p => p.CategoryId)
    ///     });
    ///
    /// // SQL: SELECT product_name, unit_price,
    /// //      MIN(unit_price) OVER (PARTITION BY category_id) AS CategoryMinPrice
    /// //      FROM products
    /// </code>
    /// </example>
    public static WindowAggregateBuilder<TFrom, T> Min<TFrom, T>(T column)
    {
        throw new InvalidOperationException(
            "Sql.Min is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Creates a windowed MAX aggregate that can be used with OVER clause.
    /// Use .Over() to convert to a window function with PARTITION BY and ORDER BY.
    /// </summary>
    /// <typeparam name="T">The type of the column.</typeparam>
    /// <param name="column">The column to find maximum of.</param>
    /// <returns>A WindowAggregateBuilder for chaining .Over() clause.</returns>
    /// <example>
    /// <code>
    /// // Maximum price in each category
    /// var maxes = db.From&lt;Product&gt;()
    ///     .Select(p => new {
    ///         p.ProductName,
    ///         p.UnitPrice,
    ///         CategoryMaxPrice = Sql.Max&lt;Product, decimal?&gt;(p.UnitPrice).Over().PartitionBy(p => p.CategoryId)
    ///     });
    ///
    /// // SQL: SELECT product_name, unit_price,
    /// //      MAX(unit_price) OVER (PARTITION BY category_id) AS CategoryMaxPrice
    /// //      FROM products
    /// </code>
    /// </example>
    public static WindowAggregateBuilder<TFrom, T> Max<TFrom, T>(T column)
    {
        throw new InvalidOperationException(
            "Sql.Max is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }
}
