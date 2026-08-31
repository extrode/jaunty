using System.Data;

using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders;

/// <summary>
/// AUD-R35-187's sibling on <c>CteBuilder</c>. Its eight first-row terminals saved
/// <c>_takeCount</c>, clamped it to 1, built the SQL and restored - with no <c>try</c>/<c>finally</c>,
/// the same shape <c>QueryBuilder</c> (AUD-R35-187) and <c>SetOperationBuilder</c> (AUD-R35-190)
/// were fixed for. A throw out of <c>BuildSql</c> left the builder permanently clamped, and a
/// CteBuilder is held and reused by design - building the CTE definition is the expensive part - so
/// a later <c>Select()</c>/<c>ToSql()</c> silently produced a one-row query. Same seam as
/// <see cref="TakeClampSurvivesAThrowTests"/>: a dialect that fails on demand is what lets one
/// builder both throw and then succeed.
/// </summary>
public class CteTakeClampSurvivesAThrowTests
{
    private readonly ThrowOnDemandDialect _dialect = new();
    private readonly CteClampConnection _connection = new();

    public CteTakeClampSurvivesAThrowTests() =>
        SqlDialectFactory.RegisterDialect(nameof(CteClampConnection), _dialect);

    private ICteQueryClause<Product> Cte() =>
        _connection.Cte<Product>("Expensive").As(q => q.Where(p => p.UnitPrice > 100));

    [Fact]
    public void AThrowingSelectFirst_LeavesTakeUnclamped()
    {
        ICteQueryClause<Product> builder = Cte();

        _dialect.Throwing = true;
        Assert.Throws<InvalidOperationException>(() => builder.SelectFirst());
        _dialect.Throwing = false;

        Assert.DoesNotContain("FETCH NEXT 1 ROWS ONLY", builder.ToSql());
    }

    [Fact]
    public void AThrowingSelectFirstOrDefault_LeavesTakeUnclamped()
    {
        ICteQueryClause<Product> builder = Cte();

        _dialect.Throwing = true;
        Assert.Throws<InvalidOperationException>(() => builder.SelectFirstOrDefault());
        _dialect.Throwing = false;

        Assert.DoesNotContain("FETCH NEXT 1 ROWS ONLY", builder.ToSql());
    }

    [Fact]
    public async Task AThrowingSelectFirstAsync_LeavesTakeUnclamped()
    {
        ICteQueryClause<Product> builder = Cte();

        _dialect.Throwing = true;
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await builder.SelectFirstAsync(TestContext.Current.CancellationToken));
        _dialect.Throwing = false;

        Assert.DoesNotContain("FETCH NEXT 1 ROWS ONLY", builder.ToSql());
    }

    [Fact]
    public void AnExplicitTakeSurvivesAThrowingFirstTerminal()
    {
        ICteQueryClause<Product> builder = Cte().Take(5);

        _dialect.Throwing = true;
        Assert.Throws<InvalidOperationException>(() => builder.SelectFirst());
        _dialect.Throwing = false;

        Assert.Contains("FETCH NEXT 5 ROWS ONLY", builder.ToSql());
    }

    private sealed class ThrowOnDemandDialect : TestDialect
    {
        public bool Throwing { get; set; }

        public override string GetPagingSql(string baseSql, int offset, int fetchNext) =>
            Throwing
                ? throw new InvalidOperationException("ThrowOnDemandDialect is failing on purpose.")
                : $"{baseSql} OFFSET {offset} ROWS FETCH NEXT {fetchNext} ROWS ONLY";
    }

    private sealed class CteClampConnection : IDbConnection
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
