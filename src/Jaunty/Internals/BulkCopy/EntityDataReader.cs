using System.Collections.Concurrent;
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
internal sealed class EntityDataReader<T> : IDataReader where T : new()
{
    private readonly IEnumerator<T> _enumerator;
    private readonly ColumnMetadata[] _columns;
    private readonly Func<T, object?>[] _getters;
    private bool _disposed;

    // See GetValue: one slot of memo so IsDBNull-then-GetValue invokes the getter once. -1 means
    // "nothing memoised", and Read() resets it because the memo is scoped to a single row.
    private int _memoOrdinal = -1;
    private object? _memoValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityDataReader{T}"/> class.
    /// </summary>
    /// <param name="entities">The entities to read.</param>
    /// <param name="metadata">The entity metadata containing column information.</param>
    public EntityDataReader(IEnumerable<T> entities, EntityMetadata metadata)
    {
        _enumerator = entities.GetEnumerator();
        _columns = ColumnMetadataHelper.GetInsertableColumns(metadata).ToArray();

        // Getters are cached per distinct column layout, not just per type, so a second
        // bulk-insert of the same T with a different column subset/order gets its own
        // correctly-matching getters instead of reusing a stale layout's getters.
        _getters = EntityDataReaderCache<T>.GetGetters(_columns);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// AUD-R35-106 (round-35 batch 04a). One slot of memo, reset by <see cref="Read"/>. A consumer
    /// following the standard ADO.NET <c>IsDBNull</c>-then-<c>GetValue</c> pattern used to invoke
    /// the compiled getter and box its result twice per nullable cell, because
    /// <see cref="IsDBNull"/> is implemented on top of this method; now the second call reads the
    /// slot. No in-tree provider takes that route - all three consume the reader through
    /// <c>GetValue</c>/<c>FieldCount</c> only - but this object is handed to <c>SqlBulkCopy</c>,
    /// whose per-cell access pattern Jaunty does not control. That is the same third-party-contract
    /// argument the <see cref="GetOrdinal"/> fix was made on (AUD-R25 B3-4).
    /// <para>
    /// The cost on the plain path is an int compare and two field writes per cell, against a saved
    /// delegate invocation and box per <c>IsDBNull</c>. The memo is scoped to the current row and
    /// the current ordinal, so it holds unless the caller mutates the entity between two reads of
    /// the same cell in the same row, which no bulk-copy consumer does.
    /// </para>
    /// </remarks>
    public object GetValue(int i)
    {
        if (_enumerator.Current is null)
            return DBNull.Value;

        if (_memoOrdinal == i)
            return _memoValue!;

        object value = _getters[i](_enumerator.Current) ?? DBNull.Value;
        _memoOrdinal = i;
        _memoValue = value;
        return value;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// AUD-R35-110, first filed round 27. This wrote <c>_columns.Length</c> elements without
    /// consulting <c>values.Length</c>, so a caller passing a shorter array - which
    /// <see cref="IDataRecord.GetValues"/> explicitly allows, specifying a partial copy and a
    /// returned count - got <see cref="IndexOutOfRangeException"/> instead.
    /// </remarks>
    public int GetValues(object[] values)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(values);
#else
        if (values is null) throw new ArgumentNullException(nameof(values));
#endif

        if (_enumerator.Current is null)
            return 0;

        int count = values.Length < _columns.Length ? values.Length : _columns.Length;

        for (int i = 0; i < count; i++)
        {
            values[i] = _getters[i](_enumerator.Current) ?? DBNull.Value;
        }

        return count;
    }

    /// <inheritdoc/>
    public bool Read()
    {
        _memoOrdinal = -1;
        return _enumerator.MoveNext();
    }

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
    /// <remarks>
    /// AUD-R35-109, first filed round 9. This was an empty body while <see cref="IsClosed"/>
    /// reported <c>_disposed</c>, so after <c>Close()</c> the reader still said it was open and the
    /// enumerator was still undisposed - <see cref="IDataReader"/> requires <c>IsClosed</c> to be
    /// true once <c>Close</c> has been called. Close and Dispose do the same thing here, which is
    /// the shape every ADO.NET reader has.
    /// </remarks>
    public void Close() => Dispose();

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
    /// <remarks>
    /// AUD-R25: this was an ordinal, case-sensitive comparison only.
    /// <see cref="IDataRecord.GetOrdinal"/> is documented to try a case-sensitive lookup first and
    /// then fall back to a case-insensitive one, which every ADO.NET provider reader implements, and
    /// every other column-name lookup in Jaunty is deliberately case-insensitive
    /// (<c>EntityMetadata.ParameterMap</c>, <c>SpParameters.Get</c>, the dictionary special-type
    /// mappers). A caller resolving a differently-cased name got
    /// <see cref="IndexOutOfRangeException"/> instead of the column. No in-tree caller reaches it -
    /// the providers feed back names they got from <see cref="GetName"/> - but this reader is handed
    /// to third-party provider bulk-copy APIs whose lookup behaviour Jaunty does not control.
    /// </remarks>
    public int GetOrdinal(string name)
    {
        for (int i = 0; i < _columns.Length; i++)
        {
            if (string.Equals(_columns[i].ColumnName, name, StringComparison.Ordinal))
                return i;
        }

        // Second pass, not a single case-insensitive pass: an exact match must win over a
        // differently-cased one when an entity maps two columns whose names differ only by case.
        for (int i = 0; i < _columns.Length; i++)
        {
            if (string.Equals(_columns[i].ColumnName, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new IndexOutOfRangeException($"Column '{name}' not found in entity metadata.");
    }

    /// <inheritdoc/>
    public string GetName(int i) => _columns[i].ColumnName;

    /// <inheritdoc/>
#pragma warning disable IL2093 // Interface mismatch in DynamicallyAccessedMembersAttribute
    public Type GetFieldType(int i) => _columns[i].PropertyType;
#pragma warning restore IL2093

    /// <inheritdoc/>
    public string GetDataTypeName(int i) => GetFieldType(i).Name;

    // AUD-R35-111, first filed round 9 and again in round 34. This type used to implement
    // IEnumerable, whose explicit GetEnumerator returned the same _enumerator instance Read()
    // advances rather than a fresh one over the source - so any consumer that enumerated after a
    // Read() resumed mid-stream, and the two surfaces interleaved on one cursor. Nothing needed the
    // interface: the three bulk-copy providers consume this only as IDataReader. Removed rather
    // than fixed, which is what both earlier reports recommended.

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
    /// Static generic cache for entity readers.
    /// Compiled getters are keyed by column layout (not just by type), so distinct
    /// column subsets/orderings for the same <typeparamref name="T"/> each get their
    /// own correctly-matching getters instead of one layout's getters being reused
    /// for a differently-shaped one.
    /// </summary>
    private static class EntityDataReaderCache<TEntity> where TEntity : new()
    {
        private static readonly ConcurrentDictionary<string, Func<TEntity, object?>[]> _gettersByLayout = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets the cached compiled property getters for type TEntity matching the given
        /// column layout, building and caching them on first use for that layout.
        /// </summary>
        /// <param name="columns">The column metadata identifying the layout to get getters for.</param>
        public static Func<TEntity, object?>[] GetGetters(ColumnMetadata[] columns)
            => _gettersByLayout.GetOrAdd(BuildLayoutKey(columns), _ => BuildGetters(columns));

        private static string BuildLayoutKey(ColumnMetadata[] columns)
        {
            int columnCount = columns.Length;
            var parts = new string[columnCount + 1];
            parts[0] = columnCount.ToString();
            for (int i = 0; i < columnCount; i++) parts[i + 1] = columns[i].ColumnName ?? string.Empty;
            // Unit Separator (0x1F) prevents adjacent column names from colliding when concatenated
            // (e.g. ["ab","c"] and ["a","bc"] would otherwise both key to "2abc") - mirrors
            // MultiRowInsertCache.BuildLayoutKey in Internals/Write/MultiRowInsertCache.cs. Uses the
            // \x1F escape (not a raw embedded byte) so the separator is visible in source and to
            // tooling, after two separate audit rounds mistook the byte for a missing separator.
            return string.Join("\x1F", parts);
        }

        private static Func<TEntity, object?>[] BuildGetters(ColumnMetadata[] columns)
        {
            var getters = new Func<TEntity, object?>[columns.Length];

            for (int i = 0; i < columns.Length; i++)
            {
                ColumnMetadata column = columns[i];
                if (column.Getter is { } getter)
                {
                    getters[i] = entity => getter(entity!);
                    continue;
                }

                ParameterExpression param = Expression.Parameter(typeof(TEntity), "e");
                MemberExpression access = Expression.Property(param, column.Property!);
                UnaryExpression box = Expression.Convert(access, typeof(object));
                getters[i] = Expression.Lambda<Func<TEntity, object?>>(box, param).Compile();
            }

            return getters;
        }
    }
}