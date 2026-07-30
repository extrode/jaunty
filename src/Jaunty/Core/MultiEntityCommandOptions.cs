using System;
using System.Data;

using Jaunty.Core;

namespace Jaunty.Core;

// ============================================================
//  Arity-2: MultiEntityCommandOptions<T1,T2>
// ============================================================

/// <summary>
/// Options for multi-entity command execution with two entity types.
/// </summary>
public readonly struct MultiEntityCommandOptions<T1, T2>
    where T1 : new()
    where T2 : new()
{
    /// <summary>Transaction to use for command execution.</summary>
    public readonly IDbTransaction? Transaction;

    /// <summary>Command timeout in seconds.</summary>
    public readonly int? CommandTimeout;

    /// <summary>Command type. Defaults to Text.</summary>
    public readonly CommandType CommandType;

    /// <summary>
    /// Hint for the number of rows the query is expected to return, used to pre-size the result
    /// list. Null means use <c>JauntyConfig.QueryResultCapacity</c>.
    /// </summary>
    /// <remarks>
    /// AUD-R26-054 (batch 4, low/consistency). This field did not exist, so the implicit conversion
    /// below always produced a null <c>ExpectedRowCount</c> - and <c>QueryCore</c>/<c>QueryCoreAsync</c>
    /// read exactly that at all twelve multi-entity call sites. The consumer was already there; only
    /// the field a caller could set was missing, so no multi-entity query could pre-size its list.
    /// </remarks>
    public readonly int? ExpectedRowCount;

    /// <summary>Initializes a new instance with execution options.</summary>
    public MultiEntityCommandOptions(IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text, int? expectedRowCount = null)
    {
        Transaction = transaction;
        CommandTimeout = commandTimeout;
        CommandType = commandType;
        ExpectedRowCount = expectedRowCount;
    }

    /// <summary>Implicitly converts to a generic CommandOptions&lt;(T1, T2)&gt; for use with the multi-entity query core.</summary>
    public static implicit operator CommandOptions<(T1, T2)>(MultiEntityCommandOptions<T1, T2> opts) =>
        new(mapper: null, transaction: opts.Transaction, commandTimeout: opts.CommandTimeout, commandType: opts.CommandType, expectedRowCount: opts.ExpectedRowCount);
}

// ============================================================
//  Arity-3: MultiEntityCommandOptions<T1,T2,T3>
// ============================================================

/// <summary>
/// Options for multi-entity command execution with three entity types.
/// </summary>
public readonly struct MultiEntityCommandOptions<T1, T2, T3>
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    /// <summary>Transaction to use for command execution.</summary>
    public readonly IDbTransaction? Transaction;

    /// <summary>Command timeout in seconds.</summary>
    public readonly int? CommandTimeout;

    /// <summary>Command type. Defaults to Text.</summary>
    public readonly CommandType CommandType;

    /// <summary>
    /// Hint for the number of rows the query is expected to return, used to pre-size the result
    /// list. Null means use <c>JauntyConfig.QueryResultCapacity</c>.
    /// </summary>
    /// <remarks>
    /// AUD-R26-054 (batch 4, low/consistency). This field did not exist, so the implicit conversion
    /// below always produced a null <c>ExpectedRowCount</c> - and <c>QueryCore</c>/<c>QueryCoreAsync</c>
    /// read exactly that at all twelve multi-entity call sites. The consumer was already there; only
    /// the field a caller could set was missing, so no multi-entity query could pre-size its list.
    /// </remarks>
    public readonly int? ExpectedRowCount;

    /// <summary>Initializes a new instance with execution options.</summary>
    public MultiEntityCommandOptions(IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text, int? expectedRowCount = null)
    {
        Transaction = transaction;
        CommandTimeout = commandTimeout;
        CommandType = commandType;
        ExpectedRowCount = expectedRowCount;
    }

    /// <summary>Implicitly converts to a generic CommandOptions&lt;(T1, T2, T3)&gt; for use with the multi-entity query core.</summary>
    public static implicit operator CommandOptions<(T1, T2, T3)>(MultiEntityCommandOptions<T1, T2, T3> opts) =>
        new(mapper: null, transaction: opts.Transaction, commandTimeout: opts.CommandTimeout, commandType: opts.CommandType, expectedRowCount: opts.ExpectedRowCount);
}

// ============================================================
//  Arity-4: MultiEntityCommandOptions<T1,T2,T3,T4>
// ============================================================

/// <summary>
/// Options for multi-entity command execution with four entity types.
/// </summary>
public readonly struct MultiEntityCommandOptions<T1, T2, T3, T4>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    /// <summary>Transaction to use for command execution.</summary>
    public readonly IDbTransaction? Transaction;

    /// <summary>Command timeout in seconds.</summary>
    public readonly int? CommandTimeout;

    /// <summary>Command type. Defaults to Text.</summary>
    public readonly CommandType CommandType;

    /// <summary>
    /// Hint for the number of rows the query is expected to return, used to pre-size the result
    /// list. Null means use <c>JauntyConfig.QueryResultCapacity</c>.
    /// </summary>
    /// <remarks>
    /// AUD-R26-054 (batch 4, low/consistency). This field did not exist, so the implicit conversion
    /// below always produced a null <c>ExpectedRowCount</c> - and <c>QueryCore</c>/<c>QueryCoreAsync</c>
    /// read exactly that at all twelve multi-entity call sites. The consumer was already there; only
    /// the field a caller could set was missing, so no multi-entity query could pre-size its list.
    /// </remarks>
    public readonly int? ExpectedRowCount;

    /// <summary>Initializes a new instance with execution options.</summary>
    public MultiEntityCommandOptions(IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text, int? expectedRowCount = null)
    {
        Transaction = transaction;
        CommandTimeout = commandTimeout;
        CommandType = commandType;
        ExpectedRowCount = expectedRowCount;
    }

    /// <summary>Implicitly converts to a generic CommandOptions&lt;(T1, T2, T3, T4)&gt; for use with the multi-entity query core.</summary>
    public static implicit operator CommandOptions<(T1, T2, T3, T4)>(MultiEntityCommandOptions<T1, T2, T3, T4> opts) =>
        new(mapper: null, transaction: opts.Transaction, commandTimeout: opts.CommandTimeout, commandType: opts.CommandType, expectedRowCount: opts.ExpectedRowCount);
}

// ============================================================
//  Arity-5: MultiEntityCommandOptions<T1,T2,T3,T4,T5>
// ============================================================

/// <summary>
/// Options for multi-entity command execution with five entity types.
/// </summary>
public readonly struct MultiEntityCommandOptions<T1, T2, T3, T4, T5>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
    where T5 : new()
{
    /// <summary>Transaction to use for command execution.</summary>
    public readonly IDbTransaction? Transaction;

    /// <summary>Command timeout in seconds.</summary>
    public readonly int? CommandTimeout;

    /// <summary>Command type. Defaults to Text.</summary>
    public readonly CommandType CommandType;

    /// <summary>
    /// Hint for the number of rows the query is expected to return, used to pre-size the result
    /// list. Null means use <c>JauntyConfig.QueryResultCapacity</c>.
    /// </summary>
    /// <remarks>
    /// AUD-R26-054 (batch 4, low/consistency). This field did not exist, so the implicit conversion
    /// below always produced a null <c>ExpectedRowCount</c> - and <c>QueryCore</c>/<c>QueryCoreAsync</c>
    /// read exactly that at all twelve multi-entity call sites. The consumer was already there; only
    /// the field a caller could set was missing, so no multi-entity query could pre-size its list.
    /// </remarks>
    public readonly int? ExpectedRowCount;

    /// <summary>Initializes a new instance with execution options.</summary>
    public MultiEntityCommandOptions(IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text, int? expectedRowCount = null)
    {
        Transaction = transaction;
        CommandTimeout = commandTimeout;
        CommandType = commandType;
        ExpectedRowCount = expectedRowCount;
    }

    /// <summary>Implicitly converts to a generic CommandOptions&lt;(T1, T2, T3, T4, T5)&gt; for use with the multi-entity query core.</summary>
    public static implicit operator CommandOptions<(T1, T2, T3, T4, T5)>(MultiEntityCommandOptions<T1, T2, T3, T4, T5> opts) =>
        new(mapper: null, transaction: opts.Transaction, commandTimeout: opts.CommandTimeout, commandType: opts.CommandType, expectedRowCount: opts.ExpectedRowCount);
}

// ============================================================
//  Arity-6: MultiEntityCommandOptions<T1,T2,T3,T4,T5,T6>
// ============================================================

/// <summary>
/// Options for multi-entity command execution with six entity types.
/// </summary>
public readonly struct MultiEntityCommandOptions<T1, T2, T3, T4, T5, T6>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
    where T5 : new()
    where T6 : new()
{
    /// <summary>Transaction to use for command execution.</summary>
    public readonly IDbTransaction? Transaction;

    /// <summary>Command timeout in seconds.</summary>
    public readonly int? CommandTimeout;

    /// <summary>Command type. Defaults to Text.</summary>
    public readonly CommandType CommandType;

    /// <summary>
    /// Hint for the number of rows the query is expected to return, used to pre-size the result
    /// list. Null means use <c>JauntyConfig.QueryResultCapacity</c>.
    /// </summary>
    /// <remarks>
    /// AUD-R26-054 (batch 4, low/consistency). This field did not exist, so the implicit conversion
    /// below always produced a null <c>ExpectedRowCount</c> - and <c>QueryCore</c>/<c>QueryCoreAsync</c>
    /// read exactly that at all twelve multi-entity call sites. The consumer was already there; only
    /// the field a caller could set was missing, so no multi-entity query could pre-size its list.
    /// </remarks>
    public readonly int? ExpectedRowCount;

    /// <summary>Initializes a new instance with execution options.</summary>
    public MultiEntityCommandOptions(IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text, int? expectedRowCount = null)
    {
        Transaction = transaction;
        CommandTimeout = commandTimeout;
        CommandType = commandType;
        ExpectedRowCount = expectedRowCount;
    }

    /// <summary>Implicitly converts to a generic CommandOptions&lt;(T1, T2, T3, T4, T5, T6)&gt; for use with the multi-entity query core.</summary>
    public static implicit operator CommandOptions<(T1, T2, T3, T4, T5, T6)>(MultiEntityCommandOptions<T1, T2, T3, T4, T5, T6> opts) =>
        new(mapper: null, transaction: opts.Transaction, commandTimeout: opts.CommandTimeout, commandType: opts.CommandType, expectedRowCount: opts.ExpectedRowCount);
}

// ============================================================
//  Arity-7: MultiEntityCommandOptions<T1,T2,T3,T4,T5,T6,T7>
// ============================================================

/// <summary>
/// Options for multi-entity command execution with seven entity types.
/// </summary>
public readonly struct MultiEntityCommandOptions<T1, T2, T3, T4, T5, T6, T7>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
    where T5 : new()
    where T6 : new()
    where T7 : new()
{
    /// <summary>Transaction to use for command execution.</summary>
    public readonly IDbTransaction? Transaction;

    /// <summary>Command timeout in seconds.</summary>
    public readonly int? CommandTimeout;

    /// <summary>Command type. Defaults to Text.</summary>
    public readonly CommandType CommandType;

    /// <summary>
    /// Hint for the number of rows the query is expected to return, used to pre-size the result
    /// list. Null means use <c>JauntyConfig.QueryResultCapacity</c>.
    /// </summary>
    /// <remarks>
    /// AUD-R26-054 (batch 4, low/consistency). This field did not exist, so the implicit conversion
    /// below always produced a null <c>ExpectedRowCount</c> - and <c>QueryCore</c>/<c>QueryCoreAsync</c>
    /// read exactly that at all twelve multi-entity call sites. The consumer was already there; only
    /// the field a caller could set was missing, so no multi-entity query could pre-size its list.
    /// </remarks>
    public readonly int? ExpectedRowCount;

    /// <summary>Initializes a new instance with execution options.</summary>
    public MultiEntityCommandOptions(IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text, int? expectedRowCount = null)
    {
        Transaction = transaction;
        CommandTimeout = commandTimeout;
        CommandType = commandType;
        ExpectedRowCount = expectedRowCount;
    }

    /// <summary>Implicitly converts to a generic CommandOptions&lt;(T1, T2, T3, T4, T5, T6, T7)&gt; for use with the multi-entity query core.</summary>
    public static implicit operator CommandOptions<(T1, T2, T3, T4, T5, T6, T7)>(MultiEntityCommandOptions<T1, T2, T3, T4, T5, T6, T7> opts) =>
        new(mapper: null, transaction: opts.Transaction, commandTimeout: opts.CommandTimeout, commandType: opts.CommandType, expectedRowCount: opts.ExpectedRowCount);
}
