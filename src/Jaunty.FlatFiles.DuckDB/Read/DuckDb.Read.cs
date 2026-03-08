using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public IFromClause<T> Query<T>() where T : class, new()
    {
        GetSourceOrThrow<T>();
        return FluentExtensions.From<T>(_connection);
    }

    /// <inheritdoc />
    public List<T> Query<T>(string sql) where T : class, new()
    {
        return QueryInternal<T>(sql, []);
    }

    /// <inheritdoc />
    public List<T> Query<T>(string sql, params (string Name, object? Value)[] parameters) where T : class, new()
    {
        return QueryInternal<T>(sql, parameters);
    }

    private List<T> QueryInternal<T>(string sql, (string Name, object? Value)[] parameters) where T : class, new()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        foreach (var (_, value) in parameters)
        {
            var param = cmd.CreateParameter();
            param.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }

        using var reader = cmd.ExecuteReader();

        var columnOrdinals = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reader.FieldCount; i++)
            columnOrdinals[reader.GetName(i)] = i;

        var mappings = ColumnMappingCache.Get(typeof(T));
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
                    var targetType = mappingList[i].PropertyType;
                    if (value != null && value.GetType() != targetType)
                    {
                        try
                        {
                            value = System.Convert.ChangeType(value, Nullable.GetUnderlyingType(targetType) ?? targetType);
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