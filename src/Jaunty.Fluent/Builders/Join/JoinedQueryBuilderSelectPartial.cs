using System.Data;

using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent;

/// <summary>
/// SelectPartial operations for 2-table joins.
/// </summary>
internal partial class JoinedQueryBuilder<TFrom, TJoin>
{
    public List<IDictionary<string, object?>> SelectPartial(string columns)
    {
        string sql = BuildSelectPartialSql(columns);
        var results = new List<IDictionary<string, object?>>();

        using IDbCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed)
            _connection.Open();

        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
                results.Add(MapToDictionary(reader));
        }
        finally
        {
            if (wasClosed)
                _connection.Close();
        }

        return results;
    }

    public List<T> SelectPartial<T>(string columns, Func<IDataReader, T> mapper)
    {
        string sql = BuildSelectPartialSql(columns);
        var results = new List<T>();

        using IDbCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed)
            _connection.Open();

        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
                results.Add(mapper(reader));
        }
        finally
        {
            if (wasClosed)
                _connection.Close();
        }

        return results;
    }

    public IDictionary<string, object?> SelectPartialFirst(string columns)
    {
        IDictionary<string, object?>? result = SelectPartialFirstOrDefault(columns);
        return result ?? throw new InvalidOperationException("Sequence contains no elements.");
    }

    public T SelectPartialFirst<T>(string columns, Func<IDataReader, T> mapper)
    {
        T? result = SelectPartialFirstOrDefault(columns, mapper);
        return result ?? throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
    }

    public IDictionary<string, object?>? SelectPartialFirstOrDefault(string columns)
    {
        string sql = _dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 1);

        using IDbCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed)
            _connection.Open();

        try
        {
            using IDataReader reader = command.ExecuteReader();
            return reader.Read() ? MapToDictionary(reader) : null;
        }
        finally
        {
            if (wasClosed)
                _connection.Close();
        }
    }

    public T? SelectPartialFirstOrDefault<T>(string columns, Func<IDataReader, T> mapper)
    {
        string sql = _dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 1);

        using IDbCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed)
            _connection.Open();

        try
        {
            using IDataReader reader = command.ExecuteReader();
            return reader.Read() ? mapper(reader) : default;
        }
        finally
        {
            if (wasClosed)
                _connection.Close();
        }
    }

    public IDictionary<string, object?> SelectPartialSingle(string columns)
    {
        IDictionary<string, object?>? result = SelectPartialSingleOrDefault(columns);
        return result ?? throw new InvalidOperationException("Sequence contains no elements.");
    }

    public T SelectPartialSingle<T>(string columns, Func<IDataReader, T> mapper)
    {
        T? result = SelectPartialSingleOrDefault(columns, mapper);
        return result ?? throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
    }

    public IDictionary<string, object?>? SelectPartialSingleOrDefault(string columns)
    {
        string sql = _dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 2);
        IDictionary<string, object?>? result = null;
        int count = 0;

        using IDbCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed)
            _connection.Open();

        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
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
                _connection.Close();
        }

        return result;
    }

    public T? SelectPartialSingleOrDefault<T>(string columns, Func<IDataReader, T> mapper)
    {
        string sql = _dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 2);
        T? result = default;
        int count = 0;

        using IDbCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed)
            _connection.Open();

        try
        {
            using IDataReader reader = command.ExecuteReader();

            while (reader.Read())
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
                _connection.Close();
        }

        return result;
    }

    private static IDictionary<string, object?> MapToDictionary(IDataReader reader)
    {
        var dictionary = new Dictionary<string, object?>(reader.FieldCount);

        for (int i = 0; i < reader.FieldCount; i++)
        {
            string name = reader.GetName(i);
            if (dictionary.ContainsKey(name))
                throw new InvalidOperationException(
                    $"Column '{name}' is ambiguous: it appears more than once in the joined result set. " +
                    "Use a column alias in the SelectPartial column list to disambiguate.");

            object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            dictionary[name] = value;
        }

        return dictionary;
    }
}
