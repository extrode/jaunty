using Jaunty.Attributes;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.Read;

/// <summary>
/// Edge case tests: empty files, no-header CSVs, column mismatches, schema validation.
/// </summary>
public class EdgeCaseTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    // ==========================================
    // T023 — Empty file handling (P0)
    // ==========================================

    [Fact]
    public void EmptyFile_ReturnsEmptyList()
    {
        var emptyPath = Path.Combine(DataDir, "csv", "empty.csv");
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(emptyPath);

        using var db = new DuckDb(options);
        var results = db.Connection.From<SalesRecord>().Select();

        Assert.Empty(results);
    }

    [Fact]
    public void EmptyFile_Count_ReturnsZero()
    {
        var emptyPath = Path.Combine(DataDir, "csv", "empty.csv");
        var options = new FlatFileOptions();
        // Use a table name that won't conflict — empty_sales maps to "sales" via [Table],
        // so use raw source with explicit table name
        var source = new CsvFileSource("empty_sales", emptyPath, typeof(SalesRecord));
        options.Sources.Add(source);

        using var db = new DuckDb(options);

        // Query via raw SQL since entity table name "sales" != view name "empty_sales"
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM \"empty_sales\"";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.Equal(0, count);
    }

    // ==========================================
    // T024 — No-header CSV (P1)
    // ==========================================

    [Fact]
    public void NoHeader_RawQuery_Queryable()
    {
        var noHeaderPath = Path.Combine(DataDir, "csv", "no-header.csv");
        var source = new CsvFileSource("no_header", noHeaderPath, typeof(object))
        {
            HasHeader = false
        };

        var options = new FlatFileOptions();
        options.Sources.Add(source);

        using var db = new DuckDb(options);

        // With HasHeader=false, DuckDB names columns "column0", "column1", etc.
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM \"no_header\"";
        using var reader = cmd.ExecuteReader();

        int rowCount = 0;
        while (reader.Read()) rowCount++;
        Assert.Equal(3, rowCount); // no-header.csv has 3 rows
    }

    // ==========================================
    // T025 — Column name mismatch (P1)
    // ==========================================

    [Fact]
    public void ColumnAttribute_ResolvesNameMismatch()
    {
        // SalesRecord has [Column("product_name")] on ProductName property
        // CSV file has column "product_name" — the attribute should resolve this
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var options = new FlatFileOptions();
        options.AddCsv<SalesRecord>(csvPath);

        using var db = new DuckDb(options);
        var results = db.Connection.From<SalesRecord>()
            .Where(s => s.ProductName == "Gadget X")
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("Gadget X", r.ProductName));
    }

    // ==========================================
    // T020 — Schema validation (P1)
    // ==========================================

    /// <summary>
    /// Entity with a column name that doesn't exist in the file.
    /// </summary>
    [Table("mismatch_test")]
    public class MismatchEntity
    {
        [Key] public int Id { get; set; }
        public string NonExistentColumn { get; set; } = "";
    }

    [Fact]
    public void SchemaValidation_MissingColumn_ThrowsDescriptiveError()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var source = new CsvFileSource("mismatch_test", csvPath, typeof(MismatchEntity));

        var options = new FlatFileOptions
        {
            ValidateSchema = true
        };
        options.Sources.Add(source);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new DuckDb(options));

        Assert.Contains("NonExistentColumn", ex.Message);
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public void SchemaValidation_Disabled_DoesNotThrow()
    {
        var csvPath = Path.Combine(DataDir, "csv", "sales.csv");
        var source = new CsvFileSource("mismatch_test2", csvPath, typeof(MismatchEntity));

        var options = new FlatFileOptions
        {
            ValidateSchema = false // default
        };
        options.Sources.Add(source);

        // Should not throw even with mismatched schema
        using var db = new DuckDb(options);
        Assert.NotNull(db);
    }

    // ==========================================
    // Special characters test
    // ==========================================

    [Table("special")]
    public class SpecialCharsEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    [Fact]
    public void SpecialChars_CsvParsedCorrectly()
    {
        var specialPath = Path.Combine(DataDir, "csv", "special-chars.csv");
        var source = new CsvFileSource("special", specialPath, typeof(SpecialCharsEntity));

        var options = new FlatFileOptions();
        options.Sources.Add(source);

        using var db = new DuckDb(options);

        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM \"special\" ORDER BY \"Id\"";
        using var reader = cmd.ExecuteReader();

        var rows = new List<(int id, string name, string desc)>();
        while (reader.Read())
        {
            rows.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2)
            ));
        }

        Assert.Equal(3, rows.Count);
        Assert.Equal("O'Brien", rows[0].name);         // single quote
        Assert.Equal("Smith, Jr.", rows[1].name);        // comma in value
        Assert.Contains("hello", rows[2].name);          // double quotes in name
    }
}
