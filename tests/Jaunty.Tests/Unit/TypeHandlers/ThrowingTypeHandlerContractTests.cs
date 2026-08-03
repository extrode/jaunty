using System.Data;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Extensions.Reflection;
using Jaunty.Internals.Parameters;
using Jaunty.TypeHandlers;

namespace Jaunty.Tests.Unit.TypeHandlers;

/// <summary>
/// AUD-R35-115. Three write paths call the same type-handler registry and only one of them wrapped
/// a throwing <c>ToDbValue</c>. These pin the single contract they now share.
/// </summary>
[Collection("Type Handler Operations")]
public class ThrowingTypeHandlerContractTests : IDisposable
{
    public ThrowingTypeHandlerContractTests()
    {
        JauntyReflectionExtensions.UseReflectionMapping();
        JauntyConfig.RegisterTypeHandler(new ThrowingGuidHandler());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.RemoveTypeHandler<Guid>();
        JauntyConfig.RemoveTypeHandler<Colour>();
    }

    public enum Colour
    {
        Red = 0,
        Green = 1,
    }

    private sealed class ThrowingGuidHandler : TypeHandler<Guid>
    {
        public override Guid Parse(object? dbValue) => Guid.Empty;
        public override object? ToDbValue(Guid value) => throw new FormatException("handler is broken");
    }

    private sealed class ThrowingColourHandler : TypeHandler<Colour>
    {
        public override Colour Parse(object? dbValue) => Colour.Red;
        public override object? ToDbValue(Colour value) => throw new FormatException("handler is broken");
    }

    // Registration adapts a TypeHandler<T> into an internal ITypeHandler, so the name the message
    // carries is the adapter's, not the caller's handler class. Asserted directly only in the
    // wrapper test below, where the handler is passed in unadapted.
    private sealed class RawThrowingHandler : ITypeHandler
    {
        public object? Parse(object? dbValue) => dbValue;
        public object? ToDbValue(object? value) => throw new FormatException("handler is broken");
    }

    [Table("throwing_handler_rows")]
    public class ThrowingHandlerRow
    {
        [Key]
        public int Id { get; set; }

        public Guid Token { get; set; }
    }

    private static void AssertJauntyWrapped(InvalidOperationException ex, string valueTypeName)
    {
        Assert.Contains("Type handler", ex.Message, StringComparison.Ordinal);
        Assert.Contains("failed to convert", ex.Message, StringComparison.Ordinal);
        Assert.Contains(valueTypeName, ex.Message, StringComparison.Ordinal);
        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Fact]
    public void TheSharedWrapper_NamesTheHandlerAndTheValueType_AndKeepsTheOriginal()
    {
        var handler = new RawThrowingHandler();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => TypeHandlerRegistry.ToDbValueOrThrow(handler, Guid.NewGuid()));

        Assert.Contains(nameof(RawThrowingHandler), ex.Message, StringComparison.Ordinal);
        AssertJauntyWrapped(ex, nameof(Guid));
    }

    [Fact]
    public void TheAdHocParameterPath_WrapsAThrowingHandler()
    {
        var command = new CapturingCommand { CommandText = "SELECT * FROM t WHERE Token = @Token" };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ParameterBinder.Bind(command, new { Token = Guid.NewGuid() }));

        AssertJauntyWrapped(ex, nameof(Guid));
    }

    [Fact]
    public void TheGeneratedPath_WrapsAThrowingHandler()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => GeneratedBindingSupport.ToDbValue(Guid.NewGuid()));

        AssertJauntyWrapped(ex, nameof(Guid));
    }

    [Fact]
    public void TheGeneratedEnumPath_WrapsAThrowingHandler()
    {
        JauntyConfig.RegisterTypeHandler(new ThrowingColourHandler());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => GeneratedBindingSupport.ToDbEnumValue(Colour.Green, EnumStorage.String));

        AssertJauntyWrapped(ex, nameof(Colour));
    }

    [Fact]
    public void TheReflectionEntityPath_WrapsAThrowingHandler()
    {
        Func<Type, Action<IDbCommand, object>>? resolver = JauntyConfig.ReflectionInsertBinderResolver;
        Assert.NotNull(resolver);

        Action<IDbCommand, object> bind = resolver(typeof(ThrowingHandlerRow));
        var command = new CapturingCommand();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => bind(command, new ThrowingHandlerRow { Id = 1, Token = Guid.NewGuid() }));

        AssertJauntyWrapped(ex, nameof(Guid));
    }

    private sealed class CapturingParameter : IDbDataParameter
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

    private sealed class CapturingCommand : IDbCommand
    {
        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; } = new CapturingCollection();
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new CapturingParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotSupportedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
        public object? ExecuteScalar() => null;
        public void Prepare() { }

        private sealed class CapturingCollection : List<object>, IDataParameterCollection
        {
            public object this[string parameterName]
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public bool Contains(string parameterName) => false;
            public int IndexOf(string parameterName) => -1;
            public void RemoveAt(string parameterName) { }
        }
    }
}
