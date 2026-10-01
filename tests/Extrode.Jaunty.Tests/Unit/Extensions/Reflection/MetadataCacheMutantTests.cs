using System.Data;
using System.Reflection;
#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Core;
using Extrode.Jaunty.Dialects;
using Extrode.Jaunty.Extensions.Reflection;
using Extrode.Jaunty.Extensions.Reflection.Dialects;
using Extrode.Jaunty.Tests.Helpers;
using Extrode.Jaunty.TypeHandlers;

namespace Extrode.Jaunty.Tests.Unit.Extensions.Reflection;

[Collection("Type Handler Operations")]
public class MetadataCacheMutantTests : IDisposable
{
    public void Dispose()
    {
        TypeHandlerRegistry.Remove<int>();
        JauntyConfig.ColumnNameResolver = null;
        GC.SuppressFinalize(this);
    }

    public enum Shade { Light = 1, Dark = 2 }

    public class NullableCount
    {
        public int? Count { get; set; }
    }

    public class PlainCount
    {
        public int Count { get; set; }
    }

    public class BindsTo
    {
        [Column("first_name")]
        public string? FirstName { get; set; }

        public string? LastName { get; set; }
    }

    private static PropertyContext<BindsTo> ContextFor(string propertyName)
        => Array.Find(MetadataCache<BindsTo>.Properties, p => p.Property.Name == propertyName);

    [Fact]
    public void ColumnBindsTo_MatchesTheOwningPropertyOnly()
    {
        PropertyContext<BindsTo> first = ContextFor(nameof(BindsTo.FirstName));
        PropertyContext<BindsTo> last = ContextFor(nameof(BindsTo.LastName));

        Assert.True(MetadataCache<BindsTo>.ColumnBindsTo("first_name", first));
        Assert.True(MetadataCache<BindsTo>.ColumnBindsTo("LastName", last));
        Assert.False(MetadataCache<BindsTo>.ColumnBindsTo("first_name", last));
        Assert.False(MetadataCache<BindsTo>.ColumnBindsTo("LastName", first));
        Assert.False(MetadataCache<BindsTo>.ColumnBindsTo("surname", last));
    }

    [Fact]
    public void ColumnBindsTo_FallsBackToTheConfiguredResolver()
    {
        PropertyContext<BindsTo> first = ContextFor(nameof(BindsTo.FirstName));
        PropertyContext<BindsTo> last = ContextFor(nameof(BindsTo.LastName));
        JauntyConfig.ColumnNameResolver = name => "x_" + name.ToLowerInvariant();

        Assert.True(MetadataCache<BindsTo>.ColumnBindsTo("x_firstname", first));
        Assert.False(MetadataCache<BindsTo>.ColumnBindsTo("x_firstname", last));
        Assert.True(MetadataCache<BindsTo>.ColumnBindsTo("x_lastname", last));
        Assert.False(MetadataCache<BindsTo>.ColumnBindsTo("unmapped", first));
    }

    public class StringStored
    {
        [EnumStorage(EnumStorage.String)]
        public Shade Tone { get; set; }
    }

    public class NumericStored
    {
        [EnumStorage(EnumStorage.Numeric)]
        public Shade Tone { get; set; }
    }

    private sealed class TextHandler : ITypeHandler
    {
        public object? Parse(object? dbValue) => "5";

        public object? ToDbValue(object? value) => value;
    }

    private static T Read<T>(string column, object value) where T : new()
    {
        var reader = new MutableStubReader([column], [value]);
        PropertySetter<T>[] setters = MetadataCache<T>.GetSetters(reader, MappingMode.Projection);
        var target = new T();
        foreach (PropertySetter<T> setter in setters)
            setter.Set(target, reader);
        return target;
    }

    [Fact]
    public void ANullableProperty_ConvertsAHandlersResultToTheUnderlyingType()
    {
        TypeHandlerRegistry.Register<int>(new TextHandler());

        Assert.Equal(5, Read<NullableCount>("Count", 1).Count);
    }

    [Fact]
    public void ANonNullableProperty_ConvertsAHandlersCompatibleResult()
    {
        TypeHandlerRegistry.Register<int>(new TextHandler());

        Assert.Equal(5, Read<PlainCount>("Count", 1).Count);
    }

    [Fact]
    public void AStringStoredEnum_ReportsAnUnknownNameThroughItsOwnMessage()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Read<StringStored>("Tone", "Bogus"));

        Assert.StartsWith("Cannot convert value 'Bogus' to enum type 'Shade'", ex.Message);
    }

    [Fact]
    public void ANumericStoredEnum_DoesNotUseTheStringEnumMessage()
    {
        var ex = Record.Exception(() => Read<NumericStored>("Tone", "Bogus"));

        Assert.NotNull(ex);
        Assert.DoesNotContain("Cannot convert value", ex!.Message);
    }

    private static object NewSignature(Type entity, IDataReader reader, MappingMode mode, Func<string, string>? resolver)
    {
        Type snapshot = typeof(MetadataCache<>).GetNestedType("Snapshot", BindingFlags.NonPublic)!;
        Type signature = snapshot.GetNestedType("ReaderSignature", BindingFlags.NonPublic)!.MakeGenericType(entity);
        return Activator.CreateInstance(signature, reader, mode, resolver)!;
    }

    [Fact]
    public void TwoReaderSignatures_AreEqualOnlyWhenModeShapeAndResolverAllMatch()
    {
        Func<string, string> resolver = name => name;
        Func<string, string> other = name => name;
        var reader = new MutableStubReader(["Id"], [1]);

        object baseline = NewSignature(typeof(PlainCount), reader, MappingMode.Projection, resolver);

        Assert.Equal(baseline, NewSignature(typeof(PlainCount), reader, MappingMode.Projection, resolver));
        Assert.NotEqual(baseline, NewSignature(typeof(PlainCount), reader, MappingMode.Projection, other));
        Assert.NotEqual(baseline, NewSignature(typeof(PlainCount), reader, MappingMode.Projection, null));
        Assert.NotEqual(baseline, NewSignature(typeof(PlainCount), reader, MappingMode.Strict, resolver));
        Assert.NotEqual(baseline, NewSignature(typeof(PlainCount), new MutableStubReader(["Other"], [1]), MappingMode.Projection, resolver));
    }

    [Fact]
    public void RegisteringSpecialTypeMappers_KeepsAResolverAlreadyInstalled()
    {
        Func<Type, IDataReader, object>? previous = JauntyConfig.SpecialTypeMapperResolver;
        Func<Type, IDataReader, object> custom = (_, _) => new object();
        try
        {
            JauntyConfig.SpecialTypeMapperResolver = custom;

            SpecialTypeMappers.Register();

            Assert.Same(custom, JauntyConfig.SpecialTypeMapperResolver);
        }
        finally
        {
            JauntyConfig.SpecialTypeMapperResolver = previous;
        }
    }

#if !NETFRAMEWORK
    private sealed class IsolatedContext : AssemblyLoadContext
    {
        private readonly string _path;

        public IsolatedContext(string path) : base(isCollectible: true) => _path = path;

        protected override Assembly? Load(AssemblyName name) =>
            name.Name == "Extrode.Jaunty.Extensions.Reflection" ? LoadFromAssemblyPath(_path) : null;
    }

    [Fact]
    public void AFreshBulkCopyDialectFactory_HandsTheDialectBackUntouched()
    {
        string path = typeof(SpecialTypeMappers).Assembly.Location;
        var context = new IsolatedContext(path);
        try
        {
            Assembly fresh = context.LoadFromAssemblyPath(path);
            Type factory = fresh.GetType("Extrode.Jaunty.Extensions.Reflection.Dialects.BulkCopyDialectFactory")!;
            MethodInfo getDialect = factory.GetMethod("GetDialect", BindingFlags.Public | BindingFlags.Static)!;
            var dialect = new SQLiteDialect();

            object? result = getDialect.Invoke(null, [dialect]);

            Assert.Same(dialect, result);
        }
        finally
        {
            context.Unload();
        }
    }
#endif
}
