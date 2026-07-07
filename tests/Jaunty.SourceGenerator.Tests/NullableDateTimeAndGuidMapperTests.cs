using Jaunty;
using Jaunty.SourceGenerator.Tests.Entities;

using Microsoft.Data.Sqlite;

namespace Jaunty.SourceGenerator.Tests;

public sealed class NullableDateTimeAndGuidMapperTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public NullableDateTimeAndGuidMapperTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE gen_events (
                event_id INTEGER PRIMARY KEY,
                occurred_at TEXT NULL,
                trace_id TEXT NULL
            );
            INSERT INTO gen_events VALUES (1, '2026-01-15 10:00:00', '5b2f6a3e-8c1d-4a7f-9e2b-1234567890ab');
            INSERT INTO gen_events VALUES (2, NULL, NULL);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Query_NullableDateTimeAndGuidColumns_MapNonNullValuesCorrectly()
    {
        List<GenEvent> rows = _connection.Query<GenEvent>(
            "SELECT event_id, occurred_at, trace_id FROM gen_events WHERE event_id = 1");

        GenEvent row = Assert.Single(rows);
        Assert.Equal(new DateTime(2026, 1, 15, 10, 0, 0), row.OccurredAt);
        Assert.Equal(Guid.Parse("5b2f6a3e-8c1d-4a7f-9e2b-1234567890ab"), row.TraceId);
    }

    [Fact]
    public void Query_NullableDateTimeAndGuidColumns_MapNullValuesCorrectly()
    {
        List<GenEvent> rows = _connection.Query<GenEvent>(
            "SELECT event_id, occurred_at, trace_id FROM gen_events WHERE event_id = 2");

        GenEvent row = Assert.Single(rows);
        Assert.Null(row.OccurredAt);
        Assert.Null(row.TraceId);
    }
}
