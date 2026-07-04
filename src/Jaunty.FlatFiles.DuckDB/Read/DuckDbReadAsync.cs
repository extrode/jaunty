using System.Data.Common;

using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public async ValueTask<List<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken = default) where T : class, new()
    {
        return await QueryInternalAsync<T>(sql, [], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<List<T>> QueryAsync<T>(string sql, IEnumerable<(string Name, object? Value)> parameters, CancellationToken cancellationToken = default) where T : class, new()
    {
        return await QueryInternalAsync<T>(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<List<T>> QueryInternalAsync<T>(string sql, IEnumerable<(string Name, object? Value)> parameters, CancellationToken cancellationToken) where T : class, new()
    {
        DuckDBCommand cmd = _connection.CreateCommand();
        await using var cmdDisposer = cmd.ConfigureAwait(false);
        cmd.CommandText = sql;

        foreach ((string _, object? value) in parameters)
        {
            DbParameter param = cmd.CreateParameter();
            param.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }

        DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerDisposer = reader.ConfigureAwait(false);

        var columnOrdinals = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reader.FieldCount; i++)
            columnOrdinals[reader.GetName(i)] = i;

        IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(typeof(T));
        var mappingList = mappings.Values.ToList();
        var ordinalMap = new int[mappingList.Count];
        for (int i = 0; i < mappingList.Count; i++)
            ordinalMap[i] = columnOrdinals.TryGetValue(mappingList[i].ColumnName, out var ord) ? ord : -1;

        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
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
