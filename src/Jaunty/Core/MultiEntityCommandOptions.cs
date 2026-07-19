using System;
using System.Data;

using Jaunty.Core;

namespace Jaunty.Core;

// ============================================================
//  Arity-2: MultiEntityCommandOptions<T1,T2>
// ============================================================

/// <summary>
/// Options for multi-entity command execution with two entity types.
/// Per-position mapper delegates allow individual type positions to bypass automatic
/// ordinal-claiming and use a caller-supplied mapping function instead.
/// </summary>
public readonly struct MultiEntityCommandOptions<T1, T2>
    where T1 : new()
    where T2 : new()
{
    /// <summary>Custom mapper for position 1. When set, T1 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T1>? Mapper1;
    /// <summary>Custom mapper for position 2. When set, T2 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T2>? Mapper2;

    /// <summary>Transaction to use for command execution.</summary>
    public readonly IDbTransaction? Transaction;

    /// <summary>Command timeout in seconds.</summary>
    public readonly int? CommandTimeout;

    /// <summary>Command type. Defaults to Text.</summary>
    public readonly CommandType CommandType;

    /// <summary>Initializes a new instance with optional per-position mappers and execution options.</summary>
    public MultiEntityCommandOptions(Func<IDataReader, T1>? mapper1 = null, Func<IDataReader, T2>? mapper2 = null, IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
    {
        Mapper1 = mapper1;
        Mapper2 = mapper2;
        Transaction = transaction;
        CommandTimeout = commandTimeout;
        CommandType = commandType;
    }

    /// <summary>Implicitly converts to a generic CommandOptions&lt;(T1, T2)&gt; for use with the multi-entity query core.</summary>
    public static implicit operator CommandOptions<(T1, T2)>(MultiEntityCommandOptions<T1, T2> opts) =>
        new(mapper: null, transaction: opts.Transaction, commandTimeout: opts.CommandTimeout, commandType: opts.CommandType);
}

// ============================================================
//  Arity-3: MultiEntityCommandOptions<T1,T2,T3>
// ============================================================

/// <summary>
/// Options for multi-entity command execution with three entity types.
/// Per-position mapper delegates allow individual type positions to bypass automatic
/// ordinal-claiming and use a caller-supplied mapping function instead.
/// </summary>
public readonly struct MultiEntityCommandOptions<T1, T2, T3>
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    /// <summary>Custom mapper for position 1. When set, T1 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T1>? Mapper1;
    /// <summary>Custom mapper for position 2. When set, T2 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T2>? Mapper2;
    /// <summary>Custom mapper for position 3. When set, T3 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T3>? Mapper3;

    /// <summary>Transaction to use for command execution.</summary>
    public readonly IDbTransaction? Transaction;

    /// <summary>Command timeout in seconds.</summary>
    public readonly int? CommandTimeout;

    /// <summary>Command type. Defaults to Text.</summary>
    public readonly CommandType CommandType;

    /// <summary>Initializes a new instance with optional per-position mappers and execution options.</summary>
    public MultiEntityCommandOptions(Func<IDataReader, T1>? mapper1 = null, Func<IDataReader, T2>? mapper2 = null, Func<IDataReader, T3>? mapper3 = null, IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
    {
        Mapper1 = mapper1;
        Mapper2 = mapper2;
        Mapper3 = mapper3;
        Transaction = transaction;
        CommandTimeout = commandTimeout;
        CommandType = commandType;
    }

    /// <summary>Implicitly converts to a non-generic CommandOptions for use with ExecuteReader.</summary>
    public static implicit operator CommandOptions(MultiEntityCommandOptions<T1, T2, T3> opts) =>
        new(opts.Transaction, opts.CommandTimeout, opts.CommandType);
}

// ============================================================
//  Arity-4: MultiEntityCommandOptions<T1,T2,T3,T4>
// ============================================================

/// <summary>
/// Options for multi-entity command execution with four entity types.
/// Per-position mapper delegates allow individual type positions to bypass automatic
/// ordinal-claiming and use a caller-supplied mapping function instead.
/// </summary>
public readonly struct MultiEntityCommandOptions<T1, T2, T3, T4>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    /// <summary>Custom mapper for position 1. When set, T1 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T1>? Mapper1;
    /// <summary>Custom mapper for position 2. When set, T2 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T2>? Mapper2;
    /// <summary>Custom mapper for position 3. When set, T3 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T3>? Mapper3;
    /// <summary>Custom mapper for position 4. When set, T4 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T4>? Mapper4;

    /// <summary>Transaction to use for command execution.</summary>
    public readonly IDbTransaction? Transaction;

    /// <summary>Command timeout in seconds.</summary>
    public readonly int? CommandTimeout;

    /// <summary>Command type. Defaults to Text.</summary>
    public readonly CommandType CommandType;

    /// <summary>Initializes a new instance with optional per-position mappers and execution options.</summary>
    public MultiEntityCommandOptions(Func<IDataReader, T1>? mapper1 = null, Func<IDataReader, T2>? mapper2 = null, Func<IDataReader, T3>? mapper3 = null, Func<IDataReader, T4>? mapper4 = null, IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
    {
        Mapper1 = mapper1;
        Mapper2 = mapper2;
        Mapper3 = mapper3;
        Mapper4 = mapper4;
        Transaction = transaction;
        CommandTimeout = commandTimeout;
        CommandType = commandType;
    }

    /// <summary>Implicitly converts to a non-generic CommandOptions for use with ExecuteReader.</summary>
    public static implicit operator CommandOptions(MultiEntityCommandOptions<T1, T2, T3, T4> opts) =>
        new(opts.Transaction, opts.CommandTimeout, opts.CommandType);
}

// ============================================================
//  Arity-5: MultiEntityCommandOptions<T1,T2,T3,T4,T5>
// ============================================================

/// <summary>
/// Options for multi-entity command execution with five entity types.
/// Per-position mapper delegates allow individual type positions to bypass automatic
/// ordinal-claiming and use a caller-supplied mapping function instead.
/// </summary>
public readonly struct MultiEntityCommandOptions<T1, T2, T3, T4, T5>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
    where T5 : new()
{
    /// <summary>Custom mapper for position 1. When set, T1 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T1>? Mapper1;
    /// <summary>Custom mapper for position 2. When set, T2 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T2>? Mapper2;
    /// <summary>Custom mapper for position 3. When set, T3 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T3>? Mapper3;
    /// <summary>Custom mapper for position 4. When set, T4 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T4>? Mapper4;
    /// <summary>Custom mapper for position 5. When set, T5 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T5>? Mapper5;

    /// <summary>Transaction to use for command execution.</summary>
    public readonly IDbTransaction? Transaction;

    /// <summary>Command timeout in seconds.</summary>
    public readonly int? CommandTimeout;

    /// <summary>Command type. Defaults to Text.</summary>
    public readonly CommandType CommandType;

    /// <summary>Initializes a new instance with optional per-position mappers and execution options.</summary>
    public MultiEntityCommandOptions(Func<IDataReader, T1>? mapper1 = null, Func<IDataReader, T2>? mapper2 = null, Func<IDataReader, T3>? mapper3 = null, Func<IDataReader, T4>? mapper4 = null, Func<IDataReader, T5>? mapper5 = null, IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
    {
        Mapper1 = mapper1;
        Mapper2 = mapper2;
        Mapper3 = mapper3;
        Mapper4 = mapper4;
        Mapper5 = mapper5;
        Transaction = transaction;
        CommandTimeout = commandTimeout;
        CommandType = commandType;
    }

    /// <summary>Implicitly converts to a non-generic CommandOptions for use with ExecuteReader.</summary>
    public static implicit operator CommandOptions(MultiEntityCommandOptions<T1, T2, T3, T4, T5> opts) =>
        new(opts.Transaction, opts.CommandTimeout, opts.CommandType);
}

// ============================================================
//  Arity-6: MultiEntityCommandOptions<T1,T2,T3,T4,T5,T6>
// ============================================================

/// <summary>
/// Options for multi-entity command execution with six entity types.
/// Per-position mapper delegates allow individual type positions to bypass automatic
/// ordinal-claiming and use a caller-supplied mapping function instead.
/// </summary>
public readonly struct MultiEntityCommandOptions<T1, T2, T3, T4, T5, T6>
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
    where T5 : new()
    where T6 : new()
{
    /// <summary>Custom mapper for position 1. When set, T1 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T1>? Mapper1;
    /// <summary>Custom mapper for position 2. When set, T2 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T2>? Mapper2;
    /// <summary>Custom mapper for position 3. When set, T3 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T3>? Mapper3;
    /// <summary>Custom mapper for position 4. When set, T4 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T4>? Mapper4;
    /// <summary>Custom mapper for position 5. When set, T5 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T5>? Mapper5;
    /// <summary>Custom mapper for position 6. When set, T6 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T6>? Mapper6;

    /// <summary>Transaction to use for command execution.</summary>
    public readonly IDbTransaction? Transaction;

    /// <summary>Command timeout in seconds.</summary>
    public readonly int? CommandTimeout;

    /// <summary>Command type. Defaults to Text.</summary>
    public readonly CommandType CommandType;

    /// <summary>Initializes a new instance with optional per-position mappers and execution options.</summary>
    public MultiEntityCommandOptions(Func<IDataReader, T1>? mapper1 = null, Func<IDataReader, T2>? mapper2 = null, Func<IDataReader, T3>? mapper3 = null, Func<IDataReader, T4>? mapper4 = null, Func<IDataReader, T5>? mapper5 = null, Func<IDataReader, T6>? mapper6 = null, IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
    {
        Mapper1 = mapper1;
        Mapper2 = mapper2;
        Mapper3 = mapper3;
        Mapper4 = mapper4;
        Mapper5 = mapper5;
        Mapper6 = mapper6;
        Transaction = transaction;
        CommandTimeout = commandTimeout;
        CommandType = commandType;
    }

    /// <summary>Implicitly converts to a non-generic CommandOptions for use with ExecuteReader.</summary>
    public static implicit operator CommandOptions(MultiEntityCommandOptions<T1, T2, T3, T4, T5, T6> opts) =>
        new(opts.Transaction, opts.CommandTimeout, opts.CommandType);
}

// ============================================================
//  Arity-7: MultiEntityCommandOptions<T1,T2,T3,T4,T5,T6,T7>
// ============================================================

/// <summary>
/// Options for multi-entity command execution with seven entity types.
/// Per-position mapper delegates allow individual type positions to bypass automatic
/// ordinal-claiming and use a caller-supplied mapping function instead.
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
    /// <summary>Custom mapper for position 1. When set, T1 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T1>? Mapper1;
    /// <summary>Custom mapper for position 2. When set, T2 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T2>? Mapper2;
    /// <summary>Custom mapper for position 3. When set, T3 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T3>? Mapper3;
    /// <summary>Custom mapper for position 4. When set, T4 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T4>? Mapper4;
    /// <summary>Custom mapper for position 5. When set, T5 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T5>? Mapper5;
    /// <summary>Custom mapper for position 6. When set, T6 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T6>? Mapper6;
    /// <summary>Custom mapper for position 7. When set, T7 is excluded from ordinal claiming.</summary>
    public readonly Func<IDataReader, T7>? Mapper7;

    /// <summary>Transaction to use for command execution.</summary>
    public readonly IDbTransaction? Transaction;

    /// <summary>Command timeout in seconds.</summary>
    public readonly int? CommandTimeout;

    /// <summary>Command type. Defaults to Text.</summary>
    public readonly CommandType CommandType;

    /// <summary>Initializes a new instance with optional per-position mappers and execution options.</summary>
    public MultiEntityCommandOptions(Func<IDataReader, T1>? mapper1 = null, Func<IDataReader, T2>? mapper2 = null, Func<IDataReader, T3>? mapper3 = null, Func<IDataReader, T4>? mapper4 = null, Func<IDataReader, T5>? mapper5 = null, Func<IDataReader, T6>? mapper6 = null, Func<IDataReader, T7>? mapper7 = null, IDbTransaction? transaction = null, int? commandTimeout = null, CommandType commandType = CommandType.Text)
    {
        Mapper1 = mapper1;
        Mapper2 = mapper2;
        Mapper3 = mapper3;
        Mapper4 = mapper4;
        Mapper5 = mapper5;
        Mapper6 = mapper6;
        Mapper7 = mapper7;
        Transaction = transaction;
        CommandTimeout = commandTimeout;
        CommandType = commandType;
    }

    /// <summary>Implicitly converts to a non-generic CommandOptions for use with ExecuteReader.</summary>
    public static implicit operator CommandOptions(MultiEntityCommandOptions<T1, T2, T3, T4, T5, T6, T7> opts) =>
        new(opts.Transaction, opts.CommandTimeout, opts.CommandType);
}
