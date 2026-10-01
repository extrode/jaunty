using System.Data.SQLite;

using Extrode.Jaunty.Attributes;

namespace Extrode.Jaunty.Fluent.Tests.Integration;

/// <summary>
/// AUD-R38-033: Min/Max/Sum converted the scalar with a bare Convert.ChangeType, which cannot reach
/// an enum or a Guid stored as text.
/// </summary>
public sealed class ScalarAggregateConversionTests : IDisposable
{
    public enum Priority { Low = 1, Medium = 2, High = 3 }

    [Table("tickets")]
    public class Ticket
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("priority")]
        public Priority Priority { get; set; }

        [Column("uid")]
        public Guid Uid { get; set; }
    }

    private static readonly Guid Smallest = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly SQLiteConnection _connection = new("Data Source=:memory:");

    public ScalarAggregateConversionTests()
    {
        _connection.Open();
        using SQLiteCommand cmd = _connection.CreateCommand();
        cmd.CommandText =
            "CREATE TABLE tickets (id INTEGER PRIMARY KEY, priority INTEGER NOT NULL, uid TEXT NOT NULL);" +
            "INSERT INTO tickets VALUES " +
            "(1, 1, '33333333-3333-3333-3333-333333333333'), " +
            "(2, 3, '11111111-1111-1111-1111-111111111111'), " +
            "(3, 2, '22222222-2222-2222-2222-222222222222');";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Max_OverAnIntegerBackedEnum_ReturnsTheEnum()
    {
        Assert.Equal(Priority.High, _connection.From<Ticket>().Max(t => t.Priority));
    }

    [Fact]
    public void SelectMin_OverAnIntegerBackedEnum_ReturnsTheEnum()
    {
        Assert.Equal(Priority.Low, _connection.From<Ticket>().SelectMin(t => t.Priority));
    }

    [Fact]
    public void Min_OverATextBackedGuid_ReturnsTheGuid()
    {
        Assert.Equal(Smallest, _connection.From<Ticket>().Min(t => t.Uid));
    }

    [Fact]
    public async Task MaxAsync_OverAnIntegerBackedEnum_ReturnsTheEnum()
    {
        Assert.Equal(Priority.High, await _connection.From<Ticket>().MaxAsync(t => t.Priority));
    }

    [Fact]
    public async Task MinAsync_OverATextBackedGuid_ReturnsTheGuid()
    {
        Assert.Equal(Smallest, await _connection.From<Ticket>().MinAsync(t => t.Uid));
    }

    [Fact]
    public void Max_OverAnEmptySet_ReturnsDefault()
    {
        Assert.Equal(default, _connection.From<Ticket>().Where(t => t.Id > 100).Max(t => t.Priority));
    }

    [Fact]
    public void Sum_OverAnIntegerColumn_StillConverts()
    {
        Assert.Equal(6, _connection.From<Ticket>().Sum(t => t.Id));
    }
}
