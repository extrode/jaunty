using System.Data;

using Jaunty.Dialects;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// R27 batch 7, two guards on <see cref="SqlDialectFactory"/>. The decorator recursion in
/// <c>GetDialect</c> had no depth bound - <c>ReferenceEquals</c> catches a decorator returning
/// itself but not a two-element cycle (A exposes B, B exposes A), which recursed until the stack
/// overflowed and killed the process. And neither <c>RegisterDialect</c> overload rejected a null
/// dialect, so the null was stored and surfaced later as a NullReferenceException at an unrelated
/// <c>GetDialect</c> call site.
/// </summary>
[Collection("Dialect Factory State")]
public class DialectFactoryGuardTests
{
    /// <summary>
    /// Pre-fix this was not a failing assertion but a StackOverflowException crashing the test
    /// host - the depth bound turns it into the factory's ordinary unresolvable-connection error.
    /// </summary>
    [Fact]
    public void ATwoElementDecoratorCycleThrowsInsteadOfOverflowingTheStack()
    {
        var a = new CycleConnectionA();
        var b = new CycleConnectionB { Inner = a };
        a.Inner = b;

        Assert.Throws<InvalidOperationException>(() => SqlDialectFactory.GetDialect(a));
    }

    [Fact]
    public void RegisterDialectByName_NullDialect_ThrowsAtRegistration()
    {
        Assert.Throws<ArgumentNullException>(
            () => SqlDialectFactory.RegisterDialect("R27GuardTestConnection", null!));
    }

    [Fact]
    public void RegisterDialectByType_NullDialect_ThrowsAtRegistration()
    {
        Assert.Throws<ArgumentNullException>(
            () => SqlDialectFactory.RegisterDialect<CycleConnectionA>(null!));
    }

    private sealed class CycleConnectionA : StubConnection
    {
        public IDbConnection? Inner { get; set; }

        public IDbConnection? WrappedConnection => Inner;
    }

    private sealed class CycleConnectionB : StubConnection
    {
        public IDbConnection? Inner { get; set; }

        public IDbConnection? WrappedConnection => Inner;
    }

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
