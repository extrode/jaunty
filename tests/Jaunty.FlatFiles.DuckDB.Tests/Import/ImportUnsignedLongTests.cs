using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Internals.Import;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

using Microsoft.Data.Sqlite;

namespace Jaunty.FlatFiles.DuckDB.Tests.Import;

/// <summary>
/// AUD-R34-029. SqliteImportDialect mapped <c>ulong</c> to <c>INTEGER</c>, whose storage class is a
/// signed 64-bit two's-complement value, so everything above <see cref="long.MaxValue"/> was stored
/// as a negative number with no error from either the provider or SQLite. SqlServerImportDialect and
/// PostgreSqlImportDialect widen to DECIMAL(20,0)/NUMERIC(20,0); SQLite has no unsigned and no
/// 128-bit integer type, so the representation has to change - TEXT plus a binding transform, since
/// Microsoft.Data.Sqlite binds a ulong as -1 whatever the column is declared as.
/// </summary>
public class ImportUnsignedLongTests : IDisposable
{
    private readonly string DataDir = Path.Combine(Path.GetTempPath(), $"jaunty_import_ulong_tests_{Guid.NewGuid():N}");
    private readonly string _parquetPath;
    private readonly DuckDb _db;

    public ImportUnsignedLongTests()
    {
        Directory.CreateDirectory(DataDir);
        _parquetPath = Path.Combine(DataDir, "counters.parquet");

        using var genConnection = new DuckDBConnection("DataSource=:memory:");
        genConnection.Open();
        using var cmd = genConnection.CreateCommand();

        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, 18446744073709551615::UBIGINT),
                    (2, 9223372036854775808::UBIGINT),
                    (3, 42::UBIGINT)
                ) AS t(""Id"", ""Counter"")
            ) TO '{_parquetPath.Replace("\\", "/").Replace("'", "''")}' (FORMAT PARQUET)";
        cmd.ExecuteNonQuery();

        var options = new FlatFileOptions();
        options.AddParquet<UnsignedCounterRecord>(_parquetPath);
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
    public void MapClrTypeToSqlType_ULong_IsNotAnIntegerColumn()
    {
        Assert.Equal("TEXT", SqliteImportDialect.Instance.MapClrTypeToSqlType(typeof(ulong)));
    }

    [Fact]
    public void MapClrTypeToSqlType_ULongsSignedAndSmallerUnsignedNeighbours_AreStillInteger()
    {
        Assert.Equal("INTEGER", SqliteImportDialect.Instance.MapClrTypeToSqlType(typeof(long)));
        Assert.Equal("INTEGER", SqliteImportDialect.Instance.MapClrTypeToSqlType(typeof(uint)));
        Assert.Equal("INTEGER", SqliteImportDialect.Instance.MapClrTypeToSqlType(typeof(ushort)));
    }

    [Fact]
    public void GenerateCreateTableSql_DeclaresTheULongColumnAsText()
    {
        string sql = SqliteImportDialect.Instance.GenerateCreateTableSql(
            "unsigned_counters",
            new[]
            {
                ("Id", typeof(int), true, false),
                ("Counter", typeof(ulong), false, false)
            });

        Assert.Contains("\"Counter\" TEXT", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportIntoAsync_ULongAboveLongMaxValue_RoundTripsExactly()
    {
        using var sqlite = CreateSqliteConnection();

        long count = await _db.ImportIntoAsync<UnsignedCounterRecord>(sqlite, new ImportOptions(createTableIfMissing: true));

        Assert.Equal(3, count);

        Assert.Equal(ulong.MaxValue, ReadCounter(sqlite, 1));
        Assert.Equal(9223372036854775808UL, ReadCounter(sqlite, 2));
        Assert.Equal(42UL, ReadCounter(sqlite, 3));
    }

    [Fact]
    public async Task ImportIntoAsync_ULong_StoresDecimalDigitsNotATwosComplementNegative()
    {
        using var sqlite = CreateSqliteConnection();

        await _db.ImportIntoAsync<UnsignedCounterRecord>(sqlite, new ImportOptions(createTableIfMissing: true));

        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = "SELECT CAST(\"Counter\" AS TEXT) FROM \"unsigned_counters\" WHERE \"Id\" = 1";
        string stored = (string)cmd.ExecuteScalar()!;

        Assert.Equal("18446744073709551615", stored);
        Assert.DoesNotContain("-", stored, StringComparison.Ordinal);
    }

    private static ulong ReadCounter(SqliteConnection sqlite, int id)
    {
        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = $"SELECT CAST(\"Counter\" AS TEXT) FROM \"unsigned_counters\" WHERE \"Id\" = {id}";
        return ulong.Parse((string)cmd.ExecuteScalar()!, System.Globalization.CultureInfo.InvariantCulture);
    }
}
