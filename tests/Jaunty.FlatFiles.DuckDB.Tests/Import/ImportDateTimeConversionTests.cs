using System.Reflection;

using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Internals.Import;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

using Microsoft.Data.Sqlite;

namespace Jaunty.FlatFiles.DuckDB.Tests.Import;

/// <summary>
/// R16 batch-7: ImportExecutor.ConvertValue checked for legacy DuckDBDateOnly/DuckDBTimeOnly
/// wrapper types that the pinned DuckDB.NET 1.3.0 package no longer returns - its reader now
/// returns System.DateOnly/System.TimeOnly directly, so the old checks were dead code and DATE/TIME
/// columns silently fell through to Convert.ChangeType (which throws for DateOnly/TimeOnly since
/// neither implements IConvertible) and got passed through as raw structs. Fixed to match on the
/// actual DateOnly/TimeOnly types. Parquet preserves DATE/TIME as native typed columns (unlike CSV,
/// which relies on text-format sniffing), so importing from Parquet guarantees the DuckDB reader
/// actually returns DateOnly/TimeOnly values for this path to convert.
/// </summary>
public class ImportDateTimeConversionTests : IDisposable
{
    private readonly string DataDir = Path.Combine(Path.GetTempPath(), $"jaunty_import_datetime_tests_{Guid.NewGuid():N}");
    private readonly string _parquetPath;
    private readonly DuckDb _db;

    public ImportDateTimeConversionTests()
    {
        Directory.CreateDirectory(DataDir);
        _parquetPath = Path.Combine(DataDir, "temporal.parquet");

        using var genConnection = new DuckDBConnection("DataSource=:memory:");
        genConnection.Open();
        using var cmd = genConnection.CreateCommand();

        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, DATE '2024-01-15', TIME '14:30:00'),
                    (2, DATE '2023-06-30', TIME '09:05:42')
                ) AS t(""Id"", ""EventDate"", ""EventTime"")
            ) TO '{_parquetPath.Replace("\\", "/").Replace("'", "''")}' (FORMAT PARQUET)";
        cmd.ExecuteNonQuery();

        var options = new FlatFileOptions();
        options.AddParquet<TemporalRecord>(_parquetPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(DataDir, true); } catch { }
    }

    private static SqliteConnection CreateSqliteConnection()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        return conn;
    }

    [Fact]
    public async Task ImportIntoAsync_ParquetDateAndTimeColumns_ConvertedCorrectly()
    {
        using var sqlite = CreateSqliteConnection();

        var count = await _db.ImportIntoAsync<TemporalRecord>(sqlite, new ImportOptions(createTableIfMissing: true));

        Assert.Equal(2, count);

        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = "SELECT \"Id\", \"EventDate\", \"EventTime\" FROM \"temporal_records\" ORDER BY \"Id\"";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal(new DateTime(2024, 1, 15), DateTime.Parse(reader.GetString(1)));
        Assert.Equal(new TimeSpan(14, 30, 0), TimeSpan.Parse(reader.GetString(2)));

        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.Equal(new DateTime(2023, 6, 30), DateTime.Parse(reader.GetString(1)));
        Assert.Equal(new TimeSpan(9, 5, 42), TimeSpan.Parse(reader.GetString(2)));

        Assert.False(reader.Read());
    }

    [Fact]
    public void ConvertValue_DateOnlyToDateTime_ReturnsDateTime()
    {
        var result = InvokeConvertValue(new DateOnly(2024, 1, 15), typeof(DateTime));

        var dateTime = Assert.IsType<DateTime>(result);
        Assert.Equal(new DateTime(2024, 1, 15), dateTime);
    }

    [Fact]
    public void ConvertValue_TimeOnlyToTimeSpan_ReturnsTimeSpan()
    {
        var result = InvokeConvertValue(new TimeOnly(14, 30, 0), typeof(TimeSpan));

        var timeSpan = Assert.IsType<TimeSpan>(result);
        Assert.Equal(new TimeSpan(14, 30, 0), timeSpan);
    }

    private static object? InvokeConvertValue(object value, Type targetType)
    {
        var method = typeof(ImportExecutor).GetMethod("ConvertValue",
            BindingFlags.Static | BindingFlags.NonPublic);
        return method?.Invoke(null, new[] { value, targetType });
    }
}
