using Extrode.Jaunty.Dialects;
using Extrode.Jaunty.Tests.Helpers.Dialects;

namespace Extrode.Jaunty.Tests.Integration.Dialects;

/// <summary>
/// AUD-R38-118/119 against a live server: SQL Server's LEN dropped trailing spaces, and
/// PostgreSQL's keyword set missed SYSTEM_USER, so such a column went out bare.
/// </summary>
public class SqlServerPostgresDialectLiveTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public SqlServerPostgresDialectLiveTests(DialectFixture fixture) => _fixture = fixture;

    private static object? Scalar(IDbConnection connection, string sql)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    [Theory]
    [SqlServer]
    public void SqlServer_GenerateLength_CountsTrailingSpaces(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);
        var sqlServer = new SqlServerDialect();

        Assert.Equal(5, Convert.ToInt32(Scalar(connection, $"SELECT {sqlServer.GenerateLength("'abc  '")}"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(5, Convert.ToInt32(Scalar(connection, $"SELECT {sqlServer.GenerateLength("N'a b c'")}"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(8000, Convert.ToInt32(Scalar(connection, $"SELECT {sqlServer.GenerateLength("CAST(REPLICATE('a', 8000) AS varchar(8000))")}"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(DBNull.Value, Scalar(connection, $"SELECT {sqlServer.GenerateLength("CAST(NULL AS varchar(10))")}"));
    }

    [Theory]
    [Postgres]
    public void Postgres_ASystemUserColumn_IsReadAsTheColumn(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);
        string column = new PostgreSqlDialect().EscapeColumnName("system_user");

        Assert.Equal(7, Convert.ToInt32(Scalar(connection, $"SELECT {column} FROM (SELECT 7 AS \"system_user\") t"), System.Globalization.CultureInfo.InvariantCulture));
    }
}
