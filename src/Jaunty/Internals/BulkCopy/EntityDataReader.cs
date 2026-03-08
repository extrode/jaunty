using System.Collections;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

using Jaunty.Internals.Entity;

namespace Jaunty.Internals.BulkCopy;

/// <summary>
/// Adapts an enumerable of entities to <see cref="IDataReader"/> for bulk copy operations.
/// Uses compiled property getters cached per type for zero-reflection performance.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
internal sealed class EntityDataReader<T> : IDataReader, IEnumerable where T : new()
{
    private readonly IEnumerator<T> _enumerator;
    private readonly ColumnMetadata[] _columns;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityDataReader{T}"/> class.
    /// </summary>
    /// <param name="entities">The entities to read.</param>
    /// <param name="metadata">The entity metadata containing column information.</param>
    public EntityDataReader(IEnumerable<T> entities, EntityMetadata metadata)
    {
        _enumerator = entities.GetEnumerator();
        _columns = ColumnMetadataHelper.GetInsertableColumns(metadata).ToArray();

        // Initialize cached getters for this type if not already done
        if (Getters.Length == 0)
        {
            EntityDataReaderCache<T>.Initialize(_columns);
        }
    }

    /// <inheritdoc/>
    public object GetValue(int i)
    {
        if (_enumerator.Current is null)
            return DBNull.Value;

        var value = Getters[i](_enumerator.Current);
        return value ?? DBNull.Value;
    }

    /// <inheritdoc/>
    public int GetValues(object[] values)
    {
        if (_enumerator.Current is null)
            return 0;

        for (int i = 0; i < _columns.Length; i++)
        {
            values[i] = Getters[i](_enumerator.Current) ?? DBNull.Value;
        }

        return _columns.Length;
    }

    /// <inheritdoc/>
    public bool Read() => _enumerator.MoveNext();

    /// <inheritdoc/>
    public int Depth => 0;

    /// <inheritdoc/>
    public bool IsClosed => _disposed;

    /// <inheritdoc/>
    public int RecordsAffected => 0;

    /// <inheritdoc/>
    public int FieldCount => _columns.Length;

    /// <inheritdoc/>
    public object this[int i] => GetValue(i);

    /// <inheritdoc/>
    public object this[string name] => GetValue(GetOrdinal(name));

    /// <inheritdoc/>
    public void Close() { }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _enumerator.Dispose();
            _disposed = true;
        }
    }

    /// <inheritdoc/>
    public DataTable GetSchemaTable() => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool NextResult() => false;

    /// <inheritdoc/>
    public bool IsDBNull(int i) => GetValue(i) is DBNull;

    /// <inheritdoc/>
    public int GetOrdinal(string name)
    {
        for (int i = 0; i < _columns.Length; i++)
        {
            if (_columns[i].ColumnName == name)
                return i;
        }

        throw new IndexOutOfRangeException($"Column '{name}' not found in entity metadata.");
    }

    /// <inheritdoc/>
    public string GetName(int i) => _columns[i].ColumnName;

    /// <inheritdoc/>
#pragma warning disable IL2093 // Interface mismatch in DynamicallyAccessedMembersAttribute
    public Type GetFieldType(int i) => _columns[i].Property.PropertyType;
#pragma warning restore IL2093

    /// <inheritdoc/>
    public string GetDataTypeName(int i) => GetFieldType(i).Name;

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => _enumerator;

    /// <inheritdoc/>
    IDataReader IDataRecord.GetData(int i) => throw new NotSupportedException();

    // IDataReader methods not typically used by bulk copy APIs
    /// <inheritdoc/>
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool GetBoolean(int i) => (bool)GetValue(i);

    /// <inheritdoc/>
    public byte GetByte(int i) => (byte)GetValue(i);

    /// <inheritdoc/>
    public char GetChar(int i) => (char)GetValue(i);

    /// <inheritdoc/>
    public DateTime GetDateTime(int i) => (DateTime)GetValue(i);

    /// <inheritdoc/>
    public decimal GetDecimal(int i) => (decimal)GetValue(i);

    /// <inheritdoc/>
    public double GetDouble(int i) => (double)GetValue(i);

    /// <inheritdoc/>
    public float GetFloat(int i) => (float)GetValue(i);

    /// <inheritdoc/>
    public Guid GetGuid(int i) => (Guid)GetValue(i);

    /// <inheritdoc/>
    public short GetInt16(int i) => (short)GetValue(i);

    /// <inheritdoc/>
    public int GetInt32(int i) => (int)GetValue(i);

    /// <inheritdoc/>
    public long GetInt64(int i) => (long)GetValue(i);

    /// <inheritdoc/>
    public string GetString(int i) => (string)GetValue(i);

    /// <inheritdoc/>
    public object? GetProviderSpecificValue(int i) => GetValue(i);

    /// <inheritdoc/>
    public int GetProviderSpecificValues(object[] values) => GetValues(values);

    /// <inheritdoc/>
    public Task<bool> IsDBNullAsync(int i, CancellationToken cancellationToken)
        => Task.FromResult(IsDBNull(i));

    /// <summary>
    /// Gets the cached compiled property getters for type T.
    /// Getters are compiled once per type and reused for all instances.
    /// </summary>
    private static Func<T, object?>[] Getters => EntityDataReaderCache<T>.Getters;

    /// <summary>
    /// Static generic cache for entity readers.
    /// Compiled getters are created once per type and reused forever.
    /// Thread-safe initialization using double-check locking pattern.
    /// </summary>
    private static class EntityDataReaderCache<TEntity> where TEntity : new()
    {
        private static readonly object _lock = new();
        private static Func<TEntity, object?>[]? _getters;

        /// <summary>
        /// Gets the cached compiled property getters for type TEntity.
        /// Returns empty array if not yet initialized.
        /// </summary>
        public static Func<TEntity, object?>[] Getters => _getters ?? Array.Empty<Func<TEntity, object?>>();

        /// <summary>
        /// Initializes the cached getters for this type.
        /// Thread-safe: only the first call takes effect.
        /// </summary>
        /// <param name="columns">The column metadata to build getters for.</param>
        public static void Initialize(ColumnMetadata[] columns)
        {
            if (_getters is null)
            {
                lock (_lock)
                {
                    if (_getters is null)
                        _getters = BuildGetters(columns);
                }
            }
        }

        private static Func<TEntity, object?>[] BuildGetters(ColumnMetadata[] columns)
        {
            var getters = new Func<TEntity, object?>[columns.Length];

            for (int i = 0; i < columns.Length; i++)
            {
                PropertyInfo prop = columns[i].Property;
                ParameterExpression param = Expression.Parameter(typeof(TEntity), "e");
                MemberExpression access = Expression.Property(param, prop);
                UnaryExpression box = Expression.Convert(access, typeof(object));
                getters[i] = Expression.Lambda<Func<TEntity, object?>>(box, param).Compile();
            }

            return getters;
        }
    }
}