using System.Data;
using System.Linq.Expressions;

using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// AUD-R35-019. AUD-R34-021 rejected a nested member access - <c>o.OrderDate.Year</c> - in
/// <c>WhereExpressionVisitor</c> only. The SELECT visitor and <c>PropertyExtractor</c> had the
/// identical hole: both resolve a member expression to a column through a lookup that falls back
/// to <c>EscapeColumnName(propertyName)</c>, so the chain's leaf became a plausible-looking column
/// name. Where the entity maps a column called <c>Year</c>, <c>Date</c> or <c>Day</c> the wrong
/// column was used silently.
/// </summary>
public class NestedMemberColumnRejectionTests
{
    private readonly NestedMemberConnection _connection;

    public NestedMemberColumnRejectionTests()
    {
        SqlDialectFactory.RegisterDialect(nameof(NestedMemberConnection), new TestDialect());
        _connection = new NestedMemberConnection();
    }

    // ------------------------------------------------------------------
    // PropertyExtractor - join keys, ORDER BY, INSERT column lists
    // ------------------------------------------------------------------

    [Fact]
    public void ExtractPropertyName_NestedMember_Throws()
    {
        Expression<Func<Order, int>> selector = o => o.OrderDate!.Value.Year;

        var ex = Assert.Throws<NotSupportedException>(() => PropertyExtractor.ExtractPropertyName(selector));

        Assert.Contains("Year", ex.Message);
        Assert.Contains("is a member of a column, not a column", ex.Message);
    }

    [Fact]
    public void ExtractPropertyNames_NestedMember_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            PropertyExtractor.ExtractPropertyNames<Order>(o => o.OrderDate!.Value.Year));
    }

    [Fact]
    public void ExtractOrderByProperty_NestedMember_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            PropertyExtractor.ExtractOrderByProperty<Order>(o => o.OrderDate!.Value.Month));
    }

    [Fact]
    public void ExtractPropertyName_NestedMemberBehindABoxingConvert_Throws()
    {
        // ExtractPropertyNames takes Expression<Func<T, object?>>, so a value-typed leaf arrives
        // wrapped in a Convert; the guard must sit inside the unwrap, not in front of it.
        Assert.Throws<NotSupportedException>(() =>
            PropertyExtractor.ExtractPropertyNames<Order>(o => (object?)o.OrderDate!.Value.Day));
    }

    [Fact]
    public void ExtractPropertyName_DirectMember_StillResolves()
    {
        Assert.Equal("OrderDate", PropertyExtractor.ExtractPropertyName<Order, DateTime?>(o => o.OrderDate));
        Assert.Equal("OrderId", PropertyExtractor.ExtractPropertyName<Order, int>(o => o.OrderId));
    }

    [Fact]
    public void ExtractPropertyName_NullableValueOnTheParameter_StillUnwraps()
    {
        // The one nested shape with a meaning: p.CategoryId!.Value names CategoryId. Its receiver
        // is the parameter, so the guard passes.
        Assert.Equal("CategoryId", PropertyExtractor.ExtractPropertyName<Product, int>(p => p.CategoryId!.Value));
    }

    // ------------------------------------------------------------------
    // SelectExpressionVisitor
    // ------------------------------------------------------------------

    [Fact]
    public void SelectPartial_NestedMemberInAnAnonymousProjection_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            _connection.From<Order>().ToSql(o => new { Year = o.OrderDate!.Value.Year }));

        Assert.Contains("is a member of a column, not a column", ex.Message);
    }

    [Fact]
    public void SelectPartial_StandaloneNestedMember_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            _connection.From<Order>().ToSql(o => o.OrderDate!.Value.Year));
    }

    [Fact]
    public void SelectPartial_DirectMembers_StillProject()
    {
        // The control: the guard must not reject the ordinary projection.
        string sql = _connection.From<Order>().ToSql(o => new { o.OrderId, o.OrderDate });

        Assert.Contains("[order_id]", sql);
        Assert.Contains("[order_date]", sql);
    }

    // ------------------------------------------------------------------
    // WhereExpressionVisitor - the AUD-R34-021 original, still guarded
    // ------------------------------------------------------------------

    [Fact]
    public void Where_NestedMember_StillThrows()
    {
        Assert.Throws<NotSupportedException>(() =>
            _connection.From<Order>().Where(o => o.OrderDate!.Value.Year == 1997).ToSql());
    }

    private sealed class NestedMemberConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => string.Empty;
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Dispose() { }
        public void Open() { }
    }
}
