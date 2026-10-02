using System.ComponentModel;
using System.Threading;

using Extrode.Jaunty.Internals;
using Extrode.Jaunty.Internals.Entity;

namespace Extrode.Jaunty.Core;

/// <summary>
/// One source-generated entity's table, schema, column and parameter names, resolved under one
/// configuration. Not intended for application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GeneratedNames
{
    private readonly string[] _columns;
    private readonly string[] _parameters;

    internal GeneratedNames(string tableName, string? schemaName, string[] columns)
    {
        TableName = tableName;
        SchemaName = schemaName;
        _columns = columns;
        _parameters = new string[columns.Length];
        for (int i = 0; i < columns.Length; i++)
            _parameters[i] = "@" + columns[i];
    }

    /// <summary>The resolved table name.</summary>
    public string TableName { get; }

    /// <summary>The resolved schema name, or <see langword="null"/> for none.</summary>
    public string? SchemaName { get; }

    /// <summary>The resolved column name of the property at <paramref name="index"/>, in declaration order.</summary>
    public string Column(int index) => _columns[index];

    /// <summary>
    /// The parameter name the generated binders bind the property at <paramref name="index"/>
    /// under: <c>"@"</c> plus its column name, as <c>CrudSqlCache</c> names it in the SQL.
    /// </summary>
    public string Parameter(int index) => _parameters[index];
}

/// <summary>
/// Resolves a source-generated entity's names through <see cref="NameResolution"/> and caches
/// what the generated code builds from them, once per configuration generation. Not intended for
/// application code.
/// </summary>
/// <remarks>
/// The generator records which names came from an attribute and leaves the rest to this class, so
/// a <c>JauntyConfig</c> resolver applies to a source-generated entity exactly as it does under
/// reflection. Every resolver setter bumps the configuration generation, and the next
/// <see cref="Current"/> rebuilds; with no resolver change, a read costs one generation compare.
/// Generated code runs in the consumer's assembly, so this bridge must be public.
/// </remarks>
/// <typeparam name="TState">What the generated code builds from the resolved names.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GeneratedNameCache<TState> where TState : class
{
    private readonly Type _entityType;
    private readonly string? _attributeTable;
    private readonly string? _attributeSchema;
    private readonly string[] _propertyNames;
    private readonly string?[] _attributeColumns;
    private readonly Func<GeneratedNames, TState> _build;
    private Entry? _entry;

    /// <param name="entityType">The entity, handed to the table and schema resolvers.</param>
    /// <param name="attributeTable">The <c>[Table]</c> name, or <see langword="null"/> when none applies.</param>
    /// <param name="attributeSchema">The <c>[Table]</c> schema, or <see langword="null"/> when none applies.</param>
    /// <param name="propertyNames">Every mapped property, in declaration order.</param>
    /// <param name="attributeColumns">Each property's <c>[Column]</c> name, or <see langword="null"/> where none applies.</param>
    /// <param name="build">Builds the generated state from the resolved names.</param>
    public GeneratedNameCache(
        Type entityType,
        string? attributeTable,
        string? attributeSchema,
        string[] propertyNames,
        string?[] attributeColumns,
        Func<GeneratedNames, TState> build)
    {
        _entityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        _propertyNames = propertyNames ?? throw new ArgumentNullException(nameof(propertyNames));
        _attributeColumns = attributeColumns ?? throw new ArgumentNullException(nameof(attributeColumns));
        _build = build ?? throw new ArgumentNullException(nameof(build));

        if (attributeColumns.Length != propertyNames.Length)
            throw new ArgumentException("There must be one attribute column entry per property.", nameof(attributeColumns));

        _attributeTable = attributeTable;
        _attributeSchema = attributeSchema;
    }

    /// <summary>The state built from the names resolved under the current configuration.</summary>
    public TState Current
    {
        get
        {
            // Read the generation before the lookup, never after: see ConfigurationGeneration.Current.
            int generation = ConfigurationGeneration.Current;
            Entry? entry = Volatile.Read(ref _entry);
            if (entry is not null && entry.Generation == generation)
                return entry.State;

            return Rebuild(generation);
        }
    }

    private TState Rebuild(int generation)
    {
        var columns = new string[_propertyNames.Length];
        for (int i = 0; i < columns.Length; i++)
            columns[i] = NameResolution.Column(_propertyNames[i], _attributeColumns[i]);

        ThrowIfResolverMergedColumns(columns);

        var names = new GeneratedNames(
            NameResolution.Table(_entityType, _attributeTable),
            NameResolution.Schema(_entityType, _attributeSchema),
            columns);

        TState state = _build(names);
        Volatile.Write(ref _entry, new Entry(generation, state));
        return state;
    }

    /// <summary>
    /// Rejects a resolver that maps two properties onto one column, as reflection metadata does.
    /// </summary>
    /// <remarks>
    /// The generated reader never builds <c>EntityMetadata</c>, so without this check both
    /// properties would silently read the same column. A collision already present in the build-time
    /// names is left alone: the generator reports it as a diagnostic, and the generated code has
    /// always tolerated it on reads.
    /// </remarks>
    private void ThrowIfResolverMergedColumns(string[] columns)
    {
        var seen = new Dictionary<string, int>(columns.Length, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < columns.Length; i++)
        {
            if (!seen.TryGetValue(columns[i], out int first))
            {
                seen[columns[i]] = i;
                continue;
            }

            if (!string.Equals(BuildTimeName(first), BuildTimeName(i), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"JauntyConfig.ColumnNameResolver maps more than one property of '{_entityType.Name}' to column " +
                    $"'{columns[i]}': '{_propertyNames[first]}' and '{_propertyNames[i]}'. Column names are matched " +
                    "case-insensitively, so two properties cannot share one. Give one of them a [Column(\"...\")] name, " +
                    "or make the resolver return distinct names.",
                    "columns");
            }
        }
    }

    private string BuildTimeName(int index) =>
        string.IsNullOrEmpty(_attributeColumns[index]) ? _propertyNames[index] : _attributeColumns[index]!;

    private sealed class Entry
    {
        public Entry(int generation, TState state)
        {
            Generation = generation;
            State = state;
        }

        public int Generation { get; }

        public TState State { get; }
    }
}
