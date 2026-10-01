using System.Data;

using Extrode.Jaunty.Core;
using Extrode.Jaunty.Fluent.Internals;
using Extrode.Jaunty.Internals;

namespace Extrode.Jaunty.Fluent;

/// <summary>
/// SelectPartial operations for 2-table joins.
/// </summary>
internal partial class JoinedQueryBuilder<TFrom, TJoin>
{
    public List<IDictionary<string, object?>> SelectPartial(string columns)
        => SelectPartial(columns, default(CommandOptions));

    public List<IDictionary<string, object?>> SelectPartial(string columns, CommandOptions options)
        => ExecutePartial(BuildSelectPartialSql(columns), options, reader =>
        {
            var results = new List<IDictionary<string, object?>>();

            while (reader.Read())
                results.Add(MapToDictionary(reader));

            return results;
        });

    public List<T> SelectPartial<T>(string columns, Func<IDataReader, T> mapper)
        => SelectPartial(columns, mapper, default);

    public List<T> SelectPartial<T>(string columns, Func<IDataReader, T> mapper, CommandOptions options)
        => ExecutePartial(BuildSelectPartialSql(columns), options, reader =>
        {
            var results = new List<T>();

            while (reader.Read())
                results.Add(mapper(reader));

            return results;
        });

    public IDictionary<string, object?> SelectPartialFirst(string columns)
        => SelectPartialFirst(columns, default(CommandOptions));

    public IDictionary<string, object?> SelectPartialFirst(string columns, CommandOptions options)
    {
        IDictionary<string, object?>? result = SelectPartialFirstOrDefault(columns, options);
        return result ?? throw new InvalidOperationException("Sequence contains no elements.");
    }

    public T SelectPartialFirst<T>(string columns, Func<IDataReader, T> mapper)
        => SelectPartialFirst(columns, mapper, default);

    public T SelectPartialFirst<T>(string columns, Func<IDataReader, T> mapper, CommandOptions options)
    {
        (bool found, T? result) = SelectPartialFirstCore(columns, mapper, options);
        if (!found)
            throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
        return result!;
    }

    public IDictionary<string, object?>? SelectPartialFirstOrDefault(string columns)
        => SelectPartialFirstOrDefault(columns, default(CommandOptions));

    public IDictionary<string, object?>? SelectPartialFirstOrDefault(string columns, CommandOptions options)
        => ExecutePartial(_dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 1), options,
            reader => reader.Read() ? MapToDictionary(reader) : null);

    public T? SelectPartialFirstOrDefault<T>(string columns, Func<IDataReader, T> mapper)
        => SelectPartialFirstCore(columns, mapper, default).Value;

    public T? SelectPartialFirstOrDefault<T>(string columns, Func<IDataReader, T> mapper, CommandOptions options)
        => SelectPartialFirstCore(columns, mapper, options).Value;

    /// <summary>
    /// R27 batch 8: emptiness is reported by the flag, not a null test on the mapped value -
    /// with a value-type <typeparamref name="T"/> the old "result ?? throw" saw
    /// <c>default(T)</c> after zero rows and returned 0 instead of throwing, and a mapper
    /// legitimately mapping a row to null was misreported as "no elements".
    /// </summary>
    private (bool Found, T? Value) SelectPartialFirstCore<T>(string columns, Func<IDataReader, T> mapper, CommandOptions options)
        => ExecutePartial(_dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 1), options,
            reader => reader.Read() ? (true, mapper(reader)) : (false, default(T)));

    public IDictionary<string, object?> SelectPartialSingle(string columns)
        => SelectPartialSingle(columns, default(CommandOptions));

    public IDictionary<string, object?> SelectPartialSingle(string columns, CommandOptions options)
    {
        IDictionary<string, object?>? result = SelectPartialSingleOrDefault(columns, options);
        return result ?? throw new InvalidOperationException("Sequence contains no elements.");
    }

    public T SelectPartialSingle<T>(string columns, Func<IDataReader, T> mapper)
        => SelectPartialSingle(columns, mapper, default);

    public T SelectPartialSingle<T>(string columns, Func<IDataReader, T> mapper, CommandOptions options)
    {
        (int count, T? result) = SelectPartialSingleCore(columns, mapper, options);
        if (count == 0)
            throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
        return result!;
    }

    public IDictionary<string, object?>? SelectPartialSingleOrDefault(string columns)
        => SelectPartialSingleOrDefault(columns, default(CommandOptions));

    public IDictionary<string, object?>? SelectPartialSingleOrDefault(string columns, CommandOptions options)
        => ExecutePartial(_dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 2), options, reader =>
        {
            IDictionary<string, object?>? result = null;
            int count = 0;

            while (reader.Read())
            {
                count++;
                if (count > 1)
                    throw new InvalidOperationException("Sequence contains more than one element.");
                result = MapToDictionary(reader);
            }

            return result;
        });

    public T? SelectPartialSingleOrDefault<T>(string columns, Func<IDataReader, T> mapper)
        => SelectPartialSingleCore(columns, mapper, default).Value;

    public T? SelectPartialSingleOrDefault<T>(string columns, Func<IDataReader, T> mapper, CommandOptions options)
        => SelectPartialSingleCore(columns, mapper, options).Value;

    /// <summary>
    /// R27 batch 8: same flag-over-null contract as <see cref="SelectPartialFirstCore{T}"/>,
    /// with the row count carrying both the zero-row and more-than-one-row outcomes.
    /// </summary>
    private (int Count, T? Value) SelectPartialSingleCore<T>(string columns, Func<IDataReader, T> mapper, CommandOptions options)
        => ExecutePartial(_dialect.GetPagingSql(BuildSelectPartialSql(columns), 0, 2), options, reader =>
        {
            T? result = default;
            int count = 0;

            while (reader.Read())
            {
                count++;
                if (count > 1)
                    throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.");
                result = mapper(reader);
            }

            return (count, result);
        });

    /// <summary>
    /// Runs one SelectPartial statement: the command, its options, parameters, observation and the
    /// open/close of a closed connection, with <paramref name="read"/> consuming the reader.
    /// </summary>
    /// <remarks>
    /// AUD-R38-031: every SelectPartial terminal built its own command and none set a transaction,
    /// so on SqlClient (which refuses a command without one while a local transaction is pending)
    /// no joined SelectPartial could run inside the caller's transaction. AUD-R26-060 closed the
    /// same gap for the query, grouped and CTE builders through <see cref="FluentCommandOptions"/>.
    /// </remarks>
    private TResult ExecutePartial<TResult>(string sql, CommandOptions options, Func<IDataReader, TResult> read)
    {
        return CommandObservation.Execute(
            sql, DescribeParameters(), _connection, FluentCommandOptions.Describe(options), Body);

        TResult Body()
        {
            using IDbCommand command = _connection.CreateCommand();
            command.CommandText = sql;
            FluentCommandOptions.Apply(command, _connection, options);
            BindParameters(command);

            CommandObservation.Log(sql, DescribeParameters());

            bool wasClosed = _connection.State == ConnectionState.Closed;
            if (wasClosed)
                _connection.Open();

            try
            {
                using IDataReader reader = command.ExecuteReader();
                return read(reader);
            }
            finally
            {
                if (wasClosed)
                    _connection.Close();
            }
        }
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
