using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a multi‑result query synchronously.
    /// </summary>
    /// <param name="connection">A <see cref="DbConnection"/> that will be opened if it is closed.</param>
    /// <param name="sql">SQL containing one or more result sets.</param>
    /// <param name="parameters">Optional parameters for the command.</param>
    /// <param name="options">Transaction, timeout, etc.</param>
    /// <returns>A <see cref="GridReader"/> that owns the <see cref="DbDataReader"/> and the command.</returns>
    private static GridReader ExecuteQueryMultiple(IDbConnection connection, string sql, object? parameters, CommandOptions options)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif

        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
            connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = sql;

        if (options.Transaction is DbTransaction dbTransaction)
            command.Transaction = dbTransaction;

        if (options.CommandTimeout.HasValue)
            command.CommandTimeout = options.CommandTimeout.Value;

        if (parameters is not null)
            ParameterBinder.Bind(command, parameters);

        var reader = command.ExecuteReader();
        return new GridReader(reader, connection, wasClosed);
        //finally
        //{
        //    if (wasClosed && connection.State != ConnectionState.Closed)
        //        connection.Close();
        //}
    }
}
