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
}
