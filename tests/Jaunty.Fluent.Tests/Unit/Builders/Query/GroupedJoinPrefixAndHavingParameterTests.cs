using System.Data;

using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Unit.Builders.Query;

/// <summary>
/// Two grouped-path defects, both invisible to the suite because every grouped test used
/// unproblematic table names and a single grouping per builder.
/// <list type="bullet">
/// <item><description>
/// AUD-R35-015 - the three grouped-join builders resolved an unaliased table prefix as the raw
/// <c>metadata.TableName</c>, so GROUP BY and aggregate column references came out
/// <c>raw_table.[column]</c> while FROM/JOIN named the same table escaped. Round 17 fixed exactly
/// this across the non-grouped join builders and never reached these three.
/// </description></item>
/// <item><description>
/// AUD-R35-016 - HAVING operand names were sequenced per grouped builder but written into the
/// parent's shared parameter collection, so a second grouping off one query threw
/// <c>A parameter named '@hp0' has already been added</c>.
/// </description></item>
/// </list>
/// Uses <see cref="TestDialect"/>, which brackets every identifier, so raw-vs-escaped is visible
/// regardless of whether the name is a keyword.
/// </summary>
public class GroupedJoinPrefixAndHavingParameterTests
{
    private readonly GroupedPrefixConnection _connection;

    public GroupedJoinPrefixAndHavingParameterTests()
    {
        SqlDialectFactory.RegisterDialect(nameof(GroupedPrefixConnection), new TestDialect());
        _connection = new GroupedPrefixConnection();
    }

    private IJoinedQuery<Product, Category> TwoTable() =>
        _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId);

    private IJoinedQuery3<Product, Category, Supplier> ThreeTable() =>
        TwoTable()
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId);

    private IJoinedQuery4<Product, Category, Supplier, Order> FourTable() =>
        ThreeTable()
            .LeftJoin<Product, Category, Supplier, Order>()
            .On("orders.order_id > 0");

    // ------------------------------------------------------------------
    // AUD-R35-015: the unaliased table prefix
    // ------------------------------------------------------------------

    [Fact]
    public void TwoTableGroupedJoin_NoAlias_QualifiesGroupByWithTheEscapedTableName()
    {
        string sql = TwoTable()
            .GroupBy((p, c) => p.CategoryId)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.Contains("[products].[category_id]", sql);
        Assert.DoesNotContain("products.[category_id]", sql);
    }

    [Fact]
    public void TwoTableGroupedJoin_AggregateOverAJoinedColumn_IsAlsoEscaped()
    {
        string sql = TwoTable()
            .GroupBy((p, c) => c.CategoryName)
            .ToSql(g => new { g.Key, Total = g.Sum((p, c) => p.UnitPrice) });

        Assert.Contains("[categories].[category_name]", sql);
        Assert.DoesNotContain("categories.[category_name]", sql);
        Assert.Contains("[products].[unit_price]", sql);
        Assert.DoesNotContain("products.[unit_price]", sql);
    }

    [Fact]
    public void ThreeTableGroupedJoin_NoAlias_QualifiesWithTheEscapedTableName()
    {
        string sql = ThreeTable()
            .GroupBy((p, c, s) => s.SupplierId)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.Contains("[suppliers].[supplier_id]", sql);
        Assert.DoesNotContain("suppliers.[supplier_id]", sql);
    }

    [Fact]
    public void FourTableGroupedJoin_NoAlias_QualifiesWithTheEscapedTableName()
    {
        string sql = FourTable()
            .GroupBy((p, c, s, o) => p.CategoryId)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.Contains("[products].[category_id]", sql);
        Assert.DoesNotContain("products.[category_id]", sql);
    }

    [Fact]
    public void AnExplicitAliasStillWins()
    {
        // The other half of the fallback: escaping must not be applied to a caller's alias.
        string sql = _connection.From<Product>("p")
            .InnerJoin<Category>("c")
            .On(p => p.CategoryId, cat => cat.CategoryId)
            .GroupBy((p, c) => p.CategoryId)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.Contains("p.[category_id]", sql);
        Assert.DoesNotContain("[p].[category_id]", sql);
    }

    // ------------------------------------------------------------------
    // AUD-R35-016: HAVING operands across two groupings off one builder
    // ------------------------------------------------------------------

    [Fact]
    public void TwoGroupingsOffOneJoin_EachWithABoundHavingOperand_DoNotCollide()
    {
        var joined = TwoTable();

        string first = joined.GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Count() > 3)
            .ToSql(g => new { g.Key, Count = g.Count() });

        string second = joined.GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Count() > 5)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.NotEqual(HavingOperand(first), HavingOperand(second));
    }

    [Fact]
    public void ThreeGroupingsOffOneJoin_StillDoNotCollide()
    {
        var joined = TwoTable();
        var operands = new HashSet<string>();

        for (int i = 0; i < 3; i++)
        {
            string sql = joined.GroupBy((p, c) => p.CategoryId)
                .Having(g => g.Count() > i)
                .ToSql(g => new { g.Key, Count = g.Count() });

            Assert.True(operands.Add(HavingOperand(sql)), "a HAVING operand name was reused");
        }
    }

    [Fact]
    public void ThreeTableJoin_TwoGroupingsWithHaving_DoNotCollide()
    {
        var joined = ThreeTable();

        string first = joined.GroupBy((p, c, s) => p.CategoryId).Having(g => g.Count() > 3)
            .ToSql(g => new { g.Key, Count = g.Count() });
        string second = joined.GroupBy((p, c, s) => p.CategoryId).Having(g => g.Count() > 5)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.NotEqual(HavingOperand(first), HavingOperand(second));
    }

    [Fact]
    public void FourTableJoin_TwoGroupingsWithHaving_DoNotCollide()
    {
        var joined = FourTable();

        string first = joined.GroupBy((p, c, s, o) => p.CategoryId).Having(g => g.Count() > 3)
            .ToSql(g => new { g.Key, Count = g.Count() });
        string second = joined.GroupBy((p, c, s, o) => p.CategoryId).Having(g => g.Count() > 5)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.NotEqual(HavingOperand(first), HavingOperand(second));
    }

    [Fact]
    public void SingleTable_TwoGroupingsOffOneQuery_DoNotCollide()
    {
        var query = _connection.From<Product>();

        string first = query.GroupBy(p => p.CategoryId).Having(g => g.Count() > 3)
            .ToSql(g => new { g.Key, Count = g.Count() });
        string second = query.GroupBy(p => p.CategoryId).Having(g => g.Count() > 5)
            .ToSql(g => new { g.Key, Count = g.Count() });

        Assert.NotEqual(HavingOperand(first), HavingOperand(second));
    }

    [Fact]
    public void OneGroupingWithTwoHavingOperands_StillBindsThemSeparately()
    {
        // The control: within a single builder the operands were already distinct, and the
        // rename must not have collapsed them onto one name.
        string sql = TwoTable()
            .GroupBy((p, c) => p.CategoryId)
            .Having(g => g.Count() > 3 && g.Sum((p, c) => p.UnitPrice) > 150)
            .ToSql(g => new { g.Key, Count = g.Count() });

        string[] operands = OperandNames(sql);

        Assert.Equal(2, operands.Length);
        Assert.NotEqual(operands[0], operands[1]);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string HavingOperand(string sql)
    {
        string[] names = OperandNames(sql);
        Assert.NotEmpty(names);
        return names[0];
    }

    private static string[] OperandNames(string sql)
    {
        int having = sql.IndexOf("HAVING", StringComparison.Ordinal);
        Assert.True(having >= 0, $"no HAVING clause in: {sql}");

        var names = new List<string>();
        string clause = sql.Substring(having);

        for (int i = 0; i < clause.Length; i++)
        {
            if (clause[i] != '@') continue;

            int end = i + 1;
            while (end < clause.Length && (char.IsLetterOrDigit(clause[end]) || clause[end] == '_'))
                end++;

            names.Add(clause.Substring(i, end - i));
            i = end - 1;
        }

        return names.ToArray();
    }

    private sealed class GroupedPrefixConnection : IDbConnection
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
