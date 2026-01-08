using System.Data;
using System.Data.Common;

using Jaunty.InternalApi.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    private static async Task<GridReader> ExecuteQueryMultipleAsync(DbConnection connection, string sql, object? parameters, CommandOptions options, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException(nameof(sql));
#endif

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.Transaction is DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return new GridReader(reader, connection, wasClosed);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
        }
    }
}