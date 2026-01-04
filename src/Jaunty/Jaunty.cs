using System.Data;
using System.Data.Common;

using Jaunty.Entity;
using Jaunty.Enums;
using Jaunty.Internal.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    private static T QueryScalarCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options)
    {
        return ExecuteReader(connection, sql, parameters, options.Transaction, options.CommandTimeout, reader =>
        {
            if (reader is DbDataReader dbReader)
                return !dbReader.Read() || dbReader.IsDBNull(0) ? default! : dbReader.GetFieldValue<T>(0);

            if (!reader.Read() || reader.IsDBNull(0)) return default!;

            var obj = reader.GetValue(0);
            return (T)Convert.ChangeType(obj, typeof(T));
        });
    }

    private static List<T> QueryCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T : new()
    {
        return ExecuteReader(connection, sql, parameters, options.Transaction, options.CommandTimeout, reader =>
        {
            var results = new List<T>();
            var setters = MetadataCache<T>.GetSetters(reader, mode);

            while (reader.Read())
            {
                var entity = new T();

                for (int i = 0; i < setters.Length; i++)
                    setters[i].Set(entity, reader);

                results.Add(entity);
            }

            return results;
        });
    }

    private static TResult ExecuteReader<TResult>(IDbConnection connection, string sql, object? parameters, IDbTransaction? transaction,
        int? commandTimeout, Func<IDataReader, TResult> handler)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException(nameof(sql));
#endif
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (transaction is not null)
                command.Transaction = transaction;

            if (commandTimeout.HasValue)
                command.CommandTimeout = commandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using var reader = command.ExecuteReader();
            return handler(reader);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }
}
