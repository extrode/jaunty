using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read.Configuration;

[Collection("Configuration Operations")]
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
        using var connection = _fixture.GetConnection(dialect);

        // Custom resolver maps a property with no [Column] attribute to a SQL alias the
        // default (property-name) resolution would never produce.
        JauntyConfig.ColumnNameResolver = propertyName => $"resolved_{propertyName.ToLower()}";

        // If the resolver isn't actually consulted by the mapping pipeline, "Value" would
        // never bind to the "resolved_value" column and this would come back as the
        // property's default (0), not 42.
        var result = connection.QueryFirst<ColumnResolverProbe>("SELECT 42 AS resolved_value");

        Assert.Equal(42, result.Value);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void JauntyConfig_CustomTableNameResolver_AffectsMapping(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        // TableResolverProbe has no [Table] attribute, so without the resolver its default
        // table name ("TableResolverProbe") doesn't exist and Get<T> would throw. Redirecting
        // it to the real "categories" table proves the resolver is what determined the SQL.
        JauntyConfig.TableNameResolver = type => type == typeof(TableResolverProbe) ? "categories" : type.Name;

        var category = connection.Get<TableResolverProbe>(1);

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
    }

    private sealed class ColumnResolverProbe
    {
        public int Value { get; set; }
    }

    private sealed class TableResolverProbe
    {
        [Key]
        [Column("category_id")]
        public int CategoryId { get; set; }
    }
}