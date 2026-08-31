using System.Data;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Extensions.Reflection;

using Xunit;

namespace Jaunty.Tests.Unit.Extensions.Reflection;

/// <summary>
/// AUD-R25 (B6-4): all seven resolver delegates registered by <c>UseReflectionMapping</c> redid
/// their reflection plumbing on every invocation - <c>typeof(...).GetMethod(name, BindingFlags...)</c>
/// followed by <c>MakeGenericMethod</c> and <c>Invoke</c>, with nothing cached between calls.
///
/// <para>
/// <c>ResolveMapper</c> was the worst: <c>DrDispatcher.Resolve</c> calls it for every query that
/// falls back to reflection mapping - i.e. every query over a non-source-generated entity, which is
/// the whole point of this package - and each call performed two <c>GetMethod</c> lookups, two
/// <c>MakeGenericMethod</c> constructions and three <c>Invoke</c>s before <c>MetadataCache&lt;T&gt;</c>'s
/// own cache was even consulted. <c>ResolveMultiMapperN</c> additionally built its method name by
/// string concatenation and looked it up by reflection per multi-entity query.
/// </para>
///
/// <para>
/// This was the one layer of the package that wasn't cached - <c>MetadataCache&lt;T&gt;</c> caches
/// metadata, setters and getters; <c>MultiEntityMapper&lt;...&gt;.Get</c> caches per schema;
/// <c>PostgreSqlBulkCopyProvider</c> even added <c>WriteMethodCache</c>/<c>WriteAsyncMethodCache</c>
/// with a comment noting that <c>MakeGenericMethod</c> "is expensive ... and works against the
/// entire point" of the fast path.
/// </para>
///
/// <para>
/// Caching is directly assertable here in a way most performance work is not: a cached resolver
/// returns the <em>same delegate instance</em> on a second call, where the old one returned a fresh
/// one built by a fresh <c>Invoke</c>. The rest of these tests pin that caching a delegate did not
/// change what it does - the mapper in particular must stay reader-shape-agnostic, since it is now
/// shared across readers.
/// </para>
/// </summary>
[Collection("Type Handler Operations")]
public class ReflectionResolverCachingTests
{
    public ReflectionResolverCachingTests() => JauntyReflectionExtensions.UseReflectionMapping();

    [Table("resolver_caching_widgets")]
    public class Widget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Table("resolver_caching_gadgets")]
    public class Gadget
    {
        [Key]
        public int Id { get; set; }
        public string? Label { get; set; }
    }

    // ------------------------------------------------------------------
    // The same call twice returns the same instance
    // ------------------------------------------------------------------

    [Fact]
    public void ResolveMapper_CalledTwice_ReturnsTheSameDelegate()
    {
        object first = JauntyConfig.ReflectionMapperResolver!(typeof(Widget), MappingMode.Strict);
        object second = JauntyConfig.ReflectionMapperResolver!(typeof(Widget), MappingMode.Strict);

        Assert.Same(first, second);
    }

    [Fact]
    public void ResolveMapper_DifferentMappingModes_GetDifferentDelegates()
    {
        // The cache key has to include the mode - the mapper closes over it, so collapsing the two
        // would silently apply one mode's shape rules to the other.
        object strict = JauntyConfig.ReflectionMapperResolver!(typeof(Widget), MappingMode.Strict);
        object projection = JauntyConfig.ReflectionMapperResolver!(typeof(Widget), MappingMode.Projection);

        Assert.NotSame(strict, projection);
    }

    [Fact]
    public void ResolveMapper_DifferentEntityTypes_GetDifferentDelegates()
    {
        object widget = JauntyConfig.ReflectionMapperResolver!(typeof(Widget), MappingMode.Strict);
        object gadget = JauntyConfig.ReflectionMapperResolver!(typeof(Gadget), MappingMode.Strict);

        Assert.NotSame(widget, gadget);
    }

    [Fact]
    public void ResolveTableMetadata_CalledTwice_ReturnsTheSameInstance()
    {
        object first = JauntyConfig.ReflectionTableMetadataResolver!(typeof(Widget));
        object second = JauntyConfig.ReflectionTableMetadataResolver!(typeof(Widget));

        Assert.Same(first, second);
    }

    [Fact]
    public void ResolveInsertBinder_CalledTwice_ReturnsTheSameDelegate()
    {
        var first = JauntyConfig.ReflectionInsertBinderResolver!(typeof(Widget));
        var second = JauntyConfig.ReflectionInsertBinderResolver!(typeof(Widget));

        Assert.Same(first, second);
    }

    [Fact]
    public void ResolveUpdateBinder_CalledTwice_ReturnsTheSameDelegate()
    {
        var first = JauntyConfig.ReflectionUpdateBinderResolver!(typeof(Widget));
        var second = JauntyConfig.ReflectionUpdateBinderResolver!(typeof(Widget));

        Assert.Same(first, second);
    }

    [Fact]
    public void ResolveDeleteBinder_CalledTwice_ReturnsTheSameDelegate()
    {
        var first = JauntyConfig.ReflectionDeleteBinderResolver!(typeof(Widget));
        var second = JauntyConfig.ReflectionDeleteBinderResolver!(typeof(Widget));

        Assert.Same(first, second);
    }

    /// <remarks>
    /// Unlike the five above, this one passes without the fix too: <c>MultiEntityMapper&lt;T1, T2&gt;.Get</c>
    /// already caches per schema, so the resolver was handing back the same instance even while
    /// rebuilding the reflection plumbing to reach it. What the cache removes here is the
    /// <c>GetMethod</c> + <c>MakeGenericMethod</c> + <c>Invoke</c> per call, which is not observable
    /// from managed code. Kept as a regression guard on the cache key rather than as evidence of the
    /// finding.
    /// </remarks>
    [Fact]
    public void ResolveMultiMapper_CalledTwice_ReturnsTheSameInstance()
    {
        object first = JauntyConfig.ReflectionMultiMapperResolver!(typeof(Widget), typeof(Gadget));
        object second = JauntyConfig.ReflectionMultiMapperResolver!(typeof(Widget), typeof(Gadget));

        Assert.Same(first, second);
    }

    [Fact]
    public void ResolveMultiMapper_IsKeyedOnBothTypesInOrder()
    {
        object forward = JauntyConfig.ReflectionMultiMapperResolver!(typeof(Widget), typeof(Gadget));
        object reversed = JauntyConfig.ReflectionMultiMapperResolver!(typeof(Gadget), typeof(Widget));

        Assert.NotSame(forward, reversed);
    }

    // ------------------------------------------------------------------
    // Caching a delegate did not change what it does
    // ------------------------------------------------------------------

    [Fact]
    public void TheCachedMapper_StillMapsRows()
    {
        var map = (Func<IDataReader, Widget>)JauntyConfig.ReflectionMapperResolver!(typeof(Widget), MappingMode.Strict);
        var reader = new StubReader(["Id", "Name"], [7, "seven"]);

        Assert.True(reader.Read());
        Widget widget = map(reader);

        Assert.Equal(7, widget.Id);
        Assert.Equal("seven", widget.Name);
    }

    [Fact]
    public void TheCachedMapper_StillAdaptsToADifferentColumnOrder()
    {
        // The mapper resolves its setters per reader inside its own closure, which is what makes it
        // safe to share across calls at all. If caching had frozen a column layout, the second
        // reader's rows would be read by the first reader's ordinals and come out transposed.
        var map = (Func<IDataReader, Widget>)JauntyConfig.ReflectionMapperResolver!(typeof(Widget), MappingMode.Strict);

        var idFirst = new StubReader(["Id", "Name"], [1, "one"]);
        Assert.True(idFirst.Read());
        Widget fromIdFirst = map(idFirst);

        var nameFirst = new StubReader(["Name", "Id"], ["two", 2]);
        Assert.True(nameFirst.Read());
        Widget fromNameFirst = map(nameFirst);

        Assert.Equal(1, fromIdFirst.Id);
        Assert.Equal("one", fromIdFirst.Name);

        Assert.Equal(2, fromNameFirst.Id);
        Assert.Equal("two", fromNameFirst.Name);
    }

    [Fact]
    public void TheCachedMapper_InProjectionMode_StillToleratesAMissingColumn()
    {
        // Strict and Projection differ precisely in this, so it is the behaviour most at risk if the
        // cache key ever collapsed the two modes onto one delegate.
        var map = (Func<IDataReader, Widget>)JauntyConfig.ReflectionMapperResolver!(typeof(Widget), MappingMode.Projection);
        var narrow = new StubReader(["Name"], ["only-name"]);

        Assert.True(narrow.Read());
        Widget widget = map(narrow);

        Assert.Equal(0, widget.Id);
        Assert.Equal("only-name", widget.Name);
    }

    [Fact]
    public void TheCachedMapper_InStrictMode_StillRejectsAMissingColumn()
    {
        var map = (Func<IDataReader, Widget>)JauntyConfig.ReflectionMapperResolver!(typeof(Widget), MappingMode.Strict);
        var narrow = new StubReader(["Name"], ["only-name"]);

        Assert.True(narrow.Read());

        var exception = Assert.Throws<InvalidOperationException>(() => map(narrow));
        Assert.Contains("Strict mapping failed", exception.Message);
    }

    [Fact]
    public void TheCachedMapper_ProducesANewEntityPerCall()
    {
        // Sharing the delegate must not mean sharing the instance it builds.
        var map = (Func<IDataReader, Widget>)JauntyConfig.ReflectionMapperResolver!(typeof(Widget), MappingMode.Strict);

        var reader = new StubReader(["Id", "Name"], [1, "one"]);
        Assert.True(reader.Read());
        Widget first = map(reader);
        Widget second = map(reader);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void TheCachedInsertBinder_StillBindsParameters()
    {
        var bind = JauntyConfig.ReflectionInsertBinderResolver!(typeof(Widget));
        var command = new StubCommand();

        bind(command, new Widget { Id = 3, Name = "three" });

        Assert.NotEmpty(command.Bound);
        Assert.Contains(command.Bound, p => Equals(p.Value, "three"));
    }

    [Fact]
    public void TheCachedInsertBinder_StillRejectsAMismatchedEntity()
    {
        var bind = JauntyConfig.ReflectionInsertBinderResolver!(typeof(Widget));

        Assert.Throws<InvalidOperationException>(() => bind(new StubCommand(), new Gadget()));
    }

    [Fact]
    public void ResolveMultiMapperN_UnsupportedArity_StillThrows()
    {
        // The helper lookup is cached by arity; a miss must still surface the same error rather
        // than a null-reference from a cached null.
        var reader = new StubReader(["Id"], [1]);

        Assert.Throws<InvalidOperationException>(
            () => JauntyConfig.ReflectionMultiMapperResolverN!([typeof(Widget), typeof(Gadget)], reader));
    }

    [Fact]
    public void ResolveMultiMapperN_UnsupportedArity_ThrowsTheSameWayOnASecondCall()
    {
        var reader = new StubReader(["Id"], [1]);

        var first = Assert.Throws<InvalidOperationException>(
            () => JauntyConfig.ReflectionMultiMapperResolverN!([typeof(Widget), typeof(Gadget)], reader));
        var second = Assert.Throws<InvalidOperationException>(
            () => JauntyConfig.ReflectionMultiMapperResolverN!([typeof(Widget), typeof(Gadget)], reader));

        Assert.Equal(first.Message, second.Message);
    }

    // ------------------------------------------------------------------

    private sealed class StubReader(string[] names, object[] values) : IDataReader
    {
        private int _row;

        public int FieldCount => names.Length;
        public string GetName(int i) => names[i];
        public int GetOrdinal(string name) => Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        public object GetValue(int i) => values[i];
        public bool IsDBNull(int i) => values[i] is null or DBNull;
        public Type GetFieldType(int i) => values[i].GetType();
        public int GetInt32(int i) => Convert.ToInt32(values[i]);
        public string GetString(int i) => (string)values[i];

        public bool Read() => _row++ < 1;
        public bool NextResult() => false;
        public int Depth => 0;
        public bool IsClosed { get; private set; }
        public int RecordsAffected => 0;
        public void Close() => IsClosed = true;
        public void Dispose() => IsClosed = true;
        public DataTable? GetSchemaTable() => null;

        public object this[int i] => values[i];
        public object this[string name] => values[GetOrdinal(name)];

        public bool GetBoolean(int i) => false;
        public byte GetByte(int i) => 0;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
        public char GetChar(int i) => '\0';
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => GetFieldType(i).Name;
        public DateTime GetDateTime(int i) => default;
        public decimal GetDecimal(int i) => 0;
        public double GetDouble(int i) => 0;
        public float GetFloat(int i) => 0;
        public Guid GetGuid(int i) => default;
        public short GetInt16(int i) => 0;
        public long GetInt64(int i) => 0;
        public int GetValues(object[] target) => 0;
    }

    private sealed class StubParameter : IDbDataParameter
    {
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
    }

    private sealed class StubCommand : IDbCommand
    {
        public List<StubParameter> Bound { get; } = [];

        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; }
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public StubCommand() => Parameters = new Collection(Bound);

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new StubParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotSupportedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
        public object? ExecuteScalar() => null;
        public void Prepare() { }

        private sealed class Collection(List<StubParameter> bound) : List<object>, IDataParameterCollection
        {
            public object this[string parameterName]
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public bool Contains(string parameterName) => false;
            public int IndexOf(string parameterName) => -1;
            public void RemoveAt(string parameterName) { }

            public new int Add(object value)
            {
                if (value is StubParameter parameter)
                    bound.Add(parameter);

                base.Add(value);
                return Count - 1;
            }
        }
    }
}
