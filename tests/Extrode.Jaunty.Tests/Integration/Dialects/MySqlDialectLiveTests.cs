using Extrode.Jaunty.Dialects;
using Extrode.Jaunty.Tests.Helpers.Dialects;

namespace Extrode.Jaunty.Tests.Integration.Dialects;

/// <summary>
/// AUD-R38-051/052 against a live server: LENGTH counted bytes rather than characters, and the
/// keyword set missed words MySQL and MariaDB reserve, so those identifiers went out bare.
/// </summary>
public class MySqlDialectLiveTests : IClassFixture<DialectFixture>
{
    private static readonly MySqlDialect Dialect = new();

    private readonly DialectFixture _fixture;

    public MySqlDialectLiveTests(DialectFixture fixture) => _fixture = fixture;

    private static object? Scalar(IDbConnection connection, string sql, string? parameter = null)
    {
        using IDbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        if (parameter is not null)
        {
            IDbDataParameter p = command.CreateParameter();
            p.ParameterName = "@v";
            p.Value = parameter;
            command.Parameters.Add(p);
        }

        return command.ExecuteScalar();
    }

    [Theory]
    [MariaDB]
    public void GenerateLength_CountsCharactersNotBytes(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        object? length = Scalar(connection, $"SELECT {Dialect.GenerateLength("@v")}", "café");

        Assert.Equal("café".Length, Convert.ToInt32(length, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [MariaDB]
    public void EveryAddedKeyword_IsEscapedIntoAUsableIdentifier(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        foreach (string word in MySqlDialectAddedKeywords.All)
            Assert.Equal(1L, Convert.ToInt64(Scalar(connection, $"SELECT {Dialect.EscapeColumnName(word)} FROM (SELECT 1 AS {Dialect.EscapeColumnName(word)}) t"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [MariaDB]
    public void ALeadingDigitColumn_IsReadAsTheColumnNotALiteral(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        foreach (string name in new[] { "1e3", "0x1F", "0b101" })
            Assert.Equal(7L, Convert.ToInt64(Scalar(connection, $"SELECT {Dialect.EscapeColumnName(name)} FROM (SELECT 7 AS `{name}`) t"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [MariaDB]
    public void AFractionalAverage_KeepsFullPrecision(DialectInfo dialect)
    {
        using IDbConnection connection = _fixture.GetConnection(dialect);

        object? average = Scalar(connection, $"SELECT {FractionalAverage.Generate(Dialect, "v")} FROM (SELECT 1 AS v UNION ALL SELECT 1 UNION ALL SELECT 2) t");

        Assert.Equal(4.0 / 3.0, Convert.ToDouble(average, System.Globalization.CultureInfo.InvariantCulture), 12);
    }
}

internal static class MySqlDialectAddedKeywords
{
    public static readonly string[] All =
    [
        "CUBE", "FUNCTION", "GENERATED", "GET", "INTERSECT", "IO_AFTER_GTIDS", "IO_BEFORE_GTIDS",
        "OPTIMIZER_COSTS", "PARTITION", "STORED", "VIRTUAL",
        "CURRENT_ROLE", "DELETE_DOMAIN_ID", "DO_DOMAIN_IDS", "IGNORE_DOMAIN_IDS",
        "MASTER_DEMOTE_TO_REPLICA", "MASTER_DEMOTE_TO_SLAVE", "OFFSET", "PAGE_CHECKSUM",
        "PARSE_VCOL_EXPR", "PORTION", "RETURNING", "REF_SYSTEM_ID", "SQL_AFTER_GTIDS",
        "SQL_BEFORE_GTIDS", "STATS_AUTO_RECALC", "STATS_PERSISTENT", "STATS_SAMPLE_PAGES", "VECTOR",
    ];
}
