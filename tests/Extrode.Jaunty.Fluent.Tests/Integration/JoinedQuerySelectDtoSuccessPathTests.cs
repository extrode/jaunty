using System.Data;
using System.Data.SQLite;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Fluent;

using Xunit;

namespace Extrode.Jaunty.Fluent.Tests.Integration;

/// <summary>
/// coverage-gaps-2026-09-20: <c>JoinedQueryBuilder&lt;TFrom,TJoin&gt;.SelectWithMapping</c>/
/// <c>SelectWithMapper</c>'s row-loop and success return were flagged as possibly unreachable -
/// every existing custom-DTO join test (<see cref="FluentJoinTests.InnerJoin_SelectCustomType_WithAmbiguousColumn_ThrowsInvalidOperationException"/>)
/// joins two Northwind entities whose FK/PK share a column name (<c>category_id</c>), which always
/// collides under the unaliased <c>SELECT *</c> these methods issue and throws before mapping a
/// row. These two fixture tables join on differently-named columns (<c>id</c> / <c>from_id</c>), so
/// nothing collides and the mapping loop actually runs.
/// </summary>
public class JoinedQuerySelectDtoSuccessPathTests
{
    [Table("jqs_from")]
    public class JqsFrom
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("jqs_join")]
    public class JqsJoin
    {
        [Key]
        [Column("join_id")]
        public int JoinId { get; set; }
        [Column("from_id")]
        public int FromId { get; set; }
        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// <c>SelectWithMapping</c> issues an unaliased <c>SELECT *</c>, so <c>MappingMode.Strict</c>
    /// requires every returned column - including both sides' key columns - to map to a property,
    /// not just the ones the test cares about.
    /// </summary>
    public class JqsDto
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("name")]
        public string Name { get; set; } = string.Empty;
        [Column("join_id")]
        public int JoinId { get; set; }
        [Column("from_id")]
        public int FromId { get; set; }
        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }

    private static SQLiteConnection OpenSeeded()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        using SQLiteCommand cmd = connection.CreateCommand();
        cmd.CommandText =
            "CREATE TABLE jqs_from (id INTEGER PRIMARY KEY, name TEXT);" +
            "CREATE TABLE jqs_join (join_id INTEGER PRIMARY KEY, from_id INTEGER, label TEXT);" +
            "INSERT INTO jqs_from (id, name) VALUES (1, 'alpha');" +
            "INSERT INTO jqs_join (join_id, from_id, label) VALUES (10, 1, 'beta');";
        cmd.ExecuteNonQuery();
        return connection;
    }

    [Fact]
    public void Select_CustomDtoOverANonCollidingJoin_MapsAndReturnsTheRow()
    {
        using SQLiteConnection connection = OpenSeeded();

        List<JqsDto> results = connection.From<JqsFrom>()
            .InnerJoin<JqsJoin>()
            .On(f => f.Id, j => j.FromId)
            .Select<JqsDto>();

        JqsDto dto = Assert.Single(results);
        Assert.Equal("alpha", dto.Name);
        Assert.Equal("beta", dto.Label);
    }

    [Fact]
    public async Task SelectAsync_CustomDtoOverANonCollidingJoin_MapsAndReturnsTheRow()
    {
        using SQLiteConnection connection = OpenSeeded();

        List<JqsDto> results = await connection.From<JqsFrom>()
            .InnerJoin<JqsJoin>()
            .On(f => f.Id, j => j.FromId)
            .SelectAsync<JqsDto>(TestContext.Current.CancellationToken);

        JqsDto dto = Assert.Single(results);
        Assert.Equal("alpha", dto.Name);
        Assert.Equal("beta", dto.Label);
    }

    [Fact]
    public void Select_WithCustomMapperOverAJoin_MapsAndReturnsTheRow()
    {
        using SQLiteConnection connection = OpenSeeded();

        List<JqsDto> results = connection.From<JqsFrom>()
            .InnerJoin<JqsJoin>()
            .On(f => f.Id, j => j.FromId)
            .Select(reader => new JqsDto
            {
                Name = (string)reader["name"],
                Label = (string)reader["label"],
            });

        JqsDto dto = Assert.Single(results);
        Assert.Equal("alpha", dto.Name);
        Assert.Equal("beta", dto.Label);
    }

    [Fact]
    public async Task SelectAsync_WithCustomMapperOverAJoin_MapsAndReturnsTheRow()
    {
        using SQLiteConnection connection = OpenSeeded();

        List<JqsDto> results = await connection.From<JqsFrom>()
            .InnerJoin<JqsJoin>()
            .On(f => f.Id, j => j.FromId)
            .SelectAsync(
                reader => new JqsDto
                {
                    Name = (string)reader["name"],
                    Label = (string)reader["label"],
                },
                TestContext.Current.CancellationToken);

        JqsDto dto = Assert.Single(results);
        Assert.Equal("alpha", dto.Name);
        Assert.Equal("beta", dto.Label);
    }
}
