using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;

using Microsoft.Data.SqlClient;

using Xunit;

namespace Jaunty.Tests.Unit.BulkCopy;

/// <summary>
/// AUD-R26 (batch 6, medium/security). A plain <c>BulkInsert&lt;T&gt;</c> - not
/// <c>BulkInsertIgnoreConstraints</c>, the one whose name says it - silently skipped CHECK and
/// FOREIGN KEY validation on SQL Server, and only on SQL Server, and only above a row-count
/// threshold.
///
/// <para>
/// <c>BulkCopyConfiguration.DefaultCheckConstraints</c> was <see langword="false"/>, so
/// <c>CheckConstraints = !ignoreConstraints &amp;&amp; DefaultCheckConstraints</c> evaluated to
/// <see langword="false"/> for the ordinary call, and <c>SqlBulkCopy</c> without that flag bypasses
/// constraint checking entirely by documented default.
/// </para>
///
/// <para>
/// Every other route validated: below <c>MinimumRowsForNativeBulkCopy</c> it uses multi-row INSERT,
/// PostgreSQL uses <c>COPY</c>, MySQL's "native" provider is a chunked INSERT, and SQLite's provider
/// is deliberately null. So the same call with 50 rows wrote validated data and with 50,000 rows did
/// not - one provider, no error, no difference in the API surface. That is the shape of a defect
/// that a passing test suite hides: nothing fails, the wrong rows just arrive.
/// </para>
///
/// <para>
/// These tests bracket the threshold deliberately: the same violating row is inserted below it and
/// above it, and both must be rejected. A test that only exercised one side would have passed
/// before the fix.
/// </para>
/// </summary>
[Collection("Jaunty Config State")]
public class BulkInsertConstraintValidationTests : IDisposable
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("JAUNTY_TEST_SQLSERVER")
        ?? "Server=localhost;Database=JauntyBench;Trusted_Connection=True;TrustServerCertificate=True;";

    private readonly SqlConnection _connection;

    public BulkInsertConstraintValidationTests()
    {
        JauntyReflectionExtensions.UseReflectionMapping();
        JauntyReflectionExtensions.UseNativeBulkCopy();
        BulkCopyConfiguration.Reset();

        _connection = new SqlConnection(ConnectionString);
        try
        {
            _connection.Open();
        }
        catch (Exception ex)
        {
            _connection.Dispose();
            Assert.Skip($"SQL Server not reachable: {ex.Message}");
        }

        Exec("""
            IF OBJECT_ID('dbo.jaunty_constraint_probe', 'U') IS NOT NULL
                DROP TABLE dbo.jaunty_constraint_probe;
            CREATE TABLE dbo.jaunty_constraint_probe (
                probe_id INT NOT NULL PRIMARY KEY,
                quantity INT NOT NULL CONSTRAINT ck_quantity_positive CHECK (quantity > 0)
            );
            """);
    }

    public void Dispose()
    {
        try { Exec("IF OBJECT_ID('dbo.jaunty_constraint_probe', 'U') IS NOT NULL DROP TABLE dbo.jaunty_constraint_probe;"); }
        catch { /* teardown must not mask a test failure */ }

        BulkCopyConfiguration.Reset();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Exec(string sql)
    {
        using SqlCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private int RowCount()
    {
        using SqlCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.jaunty_constraint_probe;";
        return (int)command.ExecuteScalar()!;
    }

    [Table("jaunty_constraint_probe")]
    public class Probe
    {
        [Key]
        [Column("probe_id")]
        public int ProbeId { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }
    }

    /// <summary>One row violates <c>CHECK (quantity &gt; 0)</c>; the rest are valid.</summary>
    private static List<Probe> WithOneViolation(int rows)
    {
        var probes = new List<Probe>(rows);
        for (int i = 0; i < rows; i++)
            probes.Add(new Probe { ProbeId = i + 1, Quantity = i == rows / 2 ? -1 : i + 1 });

        return probes;
    }

    // ------------------------------------------------------------------
    // The defect: above the native-bulk-copy threshold
    // ------------------------------------------------------------------

    [Fact]
    public void AboveTheNativeThreshold_APlainBulkInsertRejectsAConstraintViolation()
    {
        int rows = BulkCopyConfiguration.MinimumRowsForNativeBulkCopy * 2;
        Assert.True(rows > BulkCopyConfiguration.MinimumRowsForNativeBulkCopy);

        Exception? thrown = Record.Exception(() => _connection.BulkInsert(WithOneViolation(rows)));

        Assert.True(thrown is not null,
            $"BulkInsert of {rows} rows containing a CHECK violation completed without error. " +
            "Above MinimumRowsForNativeBulkCopy this routes to SqlBulkCopy, which bypasses " +
            "constraint checking unless the CheckConstraints option is set - so the violating row " +
            "was written and the constraint silently did not apply.");

        Assert.Equal(0, RowCount());
    }

    // ------------------------------------------------------------------
    // The control: below the threshold this always worked
    // ------------------------------------------------------------------

    [Fact]
    public void BelowTheNativeThreshold_APlainBulkInsertRejectsAConstraintViolation()
    {
        int rows = BulkCopyConfiguration.MinimumRowsForNativeBulkCopy / 2;
        Assert.True(rows < BulkCopyConfiguration.MinimumRowsForNativeBulkCopy);

        Assert.ThrowsAny<Exception>(() => _connection.BulkInsert(WithOneViolation(rows)));
        Assert.Equal(0, RowCount());
    }

    /// <summary>
    /// The two routes must agree. This is the finding stated as an assertion: the same call, the
    /// same violating data, differing only in row count, must not differ in whether it validates.
    /// </summary>
    [Fact]
    public void TheTwoRoutesAgreeAboutAConstraintViolation()
    {
        int below = BulkCopyConfiguration.MinimumRowsForNativeBulkCopy / 2;
        int above = BulkCopyConfiguration.MinimumRowsForNativeBulkCopy * 2;

        Exception? belowThreshold = Record.Exception(() => _connection.BulkInsert(WithOneViolation(below)));
        Exec("DELETE FROM dbo.jaunty_constraint_probe;");
        Exception? aboveThreshold = Record.Exception(() => _connection.BulkInsert(WithOneViolation(above)));

        Assert.True(
            (belowThreshold is null) == (aboveThreshold is null),
            $"The same BulkInsert validated differently either side of MinimumRowsForNativeBulkCopy: " +
            $"{below} rows {(belowThreshold is null ? "succeeded" : "threw")}, " +
            $"{above} rows {(aboveThreshold is null ? "succeeded" : "threw")}.");
    }

    // ------------------------------------------------------------------
    // The sibling API must keep doing what its name says
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>BulkInsertIgnoreConstraints</c> discloses the bypass in its own remarks and exists for
    /// exactly this. Making the plain call safe must not take the escape hatch away.
    /// </summary>
    [Fact]
    public void BulkInsertIgnoreConstraints_StillBypassesTheCheck()
    {
        int rows = BulkCopyConfiguration.MinimumRowsForNativeBulkCopy * 2;

        _connection.BulkInsertIgnoreConstraints(WithOneViolation(rows));

        Assert.Equal(rows, RowCount());
    }

    /// <summary>
    /// The old behaviour stays reachable for callers who measured and chose it - the default moved,
    /// the capability did not.
    /// </summary>
    [Fact]
    public void OptingOutGloballyStillBypassesTheCheck()
    {
        BulkCopyConfiguration.DefaultCheckConstraints = false;

        int rows = BulkCopyConfiguration.MinimumRowsForNativeBulkCopy * 2;
        _connection.BulkInsert(WithOneViolation(rows));

        Assert.Equal(rows, RowCount());
    }

    /// <summary>Valid data must still go through the fast path untouched.</summary>
    [Fact]
    public void ValidRowsStillBulkInsertAboveTheThreshold()
    {
        int rows = BulkCopyConfiguration.MinimumRowsForNativeBulkCopy * 2;

        var probes = new List<Probe>(rows);
        for (int i = 0; i < rows; i++)
            probes.Add(new Probe { ProbeId = i + 1, Quantity = i + 1 });

        _connection.BulkInsert(probes);

        Assert.Equal(rows, RowCount());
    }
}
