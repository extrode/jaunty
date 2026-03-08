using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

public class ColumnMappingCacheTests
{
    [Fact]
    public void Get_CachesMappingsPerType()
    {
        // Act
        var mappings1 = ColumnMappingCache.Get(typeof(SalesRecord));
        var mappings2 = ColumnMappingCache.Get(typeof(SalesRecord));

        // Assert
        Assert.Same(mappings1, mappings2);
        Assert.NotEmpty(mappings1);
    }

    [Fact]
    public void Get_DifferentTypes_CachedSeparately()
    {
        // Act
        var salesMappings = ColumnMappingCache.Get(typeof(SalesRecord));
        var customerMappings = ColumnMappingCache.Get(typeof(CustomerProfile));

        // Assert
        Assert.NotSame(salesMappings, customerMappings);
        Assert.NotEmpty(salesMappings);
        Assert.NotEmpty(customerMappings);
    }

    [Fact]
    public void Get_RespectsColumnAttribute()
    {
        // Act
        var mappings = ColumnMappingCache.Get(typeof(SalesRecord));

        // Assert
        Assert.Contains("product_name", mappings.Keys);
        Assert.Contains("region", mappings.Keys);
    }

    [Fact]
    public void Get_IncludesAllPublicProperties()
    {
        // Act
        var mappings = ColumnMappingCache.Get(typeof(SalesRecord));

        // Assert
        Assert.Equal(6, mappings.Count); // Id, ProductName, Revenue, Quantity, Date, Region
    }
}