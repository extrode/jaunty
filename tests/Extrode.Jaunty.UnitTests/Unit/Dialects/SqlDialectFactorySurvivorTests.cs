using System.Data;
using System.Runtime.CompilerServices;
using System.Reflection;

using Extrode.Jaunty.Dialects;
using Extrode.Jaunty.Internals;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Dialects;

[Collection("Dialect Factory Survivors")]
public class SqlDialectFactorySurvivorTests
{
    public SqlDialectFactorySurvivorTests()
    {
        var cache = typeof(SqlDialectFactory).GetField("_innerConnectionAccessors", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        ((System.Collections.IDictionary)cache).Clear();
        SqlDialectFactory.InvalidateResolvedDialects();
    }

#if NET
    public interface ITestWrapperDialect : ISqlDialect, IDialectWrapper
    {
    }

    public class WrapperProxy : DispatchProxy
    {
        public ISqlDialect? Inner { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod!.Name == "get_InnerDialect" ? Inner : throw new NotSupportedException();
    }

    private static ITestWrapperDialect Wrapper(ISqlDialect? inner)
    {
        var proxy = DispatchProxy.Create<ITestWrapperDialect, WrapperProxy>();
        ((WrapperProxy)(object)proxy).Inner = inner;
        return proxy;
    }

    [Fact]
    public void Unwrap_AWrapperWithNoInner_ReturnsTheWrapper()
    {
        var wrapper = Wrapper(null);

        Assert.Same(wrapper, SqlDialectFactory.Unwrap(wrapper));
    }

    [Fact]
    public void Unwrap_ALongChain_StopsAfterEightUnwraps()
    {
        ISqlDialect[] chain = new ISqlDialect[11];
        chain[10] = new SQLiteDialect();
        for (int i = 9; i >= 0; i--)
            chain[i] = Wrapper(chain[i + 1]);

        Assert.Same(chain[8], SqlDialectFactory.Unwrap(chain[0]));
    }
#endif

    private abstract class StubConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = "";
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Closed;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Dispose() { }
        public void Open() { }
    }

    private sealed class SqliteConnection : StubConnection { }

    private sealed class FieldWrapper : StubConnection
    {
        private readonly IDbConnection _inner;

        public FieldWrapper(IDbConnection inner) => _inner = inner;

        public string Describe() => _inner.Database;
    }

    private sealed class PropertyWrapper : StubConnection
    {
        public PropertyWrapper(IDbConnection inner) => WrappedConnection = inner;

        public IDbConnection WrappedConnection { get; }
    }

    private sealed class ComputedPropertyWrapper : StubConnection
    {
        private readonly object[] _holder;

        public ComputedPropertyWrapper(IDbConnection inner) => _holder = [inner];

        public IDbConnection WrappedConnection => (IDbConnection)_holder[0];
    }

    private sealed class TwoFieldWrapper : StubConnection
    {
        private readonly IDbConnection _a;
        private readonly IDbConnection _b;

        public TwoFieldWrapper(IDbConnection a, IDbConnection b)
        {
            _a = a;
            _b = b;
        }

        public string Describe() => _a.Database + _b.Database;
    }

    private sealed class FieldAndOtherFieldWrapper : StubConnection
    {
        private readonly IDbConnection _inner;
        private readonly string _label = "x";

        public FieldAndOtherFieldWrapper(IDbConnection inner) => _inner = inner;

        public string Describe() => _inner.Database + _label;
    }

    private sealed class NonConnectionPropertyWrapper : StubConnection
    {
        private readonly IDbConnection _inner;

        public NonConnectionPropertyWrapper(IDbConnection inner) => _inner = inner;

        public string Inner => "not a connection";

        public string Describe() => _inner.Database;
    }

    private sealed class IndexerWrapper : StubConnection
    {
        private readonly IDbConnection _inner;

        public IndexerWrapper(IDbConnection inner) => _inner = inner;

        [IndexerName("Inner")]
        public IDbConnection this[int index] => _inner;
    }

    private sealed class WriteOnlyWrapper : StubConnection
    {
        private readonly IDbConnection _inner;

        public WriteOnlyWrapper(IDbConnection inner) => _inner = inner;

        public IDbConnection Inner
        {
            set => _ = value;
        }

        public string Describe() => _inner.Database;
    }

    private sealed class ThrowingGetterWrapper : StubConnection
    {
        public IDbConnection InnerConnection => throw new InvalidOperationException("disposed");
    }

    [Fact]
    public void AProbeThatThrows_LeavesTheBaseDialect()
    {
        var dialect = new SQLiteDialect();

        Assert.Same(dialect, SqlDialectFactory.TryEnhance(dialect, _ => throw new InvalidOperationException("boom")));
    }

    [Fact]
    public void AProbeReturningNull_LeavesTheBaseDialect()
    {
        var dialect = new SQLiteDialect();

        Assert.Same(dialect, SqlDialectFactory.TryEnhance(dialect, _ => null));
    }

    [Fact]
    public void AProbeReturningADialect_ReplacesTheBaseDialect()
    {
        var replacement = new SqlServerDialect();

        Assert.Same(replacement, SqlDialectFactory.TryEnhance(new SQLiteDialect(), _ => replacement));
    }

    private static string DialectNameOf(IDbConnection connection)
        => SqlDialectFactory.Unwrap(SqlDialectFactory.GetDialect(connection)).GetType().Name;

    [Fact]
    public void AFieldDecorator_ResolvesToTheInnerDialect()
        => Assert.Equal(nameof(SQLiteDialect), DialectNameOf(new FieldWrapper(new SqliteConnection())));

    [Fact]
    public void AComputedPropertyDecorator_ResolvesToTheInnerDialect()
        => Assert.Equal(nameof(SQLiteDialect), DialectNameOf(new ComputedPropertyWrapper(new SqliteConnection())));

    [Fact]
    public void APropertyDecorator_ResolvesToTheInnerDialect()
        => Assert.Equal(nameof(SQLiteDialect), DialectNameOf(new PropertyWrapper(new SqliteConnection())));

    [Fact]
    public void AStringPropertyNamedLikeTheInner_FallsThroughToTheField()
        => Assert.Equal(nameof(SQLiteDialect), DialectNameOf(new NonConnectionPropertyWrapper(new SqliteConnection())));

    [Fact]
    public void AnIndexerNamedLikeTheInner_FallsThroughToTheField()
        => Assert.Equal(nameof(SQLiteDialect), DialectNameOf(new IndexerWrapper(new SqliteConnection())));

    [Fact]
    public void AWriteOnlyPropertyNamedLikeTheInner_FallsThroughToTheField()
        => Assert.Equal(nameof(SQLiteDialect), DialectNameOf(new WriteOnlyWrapper(new SqliteConnection())));

    [Fact]
    public void ANonConnectionFieldBesideTheConnectionField_IsIgnored()
        => Assert.Equal(nameof(SQLiteDialect), DialectNameOf(new FieldAndOtherFieldWrapper(new SqliteConnection())));

    [Fact]
    public void TwoConnectionFields_AreNotADecorator()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => SqlDialectFactory.GetDialect(new TwoFieldWrapper(new SqliteConnection(), new SqliteConnection())));

        Assert.StartsWith("No SQL dialect is registered for connection type 'TwoFieldWrapper'.", exception.Message);
    }

    [Fact]
    public void AGetterThatThrows_IsNotARouteToTheInnerConnection()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => SqlDialectFactory.GetDialect(new ThrowingGetterWrapper()));

        Assert.StartsWith("No SQL dialect is registered for connection type 'ThrowingGetterWrapper'.", exception.Message);
    }

    [Fact]
    public void EightNestedDecorators_Resolve_NineDoNot()
    {
        IDbConnection eight = new SqliteConnection();
        for (int i = 0; i < 8; i++)
            eight = new FieldWrapper(eight);

        Assert.Equal(nameof(SQLiteDialect), DialectNameOf(eight));
        Assert.Throws<InvalidOperationException>(() => SqlDialectFactory.GetDialect(new FieldWrapper(eight)));
    }

    [Fact]
    public void TheUnresolvableMessage_SpellsOutEachPart()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => SqlDialectFactory.GetDialect(new ThrowingGetterWrapper()));

        Assert.Equal(
            "No SQL dialect is registered for connection type 'ThrowingGetterWrapper'. " +
            "Extrode.Jaunty resolves dialects from the connection type name and recognises SqlConnection, " +
            "NpgsqlConnection, MySqlConnection, SQLiteConnection and SqliteConnection. If this is a " +
            "wrapped or profiled connection, Extrode.Jaunty could not reach the connection underneath it. " +
            "Register a dialect for it with SqlDialectFactory.RegisterDialect(\"ThrowingGetterWrapper\", dialect).",
            exception.Message);
    }

    [Fact]
    public void ResolvingTheSameConnectionTypeTwice_ReturnsTheCachedDialect()
    {
        var first = SqlDialectFactory.GetDialect(new SqliteConnection());

        Assert.Same(first, SqlDialectFactory.GetDialect(new SqliteConnection()));
    }

    [Fact]
    public void InvalidatingResolvedDialects_AdvancesTheConfigurationGeneration()
    {
        int before = ConfigurationGeneration.Current;

        SqlDialectFactory.InvalidateResolvedDialects();

        Assert.True(ConfigurationGeneration.Current > before);
    }

    [Fact]
    public void ResettingRegistrations_AdvancesTheConfigurationGeneration()
    {
        int before = ConfigurationGeneration.Current;

        SqlDialectFactory.ResetRegistrations();

        Assert.True(ConfigurationGeneration.Current > before);
    }
}
