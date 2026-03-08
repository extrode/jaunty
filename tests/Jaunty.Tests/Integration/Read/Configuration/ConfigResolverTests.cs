#if NET8_0_OR_GREATER
using System.Buffers;
#endif

using Jaunty.Configuration;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read.Configuration;

public class ConfigResolverTests : IClassFixture<DialectFixture>, IDisposable
{
    private readonly DialectFixture _fixture;

    public ConfigResolverTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    public void Dispose()
    {
        _fixture.Dispose();
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();
        SpecialTypeMappers.Register();
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithSnakeCaseColumnResolver_MapsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        // Configure snake_case column resolver
        JauntyConfig.ColumnNameResolver = ToSnakeCase;

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = @Id"
            : "SELECT category_id, category_name, description FROM categories WHERE category_id = @Id";

        IEnumerable<CategorySnakeCase> categories = connection.Query<CategorySnakeCase>(sql, new { Id = 1 });

        Assert.Single(categories);
        Assert.Equal(1, categories.ElementAt(0).CategoryId);
        Assert.False(string.IsNullOrEmpty(categories.ElementAt(0).CategoryName));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartial_AttributeTakesPrecedenceOverResolver(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        // Configure a resolver that would give wrong names
        JauntyConfig.ColumnNameResolver = name => "wrong_" + name.ToLower();

        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT ProductId, ProductName, UnitPrice FROM Products WHERE ProductId = @Id"
            : "SELECT product_id, product_name, unit_price FROM products WHERE product_id = @Id";

        // ProductWithAttributes has explicit [Column] attributes which should take precedence
        var products = connection.QueryPartial<ProductWithAttributes>(sql, new { Id = 1 });

        Assert.Single(products);
        Assert.True(products[0].ProductId > 0);
    }

    private static string ToSnakeCase(string source)
    {
#if NET8_0_OR_GREATER
        if (string.IsNullOrEmpty(source)) return source;

        var sb = new System.Text.StringBuilder(source.Length * 2);
        var chars = source.AsSpan();
        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
#else
        if (string.IsNullOrEmpty(source)) return source;

        var result = new System.Text.StringBuilder(source.Length + Math.Min(2, source.Length));
        for (int i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (char.IsUpper(c))
            {
                if (i > 0) result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
#endif
    }
}

public class CategorySnakeCase
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
}