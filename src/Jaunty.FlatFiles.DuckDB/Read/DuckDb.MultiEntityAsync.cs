using Jaunty;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <summary>
    /// Executes a SQL query and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    /// <typeparam name="T1">The first entity type.</typeparam>
    /// <typeparam name="T2">The second entity type.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>A list of tuples containing mapped entities.</returns>
    public async ValueTask<List<(T1, T2)>> QueryMultiEntityAsync<T1, T2>(string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return await _connection.QueryAsync<T1, T2>(sql, cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore CS0618
    }

    /// <summary>
    /// Executes a SQL query with parameters and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    /// <typeparam name="T1">The first entity type.</typeparam>
    /// <typeparam name="T2">The second entity type.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>A list of tuples containing mapped entities.</returns>
    public async ValueTask<List<(T1, T2)>> QueryMultiEntityAsync<T1, T2>(string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return await _connection.QueryAsync<T1, T2>(sql, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore CS0618
    }
}
