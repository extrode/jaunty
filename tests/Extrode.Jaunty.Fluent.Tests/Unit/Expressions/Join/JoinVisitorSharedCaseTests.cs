using System.Linq.Expressions;

using Extrode.Jaunty.Fluent.Expressions;
using Extrode.Jaunty.Fluent.Tests.Entities;
using Extrode.Jaunty.Fluent.Tests.Helpers;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// One set of two-parameter predicates run through the two-, three- and four-table join visitors,
/// the extra parameters unused, so all three are held to the same ON-clause text and messages.
/// </summary>
public class JoinVisitorSharedCaseTests
{
    private static readonly TestDialect Dialect = new();

    private const string AddRejected2 = "Operator Add is not supported in JOIN expressions.";
    private const string AddRejected34 = "Operator Add is not supported.";

    private static readonly Dictionary<string, (LambdaExpression Predicate, string Expected)> Cases = Build();

    private static Dictionary<string, (LambdaExpression, string)> Build()
    {
        string? noName = null;
        short? noShort = null;
        int? noInt = null;
        Func<int, bool> isOne = x => x == 1;
        var list = new List<int> { 1 };
        var pc = new[] { Expression.Parameter(typeof(Product), "p"), Expression.Parameter(typeof(Category), "c") };
        Expression productId = Expression.Property(pc[0], nameof(Product.ProductId));

        return new()
        {
            ["LeftIsNull"] = (P((p, c) => p.CategoryId == null), "(p.[category_id] IS NULL)"),
            ["LeftIsNotNull"] = (P((p, c) => c.Description != null), "(c.[description] IS NOT NULL)"),
            ["RightIsNull"] = (P((p, c) => null == p.CategoryId), "(p.[category_id] IS NULL)"),
            ["RightIsNotNull"] = (P((p, c) => null != c.Description), "(c.[description] IS NOT NULL)"),
            ["CapturedNull"] = (P((p, c) => c.Description == noName), "(c.[description] IS NULL)"),
            ["ConvertedCapturedNull"] = (P((p, c) => p.ProductId == noShort), "(p.[product_id] IS NULL)"),
            ["OrderedAgainstNull"] = (P((p, c) => p.SupplierId > noInt), "(p.[supplier_id] > NULL)"),
            ["NullAgainstNull"] = (P((p, c) => noName == null), "(NULL = NULL)"),
            ["BareColumn"] = (P((p, c) => p.Discontinued), "p.[discontinued]"),
            ["Not"] = (P((p, c) => !p.Discontinued), "NOT (p.[discontinued])"),
            ["NotComparison"] = (P((p, c) => !(p.CategoryId == c.CategoryId)), "NOT ((p.[category_id] = c.[category_id]))"),
            ["Operators"] = (P((p, c) => p.ProductId != c.CategoryId && p.ProductId < c.CategoryId && p.ProductId <= c.CategoryId && p.ProductId > c.CategoryId && p.ProductId >= c.CategoryId),
                "(((((p.[product_id] <> c.[category_id]) AND (p.[product_id] < c.[category_id])) AND (p.[product_id] <= c.[category_id])) AND (p.[product_id] > c.[category_id])) AND (p.[product_id] >= c.[category_id]))"),
            ["Ternary"] = (P((p, c) => p.Discontinued ? p.ProductId == 1 : c.CategoryId == 2),
                "!Conditional (ternary) expressions are not supported in JOIN predicates. Split the predicate into separate conditions, or filter with Where after the join."),
            ["New"] = (P((p, c) => new string('a', 2) == c.CategoryName),
                "!Constructing a 'String' is not supported inside a JOIN predicate. Compute the value before the query and compare against it."),
            ["TypeTest"] = (P((p, c) => (object)c is Category),
                "!Type tests ('is', 'as') are not supported in JOIN predicates. There is no SQL equivalent of a CLR type test over a column; join on a discriminator column instead."),
            ["Invoke"] = (P((p, c) => isOne(p.ProductId)),
                "!Invoking a delegate or a nested lambda is not supported inside a JOIN predicate. Inline the condition."),
            ["NewArray"] = (P((p, c) => new[] { p.ProductId } == null),
                "!Array construction is not supported inside a JOIN predicate. Build the array before the query and pass it in."),
            ["MemberInit"] = (P((p, c) => new Category { CategoryId = 1 } == c),
                "!Object initializers ('new Category { ... }') are not supported inside a JOIN predicate. Compute the value before the query and compare against it."),
            ["ListInit"] = (P((p, c) => new List<int> { 1 } == null),
                "!Collection initializers are not supported inside a JOIN predicate. Build the collection before the query and pass it in."),
            ["Index"] = (Expression.Lambda(Expression.Equal(Expression.MakeIndex(Expression.Constant(list), typeof(List<int>).GetProperty("Item"), [Expression.Constant(0)]), productId), pc),
                "!Indexer access is not supported inside a JOIN predicate. Compute the value before the query and compare against it."),
            ["Default"] = (Expression.Lambda(Expression.Equal(Expression.Default(typeof(int)), productId), pc),
                "!'default(Int32)' is not supported inside a JOIN predicate. Write the value out, or compute it before the query."),
            ["WholeEntity"] = (P((p, c) => c == null), "!'c' is a whole entity, not a condition. Compare its properties instead."),
            ["Method"] = (P((p, c) => c.CategoryName.StartsWith("B")), "!Method 'StartsWith' is not supported in JOIN expressions."),
            ["Negate"] = (P((p, c) => -p.ProductId == c.CategoryId), "!Unary operator 'Negate' is not supported in JOIN expressions."),
            ["Add"] = (P((p, c) => p.ProductId + c.CategoryId == 3), "!" + AddRejected2),
        };
    }

    private static LambdaExpression P(Expression<Func<Product, Category, bool>> e) => e;

    public static TheoryData<string> Names => new(Cases.Keys);

    private static readonly ParameterExpression S = Expression.Parameter(typeof(Supplier), "s");
    private static readonly ParameterExpression O = Expression.Parameter(typeof(Order), "o");

    private static string Run(string name, int arity)
    {
        var (predicate, _) = Cases[name];
        var p = predicate.Parameters;
        try
        {
            return arity switch
            {
                2 => new JoinExpressionVisitor<Product, Category>(Dialect, "p", "c")
                    .Translate(Expression.Lambda<Func<Product, Category, bool>>(predicate.Body, p)).Sql,
                3 => new JoinExpressionVisitor3<Product, Category, Supplier>(Dialect, "p", "c", "s")
                    .Translate(Expression.Lambda<Func<Product, Category, Supplier, bool>>(predicate.Body, p[0], p[1], S)).Sql,
                _ => new JoinExpressionVisitor4<Product, Category, Supplier, Order>(Dialect, "p", "c", "s", "o")
                    .Translate(Expression.Lambda<Func<Product, Category, Supplier, Order, bool>>(predicate.Body, p[0], p[1], S, O)).Sql,
            };
        }
        catch (NotSupportedException ex)
        {
            return "!" + ex.Message;
        }
    }

    private static string Expected(string name, int arity)
    {
        string expected = Cases[name].Expected;
        return arity == 2 || expected != "!" + AddRejected2 ? expected : "!" + AddRejected34;
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void TwoTables(string name) => Assert.Equal(Expected(name, 2), Run(name, 2));

    [Theory]
    [MemberData(nameof(Names))]
    public void ThreeTables(string name) => Assert.Equal(Expected(name, 3), Run(name, 3));

    [Theory]
    [MemberData(nameof(Names))]
    public void FourTables(string name) => Assert.Equal(Expected(name, 4), Run(name, 4));

    [Fact]
    public void ThreeTables_ReusingTheVisitor_StartsFromEmpty()
    {
        var visitor = new JoinExpressionVisitor3<Product, Category, Supplier>(Dialect, "p", "c", "s");
        visitor.Translate((p, c, s) => s.Country == "a");

        var (sql, parameters) = visitor.Translate((p, c, s) => s.Country == "b");

        Assert.Equal("(s.[country] = @s_country)", sql);
        Assert.Equal([("@s_country", (object?)"b")], parameters);
    }

    [Fact]
    public void FourTables_ReusingTheVisitor_StartsFromEmpty()
    {
        var visitor = new JoinExpressionVisitor4<Product, Category, Supplier, Order>(Dialect, "p", "c", "s", "o");
        visitor.Translate((p, c, s, o) => o.CustomerId == "a");

        var (sql, parameters) = visitor.Translate((p, c, s, o) => o.CustomerId == "b");

        Assert.Equal("(o.[customer_id] = @o_customer_id)", sql);
        Assert.Equal([("@o_customer_id", (object?)"b")], parameters);
    }
}
