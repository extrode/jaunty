using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Internal;

namespace Jaunty.Tests.Unit;

public class NameResolverTests : IDisposable
{
    public NameResolverTests()
    {
        // Reset before each test
        JauntyConfig.Reset();
        NameResolver.ClearCache();
    }

    public void Dispose()
    {
        JauntyConfig.Reset();
        NameResolver.ClearCache();
    }

    #region Table Name Resolution

    [Fact]
    public void GetTableName_NoAttribute_ReturnsTypeName()
    {
        var name = NameResolver.GetTableName(typeof(SimpleEntity));

        Assert.Equal("SimpleEntity", name);
    }

    [Fact]
    public void GetTableName_WithAttribute_ReturnsAttributeName()
    {
        var name = NameResolver.GetTableName(typeof(EntityWithTableAttribute));

        Assert.Equal("custom_table", name);
    }

    [Fact]
    public void GetTableName_WithResolver_ReturnsResolvedName()
    {
        JauntyConfig.TableNameResolver = t => t.Name.ToLower() + "s";
        NameResolver.ClearCache();

        var name = NameResolver.GetTableName(typeof(SimpleEntity));

        Assert.Equal("simpleentitys", name);
    }

    [Fact]
    public void GetTableName_AttributeTakesPrecedenceOverResolver()
    {
        JauntyConfig.TableNameResolver = t => "wrong_name";
        NameResolver.ClearCache();

        var name = NameResolver.GetTableName(typeof(EntityWithTableAttribute));

        Assert.Equal("custom_table", name);
    }

    [Fact]
    public void GetTableName_ResolverReturnsNull_FallsBackToTypeName()
    {
        JauntyConfig.TableNameResolver = t => null!;
        NameResolver.ClearCache();

        var name = NameResolver.GetTableName(typeof(SimpleEntity));

        Assert.Equal("SimpleEntity", name);
    }

    [Fact]
    public void GetTableName_ResolverReturnsEmpty_FallsBackToTypeName()
    {
        JauntyConfig.TableNameResolver = t => string.Empty;
        NameResolver.ClearCache();

        var name = NameResolver.GetTableName(typeof(SimpleEntity));

        Assert.Equal("SimpleEntity", name);
    }

    [Fact]
    public void GetTableName_CachesResult()
    {
        var callCount = 0;
        JauntyConfig.TableNameResolver = t =>
        {
            callCount++;
            return "resolved";
        };
        NameResolver.ClearCache();

        NameResolver.GetTableName(typeof(SimpleEntity));
        NameResolver.GetTableName(typeof(SimpleEntity));
        NameResolver.GetTableName(typeof(SimpleEntity));

        Assert.Equal(1, callCount);
    }

    #endregion

    #region Column Name Resolution

    [Fact]
    public void GetColumnName_NoAttribute_ReturnsPropertyName()
    {
        var prop = typeof(SimpleEntity).GetProperty("Id")!;

        var name = NameResolver.GetColumnName(prop);

        Assert.Equal("Id", name);
    }

    [Fact]
    public void GetColumnName_WithAttribute_ReturnsAttributeName()
    {
        var prop = typeof(EntityWithColumnAttribute).GetProperty("Name")!;

        var name = NameResolver.GetColumnName(prop);

        Assert.Equal("custom_name", name);
    }

    [Fact]
    public void GetColumnName_WithResolver_ReturnsResolvedName()
    {
        JauntyConfig.ColumnNameResolver = p => p.ToLower();
        NameResolver.ClearCache();

        var prop = typeof(SimpleEntity).GetProperty("Id")!;
        var name = NameResolver.GetColumnName(prop);

        Assert.Equal("id", name);
    }

    [Fact]
    public void GetColumnName_AttributeTakesPrecedenceOverResolver()
    {
        JauntyConfig.ColumnNameResolver = p => "wrong_column";
        NameResolver.ClearCache();

        var prop = typeof(EntityWithColumnAttribute).GetProperty("Name")!;
        var name = NameResolver.GetColumnName(prop);

        Assert.Equal("custom_name", name);
    }

    #endregion

    #region Ignore Attribute

    [Fact]
    public void IsIgnored_NoAttribute_ReturnsFalse()
    {
        var prop = typeof(SimpleEntity).GetProperty("Id")!;

        var ignored = NameResolver.IsIgnored(prop);

        Assert.False(ignored);
    }

    [Fact]
    public void IsIgnored_WithAttribute_ReturnsTrue()
    {
        var prop = typeof(EntityWithIgnore).GetProperty("Ignored")!;

        var ignored = NameResolver.IsIgnored(prop);

        Assert.True(ignored);
    }

    #endregion

    #region Cache Management

    [Fact]
    public void ClearCache_ResetsTableNameCache()
    {
        var callCount = 0;
        JauntyConfig.TableNameResolver = t =>
        {
            callCount++;
            return "resolved";
        };
        NameResolver.ClearCache();

        NameResolver.GetTableName(typeof(SimpleEntity));
        NameResolver.ClearCache();
        NameResolver.GetTableName(typeof(SimpleEntity));

        Assert.Equal(2, callCount);
    }

    #endregion
}

// Test entities
public class SimpleEntity
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
}

[Table("custom_table")]
public class EntityWithTableAttribute
{
    public int Id { get; set; }
}

public class EntityWithColumnAttribute
{
    public int Id { get; set; }

    [Column("custom_name")]
    public string Name { get; set; } = string.Empty;
}

public class EntityWithIgnore
{
    public int Id { get; set; }

    [Ignore]
    public string Ignored { get; set; } = string.Empty;
}
