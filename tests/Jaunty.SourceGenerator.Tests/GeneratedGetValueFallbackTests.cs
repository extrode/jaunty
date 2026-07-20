using Jaunty;
using Jaunty.SourceGenerator.Tests.Entities;

using Microsoft.Data.Sqlite;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R9-006: property types that fall back to the GetValue-based reader path (enums,
/// TimeSpan, DateTimeOffset) previously generated dbReader.GetFieldValue&lt;object&gt;(...)
/// assigned directly to the typed property, which does not compile (CS0266). This is an
/// end-to-end check, through the actual generator output compiled into this project and a
/// real ADO.NET provider, that such entities compile and round-trip correctly.
/// </summary>
public sealed class GeneratedGetValueFallbackTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GeneratedGetValueFallbackTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE gen_event_logs (
                event_id INTEGER PRIMARY KEY,
                severity INTEGER NOT NULL,
                duration TEXT NOT NULL,
                occurred_at TEXT NOT NULL
            );
            INSERT INTO gen_event_logs VALUES (1, 2, '01:30:00', '2026-07-20T12:00:00+00:00');
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Query_EnumTimeSpanDateTimeOffsetProperties_MapCorrectly()
    {
        List<GenEventLog> rows = _connection.Query<GenEventLog>(
            "SELECT event_id, severity, duration, occurred_at FROM gen_event_logs");

        GenEventLog row = Assert.Single(rows);
        Assert.Equal(1, row.EventId);
        Assert.Equal(GenEventSeverity.Error, row.Severity);
        Assert.Equal(TimeSpan.FromMinutes(90), row.Duration);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero), row.OccurredAt);
    }
}
