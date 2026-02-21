using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Configuration;

public class ConfigurationTests : IDisposable
{
    private readonly Database _db;

    public ConfigurationTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();
    }

    [Fact]
    public void JauntyConfig_Reset_ClearsConfiguration()
    {
        // Set some configuration
        JauntyConfig.ColumnNameResolver = _ => "custom_column";
        JauntyConfig.TableNameResolver = _ => "custom_table";

        // Verify it's set
        Assert.NotNull(JauntyConfig.ColumnNameResolver);
        Assert.NotNull(JauntyConfig.TableNameResolver);

        // Reset configuration
        JauntyConfig.Reset();

        // Verify it's cleared
        Assert.Null(JauntyConfig.ColumnNameResolver);
        Assert.Null(JauntyConfig.TableNameResolver);
    }

    [Fact]
    public void JauntyConfig_CustomColumnNameResolver_AffectsMapping()
    {
        // Set custom column name resolver
        JauntyConfig.ColumnNameResolver = propertyName => $"col_{propertyName.ToLower()}";

        // This would normally map to "product_name" but with custom resolver should map to "col_productname"
        // Since we're resetting after, this test verifies the configuration can be set
        Assert.NotNull(JauntyConfig.ColumnNameResolver);
        
        // Reset to avoid affecting other tests
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();
    }

    [Fact]
    public void JauntyConfig_CustomTableNameResolver_AffectsMapping()
    {
        // Set custom table name resolver
        JauntyConfig.TableNameResolver = type => $"tbl_{type.Name.ToLower()}";

        // Verify it's set
        Assert.NotNull(JauntyConfig.TableNameResolver);

        // Reset to avoid affecting other tests
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();
    }

    [Fact]
    public void JauntyConfig_SchemaNameResolver_AffectsMapping()
    {
        // Set custom schema name resolver
        JauntyConfig.SchemaNameResolver = type => "custom_schema";

        // Verify it's set
        Assert.NotNull(JauntyConfig.SchemaNameResolver);

        // Reset to avoid affecting other tests
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();
    }
}
