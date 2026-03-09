using System.Data;
using System.Data.Common;

namespace Jaunty.Fluent;

/// <summary>
/// Async SelectPartial operations for 2-table joins.
/// </summary>
internal partial class JoinedQueryBuilder<TFrom, TJoin>
{
    public async Task<List<IDictionary<string, object?>>> SelectPartialAsync(
        string columns,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildPartialSelectSql(columns);
        var results = new List<IDictionary<string, object?>>();

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        await using DbCommand command = dbConn.CreateCommand();
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                results.Add(MapToDictionary(reader));
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        return results;
    }

    public async Task<List<T>> SelectPartialAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildPartialSelectSql(columns);
        var results = new List<T>();

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        await using DbCommand command = dbConn.CreateCommand();
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                results.Add(mapper(reader));
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        return results;
    }

    public async Task<IDictionary<string, object?>> SelectPartialFirstAsync(
        string columns,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildPartialSelectSql(columns) + " LIMIT 1";

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        await using DbCommand command = dbConn.CreateCommand();
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return MapToDictionary(reader);
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        return null;
    }

    public async Task<T> SelectPartialFirstAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildPartialSelectSql(columns) + " LIMIT 1";

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        await using DbCommand command = dbConn.CreateCommand();
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return mapper(reader);
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        throw new InvalidOperationException("Sequence contains no elements.");
    }

    public async Task<IDictionary<string, object?>?> SelectPartialFirstOrDefaultAsync(
        string columns,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildPartialSelectSql(columns) + " LIMIT 1";

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        await using DbCommand command = dbConn.CreateCommand();
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return MapToDictionary(reader);
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        return null;
    }

    public async Task<T?> SelectPartialFirstOrDefaultAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildPartialSelectSql(columns) + " LIMIT 1";

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        await using DbCommand command = dbConn.CreateCommand();
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return mapper(reader);
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        return default;
    }

    public async Task<IDictionary<string, object?>> SelectPartialSingleAsync(
        string columns,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildPartialSelectSql(columns) + " LIMIT 2";
        IDictionary<string, object?>? result = null;
        int count = 0;

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        await using DbCommand command = dbConn.CreateCommand();
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                count++;
                if (count > 1)
                    throw new InvalidOperationException("Sequence contains more than one element.");
                result = MapToDictionary(reader);
            }
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        return result ?? throw new InvalidOperationException("Sequence contains no elements.");
    }

    public async Task<T> SelectPartialSingleAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildPartialSelectSql(columns) + " LIMIT 2";
        T? result = default;
        int count = 0;

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        await using DbCommand command = dbConn.CreateCommand();
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                count++;
                if (count > 1)
                    throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.");
                result = mapper(reader);
            }
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        return result ?? throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
    }

    public async Task<IDictionary<string, object?>?> SelectPartialSingleOrDefaultAsync(
        string columns,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildPartialSelectSql(columns) + " LIMIT 2";
        IDictionary<string, object?>? result = null;
        int count = 0;

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        await using DbCommand command = dbConn.CreateCommand();
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                count++;
                if (count > 1)
                    throw new InvalidOperationException("Sequence contains more than one element.");
                result = MapToDictionary(reader);
            }
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        return result;
    }

    public async Task<T?> SelectPartialSingleOrDefaultAsync<T>(
        string columns,
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        string sql = BuildPartialSelectSql(columns) + " LIMIT 2";
        T? result = default;
        int count = 0;

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        await using DbCommand command = dbConn.CreateCommand();
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                count++;
                if (count > 1)
                    throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.");
                result = mapper(reader);
            }
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        return result;
    }
}
