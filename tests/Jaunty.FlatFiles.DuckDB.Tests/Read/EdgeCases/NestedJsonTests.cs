namespace Jaunty.FlatFiles.DuckDB.Tests.Read.EdgeCases;

/// <summary>
/// Tests for nested JSON structures and complex data types.
/// </summary>
public class NestedJsonTests : IDisposable
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private readonly DuckDb _db;

    public NestedJsonTests()
    {
        var jsonPath = Path.Combine(DataDir, "json", "nested.json");
        var source = new JsonFileSource("nested_data", jsonPath, typeof(object));

        var options = new FlatFileOptions();
        options.Sources.Add(source);

        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void NestedJson_RawQuery_AccessesNestedFields()
    {
        // DuckDB supports struct field access with dot notation
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT \"Id\", \"Name\", \"Address\".\"City\" AS City FROM \"nested_data\" ORDER BY \"Id\"";
        using var reader = cmd.ExecuteReader();

        var rows = new List<(int Id, string Name, string City)>();
        while (reader.Read())
        {
            rows.Add((
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("Name")),
                reader.GetString(reader.GetOrdinal("City"))
            ));
        }

        Assert.Equal(3, rows.Count);
        Assert.Equal("Springfield", rows[0].City);
        Assert.Equal("Shelbyville", rows[1].City);
        Assert.Equal("Capital City", rows[2].City);
    }

    [Fact]
    public void NestedJson_ArrayField_Queryable()
    {
        // DuckDB supports array_length on nested arrays
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT \"Id\", \"Name\", len(\"Tags\") AS TagCount FROM \"nested_data\" ORDER BY \"Id\"";
        using var reader = cmd.ExecuteReader();

        var rows = new List<(int Id, int TagCount)>();
        while (reader.Read())
        {
            rows.Add((
                reader.GetInt32(reader.GetOrdinal("Id")),
                Convert.ToInt32(reader.GetValue(reader.GetOrdinal("TagCount")))
            ));
        }

        Assert.Equal(3, rows.Count);
        Assert.Equal(2, rows[0].TagCount); // Alice: ["admin", "user"]
        Assert.Equal(1, rows[1].TagCount); // Bob: ["user"]
        Assert.Equal(0, rows[2].TagCount); // Charlie: []
    }

    [Fact]
    public void NestedJson_WhereOnNestedField()
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT \"Name\" FROM \"nested_data\" WHERE \"Address\".\"Zip\" = '62702'";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal("Bob", reader.GetString(0));
        Assert.False(reader.Read()); // Only one match
    }
}