using System.Data;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Import;
using Extrode.Jaunty.TypeHandlers;

namespace Extrode.Jaunty.Configuration;

/// <summary>
/// The settings passed to <see cref="JauntyConfig.Configure"/>: names, mapping hooks, enum storage
/// and type handlers. Set them inside the <c>Configure</c> callback; once it returns they are fixed
/// for the life of the process.
/// </summary>
/// <remarks>
/// Every property here decides a table, column or parameter name, or how a value is mapped. One
/// operation reads them more than once (for its SQL text, then for its parameters or reader
/// ordinals), so a change that lands between those reads would pair SQL from one configuration with
/// parameters from another. Fixing them at startup removes that window. Settings that are read once
/// per use - <see cref="JauntyConfig.Logger"/>, the capacities, interceptors,
/// <see cref="BulkCopyConfiguration"/> and dialect registration - stay settable at runtime on
/// <see cref="JauntyConfig"/>. See docs/decisions/2026-10-02-014-configuration-is-set-once-at-startup.md.
/// </remarks>
public sealed class JauntyConfigBuilder
{
    private readonly List<KeyValuePair<Type, ITypeHandler>> _typeHandlers;

    internal JauntyConfigBuilder()
    {
        _typeHandlers = new List<KeyValuePair<Type, ITypeHandler>>();
    }

    internal JauntyConfigBuilder(JauntySettings from)
    {
        SchemaNameResolver = from.SchemaNameResolver;
        TableNameResolver = from.TableNameResolver;
        ColumnNameResolver = from.ColumnNameResolver;
        ReflectionMapperResolver = from.ReflectionMapperResolver;
        ReflectionInsertBinderResolver = from.ReflectionInsertBinderResolver;
        ReflectionUpdateBinderResolver = from.ReflectionUpdateBinderResolver;
        ReflectionDeleteBinderResolver = from.ReflectionDeleteBinderResolver;
        ReflectionTableMetadataResolver = from.ReflectionTableMetadataResolver;
        ReflectionMultiMapperResolver = from.ReflectionMultiMapperResolver;
        ReflectionMultiMapperResolverN = from.ReflectionMultiMapperResolverN;
        SpecialTypeMapperResolver = from.SpecialTypeMapperResolver;
        CopyImportFactory = from.CopyImportFactory;
        DefaultEnumStorage = from.DefaultEnumStorage;
        _typeHandlers = new List<KeyValuePair<Type, ITypeHandler>>(from.TypeHandlers);
        AddedTypeHandlersFrom = _typeHandlers.Count;
    }

    /// <summary>
    /// Resolves an entity type's schema name.
    /// </summary>
    /// <remarks>
    /// Global and dialect-blind: it receives only the entity type, so one resolver serves every
    /// connection in the process and its return value is emitted verbatim on every engine. A
    /// constant such as <c>_ => "dbo"</c> therefore produces "dbo.products" on SQLite and
    /// PostgreSQL too. Scope it by type, or return <see cref="string.Empty"/> to leave an entity
    /// unqualified, which is Extrode.Jaunty's default. A non-empty [Table] schema wins over this
    /// resolver, and the resolver is then not called; a <see langword="null"/> result means no
    /// schema. Applies to reflection-mapped and source-generated entities alike.
    /// </remarks>
    public Func<Type, string>? SchemaNameResolver { get; set; }

    /// <summary>
    /// Resolves an entity type's table name.
    /// </summary>
    /// <remarks>
    /// Consulted only when no [Table] attribute gives a non-empty name. A <see langword="null"/>
    /// result falls back to the type name; any other result, <see cref="string.Empty"/> included,
    /// is used as given. Applies to reflection-mapped and source-generated entities alike.
    /// </remarks>
    public Func<Type, string>? TableNameResolver { get; set; }

    /// <summary>
    /// Resolves a property's column name.
    /// </summary>
    /// <remarks>
    /// Receives the property name, and is consulted only when no [Column] attribute gives a
    /// non-empty name. A <see langword="null"/> result falls back to the property name; any other
    /// result is used as given. Applies to reflection-mapped and source-generated entities alike.
    /// </remarks>
    public Func<string, string>? ColumnNameResolver { get; set; }

    /// <summary>
    /// Fallback mapper resolver for types that are not source-generated. Set by
    /// <c>UseReflectionMapping()</c> from Extrode.Jaunty.Extensions.Reflection. Returns a
    /// Func&lt;IDataReader, T&gt; or Func&lt;DbDataReader, T&gt; cast to object.
    /// </summary>
    public Func<Type, MappingMode, object>? ReflectionMapperResolver { get; set; }

    /// <summary>
    /// Fallback parameter binder resolver for INSERT operations.
    /// </summary>
    public Func<Type, Action<IDbCommand, object>>? ReflectionInsertBinderResolver { get; set; }

    /// <summary>
    /// Fallback parameter binder resolver for UPDATE operations.
    /// </summary>
    public Func<Type, Action<IDbCommand, object>>? ReflectionUpdateBinderResolver { get; set; }

    /// <summary>
    /// Fallback parameter binder resolver for DELETE operations.
    /// </summary>
    public Func<Type, Action<IDbCommand, object>>? ReflectionDeleteBinderResolver { get; set; }

    /// <summary>
    /// Fallback table metadata resolver for types that are not source-generated. Returns an
    /// EntityMetadata object.
    /// </summary>
    public Func<Type, object>? ReflectionTableMetadataResolver { get; set; }

    /// <summary>
    /// Fallback multi-mapper resolver for types that are not source-generated. Returns a
    /// MultiEntityMapper object.
    /// </summary>
    public Func<Type, Type, object>? ReflectionMultiMapperResolver { get; set; }

    /// <summary>
    /// Fallback multi-mapper resolver for arity-3+ multi-entity queries: one
    /// Action&lt;object, IDataRecord&gt; per entity type position.
    /// </summary>
    public Func<Type[], IDataReader, Action<object, IDataRecord>[]>? ReflectionMultiMapperResolverN { get; set; }

    /// <summary>
    /// Resolver for special types (Dictionary, KeyValuePair, ValueTuple, ExpandoObject). Set by
    /// <c>UseReflectionMapping()</c>. Returns a Func&lt;IDataReader, object&gt;.
    /// </summary>
    public Func<Type, IDataReader, object>? SpecialTypeMapperResolver { get; set; }

    /// <summary>
    /// Client-side bulk-copy provider for CSV import's streaming <c>COPY ... FROM STDIN</c> path.
    /// </summary>
    /// <remarks>
    /// Core declares no dependency on a database driver, so it cannot call a provider's copy API
    /// directly. Install <c>Extrode.Jaunty.Extensions.Npgsql</c> and call <c>UseNpgsqlCopy()</c>
    /// on this builder for PostgreSQL, or assign your own factory for another driver. Leave it unset
    /// and CSV import uses the server-side path, where the database engine opens the file.
    /// </remarks>
    public CopyImportFactory? CopyImportFactory { get; set; }

    /// <summary>
    /// How an enum without an <see cref="EnumStorageAttribute"/> is stored. Default:
    /// <see cref="EnumStorage.Numeric"/>.
    /// </summary>
    public EnumStorage DefaultEnumStorage { get; set; } = EnumStorage.Numeric;

    internal IReadOnlyList<KeyValuePair<Type, ITypeHandler>> TypeHandlers => _typeHandlers;

    /// <summary>Index of the first handler added to this builder rather than copied into it.</summary>
    internal int AddedTypeHandlersFrom { get; }

    internal void RemoveTypeHandlers(Type key) => _typeHandlers.RemoveAll(h => h.Key == key);

    /// <summary>
    /// Registers a type handler using delegate-based conversion functions.
    /// </summary>
    /// <remarks>
    /// A handler for <c>Nullable&lt;X&gt;</c> is keyed on <c>X</c>, since every read and write
    /// lookup asks for the underlying type. Registering one for <c>X</c> and one for <c>X?</c>
    /// keeps whichever came last.
    /// </remarks>
    public JauntyConfigBuilder RegisterTypeHandler<T>(Func<object?, T> fromDb, Func<T?, object?> toDb)
    {
        if (fromDb is null) throw new ArgumentNullException(nameof(fromDb));
        if (toDb is null) throw new ArgumentNullException(nameof(toDb));
        _typeHandlers.Add(new KeyValuePair<Type, ITypeHandler>(TypeHandlerRegistry.KeyFor<T>(), new DelegateTypeHandler<T>(fromDb, toDb)));
        return this;
    }

    /// <summary>
    /// Registers a type handler using a <see cref="TypeHandler{T}"/> instance.
    /// </summary>
    /// <remarks>
    /// A handler for <c>Nullable&lt;X&gt;</c> is keyed on <c>X</c>, since every read and write
    /// lookup asks for the underlying type. Registering one for <c>X</c> and one for <c>X?</c>
    /// keeps whichever came last.
    /// <para>
    /// A repeated <see cref="JauntyConfig.Configure"/> compares handlers by their type, so a new
    /// instance of the same handler class on each run counts as the same setting.
    /// </para>
    /// </remarks>
    public JauntyConfigBuilder RegisterTypeHandler<T>(TypeHandler<T> handler)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        _typeHandlers.Add(new KeyValuePair<Type, ITypeHandler>(TypeHandlerRegistry.KeyFor<T>(), new AdaptedTypeHandler<T>(handler)));
        return this;
    }

    private sealed class AdaptedTypeHandler<T> : ITypeHandler
    {
        private readonly TypeHandler<T> _handler;
        internal AdaptedTypeHandler(TypeHandler<T> handler) => _handler = handler;
        object? ITypeHandler.Parse(object? dbValue) => _handler.Parse(dbValue);
        object? ITypeHandler.ToDbValue(object? value) => _handler.ToDbValue((T?)value);
        public override bool Equals(object? obj) => obj is AdaptedTypeHandler<T> other && _handler.GetType() == other._handler.GetType();
        public override int GetHashCode() => _handler.GetType().GetHashCode();
    }

    private sealed class DelegateTypeHandler<T> : ITypeHandler
    {
        private readonly Func<object?, T> _fromDb;
        private readonly Func<T?, object?> _toDb;
        internal DelegateTypeHandler(Func<object?, T> fromDb, Func<T?, object?> toDb)
        {
            _fromDb = fromDb;
            _toDb = toDb;
        }
        object? ITypeHandler.Parse(object? dbValue) => _fromDb(dbValue);
        object? ITypeHandler.ToDbValue(object? value) => _toDb((T?)value);
        public override bool Equals(object? obj) => obj is DelegateTypeHandler<T> other && _fromDb.Equals(other._fromDb) && _toDb.Equals(other._toDb);
        public override int GetHashCode() => _fromDb.GetHashCode();
    }
}
