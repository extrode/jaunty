namespace Jaunty.Fluent;

/// <summary>
/// Marker class for building window function OVER clauses.
/// These methods are never executed directly - they exist only for expression tree analysis.
/// </summary>
/// <typeparam name="TResult">The result type of the window function.</typeparam>
public sealed class WindowBuilder<TResult>
{
    internal WindowFunctionType FunctionType { get; }
    internal int? NTileBuckets { get; }

    internal WindowBuilder(WindowFunctionType functionType, int? ntileBuckets = null)
    {
        FunctionType = functionType;
        NTileBuckets = ntileBuckets;
    }

    /// <summary>
    /// Specifies the PARTITION BY clause for the window function.
    /// </summary>
    /// <typeparam name="TKey">The type of the partition key.</typeparam>
    /// <param name="column">The column to partition by.</param>
    /// <returns>The WindowBuilder for chaining.</returns>
    public WindowBuilder<TResult> PartitionBy<TKey>(TKey column)
    {
        throw new InvalidOperationException(
            "WindowBuilder.PartitionBy is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Specifies the ORDER BY clause (ascending) for the window function.
    /// </summary>
    /// <typeparam name="TKey">The type of the order key.</typeparam>
    /// <param name="column">The column to order by.</param>
    /// <returns>The WindowBuilder for chaining.</returns>
    public WindowBuilder<TResult> OrderBy<TKey>(TKey column)
    {
        throw new InvalidOperationException(
            "WindowBuilder.OrderBy is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Specifies the ORDER BY clause (descending) for the window function.
    /// </summary>
    /// <typeparam name="TKey">The type of the order key.</typeparam>
    /// <param name="column">The column to order by descending.</param>
    /// <returns>The WindowBuilder for chaining.</returns>
    public WindowBuilder<TResult> OrderByDescending<TKey>(TKey column)
    {
        throw new InvalidOperationException(
            "WindowBuilder.OrderByDescending is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Implicit conversion to allow window functions in expressions.
    /// This is never actually called - it exists for the compiler.
    /// </summary>
    public static implicit operator TResult(WindowBuilder<TResult> builder)
    {
        throw new InvalidOperationException(
            "WindowBuilder implicit conversion is a marker for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }
}

/// <summary>
/// Types of window functions supported.
/// </summary>
public enum WindowFunctionType
{
    RowNumber,
    Rank,
    DenseRank,
    NTile,
    Sum,
    Avg,
    Count,
    Min,
    Max
}

/// <summary>
/// Builder for window aggregates (SUM, AVG, etc. with OVER clause).
/// </summary>
/// <typeparam name="TResult">The result type of the aggregate.</typeparam>
public sealed class WindowAggregateBuilder<TResult>
{
    internal WindowFunctionType FunctionType { get; }

    internal WindowAggregateBuilder(WindowFunctionType functionType)
    {
        FunctionType = functionType;
    }

    /// <summary>
    /// Converts this aggregate to a window function with OVER clause.
    /// </summary>
    /// <returns>A WindowBuilder for specifying PARTITION BY and ORDER BY.</returns>
    public WindowBuilder<TResult> Over()
    {
        throw new InvalidOperationException(
            "WindowAggregateBuilder.Over is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }
}
