using System.Data.Common;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    extension(DbConnection connection)
    {
        public Task<GridReader> QueryMultipleAsync(string sql, CancellationToken cancellationToken = default)
        {
            return ExecuteQueryMultipleAsync(connection, sql, null, default, cancellationToken);
        }

        public Task<GridReader> QueryMultipleAsync(string sql, object parameters, CancellationToken cancellationToken = default)
        {
            return ExecuteQueryMultipleAsync(connection, sql, parameters, default, cancellationToken);
        }

        public Task<GridReader> QueryMultipleAsync(string sql, CommandOptions options, CancellationToken cancellationToken = default)
        {
            return ExecuteQueryMultipleAsync(connection, sql, null, options, cancellationToken);
        }

        public Task<GridReader> QueryMultipleAsync(string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)
        {
            return ExecuteQueryMultipleAsync(connection, sql, parameters, options, cancellationToken);
        }

        public async Task QueryMultipleAsync(string sql, Action<GridReader> reader, object? parameters = null, CommandOptions options = default, CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(reader);
#else
            if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
            using var gridReader = await ExecuteQueryMultipleAsync(connection, sql, parameters, options, cancellationToken);
            reader(gridReader);
        }

        public async Task QueryMultipleAsync(string sql, Func<GridReader, Task> reader, object? parameters = null, CommandOptions options = default, CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(reader);
#else
            if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
            using var gridReader = await ExecuteQueryMultipleAsync(connection, sql, parameters, options, cancellationToken);
            await reader(gridReader);
        }

        public async Task<TResult> QueryMultipleAsync<TResult>(string sql, Func<GridReader, TResult> reader, object? parameters = null, CommandOptions options = default, CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(reader);
#else
            if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
            using var gridReader = await ExecuteQueryMultipleAsync(connection, sql, parameters, options, cancellationToken);
            return reader(gridReader);
        }

        public async Task<TResult> QueryMultipleAsync<TResult>(string sql, Func<GridReader, Task<TResult>> reader, object? parameters = null, CommandOptions options = default, CancellationToken cancellationToken = default)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(reader);
#else
            if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
            using var gridReader = await ExecuteQueryMultipleAsync(connection, sql, parameters, options, cancellationToken);
            return await reader(gridReader);
        }
    }
}
