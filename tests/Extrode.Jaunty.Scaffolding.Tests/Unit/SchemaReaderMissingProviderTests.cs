using Extrode.Jaunty.Scaffolding.Providers.MySql;
using Extrode.Jaunty.Scaffolding.Providers.PostgreSql;
using Extrode.Jaunty.Scaffolding.Providers.SQLite;
using Extrode.Jaunty.Scaffolding.Providers.SqlServer;
using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Unit;

public class SchemaReaderMissingProviderTests
{
    [Fact]
    public void MySql_WithNoProviderType_ThrowsAnInstallHint()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => MySqlSchemaReader.FromProviderType(null, "Server=x"));

        Assert.Equal("Could not find MySQL provider. Please install MySqlConnector or MySql.Data.", exception.Message);
    }

    [Fact]
    public void PostgreSql_WithNoProviderType_ThrowsAnInstallHint()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => PostgreSqlSchemaReader.FromProviderType(null, "Host=x"));

        Assert.Equal("Could not find PostgreSQL provider. Please install Npgsql.", exception.Message);
    }

    [Fact]
    public void SQLite_WithNoProviderType_ThrowsAnInstallHint()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => SQLiteSchemaReader.FromProviderType(null, "Data Source=x"));

        Assert.Equal("Could not find SQLite provider. Please install Microsoft.Data.Sqlite or System.Data.SQLite.", exception.Message);
    }

    [Fact]
    public void SqlServer_WithNoProviderType_ThrowsAnInstallHint()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => SqlServerSchemaReader.FromProviderType(null, "Server=x"));

        Assert.Equal("Could not find SQL Server provider. Please install Microsoft.Data.SqlClient or System.Data.SqlClient.", exception.Message);
    }
}
