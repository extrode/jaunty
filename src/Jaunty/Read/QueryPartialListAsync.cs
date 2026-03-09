using System.Data;
using System.Data.Common;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query asynchronously and returns all results as dictionaries with column names as keys.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of dictionaries, each representing a row with column names as keys.</returns>
    public static async ValueTask<List<IDictionary<string, object?>>> QueryPartialListAsync(this IDbConnection connection, string sql, CancellationToken cancellationToken = default)
    {
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        return await QueryCoreListAsync(dbConnection, sql, null, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters asynchronously and returns all results as dictionaries with column names as keys.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of dictionaries, each representing a row with column names as keys.</returns>
    public static async ValueTask<List<IDictionary<string, object?>>> QueryPartialListAsync(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
    {
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        return await QueryCoreListAsync(dbConnection, sql, parameters, cancellationToken);
    }

    private static async ValueTask<List<IDictionary<string, object?>>> QueryCoreListAsync(DbConnection connection, string sql, object? parameters, CancellationToken cancellationToken)
    {
        bool wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
            await connection.OpenAsync(cancellationToken);

        try
        {
            using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.BindParameters(parameters);

            var results = new List<IDictionary<string, object?>>();

            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string columnName = reader.GetName(i);
                    object? value = reader.GetValue(i);
                    row[columnName] = value == DBNull.Value ? null : value;
                }
                results.Add(row);
            }

            return results;
        }
        finally
        {
            if (wasClosed)
                connection.Close();
        }
    }
}
