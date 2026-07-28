using System.Linq.Expressions;

using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

using Xunit;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// AUD-R25 (B5-5): all five expression visitors resolved a property to its column with
/// <c>metadata.Columns.FirstOrDefault(c =&gt; c.PropertyName == propertyName)</c> - a LINQ delegate
/// allocation plus an O(columns) linear scan - and then ran the result through
/// <c>_dialect.EscapeColumnName(...)</c>, which re-executes <c>SqlIdentifierValidator</c>'s regex
/// match and a keyword <c>HashSet</c> lookup. Both costs were paid per column reference per query
/// build, so a predicate touching six columns did six linear scans and six regex matches.
///
/// <para>
/// <c>CachedDialectMetadata</c> exists precisely to eliminate this: an <c>OrdinalIgnoreCase</c>
/// dictionary of property name to <em>already-escaped</em> column name, built once per
/// (entity, dialect) pair by <c>FluentMetadataCache.GetForDialect&lt;T&gt;</c>. <c>QueryBuilder</c>,
/// <c>CteBuilder</c> and <c>InsertBuilder</c> all used it; the visitors - which do far more
/// per-column work than the builders - were the only Fluent components that bypassed it. The
/// constitution lists "Performance - No LINQ in hot paths" as priority #1.
/// </para>
///
/// <para>
/// The change is behaviour-preserving, so these are equivalence tests rather than defect
/// reproduction. Two things they do catch, and both are easy to get wrong when switching to a
/// pre-escaped source: escaping a name twice, and letting the escaped form leak into a generated
/// parameter name. The second was a real mistake made and caught while writing this fix - the raw
/// column name still has to reach <c>GetParameterName</c>.
/// </para>
/// </summary>
public class VisitorColumnResolutionTests
{
    // TestDialect brackets every identifier unconditionally, which is what makes "escaped exactly
    // once" assertable - a second pass would show up as [[x]]. The two real dialects only escape
    // reserved keywords, so they are used for the keyword cases where their behaviour is the point.
    private readonly TestDialect _dialect = new();
    private readonly SqlServerDialect _sqlServer = new();
    private readonly PostgreSqlDialect _postgres = new();

    // ------------------------------------------------------------------
    // WhereExpressionVisitor: escaping happens exactly once
    // ------------------------------------------------------------------

    [Fact]
    public void Where_MappedColumn_IsEscapedExactlyOnce()
    {
        Expression<Func<Product, bool>> predicate = p => p.ProductName == "x";

        var (sql, _) = new WhereExpressionVisitor<Product>(_dialect).Translate(predicate);

        Assert.Contains("[product_name]", sql);
        Assert.DoesNotContain("[[product_name]]", sql);
    }

    [Fact]
    public void Where_ReservedKeywordColumn_IsEscapedExactlyOnce()
    {
        // KeywordColumnEntity exists for the earlier CachedDialectMetadata double-escaping fix:
        // escaping an already-escaped keyword a second time made SqlIdentifierValidator reject the
        // bracketed text and throw. Reading pre-escaped names from that same cache must not
        // reintroduce it.
        Expression<Func<KeywordColumnEntity, bool>> predicate = e => e.Order == 1;

        var (sql, _) = new WhereExpressionVisitor<KeywordColumnEntity>(_sqlServer).Translate(predicate);

        Assert.Contains("[order]", sql);
        Assert.DoesNotContain("[[order]]", sql);
    }

    [Fact]
    public void Where_ReservedKeywordColumn_OnADoubleQuoteDialect_IsEscapedExactlyOnce()
    {
        Expression<Func<KeywordColumnEntity, bool>> predicate = e => e.Order == 1;

        var (sql, _) = new WhereExpressionVisitor<KeywordColumnEntity>(_postgres).Translate(predicate);

        Assert.Contains("\"order\"", sql);
        Assert.DoesNotContain("\"\"order\"\"", sql);
    }

    [Fact]
    public void Where_MultipleColumns_AllResolveIndependently()
    {
        Expression<Func<Product, bool>> predicate =
            p => p.ProductName == "x" && p.UnitPrice > 1m && p.Discontinued == false;

        var (sql, _) = new WhereExpressionVisitor<Product>(_dialect).Translate(predicate);

        Assert.Contains("[product_name]", sql);
        Assert.Contains("[unit_price]", sql);
        Assert.Contains("[discontinued]", sql);
        Assert.DoesNotContain("[[", sql);
    }

    [Fact]
    public void Where_StringMethod_EscapesTheColumnExactlyOnce()
    {
        Expression<Func<Product, bool>> predicate = p => p.ProductName.Contains("x");

        var (sql, _) = new WhereExpressionVisitor<Product>(_dialect).Translate(predicate);

        Assert.Contains("[product_name]", sql);
        Assert.DoesNotContain("[[", sql);
    }

    [Fact]
    public void Where_StringLength_EscapesTheInnerColumnExactlyOnce()
    {
        Expression<Func<Product, bool>> predicate = p => p.ProductName.Length > 3;

        var (sql, _) = new WhereExpressionVisitor<Product>(_dialect).Translate(predicate);

        Assert.Contains("[product_name]", sql);
        Assert.DoesNotContain("[[", sql);
    }

    // ------------------------------------------------------------------
    // The escaped form must not leak into generated parameter names
    // ------------------------------------------------------------------

    [Fact]
    public void Where_ParameterName_UsesTheUnescapedColumnName()
    {
        // A column reference feeds two different things - the SQL text, which must be escaped, and
        // the generated parameter name, which must not be. Reading only the escaped form would
        // produce "@[product_name]".
        Expression<Func<Product, bool>> predicate = p => p.ProductName == "x";

        var (sql, parameters) = new WhereExpressionVisitor<Product>(_dialect).Translate(predicate);

        Assert.Equal("@product_name", Assert.Single(parameters).Name);
        Assert.DoesNotContain("@[product_name]", sql);
    }

    [Fact]
    public void Where_StringContains_ParameterName_UsesTheUnescapedColumnName()
    {
        Expression<Func<Product, bool>> predicate = p => p.ProductName.Contains("x");

        var (_, parameters) = new WhereExpressionVisitor<Product>(_dialect).Translate(predicate);

        Assert.Equal("@product_name", Assert.Single(parameters).Name);
    }

    [Fact]
    public void Where_In_ParameterNames_UseTheUnescapedColumnName()
    {
        var ids = new[] { 1, 2 };
        Expression<Func<Product, bool>> predicate = p => ids.Contains(p.ProductId);

        var (_, parameters) = new WhereExpressionVisitor<Product>(_dialect).Translate(predicate);

        Assert.Equal(2, parameters.Count);
        Assert.All(parameters, p => Assert.StartsWith("@product_id", p.Name));
        Assert.All(parameters, p => Assert.DoesNotContain("[", p.Name));
    }

    [Fact]
    public void Where_KeywordColumn_ParameterName_IsNotBracketed()
    {
        Expression<Func<KeywordColumnEntity, bool>> predicate = e => e.Order == 1;

        var (_, parameters) = new WhereExpressionVisitor<KeywordColumnEntity>(_dialect).Translate(predicate);

        Assert.Equal("@order", Assert.Single(parameters).Name);
    }

    [Fact]
    public void Where_ParameterName_StillUsesTheColumnNameNotThePropertyName()
    {
        // The old lookup resolved [Column("product_name")] before naming the parameter. Falling
        // back to member.Member.Name would have quietly renamed every parameter.
        Expression<Func<Product, bool>> predicate = p => p.ProductName == "x";

        var (_, parameters) = new WhereExpressionVisitor<Product>(_dialect).Translate(predicate);

        Assert.DoesNotContain("ProductName", Assert.Single(parameters).Name);
    }

    // ------------------------------------------------------------------
    // SelectExpressionVisitor
    // ------------------------------------------------------------------

    [Fact]
    public void Select_MappedColumn_IsEscapedExactlyOnce()
    {
        Expression<Func<Product, object>> projection = p => p.ProductName;

        SelectColumn column = Assert.Single(new SelectExpressionVisitor<Product>(_dialect).Translate(projection));

        Assert.Equal("[product_name]", column.Sql);
    }

    [Fact]
    public void Select_KeywordColumn_IsEscapedExactlyOnce()
    {
        Expression<Func<KeywordColumnEntity, object>> projection = e => e.Order;

        SelectColumn column = Assert.Single(new SelectExpressionVisitor<KeywordColumnEntity>(_dialect).Translate(projection));

        Assert.Equal("[order]", column.Sql);
    }

    [Fact]
    public void Select_AnonymousProjection_ResolvesEveryColumn()
    {
        Expression<Func<Product, object>> projection = p => new { p.ProductName, p.UnitPrice };

        var columns = new SelectExpressionVisitor<Product>(_dialect).Translate(projection);

        Assert.Equal(2, columns.Count);
        Assert.Equal("[product_name]", columns[0].Sql);
        Assert.Equal("[unit_price]", columns[1].Sql);
    }

    // ------------------------------------------------------------------
    // Multi-entity visitors resolve each parameter against its own entity
    // ------------------------------------------------------------------

    [Fact]
    public void ThreeWayJoin_ResolvesEachParameterAgainstItsOwnEntity()
    {
        // JoinExpressionVisitor3/4 pick the entity by parameter identity, so the cached metadata
        // has to be selected in the same branch as the entity metadata - not once for the visitor.
        Expression<Func<Product, Category, Supplier, bool>> predicate =
            (p, c, s) => p.CategoryId == c.CategoryId && p.SupplierId == s.SupplierId;

        var (sql, _) = new JoinExpressionVisitor3<Product, Category, Supplier>(_dialect, "p", "c", "s")
            .Translate(predicate);

        Assert.Contains("p.[category_id]", sql);
        Assert.Contains("c.[category_id]", sql);
        Assert.Contains("s.[supplier_id]", sql);
        Assert.DoesNotContain("[[", sql);
    }

    [Fact]
    public void FourWayJoin_ResolvesEachParameterAgainstItsOwnEntity()
    {
        Expression<Func<Product, Category, Supplier, Order, bool>> predicate =
            (p, c, s, o) => p.CategoryId == c.CategoryId && p.SupplierId == s.SupplierId && o.OrderId > 0;

        var (sql, _) = new JoinExpressionVisitor4<Product, Category, Supplier, Order>(_dialect, "p", "c", "s", "o")
            .Translate(predicate);

        Assert.Contains("p.[category_id]", sql);
        Assert.Contains("s.[supplier_id]", sql);
        Assert.Contains("o.[order_id]", sql);
        Assert.DoesNotContain("[[", sql);
    }

    [Fact]
    public void SelfJoin_StillResolvesBothSidesByParameterIdentity()
    {
        // Matching by parameter reference rather than by Type is what makes a self-join work; the
        // per-branch cached metadata must not have changed that.
        Expression<Func<Product, Product, Category, bool>> predicate =
            (a, b, c) => a.ProductId == b.SupplierId && c.CategoryId == a.CategoryId;

        var (sql, _) = new JoinExpressionVisitor3<Product, Product, Category>(_dialect, "a", "b", "c")
            .Translate(predicate);

        Assert.Contains("a.[product_id]", sql);
        Assert.Contains("b.[supplier_id]", sql);
        Assert.Contains("c.[category_id]", sql);
    }

    // ------------------------------------------------------------------
    // ExistsExpressionVisitor
    // ------------------------------------------------------------------

    [Fact]
    public void Exists_ResolvesOuterAndSubqueryColumnsAgainstTheirOwnEntities()
    {
        Expression<Func<Product, Category, bool>> predicate = (p, c) => p.CategoryId == c.CategoryId;

        var visitor = new ExistsExpressionVisitor<Product, Category>(
            _dialect,
            FluentMetadataCache.GetMetadata<Product>(),
            FluentMetadataCache.GetMetadata<Category>(),
            outerAlias: "p",
            subqueryAlias: "c");

        var (sql, _) = visitor.Translate(predicate);

        Assert.Contains("p.[category_id]", sql);
        Assert.Contains("c.[category_id]", sql);
        Assert.DoesNotContain("[[", sql);
    }

    [Fact]
    public void Exists_KeywordColumn_IsEscapedExactlyOnce()
    {
        Expression<Func<KeywordColumnEntity, KeywordColumnEntity, bool>> predicate =
            (a, b) => a.Order == b.Id;

        var visitor = new ExistsExpressionVisitor<KeywordColumnEntity, KeywordColumnEntity>(
            _dialect,
            FluentMetadataCache.GetMetadata<KeywordColumnEntity>(),
            FluentMetadataCache.GetMetadata<KeywordColumnEntity>(),
            outerAlias: "a",
            subqueryAlias: "b");

        var (sql, _) = visitor.Translate(predicate);

        Assert.Contains("a.[order]", sql);
        Assert.Contains("b.[id]", sql);
        Assert.DoesNotContain("[[", sql);
    }
}
