using System.Data;

using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// AUD-R35-187. The thirty-two First/Single terminals saved <c>_take</c>, clamped it to 1 or 2,
/// built the SQL and restored - with no <c>try</c>/<c>finally</c>. <c>BuildSelectSql</c> throws when
/// the dialect rejects an identifier, and the throw left the builder permanently clamped, so a later
/// <c>Select()</c>/<c>ToSql()</c> on the same instance silently produced a one-row query.
/// A dialect that fails on demand is the only seam that lets the same builder both throw and then
/// succeed, which is what makes the leak observable.
/// </summary>
public class TakeClampSurvivesAThrowTests
{
    private readonly ThrowOnDemandDialect _dialect = new();
    private readonly ClampConnection _connection = new();

    public TakeClampSurvivesAThrowTests() =>
        SqlDialectFactory.RegisterDialect(nameof(ClampConnection), _dialect);

    [Fact]
    public void AThrowingSelectFirst_LeavesTakeUnclamped()
    {
        var builder = _connection.From<Product>().Where(p => p.UnitPrice > 0);

        _dialect.Throwing = true;
        Assert.Throws<InvalidOperationException>(() => builder.SelectFirst());
        _dialect.Throwing = false;

        Assert.DoesNotContain("FETCH NEXT 1 ROWS ONLY", builder.ToSql());
    }

    [Fact]
    public void AThrowingSelectSingle_LeavesTakeUnclamped()
    {
        var builder = _connection.From<Product>().Where(p => p.UnitPrice > 0);

        _dialect.Throwing = true;
        Assert.Throws<InvalidOperationException>(() => builder.SelectSingle());
        _dialect.Throwing = false;

        Assert.DoesNotContain("FETCH NEXT 2 ROWS ONLY", builder.ToSql());
    }

    [Fact]
    public void AThrowingSelectPartialFirst_LeavesTakeUnclamped()
    {
        var builder = _connection.From<Product>().Where(p => p.UnitPrice > 0);

        _dialect.Throwing = true;
        Assert.Throws<InvalidOperationException>(() => builder.SelectPartialFirst(p => p.ProductName));
        _dialect.Throwing = false;

        Assert.DoesNotContain("FETCH NEXT 1 ROWS ONLY", builder.ToSql());
    }

    [Fact]
    public void AnExplicitTakeSurvivesAThrowingFirstTerminal()
    {
        var builder = _connection.From<Product>().Where(p => p.UnitPrice > 0).Take(5);

        _dialect.Throwing = true;
        Assert.Throws<InvalidOperationException>(() => builder.SelectFirst());
        _dialect.Throwing = false;

        Assert.Contains("FETCH NEXT 5 ROWS ONLY", builder.ToSql());
    }

    /// <summary>
    /// A <see cref="TestDialect"/> that fails the paging wrap while
    /// <see cref="Throwing"/> is set, so one builder can be made to throw inside
    /// <c>BuildSelectSql</c> and then to succeed.
    /// </summary>
    private sealed class ThrowOnDemandDialect : TestDialect
    {
        public bool Throwing { get; set; }

        // GetPagingSql is what BuildSelectSql calls once _take is set, so this fails exactly on the
        // clamped build and succeeds on the unclamped one afterwards.
        public override string GetPagingSql(string baseSql, int offset, int fetchNext) =>
            Throwing
                ? throw new InvalidOperationException("ThrowOnDemandDialect is failing on purpose.")
                : $"{baseSql} OFFSET {offset} ROWS FETCH NEXT {fetchNext} ROWS ONLY";
    }

    private sealed class ClampConnection : IDbConnection
    {
        public string ConnectionString { get => ""; set { } }
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Closed;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }
}
