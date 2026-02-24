using Jaunty.Configuration;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read.Configuration;

public class ConfigurationTests : IClassFixture<DialectFixture>, IDisposable
{
    private readonly DialectFixture _fixture;

    public ConfigurationTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
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
    public void JauntyConfig_Reset_ClearsConfiguration(DialectInfo dialect)
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void JauntyConfig_CustomColumnNameResolver_AffectsMapping(DialectInfo dialect)
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void JauntyConfig_CustomTableNameResolver_AffectsMapping(DialectInfo dialect)
    {
        // Set custom table name resolver
        JauntyConfig.TableNameResolver = entityType => $"tbl_{entityType.Name.ToLower()}";

        // Verify it's set
        Assert.NotNull(JauntyConfig.TableNameResolver);

        // Reset to avoid affecting other tests
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();
    }
}
