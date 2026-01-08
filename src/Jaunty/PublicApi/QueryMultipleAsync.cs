using System.Data.Common;

namespace Jaunty;

public static partial class Jaunty
{
    public static async Task<GridReader> QueryMultipleAsync(this DbConnection connection, string sql, CancellationToken cancellationToken = default)
    {
        return await ExecuteQueryMultipleAsync(connection, sql, null, default, cancellationToken);
    }

    public static async Task<GridReader> QueryMultipleAsync(this DbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
    {
        return await ExecuteQueryMultipleAsync(connection, sql, parameters, default, cancellationToken);
    }

    public static async Task<GridReader> QueryMultipleAsync(this DbConnection connection, string sql, CommandOptions options, CancellationToken cancellationToken = default)
    {
        return await ExecuteQueryMultipleAsync(connection, sql, null, options, cancellationToken);
    }

    public static async Task<GridReader> QueryMultipleAsync(this DbConnection connection, string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)
    {
        return await ExecuteQueryMultipleAsync(connection, sql, parameters, options, cancellationToken);
    }

    public static async Task QueryMultipleAsync(this DbConnection connection, string sql, Action<GridReader> reader, object? parameters = null, CommandOptions options = default, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
        using var gridReader = await ExecuteQueryMultipleAsync(connection, sql, parameters, options, cancellationToken);
        reader(gridReader);
    }

    public static async Task<TResult> QueryMultipleAsync<TResult>(this DbConnection connection, string sql, Func<GridReader, TResult> reader, object? parameters = null, CommandOptions options = default, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(reader);
#else
        if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
        using var gridReader = await ExecuteQueryMultipleAsync(connection, sql, parameters, options, cancellationToken);
        return reader(gridReader);
    }
}