using System.Data.Common;

using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Internals;
using System.Globalization;
using Jaunty.Internals;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public async ValueTask<List<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return await QueryInternalAsync<T>(sql, [], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<List<T>> QueryAsync<T>(string sql, IEnumerable<(string Name, object? Value)> parameters, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return await QueryInternalAsync<T>(sql, parameters, cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<List<T>> QueryInternalAsync<T>(string sql, IEnumerable<(string Name, object? Value)> parameters, CancellationToken cancellationToken) where T : class, new()
    {
        // Materialised once: the caller's sequence is enumerated to bind the command, and
        // describing it separately for the interceptor and again for the logger would enumerate a
        // lazy sequence three times - and a sequence that yields different values on re-enumeration
        // would have the audit record disagree with what was actually bound.
        (string Name, object? Value)[] materialised =
            parameters as (string Name, object? Value)[] ?? System.Linq.Enumerable.ToArray(parameters);

        object described = DuckDbObservation.Describe(materialised);

        return CommandObservation.ExecuteAsync(
            sql, described, _connection, DuckDbObservation.Text,
            () => QueryInternalDirectAsync<T>(sql, materialised, described, cancellationToken), cancellationToken);
    }

    private async ValueTask<List<T>> QueryInternalDirectAsync<T>(string sql, (string Name, object? Value)[] parameters, object described, CancellationToken cancellationToken) where T : class, new()
    {
        CommandObservation.Log(sql, described);

        DuckDBCommand cmd = _connection.CreateCommand();
        await using var cmdDisposer = cmd.ConfigureAwait(false);
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
                    // AUD-R26: see the note in DuckDbRead.QueryInternal - shared converter so the
                    // sync and async read paths cannot drift, and so a failure names the column.
                    mappingList[i].Setter(
                        entity,
                        ReaderValueConverter.ConvertOrThrow(value, mappingList[i], typeof(T)));
                }
            }
            results.Add(entity);
        }
        return results;
    }
}
