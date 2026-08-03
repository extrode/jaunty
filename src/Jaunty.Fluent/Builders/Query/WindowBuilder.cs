using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Jaunty.Fluent;

/// <summary>
/// Marker class for building window function OVER clauses.
/// These methods are never executed directly - they exist only for expression tree analysis.
/// </summary>
/// <typeparam name="TResult">The result type of the window function.</typeparam>
[ExcludeFromCodeCoverage]
public sealed class WindowBuilder<TFrom, TResult>
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
    public WindowBuilder<TFrom, TResult> PartitionBy<TKey>(Expression<Func<TFrom, TKey>> column)
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
    public WindowBuilder<TFrom, TResult> OrderBy<TKey>(Expression<Func<TFrom, TKey>> column)
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
    public WindowBuilder<TFrom, TResult> OrderByDescending<TKey>(Expression<Func<TFrom, TKey>> column)
    {
        throw new InvalidOperationException(
            "WindowBuilder.OrderByDescending is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }

    /// <summary>
    /// Implicit conversion to allow window functions in expressions.
    /// This is never actually called - it exists for the compiler.
    /// </summary>
    public static implicit operator TResult(WindowBuilder<TFrom, TResult> builder)
    {
        throw new InvalidOperationException(
            "WindowBuilder implicit conversion is a marker for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }
}

/// <summary>
/// Types of window functions supported.
/// </summary>
/// <remarks>
/// AUD-R35-189. Nothing constructs a <see cref="WindowBuilder{TFrom, TResult}"/> or a
/// <see cref="WindowAggregateBuilder{TFrom, TResult}"/>, so no member of this enum is ever read.
/// <c>SelectExpressionVisitor</c> recognises a window function by matching the
/// <c>Sql.RowNumber</c>/<c>Sql.Rank</c>/... method name on the expression tree and emits the SQL
/// from there; the two builders exist only to give those markers a chainable return type, and their
/// <c>FunctionType</c>/<c>NTileBuckets</c> state is never populated or consulted. The type is
/// nevertheless public API a consumer can see and reasonably expect to mean something. Removing it
/// and the two internal constructors is a public-surface deletion and therefore the owner's call;
/// it is recorded in <c>work/todo.md</c> with both options. Do not add a member here expecting it
/// to change generated SQL - change the visitor's method matching instead.
/// </remarks>
public enum WindowFunctionType
{
    /// <summary>
    /// ROW_NUMBER() - assigns a unique sequential number to each row.
    /// </summary>
    RowNumber,

    /// <summary>
    /// RANK() - assigns rank with gaps for ties.
    /// </summary>
    Rank,

    /// <summary>
    /// DENSE_RANK() - assigns rank without gaps for ties.
    /// </summary>
    DenseRank,

    /// <summary>
    /// NTILE() - distributes rows into a specified number of buckets.
    /// </summary>
    NTile,

    /// <summary>
    /// SUM() OVER - windowed sum aggregate.
    /// </summary>
    Sum,

    /// <summary>
    /// AVG() OVER - windowed average aggregate.
    /// </summary>
    Avg,

    /// <summary>
    /// COUNT() OVER - windowed count aggregate.
    /// </summary>
    Count,

    /// <summary>
    /// MIN() OVER - windowed minimum aggregate.
    /// </summary>
    Min,

    /// <summary>
    /// MAX() OVER - windowed maximum aggregate.
    /// </summary>
    Max
}

/// <summary>
/// Builder for window aggregates (SUM, AVG, etc. with OVER clause).
/// </summary>
/// <typeparam name="TFrom">The type of the entity being queried from.</typeparam>
/// <typeparam name="TResult">The result type of the aggregate.</typeparam>
[ExcludeFromCodeCoverage]
public sealed class WindowAggregateBuilder<TFrom, TResult>
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
    public WindowBuilder<TFrom, TResult> Over()
    {
        throw new InvalidOperationException(
            "WindowAggregateBuilder.Over is a marker method for SQL generation and cannot be called directly. " +
            "Use it only within Jaunty fluent query expressions.");
    }
}