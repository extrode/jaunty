using System.Data.Common;
using System.Globalization;

using Jaunty.Attributes;
using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;

using Microsoft.Data.Sqlite;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// AUD-R25: all three of this package's value-conversion sites called
/// <c>Convert.ChangeType(value, targetType)</c> with no <see cref="IFormatProvider"/>, so they ran
/// under the ambient <see cref="CultureInfo.CurrentCulture"/>. Under a comma-decimal culture
/// (de-DE, fr-FR, pt-BR - most of Europe and Latin America) <c>Convert.ChangeType("1.5",
/// typeof(decimal))</c> does not throw: it reads the period as a group separator and returns 15.
///
/// <para>
/// The two raw-SQL read paths (<c>QueryInternal</c>/<c>QueryInternalAsync</c>) and the import
/// pipeline's per-cell <c>ImportExecutor.ConvertValue</c> are all covered here. Each conversion also
/// swallows failures with a bare <c>catch</c>, so a locale-corrupted value and a genuinely
/// unconvertible one were indistinguishable at the call site - which is why this was silent.
/// </para>
/// </summary>
public class QueryCultureInvarianceTests : IDisposable
{
    private readonly DuckDb _db;

    public QueryCultureInvarianceTests() => _db = new DuckDb(new FlatFileOptions());

    public void Dispose() => _db.Dispose();

    private static void InCulture(string cultureName, Action body)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static async Task InCultureAsync(string cultureName, Func<Task> body)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            await body();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // The column is VARCHAR, so DuckDB hands back a string and the mapper has to convert it -
    // exactly the shape that made this locale-dependent.
    private const string SelectStringPricedRows =
        "SELECT * FROM (VALUES ('a', '1.5'), ('b', '1234.56'), ('c', '1.234')) AS t(name, amount)";

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("pt-BR")]
    public void Query_StringColumnToDecimalProperty_IsCultureInvariant(string culture)
    {
        InCulture(culture, () =>
        {
            List<Priced> rows = _db.Query<Priced>(SelectStringPricedRows);

            Assert.Equal(3, rows.Count);
            Assert.Equal(1.5m, rows[0].Amount);
            Assert.Equal(1234.56m, rows[1].Amount);
            // "1.234" is a valid de-DE spelling of 1234, so this one succeeded and returned the
            // wrong number rather than throwing.
            Assert.Equal(1.234m, rows[2].Amount);
        });
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public async Task QueryAsync_StringColumnToDecimalProperty_IsCultureInvariant(string culture)
    {
        await InCultureAsync(culture, async () =>
        {
            List<Priced> rows = await _db.QueryAsync<Priced>(SelectStringPricedRows);

            Assert.Equal(3, rows.Count);
            Assert.Equal(1.5m, rows[0].Amount);
            Assert.Equal(1234.56m, rows[1].Amount);
            Assert.Equal(1.234m, rows[2].Amount);
        });
    }

    [Fact]
    public void Query_UnderInvariantCulture_IsUnchanged()
    {
        InCulture("en-US", () =>
        {
            List<Priced> rows = _db.Query<Priced>(SelectStringPricedRows);
            Assert.Equal(1.5m, rows[0].Amount);
        });
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public async Task ImportInto_StringSourceColumnToDecimalTarget_IsCultureInvariant(string culture)
    {
        // ImportExecutor.ConvertValue is the terminal conversion for every column of every row on
        // the import path, so a comma-decimal host silently scaled every imported decimal.
        var csvPath = Path.Combine(Path.GetTempPath(), $"jaunty_r25_culture_{Guid.NewGuid():N}.csv");
        File.WriteAllText(csvPath, "name,amount\na,1.5\nb,1234.56\nc,1.234\n");

        try
        {
            var options = new FlatFileOptions();
            // All columns read as VARCHAR so the value reaching ConvertValue is a string.
            options.AddCsv<Priced>(csvPath, csv => csv.HasHeader = true);

            using var db = new DuckDb(options);
            await using var target = new SqliteConnection("Data Source=:memory:");
            await target.OpenAsync();

            using (DbCommand create = target.CreateCommand())
            {
                create.CommandText = "CREATE TABLE priced (name TEXT, amount NUMERIC);";
                await create.ExecuteNonQueryAsync();
            }

            await InCultureAsync(culture, async () =>
            {
                long imported = await db.ImportIntoAsync<Priced>(target, new ImportOptions());
                Assert.Equal(3, imported);
            });

            using DbCommand read = target.CreateCommand();
            read.CommandText = "SELECT amount FROM priced ORDER BY rowid;";
            using DbDataReader reader = await read.ExecuteReaderAsync();

            var actual = new List<decimal>();
            while (await reader.ReadAsync())
                actual.Add(Convert.ToDecimal(reader.GetValue(0), CultureInfo.InvariantCulture));

            Assert.Equal([1.5m, 1234.56m, 1.234m], actual);
        }
        finally
        {
            File.Delete(csvPath);
        }
    }

    [Table("priced")]
    private class Priced
    {
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("amount")]
        public decimal Amount { get; set; }
    }
}
