using System.Data;

using Jaunty.Dialects;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// AUD-R26 (batch 4, medium/bug). <c>SqlDialectFactory</c> resolved a dialect from
/// <c>connection.GetType().Name</c> and handed anything it did not recognise
/// <see cref="SqlServerDialect"/> - <c>[bracket]</c> quoting, <c>MERGE</c>-based upsert,
/// <c>SCOPE_IDENTITY()</c>, <c>OFFSET ... FETCH NEXT</c> paging and a 2,100-parameter ceiling, on an
/// engine that need not support any of them.
///
/// <para>
/// Because resolution keys on the type <em>name</em>, the likeliest casualty was never an exotic
/// engine but a <b>wrapped</b> one: MiniProfiler's <c>ProfiledDbConnection</c>, OpenTelemetry
/// decorators and DI proxies all present a name matching nothing in the switch, so a decorated
/// <c>SqliteConnection</c> silently produced SQL Server SQL. Jaunty already recognised this shape on
/// the other side of the problem - <c>IDialectWrapper</c> and <c>SqlDialectFactory.Unwrap</c> see
/// through <em>dialect</em> decorators - and nothing saw through connection ones.
/// </para>
///
/// <para>
/// The repo's own <c>WriteFallbackTests</c> recorded the consequence without naming it as a defect:
/// Upsert was excluded from the wrapper suite with a note saying the wrapper "defaults to SQL Server
/// dialect which generates MERGE syntax instead of SQLite's ON CONFLICT syntax". Insert, Update and
/// Delete passed only because they look similar across the two engines.
/// </para>
/// </summary>
[Collection("Dialect Factory State")]
public class ConnectionUnwrappingTests
{
    // ------------------------------------------------------------------
    // Shapes a decorator actually takes
    // ------------------------------------------------------------------

    /// <summary>MiniProfiler's shape: a public property naming the connection it wraps.</summary>
    [Fact]
    public void ADecoratorExposingItsInnerConnectionAsAPropertyResolvesToTheInnerEngine()
    {
        var wrapped = new WrappedConnectionDecorator(new SqliteConnection());

        ISqlDialect dialect = SqlDialectFactory.GetDialect(wrapped);

        Assert.IsType<SQLiteDialect>(SqlDialectFactory.Unwrap(dialect));
    }

    /// <summary>
    /// The shape this repo's own test wrapper uses, and plenty of hand-rolled ones: the inner
    /// connection is a private field and nothing is exposed. A property-only probe would give up on
    /// exactly the case it exists to serve.
    /// </summary>
    [Fact]
    public void ADecoratorHoldingItsInnerConnectionInAPrivateFieldResolvesToTheInnerEngine()
    {
        var wrapped = new PrivateFieldDecorator(new NpgsqlConnection());

        ISqlDialect dialect = SqlDialectFactory.GetDialect(wrapped);

        Assert.IsType<PostgreSqlDialect>(SqlDialectFactory.Unwrap(dialect));
    }

    /// <summary>Decorators nest - a profiler around a tracer around the real connection.</summary>
    [Fact]
    public void NestedDecoratorsResolveToTheEngineAtTheBottom()
    {
        var wrapped = new WrappedConnectionDecorator(new PrivateFieldDecorator(new MySqlConnection()));

        ISqlDialect dialect = SqlDialectFactory.GetDialect(wrapped);

        Assert.IsType<MySqlDialect>(SqlDialectFactory.Unwrap(dialect));
    }

    // ------------------------------------------------------------------
    // Where it must refuse rather than guess
    // ------------------------------------------------------------------

    /// <summary>
    /// A type holding two connections is not a decorator in any sense this can resolve, and picking
    /// one would be the guessing the fix removes. It must fail like any other unknown type.
    /// </summary>
    [Fact]
    public void ATypeHoldingTwoConnectionsIsAmbiguousAndIsNotGuessedAt()
    {
        var ambiguous = new TwoConnectionHolder(new SqliteConnection(), new NpgsqlConnection());

        var ex = Assert.Throws<InvalidOperationException>(() => SqlDialectFactory.GetDialect(ambiguous));

        Assert.Contains(nameof(TwoConnectionHolder), ex.Message, StringComparison.Ordinal);
    }

    /// <summary>A wrapper whose inner connection is itself is a cycle, not a route anywhere.</summary>
    [Fact]
    public void ASelfReferentialWrapperDoesNotRecurseForever()
    {
        var self = new SelfReferentialConnection();

        Assert.Throws<InvalidOperationException>(() => SqlDialectFactory.GetDialect(self));
    }

    /// <summary>A decorator wrapping null has nothing underneath it to resolve.</summary>
    [Fact]
    public void ADecoratorWrappingNothingFailsLikeAnyUnknownType()
    {
        var empty = new WrappedConnectionDecorator(null);

        Assert.Throws<InvalidOperationException>(() => SqlDialectFactory.GetDialect(empty));
    }

    /// <summary>
    /// The point of the whole change: unrecognised means an error naming the type, not silently
    /// generating SQL Server SQL.
    /// </summary>
    [Fact]
    public void AnEngineJauntyDoesNotKnowIsNamedInTheError()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => SqlDialectFactory.GetDialect(new FirebirdConnection()));

        Assert.Contains(nameof(FirebirdConnection), ex.Message, StringComparison.Ordinal);
        Assert.Contains("RegisterDialect", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Caching must not key an unwrapped resolution against the wrapper's type: one wrapper type
    /// wraps different engines in different places, and caching the first would hand SQLite's
    /// dialect to a profiled PostgreSQL connection.
    /// </summary>
    [Fact]
    public void OneWrapperTypeWrappingTwoEnginesResolvesEachCorrectly()
    {
        ISqlDialect first = SqlDialectFactory.GetDialect(
            new WrappedConnectionDecorator(new SqliteConnection()));
        ISqlDialect second = SqlDialectFactory.GetDialect(
            new WrappedConnectionDecorator(new NpgsqlConnection()));

        Assert.IsType<SQLiteDialect>(SqlDialectFactory.Unwrap(first));
        Assert.IsType<PostgreSqlDialect>(SqlDialectFactory.Unwrap(second));
    }

    // ------------------------------------------------------------------
    // Test doubles. Only the type name matters for resolution; none are opened.
    // ------------------------------------------------------------------

    // Explicit constructors rather than primary constructors: a captured primary-constructor
    // parameter becomes a compiler-generated field, which would give these types a second
    // IDbConnection field and trip the deliberate "exactly one candidate" rule under test.

    private sealed class WrappedConnectionDecorator : StubConnection
    {
        public WrappedConnectionDecorator(IDbConnection? inner) => WrappedConnection = inner;

        public IDbConnection? WrappedConnection { get; }
    }

    private sealed class PrivateFieldDecorator : StubConnection
    {
        private readonly IDbConnection _inner;

        public PrivateFieldDecorator(IDbConnection inner) => _inner = inner;

        public string Describe() => _inner.Database;
    }

    private sealed class TwoConnectionHolder : StubConnection
    {
        private readonly IDbConnection _a;
        private readonly IDbConnection _b;

        public TwoConnectionHolder(IDbConnection a, IDbConnection b)
        {
            _a = a;
            _b = b;
        }

        public string Describe() => _a.Database + _b.Database;
    }

    private sealed class SelfReferentialConnection : StubConnection
    {
        public IDbConnection WrappedConnection => this;
    }

    // Resolution keys on Type.Name, so these have to be named exactly as the real providers are.
    private sealed class SqliteConnection : StubConnection { }
    private sealed class NpgsqlConnection : StubConnection { }
    private sealed class MySqlConnection : StubConnection { }
    private sealed class FirebirdConnection : StubConnection { }

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
}
