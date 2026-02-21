#if NET8_0_OR_GREATER
using System.Buffers;
#endif

using Jaunty;
using Jaunty.Configuration;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Configuration;

public class ConfigResolverTests : IDisposable
{
    private readonly Database _db;

    public ConfigResolverTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        _db.Dispose();
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();
    }

    [Fact]
    public void Query_WithSnakeCaseColumnResolver_MapsCorrectly()
    {
        // Configure snake_case column resolver
        JauntyConfig.ColumnNameResolver = ToSnakeCase;

        IEnumerable<CategorySnakeCase> categories = _db.Connection.Query<CategorySnakeCase>(
            "SELECT category_id, category_name, description FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        Assert.Single(categories);
        Assert.Equal(1, categories.ElementAt(0).CategoryId);
        Assert.False(string.IsNullOrEmpty(categories.ElementAt(0).CategoryName));
    }

    [Fact]
    public void QueryPartial_AttributeTakesPrecedenceOverResolver()
    {
        // Configure a resolver that would give wrong names
        JauntyConfig.ColumnNameResolver = name => "wrong_" + name.ToLower();

        // ProductWithAttributes has explicit [Column] attributes which should take precedence
        var products = _db.Connection.QueryPartial<ProductWithAttributes>(
            "SELECT product_id, product_name, unit_price FROM products WHERE product_id = @Id",
            new { Id = 1 });

        Assert.Single(products);
        Assert.True(products[0].ProductId > 0);
    }

    private static string ToSnakeCase(string source)
    {
        int maxLen = source.Length * 2;
        var buffer = new char[maxLen];
        int dst = 0;
        bool prevIsUpper = false;

        for (int i = 0; i < source.Length; i++)
        {
            char ch = source[i];
            bool isUpper = char.IsUpper(ch);

            if (isUpper && i > 0 && (!prevIsUpper || (i + 1 < source.Length && char.IsLower(source[i + 1]))))
                buffer[dst++] = '_';

            buffer[dst++] = char.ToLowerInvariant(ch);
            prevIsUpper = isUpper;
        }

        return new string(buffer, 0, dst);
    }
}

// Entity with PascalCase properties matching snake_case columns via resolver
public class CategorySnakeCase
{
    public long CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
