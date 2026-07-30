using System.Data;

using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;
using Jaunty.Interfaces;
using Jaunty.Internals.Read;
using Jaunty.Internals.Write;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Spec 009. <c>MappedCache&lt;T&gt;</c> and <c>WriteParameterCache&lt;T&gt;</c> now take the generated
/// mapper and binders from <see cref="IGeneratedAccessors{T}"/> instead of locating them with
/// <c>typeof(T).GetMethod(name)</c>, because reflection by name gave the trimmer nothing to hold on to
/// and a NativeAOT publish removed the members - measured as <c>No mapper found for type 'Product'</c>
/// and <c>No parameter binder found for type 'Widget'</c> on published binaries whose JIT builds worked.
///
/// <para>
/// Every entity below deliberately makes its interface accessor return a <em>different answer</em> from
/// its reflectable by-name member. That is what makes these tests discriminating: asserting only that a
/// mapper was resolved would pass whichever path ran, which is the "assertion that holds regardless of
/// the fix" shape. Here the returned value says which path ran.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>[Collection]</c> and <c>Dispose</c> are not incidental. One test here calls
/// <c>JauntyConfig.Reset()</c>, which clears the process-wide reflection resolvers, and the first
/// version of this file did that without isolating or restoring them: 46 tests elsewhere in the suite
/// then failed with "No mapper found for type 'Dictionary`2'"/"'Category'" - every test that ran
/// afterwards and needed the reflection extension. The collection serialises this class against the
/// other configuration-mutating tests, and <see cref="Dispose"/> hands the process back the way the
/// rest of the suite expects to find it, exactly as <c>ConfigurationGenerationTests</c> does.
/// </para>
/// </remarks>
[Collection("Type Handler Operations")]
public class GeneratedAccessorsResolutionTests : IDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();
    }

    #region Entities

    /// <summary>
    /// Shaped like a source-generated entity: implements both interfaces and exposes the by-name
    /// members too. <c>ReadEntity</c> reports "reflection", the accessor reports "interface".
    /// </summary>
    public class BothPathsEntity : IMapped<BothPathsEntity>, IGeneratedAccessors<BothPathsEntity>
    {
        public string ResolvedBy { get; set; } = "unset";

#if NET8_0_OR_GREATER
        public static BothPathsEntity ReadEntity(IDataReader reader)
#else
        public BothPathsEntity ReadEntity(IDataReader reader)
#endif
            => new() { ResolvedBy = "reflection" };

        public static Func<IDataReader, BothPathsEntity> CreateRowMapper(IDataReader reader)
            => _ => new BothPathsEntity { ResolvedBy = "reflection-factory" };

        public static void BindInsert(IDbCommand command, BothPathsEntity entity) => Record(command, "reflection-insert");
        public static void BindUpdate(IDbCommand command, BothPathsEntity entity) => Record(command, "reflection-update");
        public static void BindDelete(IDbCommand command, BothPathsEntity entity) => Record(command, "reflection-delete");

        Func<IDataReader, BothPathsEntity> IGeneratedAccessors<BothPathsEntity>.RowMapper
            => _ => new BothPathsEntity { ResolvedBy = "interface" };

        Func<IDataReader, Func<IDataReader, BothPathsEntity>> IGeneratedAccessors<BothPathsEntity>.RowMapperFactory
            => _ => _ => new BothPathsEntity { ResolvedBy = "interface-factory" };

        Action<IDbCommand, BothPathsEntity> IGeneratedAccessors<BothPathsEntity>.InsertBinder
            => (command, _) => Record(command, "interface-insert");

        Action<IDbCommand, BothPathsEntity> IGeneratedAccessors<BothPathsEntity>.UpdateBinder
            => (command, _) => Record(command, "interface-update");

        Action<IDbCommand, BothPathsEntity> IGeneratedAccessors<BothPathsEntity>.DeleteBinder
            => (command, _) => Record(command, "interface-delete");

        private static void Record(IDbCommand command, string marker) => command.CommandText = marker;
    }

    /// <summary>
    /// A hand-written mapper: <see cref="IMapped{T}"/> only, no accessors. This is the population the
    /// reflection fallback still exists for, and the one JAUNTYGEN002 warns about.
    /// </summary>
    public class ReflectionOnlyEntity : IMapped<ReflectionOnlyEntity>
    {
        public string ResolvedBy { get; set; } = "unset";

#if NET8_0_OR_GREATER
        public static ReflectionOnlyEntity ReadEntity(IDataReader reader)
#else
        public ReflectionOnlyEntity ReadEntity(IDataReader reader)
#endif
            => new() { ResolvedBy = "reflection" };
    }

    /// <summary>Neither path applies - nothing should be resolved.</summary>
    public class PlainEntity
    {
        public int Id { get; set; }
    }

    #endregion

    // ------------------------------------------------------------------
    // Read path
    // ------------------------------------------------------------------

    [Fact]
    public void TheInterface_WinsOverReflection_ForTheRowMapper()
    {
        Func<IDataReader, BothPathsEntity>? mapper = MappedCache<BothPathsEntity>.Mapper;

        Assert.NotNull(mapper);
        Assert.Equal("interface", mapper!(new StubReader()).ResolvedBy);
    }

    [Fact]
    public void TheInterface_WinsOverReflection_ForTheRowMapperFactory()
    {
        Func<IDataReader, Func<IDataReader, BothPathsEntity>>? factory = MappedCache<BothPathsEntity>.MapperFactory;

        Assert.NotNull(factory);
        Assert.Equal("interface-factory", factory!(new StubReader())(new StubReader()).ResolvedBy);
    }

    /// <summary>
    /// The fallback must stay working: removing it would break every hand-written
    /// <see cref="IMapped{T}"/> on the JIT, which is a supported and common shape.
    /// </summary>
    [Fact]
    public void AHandWrittenMapper_StillResolvesByReflection()
    {
        Func<IDataReader, ReflectionOnlyEntity>? mapper = MappedCache<ReflectionOnlyEntity>.Mapper;

        Assert.NotNull(mapper);
        Assert.Equal("reflection", mapper!(new StubReader()).ResolvedBy);
    }

    [Fact]
    public void APlainEntity_ResolvesNothing()
    {
        Assert.Null(MappedCache<PlainEntity>.Mapper);
        Assert.Null(MappedCache<PlainEntity>.MapperFactory);
    }

    /// <summary>
    /// The accessor instance is resolved once per closed generic, so repeated reads hand back the same
    /// delegate rather than casting and allocating again.
    /// </summary>
    [Fact]
    public void TheResolvedMapper_IsCached()
    {
        Assert.Same(MappedCache<BothPathsEntity>.Mapper, MappedCache<BothPathsEntity>.Mapper);
        Assert.Same(MappedCache<BothPathsEntity>.MapperFactory, MappedCache<BothPathsEntity>.MapperFactory);
    }

    // ------------------------------------------------------------------
    // Write path
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("insert")]
    [InlineData("update")]
    [InlineData("delete")]
    public void TheInterface_WinsOverReflection_ForEachBinder(string operation)
    {
        Action<IDbCommand, BothPathsEntity>? binder = operation switch
        {
            "insert" => WriteParameterCache<BothPathsEntity>.InsertBinder,
            "update" => WriteParameterCache<BothPathsEntity>.UpdateBinder,
            _ => WriteParameterCache<BothPathsEntity>.DeleteBinder,
        };

        Assert.NotNull(binder);

        var command = new StubCommand();
        binder!(command, new BothPathsEntity());

        Assert.Equal($"interface-{operation}", command.CommandText);
    }

    /// <summary>
    /// AUD-R26 (batch 4) made the binders re-derive on a configuration change, so that
    /// <c>JauntyConfig.Reset()</c> followed by re-registration recovers instead of leaving
    /// <c>Insert&lt;T&gt;</c> broken for that <c>T</c> forever. The accessor lookup was added inside
    /// <c>Bindings.Build()</c> to keep that working; this is the test that says so. A generated entity
    /// must survive a reset, because its binders come from the type and not from configuration.
    /// </summary>
    [Fact]
    public void AGeneratedEntitysBinders_SurviveAConfigurationReset()
    {
        Action<IDbCommand, BothPathsEntity>? before = WriteParameterCache<BothPathsEntity>.InsertBinder;
        Assert.NotNull(before);

        JauntyConfig.Reset();

        Action<IDbCommand, BothPathsEntity>? after = WriteParameterCache<BothPathsEntity>.InsertBinder;

        Assert.NotNull(after);

        var command = new StubCommand();
        after!(command, new BothPathsEntity());
        Assert.Equal("interface-insert", command.CommandText);
    }

    // ------------------------------------------------------------------
    // Stubs
    // ------------------------------------------------------------------

    private sealed class StubReader : IDataReader
    {
        public int FieldCount => 0;
        public bool Read() => false;
        public void Close() { }
        public void Dispose() { }
        public bool NextResult() => false;
        public int Depth => 0;
        public bool IsClosed => false;
        public int RecordsAffected => 0;
        public DataTable? GetSchemaTable() => null;
        public object this[int i] => throw new NotSupportedException();
        public object this[string name] => throw new NotSupportedException();
        public bool GetBoolean(int i) => throw new NotSupportedException();
        public byte GetByte(int i) => throw new NotSupportedException();
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public char GetChar(int i) => throw new NotSupportedException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => throw new NotSupportedException();
        public DateTime GetDateTime(int i) => throw new NotSupportedException();
        public decimal GetDecimal(int i) => throw new NotSupportedException();
        public double GetDouble(int i) => throw new NotSupportedException();
        public Type GetFieldType(int i) => throw new NotSupportedException();
        public float GetFloat(int i) => throw new NotSupportedException();
        public Guid GetGuid(int i) => throw new NotSupportedException();
        public short GetInt16(int i) => throw new NotSupportedException();
        public int GetInt32(int i) => throw new NotSupportedException();
        public long GetInt64(int i) => throw new NotSupportedException();
        public string GetName(int i) => throw new NotSupportedException();
        public int GetOrdinal(string name) => throw new NotSupportedException();
        public string GetString(int i) => throw new NotSupportedException();
        public object GetValue(int i) => throw new NotSupportedException();
        public int GetValues(object[] values) => throw new NotSupportedException();
        public bool IsDBNull(int i) => true;
    }

    private sealed class StubCommand : IDbCommand
    {
        public string? CommandText { get; set; } = string.Empty;
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new StubParameters();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }
        public void Cancel() { }
        public IDbDataParameter CreateParameter() => throw new NotSupportedException();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotSupportedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
        public object? ExecuteScalar() => null;
        public void Prepare() { }
    }

    private sealed class StubParameters : System.Collections.ArrayList, IDataParameterCollection
    {
        public bool Contains(string parameterName) => false;
        public int IndexOf(string parameterName) => -1;
        public void RemoveAt(string parameterName) { }
        public object this[string parameterName]
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }
}
