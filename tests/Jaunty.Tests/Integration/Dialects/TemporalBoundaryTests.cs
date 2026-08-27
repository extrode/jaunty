using System.Data;
using System.Data.Common;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Dialects;

/// <summary>
/// Hand-enumerated temporal boundary disagreements between engines — the plan's stated
/// replacement for a solver over the same surface, because the boundary set is small enough to
/// write down and each case documents a concrete engine limit rather than a derived one.
///
/// <para>
/// Only the dialects this machine can reach carry attributes: SQL Server and the two SQLite
/// providers. Postgres and MariaDB are deliberately absent rather than attached-and-skipped —
/// an assertion that has never executed is not evidence, and their connection strings are blank
/// in the local appsettings.json. See the plan for what extending them requires.
/// </para>
/// </summary>
[Collection("Dialect Boundary Operations")]
public class TemporalBoundaryTests : IClassFixture<DialectFixture>
{
    private const string TableName = "temporal_boundary_test";
    private readonly DialectFixture _fixture;

    public TemporalBoundaryTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static void CreateTemporalTable(IDbConnection connection, DialectProvider provider)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = provider switch
        {
            DialectProvider.SqlServer =>
                $"IF OBJECT_ID('dbo.{TableName}', 'U') IS NOT NULL DROP TABLE dbo.{TableName}; " +
                $"CREATE TABLE dbo.{TableName} (dt DATETIME2(7) NULL, dto DATETIMEOFFSET(7) NULL, ts TIME(7) NULL);",
            _ =>
                $"DROP TABLE IF EXISTS {TableName}; " +
                $"CREATE TABLE {TableName} (dt TEXT NULL, dto TEXT NULL, ts TEXT NULL);"
        };
        cmd.ExecuteNonQuery();
    }

    private static void Insert(IDbConnection connection, string column, object value)
    {
        connection.Execute($"INSERT INTO {TableName} ({column}) VALUES (@v)", new { v = value });
    }

    private static object? ReadBack(IDbConnection connection, string column)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT {column} FROM {TableName}";
        var raw = cmd.ExecuteScalar();
        return raw == DBNull.Value ? null : raw;
    }

    private WriteDialectContext OpenTemporalTable(DialectInfo dialect)
    {
        var ctx = _fixture.GetWriteContextForTable(dialect, "temporal_scratch");
        CreateTemporalTable(ctx.Connection, dialect.Provider);
        return ctx;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DateTimeMinValue_RoundTripsOnSqlite(DialectInfo dialect)
    {
        using var ctx = OpenTemporalTable(dialect);

        Insert(ctx.Connection, "dt", DateTime.MinValue);

        var read = ReadBack(ctx.Connection, "dt");

        Assert.Equal(DateTime.MinValue, Convert.ToDateTime(read));
    }

    /// <summary>
    /// The column is DATETIME2(7), whose floor is 0001-01-01, and the write still fails: Jaunty
    /// leaves the parameter type to SqlClient's inference, which picks the legacy <c>datetime</c>
    /// for a <see cref="DateTime"/> value, and that type's floor is 1753-01-01. The limit comes
    /// from the parameter, not the column.
    /// </summary>
    [Theory]
    [SqlServer]
    public void DateTimeMinValue_IsRejectedOnSqlServerByTheParameterTypeNotTheColumn(DialectInfo dialect)
    {
        using var ctx = OpenTemporalTable(dialect);

        var ex = Assert.ThrowsAny<Exception>(() => Insert(ctx.Connection, "dt", DateTime.MinValue));

        Assert.Contains("1753", ex.Message);
    }

    [Theory]
    [SqlServer]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DateTimeMaxValue_LosesSubMillisecondPrecisionNowhereReachable(DialectInfo dialect)
    {
        using var ctx = OpenTemporalTable(dialect);

        Insert(ctx.Connection, "dt", DateTime.MaxValue);

        var read = Convert.ToDateTime(ReadBack(ctx.Connection, "dt"));

        Assert.Equal(DateTime.MaxValue.Date, read.Date);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void HundredNanosecondTicks_SurviveTheRoundTripOnSqlite(DialectInfo dialect)
    {
        using var ctx = OpenTemporalTable(dialect);
        var value = new DateTime(2026, 8, 27, 13, 45, 12, DateTimeKind.Unspecified).AddTicks(1234567);

        Insert(ctx.Connection, "dt", value);

        var read = Convert.ToDateTime(ReadBack(ctx.Connection, "dt"));

        Assert.Equal(value.Ticks, read.Ticks);
    }

    /// <summary>
    /// Same cause as the MinValue case: the DATETIME2(7) column holds 100ns, but the inferred
    /// legacy <c>datetime</c> parameter rounds to its 1/300-second grid before the value ever
    /// reaches the column. Measured 2026-08-27: 1234567 sub-second ticks were stored as 1233333,
    /// a silent loss of up to 3.33ms on every DateTime Jaunty writes to SQL Server.
    /// </summary>
    [Theory]
    [SqlServer]
    public void HundredNanosecondTicks_AreRoundedToTheLegacyDatetimeGridOnSqlServer(DialectInfo dialect)
    {
        using var ctx = OpenTemporalTable(dialect);
        var value = new DateTime(2026, 8, 27, 13, 45, 12, DateTimeKind.Unspecified).AddTicks(1234567);

        Insert(ctx.Connection, "dt", value);

        var read = Convert.ToDateTime(ReadBack(ctx.Connection, "dt"));

        Assert.NotEqual(value.Ticks, read.Ticks);
        Assert.True(
            Math.Abs((read - value).TotalMilliseconds) <= 3.34,
            $"expected rounding within the 1/300s datetime grid, got {(read - value).TotalMilliseconds} ms");
    }

    [Theory]
    [SqlServer]
    public void TimeSpanBeyondTwentyFourHours_IsRejectedBySqlServerTime(DialectInfo dialect)
    {
        using var ctx = OpenTemporalTable(dialect);

        Assert.ThrowsAny<Exception>(() => Insert(ctx.Connection, "ts", TimeSpan.FromHours(25)));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void TimeSpanBeyondTwentyFourHours_IsAcceptedBySqliteText(DialectInfo dialect)
    {
        using var ctx = OpenTemporalTable(dialect);
        var value = TimeSpan.FromHours(25);

        Insert(ctx.Connection, "ts", value);

        var read = ReadBack(ctx.Connection, "ts");

        Assert.Equal(value, TimeSpan.Parse(Convert.ToString(read)!));
    }

    [Theory]
    [SqlServer]
    public void DateTimeOffset_PreservesItsOffsetOnSqlServer(DialectInfo dialect)
    {
        using var ctx = OpenTemporalTable(dialect);
        var value = new DateTimeOffset(2026, 8, 27, 13, 45, 12, TimeSpan.FromHours(5.5));

        Insert(ctx.Connection, "dto", value);

        var read = (DateTimeOffset)ReadBack(ctx.Connection, "dto")!;

        Assert.Equal(value.Offset, read.Offset);
        Assert.Equal(value.UtcDateTime, read.UtcDateTime);
    }
}
