using Extrode.Jaunty.Scaffolding.Providers.MySql;
using Extrode.Jaunty.Scaffolding.Providers.SQLite;
using Extrode.Jaunty.Scaffolding.Providers.SqlServer;
using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// Each schema reader falls back to a second provider when its primary one is not loadable. The
/// primary always loads in this project, so the fallback's type name is pinned here directly: a
/// typo in it would otherwise ship unnoticed and only surface for a user with just the fallback.
/// </summary>
public class FallbackProviderTypeTests
{
    [Fact]
    public void SQLite_FallsBackToSystemDataSQLite()
    {
        Assert.Equal(typeof(System.Data.SQLite.SQLiteConnection), SQLiteSchemaReader.FallbackProviderType());
    }

    [Fact]
    public void SqlServer_FallsBackToSystemDataSqlClient()
    {
#pragma warning disable CS0618 // System.Data.SqlClient is obsolete; it is the fallback under test
        Assert.Equal(typeof(System.Data.SqlClient.SqlConnection), SqlServerSchemaReader.FallbackProviderType());
#pragma warning restore CS0618
    }

    [Fact]
    public void MySql_FallsBackToMySqlData()
    {
        Assert.Equal(typeof(MySql.Data.MySqlClient.MySqlConnection), MySqlSchemaReader.FallbackProviderType());
    }
}
