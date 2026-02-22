using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    private static async ValueTask<GridReader> ExecuteQueryMultipleAsync(DbConnection connection, string sql, object? parameters, CommandOptions options, CancellationToken cancellationToken)
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
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Do NOT use 'using' — the reader may depend on the command lifetime.
        DbCommand command = connection.CreateCommand();
        command.CommandText = sql;

        if (options.Transaction is DbTransaction dbTransaction)
            command.Transaction = dbTransaction;

        if (options.CommandTimeout.HasValue)
            command.CommandTimeout = options.CommandTimeout.Value;

        if (parameters is not null)
            ParameterBinder.Bind(command, parameters);

        // Do NOT use 'using' — the GridReader owns the reader and disposes it.
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return new GridReader(reader, connection, wasClosed);
    }
}


