using System.Data;
using System.Data.Common;

using Jaunty.Core;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Applies a <see cref="CommandOptions"/> to a command the fluent builders create and execute
/// themselves, rather than delegating to core.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26-060 (batch 5, low/consistency). <see cref="CommandOptions"/> is the only way to enlist a
/// fluent call in a caller's transaction or set a command timeout, and it was available on the
/// terminals that delegate to core and absent on the ones that build and execute their own command.
/// <c>InsertBuilder</c> had no options overload at all, so there was no way to pass a transaction to
/// a fluent insert.
/// </para>
/// <para>
/// The gap was provider-dependent rather than uniformly broken, which is what kept it quiet: on
/// Microsoft.Data.Sqlite a fluent insert inside an ambient <c>BeginTransaction</c> is rolled back
/// with it, because SQLite associates commands with the connection's open transaction implicitly.
/// <c>SqlClient</c> throws when a command with no <c>Transaction</c> runs on a connection with a
/// pending local transaction, so the same application code behaves differently per provider.
/// </para>
/// <para>
/// Centralised here because the three affected builders would otherwise each repeat the
/// transaction-validation subtlety below, and one of them would eventually get it wrong.
/// </para>
/// </remarks>
internal static class FluentCommandOptions
{
    /// <summary>
    /// Applies the transaction, timeout and command type from <paramref name="options"/> to
    /// <paramref name="command"/>.
    /// </summary>
    /// <remarks>
    /// A <see cref="DbConnection"/>'s <c>IDbCommand.Transaction</c> setter is
    /// <see cref="DbCommand"/>'s explicit interface implementation, which casts to
    /// <see cref="DbTransaction"/> internally - assigning a non-<c>DbTransaction</c>
    /// <see cref="IDbTransaction"/> through it throws an opaque <see cref="InvalidCastException"/>.
    /// Validating first gives Jaunty's clear <see cref="ArgumentException"/> instead. Copied from
    /// <c>QueryBuilder.ExecuteNonQuery</c>, which is where this was already got right.
    /// </remarks>
    public static void Apply(IDbCommand command, IDbConnection connection, CommandOptions options)
    {
        if (options.Transaction is not null)
        {
            command.Transaction = connection is DbConnection
                ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
                : options.Transaction;
        }

        if (options.CommandTimeout.HasValue)
            command.CommandTimeout = options.CommandTimeout.Value;

        // Allow-list, not "anything that isn't Text", for two reasons. CommandOptions is a struct
        // whose primary constructor does not supply the parameterless one, so `default` and
        // `new CommandOptions()` both carry CommandType 0 - not CommandType.Text (1) - and a
        // "!= Text" guard would stamp that invalid 0 onto every command built from default options.
        // And SQLite's provider throws on any CommandType it does not support. Copied from
        // ExecuteNonQueryCore, which sets it under exactly this condition.
        if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
            command.CommandType = options.CommandType;
    }

    /// <summary>
    /// The command type to report to interceptors for <paramref name="options"/>.
    /// </summary>
    /// <remarks>
    /// See <see cref="Apply"/>: <c>default(CommandOptions).CommandType</c> is <c>0</c>, which is not
    /// a defined <see cref="CommandType"/>. Reporting it verbatim would put an undefined value in
    /// front of every interceptor for every call that did not pass options explicitly.
    /// </remarks>
    public static CommandType Describe(CommandOptions options) =>
        options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect
            ? options.CommandType
            : CommandType.Text;
}
