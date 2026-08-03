using System.Data.Common;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.Internals;
using Jaunty.Internals.Read;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public async ValueTask<List<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return await QueryInternalAsync<T>(sql, [], default, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a raw SQL query using <paramref name="options"/> for the command and returns the
    /// results as strongly-typed entities.
    /// </summary>
    /// <typeparam name="T">The entity type to materialize results into.</typeparam>
    /// <param name="sql">The raw SQL query to execute.</param>
    /// <param name="options">Command options - transaction, timeout.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>A list of entities matching the query.</returns>
    /// <remarks>See <see cref="Query{T}(string, CommandOptions)"/> - AUD-R32-009.</remarks>
    public async ValueTask<List<T>> QueryAsync<T>(string sql, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return await QueryInternalAsync<T>(sql, [], options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="QueryAsync{T}(string, CommandOptions, CancellationToken)"/>
    /// <param name="sql">The raw SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="options">Command options - transaction, timeout.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    public async ValueTask<List<T>> QueryAsync<T>(string sql, IEnumerable<(string Name, object? Value)> parameters, CommandOptions options, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        // AUD-R35-257: the sync path used to bind a null array and throw NullReferenceException
        // from the binding loop; the async path threw ArgumentNullException from ToArray. Same
        // mistake, two different failures, neither naming the argument at the call site.
        ArgumentNullException.ThrowIfNull(parameters);

        return await QueryInternalAsync<T>(sql, parameters, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<List<T>> QueryAsync<T>(string sql, IEnumerable<(string Name, object? Value)> parameters, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        // AUD-R35-257: the sync path used to bind a null array and throw NullReferenceException
        // from the binding loop; the async path threw ArgumentNullException from ToArray. Same
        // mistake, two different failures, neither naming the argument at the call site.
        ArgumentNullException.ThrowIfNull(parameters);

        return await QueryInternalAsync<T>(sql, parameters, default, cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<List<T>> QueryInternalAsync<T>(string sql, IEnumerable<(string Name, object? Value)> parameters, CommandOptions options, CancellationToken cancellationToken) where T : class, new()
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
            () => QueryInternalDirectAsync<T>(sql, materialised, described, options, cancellationToken), cancellationToken);
    }

    private async ValueTask<List<T>> QueryInternalDirectAsync<T>(string sql, (string Name, object? Value)[] parameters, object described, CommandOptions options, CancellationToken cancellationToken) where T : class, new()
    {
        CommandObservation.Log(sql, described);

        DuckDBCommand cmd = _connection.CreateCommand();
        await using var cmdDisposer = cmd.ConfigureAwait(false);
        cmd.CommandText = sql;
        NonQueryExecutor.ApplyOptions(cmd, options);

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

        // AUD-R35-075: a repeated column name used to overwrite here, so for `SELECT s.*, c.*`
        // over two tables that both have Id, an Id property was filled from the RIGHTMOST Id with
        // no diagnostic. Core's QueryCoreListDirect routes the same situation through
        // DuplicateColumnNames.Disambiguate, which keeps the first occurrence under its bare name
        // and suffixes later ones _N so no value is lost - the same entity type and the same SQL
        // otherwise mapped differently depending on whether it was read through DuckDb.Query<T>
        // or connection.Query<T>. Distinct from the ColumnMappingCache last-wins collapse
        // (AUD-R26), which is two properties claiming one column name; this is two columns
        // claiming one name.
        var readerColumnNames = new string[reader.FieldCount];
        for (int i = 0; i < reader.FieldCount; i++)
            readerColumnNames[i] = reader.GetName(i);

        string[] uniqueColumnNames = DuplicateColumnNames.Disambiguate(readerColumnNames);

        var columnOrdinals = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < uniqueColumnNames.Length; i++)
            columnOrdinals[uniqueColumnNames[i]] = i;

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
