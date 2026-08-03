using System.Data.Common;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.Fluent;
using System.Globalization;
using Jaunty.Internals;
using Jaunty.Internals.Read;

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

        return QueryInternal<T>(sql, [], default);
    }

    /// <summary>
    /// Executes a raw SQL query using <paramref name="options"/> for the command and returns the
    /// results as strongly-typed entities.
    /// </summary>
    /// <typeparam name="T">The entity type to materialize results into.</typeparam>
    /// <param name="sql">The raw SQL query to execute.</param>
    /// <param name="options">Command options - transaction, timeout.</param>
    /// <returns>A list of entities matching the query.</returns>
    /// <remarks>
    /// AUD-R32-009: the raw-SQL single-entity read was the last read surface on <see cref="DuckDb"/>
    /// with no options overload, so it could not be enlisted in the same transaction as the
    /// <c>Insert</c>/<c>Update</c>/<c>Delete</c> calls beside it. Same reasoning as AUD-R26-068,
    /// which gave <c>QueryMultiEntity</c> its overload.
    /// </remarks>
    public List<T> Query<T>(string sql, CommandOptions options) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return QueryInternal<T>(sql, [], options);
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

        return QueryInternal<T>(sql, parameters, default);
    }

    /// <inheritdoc cref="Query{T}(string, CommandOptions)"/>
    /// <param name="sql">The raw SQL query to execute.</param>
    /// <param name="options">Command options - transaction, timeout.</param>
    /// <param name="parameters">Parameters for the query.</param>
    public List<T> Query<T>(string sql, CommandOptions options, params (string Name, object? Value)[] parameters) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        return QueryInternal<T>(sql, parameters, options);
    }

    private List<T> QueryInternal<T>(string sql, (string Name, object? Value)[] parameters, CommandOptions options) where T : class, new()
        => CommandObservation.Execute(
            sql, DuckDbObservation.Describe(parameters), _connection, DuckDbObservation.Text,
            () => QueryInternalDirect<T>(sql, parameters, options));

    private List<T> QueryInternalDirect<T>(string sql, (string Name, object? Value)[] parameters, CommandOptions options) where T : class, new()
    {
        CommandObservation.Log(sql, DuckDbObservation.Describe(parameters));

        using DuckDBCommand cmd = _connection.CreateCommand();
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

        using DuckDBDataReader reader = cmd.ExecuteReader();

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
        while (reader.Read())
        {
            var entity = new T();
            for (int i = 0; i < mappingList.Count; i++)
            {
                var ordinal = ordinalMap[i];
                if (ordinal >= 0 && !reader.IsDBNull(ordinal))
                {
                    var value = reader.GetValue(ordinal);
                    // AUD-R26: routed through the shared converter so the enum and TimeSpan cases
                    // work here as well as on the import path, and so a value that genuinely cannot
                    // be converted reports the column and property rather than reaching the compiled
                    // setter and throwing a bare InvalidCastException that names neither.
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