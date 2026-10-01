using System.Data;
using System.Reflection;

using Extrode.Jaunty.Internals.Parameters;

using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

using MySql.Data.MySqlClient;

namespace Extrode.Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R38-056: a second <c>$</c> inside an identifier (<c>sales$q1$2024</c>) was read as an
/// unterminated dollar-quote tag, swallowing every parameter after it.
/// AUD-R38-057: SQL Server's <c>$action</c>, <c>$IDENTITY</c> and <c>$ROWGUID</c> pseudo-columns
/// became phantom parameters, and MySQL's <c>$</c> identifiers likewise.
/// </summary>
public class ParameterBinderDollarSignTests
{
    private static List<string> ParameterNames(IDbCommand command)
    {
        var names = new List<string>();
        foreach (IDbDataParameter parameter in command.Parameters)
            names.Add(parameter.ParameterName.TrimStart('@', '$', ':'));
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private static string[] Classic(string sql, bool dollarSigil)
    {
        MethodInfo method = typeof(SqlParameterParser).GetMethod(
            "ExtractParameterNamesClassic", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string[])method.Invoke(null, new object[] { sql, false, dollarSigil, true })!;
    }

    [Theory]
    [InlineData("SELECT sales$q1$2024 FROM t WHERE id = @Id")]
    [InlineData("SELECT a$$b FROM t WHERE id = @Id")]
    [InlineData("SELECT x.sales$q1$ FROM t x WHERE id = @Id")]
    public void ExtractParameterNames_DollarsInsideAnIdentifier_DoNotOpenADollarQuote(string sql)
    {
        Assert.Equal(new[] { "Id" }, SqlParameterParser.ExtractParameterNames(sql));
        Assert.Equal(new[] { "Id" }, Classic(sql, dollarSigil: true));
    }

    [Fact]
    public void ExtractParameterNames_DollarQuoteAtATokenStart_IsStillSkipped()
    {
        Assert.Equal(new[] { "Id" }, SqlParameterParser.ExtractParameterNames("SELECT $q$ @Fake $q$ FROM t WHERE id = @Id"));
    }

    [Fact]
    public void ExtractParameterNames_DollarSigil_StillFindsDollarParameters()
    {
        Assert.Equal(new[] { "name" }, SqlParameterParser.ExtractParameterNames("SELECT * FROM t WHERE n = $name"));
    }

    [Theory]
    [InlineData("MERGE t USING s ON t.id = s.id WHEN MATCHED THEN DELETE OUTPUT $action WHERE x = @Id")]
    [InlineData("SELECT $IDENTITY, $ROWGUID FROM t WHERE id = @Id")]
    [InlineData("SELECT $5.00 + price FROM t WHERE id = @Id")]
    public void ExtractParameterNames_DollarSigilOff_IgnoresDollarRuns(string sql)
    {
        Assert.Equal(new[] { "Id" }, SqlParameterParser.ExtractParameterNames(sql, dollarSigil: false));
        Assert.Equal(new[] { "Id" }, Classic(sql, dollarSigil: false));
    }

    [Fact]
    public void ExtractParameterNames_DollarSigilOff_DoesNotOpenDollarQuotes()
    {
        Assert.Equal(new[] { "Fake", "Id" }, SqlParameterParser.ExtractParameterNames("SELECT $q$ @Fake $q$ FROM t WHERE id = @Id", dollarSigil: false));
        Assert.Equal(new[] { "Fake", "Id" }, Classic("SELECT $q$ @Fake $q$ FROM t WHERE id = @Id", dollarSigil: false));
    }

    [Fact]
    public void DetectParameterPrefix_DollarsInsideAnIdentifier_AreNotTheSigil()
    {
        Assert.Equal("@", ParameterBinder.DetectParameterPrefix("SELECT sales$q1$2024 FROM t WHERE id IN @Ids"));
    }

    [Fact]
    public void DetectParameterPrefix_ADollarTagPairAcrossIdentifiers_DoesNotHideTheFirstSigil()
    {
        Assert.Equal("@", ParameterBinder.DetectParameterPrefix("SELECT a$q$b FROM t WHERE x = @A AND c$q$d = $B"));
    }

    [Fact]
    public void DetectParameterPrefix_DollarSigilOff_SkipsPseudoColumns()
    {
        Assert.Equal("@", ParameterBinder.DetectParameterPrefix("SELECT $IDENTITY FROM t WHERE id IN @Ids", dollarSigil: false));
    }

    [Fact]
    public void Bind_SqlServerOutputAction_BindsOnlyTheRealParameter()
    {
        using var connection = new SqlConnection();
        using SqlCommand command = connection.CreateCommand();
        command.CommandText = "MERGE t USING s ON t.id = s.id WHEN MATCHED THEN DELETE OUTPUT $action, deleted.id WHERE s.k = @Id;";

        ParameterBinder.Bind(command, new { Id = 1 });

        Assert.Equal(new List<string> { "Id" }, ParameterNames(command));
    }

    [Fact]
    public void Bind_SqlServerPseudoColumn_StillExpandsACollection()
    {
        using var connection = new SqlConnection();
        using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT $IDENTITY FROM t WHERE id IN @Ids";

        ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 } });

        Assert.Equal("SELECT $IDENTITY FROM t WHERE id IN (@Ids0, @Ids1)", command.CommandText);
        Assert.Equal(2, command.Parameters.Count);
    }

    [Fact]
    public void Bind_SqlServerDollarTagPair_IsNotADollarQuote()
    {
        using var connection = new SqlConnection();
        using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT $q$ FROM t WHERE id IN @Ids AND n = $q$";

        ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 } });

        Assert.Equal("SELECT $q$ FROM t WHERE id IN (@Ids0, @Ids1) AND n = $q$", command.CommandText);
    }

    [Fact]
    public void Bind_MySqlDollarTagPair_IsNotADollarQuote()
    {
        using var connection = new MySqlConnection();
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT $q$ FROM t WHERE id IN @Ids AND n = $q$";

        ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 } });

        Assert.Equal("SELECT $q$ FROM t WHERE id IN (@Ids0, @Ids1) AND n = $q$", command.CommandText);
    }

    [Fact]
    public void Bind_CommandWithoutAConnection_KeepsDollarParameters()
    {
        using var command = new SqliteCommand("SELECT * FROM detached WHERE n = $name");

        ParameterBinder.Bind(command, new { name = "a" });

        Assert.Equal(new List<string> { "name" }, ParameterNames(command));
    }

    [Fact]
    public void Bind_MySqlDollarIdentifier_StillFindsTheParameterAfterIt()
    {
        using var connection = new MySqlConnection();
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT $total FROM t WHERE id = @Id";

        ParameterBinder.Bind(command, new { Id = 2 });

        Assert.Equal(new List<string> { "Id" }, ParameterNames(command));
    }

    [Fact]
    public void Bind_SqliteDollarsInsideAnIdentifier_StillExpandsACollectionAfterThem()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT sales$q1$2024 FROM t WHERE n = @Name AND id IN @Ids";

        ParameterBinder.Bind(command, new { Name = "a", Ids = new[] { 1, 2 } });

        Assert.Equal("SELECT sales$q1$2024 FROM t WHERE n = @Name AND id IN (@Ids0, @Ids1)", command.CommandText);
        Assert.Equal(new List<string> { "Ids0", "Ids1", "Name" }, ParameterNames(command));
    }

    [Fact]
    public void Bind_SqliteDollarParameter_StillBinds()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM t WHERE n = $name";

        ParameterBinder.Bind(command, new { name = "a" });

        Assert.Equal(new List<string> { "name" }, ParameterNames(command));
    }

    [Fact]
    public void Cache_SameSql_ParsesDifferentlyPerDollarFlag()
    {
        const string sql = "SELECT $cachesplit FROM t WHERE id = @Id";

        Assert.Equal(new[] { "cachesplit", "Id" }, SqlParameterParserCache.GetOrAdd(sql));
        Assert.Equal(new[] { "Id" }, SqlParameterParserCache.GetOrAdd(sql, dollarSigil: false));
        Assert.Equal(new[] { "cachesplit", "Id" }, SqlParameterParserCache.GetOrAdd(sql));
    }
}
