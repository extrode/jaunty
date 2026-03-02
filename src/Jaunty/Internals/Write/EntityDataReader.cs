using System.Collections;
using System.Data;
using System.Linq.Expressions;

using Jaunty.Internals.Entity;

namespace Jaunty.Internals.Write;

/// <summary>
/// Adapts an enumerable of entities to <see cref="IDataReader"/> for bulk copy operations.
/// Uses compiled property getters for efficient value access.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
internal sealed class EntityDataReader<T> : IDataReader, IEnumerable
{
    private readonly IEnumerator<T> _enumerator;
    private readonly Func<T, object?>[] _getters;
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
        _columns = GetInsertableColumns(metadata).ToArray();
        _getters = BuildGetters(metadata);
    }

    /// <inheritdoc/>
    public object GetValue(int i)
    {
        if (_enumerator.Current is null)
            return DBNull.Value;

        var value = _getters[i](_enumerator.Current);
        return value ?? DBNull.Value;
    }

    /// <inheritdoc/>
    public int GetValues(object[] values)
    {
        if (_enumerator.Current is null)
            return 0;

        for (int i = 0; i < _columns.Length; i++)
        {
            values[i] = _getters[i](_enumerator.Current) ?? DBNull.Value;
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
    public Type GetFieldType(int i) => _columns[i].Property.PropertyType;

    /// <inheritdoc/>
    public string GetDataTypeName(int i) => GetFieldType(i).Name;

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator)_enumerator;

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
    /// Gets the insertable (non-identity, non-computed) columns from metadata.
    /// </summary>
    private static List<ColumnMetadata> GetInsertableColumns(EntityMetadata metadata)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.NonIdentityColumns;
        var insertable = new List<ColumnMetadata>(columns.Count);
        for (int i = 0; i < columns.Count; i++)
        {
            if (!columns[i].IsComputed)
                insertable.Add(columns[i]);
        }
        return insertable;
    }

    /// <summary>
    /// Builds compiled property getters for efficient value access.
    /// </summary>
    private static Func<T, object?>[] BuildGetters(EntityMetadata metadata)
    {
        var columns = GetInsertableColumns(metadata);
        var getters = new Func<T, object?>[columns.Count];

        for (int i = 0; i < columns.Count; i++)
        {
            var prop = columns[i].Property;
            var param = Expression.Parameter(typeof(T), "e");
            var access = Expression.Property(param, prop);
            var box = Expression.Convert(access, typeof(object));
            getters[i] = Expression.Lambda<Func<T, object?>>(box, param).Compile();
        }

        return getters;
    }
}
