using System.Data;
using System.Dynamic;

using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Internals.Parameters;
using Extrode.Jaunty.Tests.Helpers;
using Extrode.Jaunty.TypeHandlers;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R38-058: a registered handler for a collection type was ignored by IN-clause expansion, so
/// <c>SET Tags = @Tags</c> became <c>SET Tags = (@Tags0, @Tags1)</c> and the handler never ran.
/// </summary>
[Collection("Type Handler Operations")]
public class ParameterBinderCollectionHandlerTests : IDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.Reset();
        TestInitializer.Initialize();
    }

    private sealed class JoinedTagsHandler : TypeHandler<List<string>>
    {
        public override List<string>? Parse(object? dbValue) =>
            dbValue is string s ? [.. s.Split(',')] : null;

        public override object? ToDbValue(List<string>? value) => value is null ? null : string.Join(",", value);
    }

    private sealed class TagsHolder
    {
        public List<string>? Tags { get; set; }

        public int Id { get; set; }
    }

    private static SqliteCommand Command(string sql)
    {
        var command = new SqliteCommand(sql, new SqliteConnection("Data Source=:memory:"));
        return command;
    }

    [Fact]
    public void Bind_HandledCollection_BindsOneScalarThroughTheHandler()
    {
        JauntyConfig.RegisterTypeHandler(new JoinedTagsHandler());
        using SqliteCommand command = Command("UPDATE handled_t SET Tags = @Tags WHERE Id = @Id");

        ParameterBinder.Bind(command, new TagsHolder { Tags = ["a", "b"], Id = 1 });

        Assert.Equal("UPDATE handled_t SET Tags = @Tags WHERE Id = @Id", command.CommandText);
        Assert.Equal("a,b", command.Parameters["Tags"].Value);
    }

    [Fact]
    public void Bind_NullHandledCollection_BindsDbNullInsteadOfAnEmptySet()
    {
        JauntyConfig.RegisterTypeHandler(new JoinedTagsHandler());
        using SqliteCommand command = Command("UPDATE handled_null SET Tags = @Tags WHERE Id = @Id");

        ParameterBinder.Bind(command, new TagsHolder { Tags = null, Id = 1 });

        Assert.Equal("UPDATE handled_null SET Tags = @Tags WHERE Id = @Id", command.CommandText);
        Assert.Equal(DBNull.Value, command.Parameters["Tags"].Value);
    }

    [Fact]
    public void Bind_HandledCollectionInADictionary_BindsOneScalarThroughTheHandler()
    {
        JauntyConfig.RegisterTypeHandler(new JoinedTagsHandler());
        using SqliteCommand command = Command("UPDATE handled_dict SET Tags = @Tags WHERE Id = @Id");

        ParameterBinder.Bind(command, new Dictionary<string, object?> { ["Tags"] = new List<string> { "a", "b" }, ["Id"] = 1 });

        Assert.Equal("UPDATE handled_dict SET Tags = @Tags WHERE Id = @Id", command.CommandText);
        Assert.Equal("a,b", command.Parameters["Tags"].Value);
    }

    [Fact]
    public void Bind_UnhandledCollection_StillExpands()
    {
        JauntyConfig.RegisterTypeHandler(new JoinedTagsHandler());
        using SqliteCommand command = Command("SELECT 1 FROM unhandled WHERE Id IN @Ids");

        ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 } });

        Assert.Equal("SELECT 1 FROM unhandled WHERE Id IN (@Ids0, @Ids1)", command.CommandText);
    }

    [Fact]
    public void Execute_HandledCollection_RoundTripsThroughSqlite()
    {
        JauntyConfig.RegisterTypeHandler(new JoinedTagsHandler());
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        connection.Execute("CREATE TABLE tagged (Id INTEGER, Tags TEXT)");
        connection.Execute("INSERT INTO tagged (Id, Tags) VALUES (1, NULL)");

        connection.Execute("UPDATE tagged SET Tags = @Tags WHERE Id = @Id", new TagsHolder { Tags = ["x", "y"], Id = 1 });

        Assert.Equal("x,y", connection.ExecuteScalar<string>("SELECT Tags FROM tagged WHERE Id = 1"));
    }
}

/// <summary>
/// AUD-R38-059: dictionary parameters never got IN-clause expansion, while the documented-equivalent
/// object form did.
/// </summary>
public class ParameterBinderDictionaryExpansionTests
{
    private static List<string> ParameterNames(IDbCommand command)
    {
        var names = new List<string>();
        foreach (IDbDataParameter parameter in command.Parameters)
            names.Add(parameter.ParameterName.TrimStart('@'));
        return names;
    }

    [Fact]
    public void Bind_DictionaryCollection_Expands()
    {
        using var command = new SqliteCommand("SELECT 1 FROM d1 WHERE Name = @Name AND Id IN @Ids AND k = @K");

        ParameterBinder.Bind(command, new Dictionary<string, object?> { ["Name"] = "n", ["Ids"] = new[] { 4, 5 }, ["K"] = 9 });

        Assert.Equal("SELECT 1 FROM d1 WHERE Name = @Name AND Id IN (@Ids0, @Ids1) AND k = @K", command.CommandText);
        Assert.Equal(["Name", "Ids0", "Ids1", "K"], ParameterNames(command));
        Assert.Equal("n", command.Parameters["Name"].Value);
        Assert.Equal(4, command.Parameters["Ids0"].Value);
        Assert.Equal(5, command.Parameters["Ids1"].Value);
        Assert.Equal(9, command.Parameters["K"].Value);
    }

    [Fact]
    public void Bind_DictionaryCollection_MatchesKeysCaseInsensitively()
    {
        using var command = new SqliteCommand("SELECT 1 FROM d2 WHERE a = @First AND Id IN @Ids AND b = @Last");

        ParameterBinder.Bind(command, new Dictionary<string, object?> { ["first"] = 1, ["ids"] = new List<int> { 7 }, ["last"] = 2 });

        Assert.Equal("SELECT 1 FROM d2 WHERE a = @First AND Id IN (@Ids0) AND b = @Last", command.CommandText);
        Assert.Equal(1, command.Parameters["First"].Value);
        Assert.Equal(7, command.Parameters["Ids0"].Value);
        Assert.Equal(2, command.Parameters["Last"].Value);
    }

    [Fact]
    public void Bind_DictionaryCollection_RepeatedPlaceholdersBindOnce()
    {
        using var command = new SqliteCommand("SELECT 1 FROM d7 WHERE a = @K AND Id IN @Ids OR b = @K OR Id IN @Ids");

        ParameterBinder.Bind(command, new Dictionary<string, object?> { ["K"] = 1, ["Ids"] = new[] { 2, 3 } });

        Assert.Equal("SELECT 1 FROM d7 WHERE a = @K AND Id IN (@Ids0, @Ids1) OR b = @K OR Id IN (@Ids0, @Ids1)", command.CommandText);
        Assert.Equal(["K", "Ids0", "Ids1"], ParameterNames(command));
    }

    [Fact]
    public void Bind_DictionaryEnumCollection_BindsTheUnderlyingValues()
    {
        using var command = new SqliteCommand("SELECT 1 FROM d8 WHERE Day IN @Days");

        ParameterBinder.Bind(command, new Dictionary<string, object?> { ["Days"] = new[] { DayOfWeek.Monday, DayOfWeek.Friday } });

        Assert.Equal(1, command.Parameters["Days0"].Value);
        Assert.Equal(5, command.Parameters["Days1"].Value);
    }

    [Fact]
    public void Bind_DictionaryEmptyCollection_UsesTheEmptySetRewrite()
    {
        using var command = new SqliteCommand("SELECT 1 FROM d3 WHERE Id IN @Ids");

        ParameterBinder.Bind(command, new Dictionary<string, object?> { ["Ids"] = Array.Empty<int>() });

        Assert.Equal("SELECT 1 FROM d3 WHERE Id IN (SELECT NULL WHERE 1 = 0)", command.CommandText);
        Assert.Equal(0, command.Parameters.Count);
    }

    [Fact]
    public void Bind_DictionaryCollection_MintsNamesClearOfExistingKeys()
    {
        using var command = new SqliteCommand("SELECT 1 FROM d4 WHERE Id IN @Ids AND b = @Ids0");

        ParameterBinder.Bind(command, new Dictionary<string, object?> { ["Ids"] = new[] { 1, 2 }, ["Ids0"] = 5 });

        Assert.Equal("SELECT 1 FROM d4 WHERE Id IN (@Ids_0, @Ids_1) AND b = @Ids0", command.CommandText);
        Assert.Equal(5, command.Parameters["Ids0"].Value);
    }

    [Fact]
    public void Bind_DictionaryStringAndBlob_AreNotExpanded()
    {
        using var command = new SqliteCommand("SELECT 1 FROM d5 WHERE s = @S AND b = @B");

        ParameterBinder.Bind(command, new Dictionary<string, object?> { ["S"] = "abc", ["B"] = new byte[] { 1 } });

        Assert.Equal("SELECT 1 FROM d5 WHERE s = @S AND b = @B", command.CommandText);
    }

    [Fact]
    public void Bind_DictionaryCollection_StillRejectsUnusedKeys()
    {
        using var command = new SqliteCommand("SELECT 1 FROM d6 WHERE Id IN @Ids");

        var ex = Assert.Throws<ArgumentException>(() =>
            ParameterBinder.Bind(command, new Dictionary<string, object?> { ["Ids"] = new[] { 1 }, ["Extra"] = 1 }));

        Assert.Contains("Extra", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_ExpandoCollection_FiltersOnSqlite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        dynamic parameters = new ExpandoObject();
        parameters.Ids = new[] { 1L, 3L };

        long sum = connection.ExecuteScalar<long>(
            "SELECT SUM(v) FROM (SELECT 1 AS v UNION ALL SELECT 2 UNION ALL SELECT 3) WHERE v IN @Ids", (object)parameters);

        Assert.Equal(4L, sum);
    }
}
