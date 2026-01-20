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
public static class Sql
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
    /// <typeparam name="TResult">The type of the CASE expression result.</typeparam>
    /// <returns>A CaseBuilder to chain When/Else clauses.</returns>
    /// <example>
    /// <code>
    /// // Categorize products by price in WHERE clause
    /// db.From&lt;Product&gt;()
    ///   .Where(p => Sql.Case&lt;string&gt;()
    ///       .When(p.UnitPrice &lt; 10, "Budget")
    ///       .When(p.UnitPrice &lt; 50, "Standard")
    ///       .Else("Premium") == "Budget")
    ///   .Select();
    ///
    /// // SQL: WHERE CASE WHEN unit_price &lt; 10 THEN 'Budget'
    /// //           WHEN unit_price &lt; 50 THEN 'Standard'
    /// //           ELSE 'Premium' END = 'Budget'
    /// </code>
    /// </example>
    public static CaseBuilder<TResult> Case<TResult>()
    {
        throw new InvalidOperationException(
            "Sql.Case is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }
}

/// <summary>
/// Builder for SQL CASE expressions. Used as a marker pattern for expression translation.
/// </summary>
/// <typeparam name="TResult">The type of the CASE expression result.</typeparam>
public sealed class CaseBuilder<TResult>
{
    /// <summary>
    /// Adds a WHEN clause to the CASE expression.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="result">The result if the condition is true.</param>
    /// <returns>The CaseBuilder for chaining.</returns>
    public CaseBuilder<TResult> When(bool condition, TResult result)
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
