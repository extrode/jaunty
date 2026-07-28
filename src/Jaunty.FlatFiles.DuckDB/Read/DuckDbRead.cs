using System.Data.Common;

using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.Fluent;
using System.Globalization;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public IFromClause<T> Query<T>() where T : class, new()
    {
        GetSourceOrThrow<T>();
        return FluentExtensions.From<T>(_connection);
    }

    /// <summary>
    /// Executes a raw SQL query and returns the results as strongly-typed entities.
    /// </summary>
    /// <typeparam name="T">The entity type to materialize results into.</typeparam>
    /// <param name="sql">The raw SQL query to execute.</param>
    /// <returns>A list of entities matching the query.</returns>
    public List<T> Query<T>(string sql) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return QueryInternal<T>(sql, []);
    }

    /// <summary>
    /// Executes a raw SQL query with parameters and returns the results as strongly-typed entities.
    /// </summary>
    /// <typeparam name="T">The entity type to materialize results into.</typeparam>
    /// <param name="sql">The raw SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <returns>A list of entities matching the query.</returns>
    public List<T> Query<T>(string sql, params (string Name, object? Value)[] parameters) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return QueryInternal<T>(sql, parameters);
    }

    private List<T> QueryInternal<T>(string sql, (string Name, object? Value)[] parameters) where T : class, new()
    {
        using DuckDBCommand cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        foreach ((string name, object? value) in parameters)
        {
            DbParameter param = cmd.CreateParameter();
            // Callers may pass either the bare name ("region", matching a "$region" placeholder)
            // or the placeholder text itself including its "$" prefix ("$1", matching "$1") - strip
            // a leading "$" so both forms bind to the DuckDB ADO.NET parameter name, which excludes it.
            param.ParameterName = name.Length > 0 && name[0] == '$' ? name[1..] : name;
            param.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }

        using DuckDBDataReader reader = cmd.ExecuteReader();

        var columnOrdinals = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reader.FieldCount; i++)
            columnOrdinals[reader.GetName(i)] = i;

        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(T));
        var mappingList = mappings.Values.ToList();
        var ordinalMap = new int[mappingList.Count];
        for (int i = 0; i < mappingList.Count; i++)
            ordinalMap[i] = columnOrdinals.TryGetValue(mappingList[i].ColumnName, out var ord) ? ord : -1;

        var results = new List<T>();
        while (reader.Read())
        {
            var entity = new T();
            for (int i = 0; i < mappingList.Count; i++)
            {
                var ordinal = ordinalMap[i];
                if (ordinal >= 0 && !reader.IsDBNull(ordinal))
                {
                    var value = reader.GetValue(ordinal);
                    Type targetType = mappingList[i].PropertyType;
                    if (value != null && value.GetType() != targetType)
                    {
                        try
                        {
                            value = System.Convert.ChangeType(value, Nullable.GetUnderlyingType(targetType) ?? targetType, CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            // If conversion fails, let the property setter handle it
                        }
                    }
                    mappingList[i].Setter(entity, value);
                }
            }
            results.Add(entity);
        }
        return results;
    }
}