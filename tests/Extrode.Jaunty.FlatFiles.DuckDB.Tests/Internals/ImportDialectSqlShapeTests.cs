using Extrode.Jaunty.FlatFiles.DuckDB.Internals.Import;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// The exact SQL each import dialect generates. The SQL Server and PostgreSQL paths otherwise run
/// only against live servers, which the mutation runner does not have, so separators, clause order
/// and the NOT NULL rules had no test that could see them change.
/// </summary>
public class ImportDialectSqlShapeTests
{
    private static readonly string[] Columns = ["Id", "Name", "Qty"];
    private static readonly string[] Parameters = ["@p0", "@p1", "@p2"];

    private static readonly (string, Type, bool, bool)[] TableColumns =
    [
        ("Id", typeof(int), true, false),
        ("Name", typeof(string), false, true),
        ("Qty", typeof(int), false, false),
    ];

    private const string KeylessMessage =
        "Table 't' has no [Key] property to use for conflict resolution. " +
        "The Upsert conflict strategy requires a [Key]-attributed property; " +
        "use ConflictStrategy.Error (the default) instead, or add a [Key] attribute to the entity.";

    // ------------------------------------------------------------------
    // SQL Server
    // ------------------------------------------------------------------

    [Fact]
    public void SqlServer_Insert() =>
        Assert.Equal(
            "INSERT INTO [t] ([Id], [Name], [Qty]) VALUES (@p0, @p1, @p2)",
            SqlServerImportDialect.Instance.GenerateInsertSql("t", Columns, Parameters, ConflictStrategy.Error, "Id"));

    [Fact]
    public void SqlServer_Skip_IsAMergeWithoutAnUpdate() =>
        Assert.Equal(
            "MERGE [t] WITH (HOLDLOCK) AS target USING (SELECT @p0 AS [Id], @p1 AS [Name], @p2 AS [Qty]) AS source " +
            "ON target.[Id] = source.[Id] " +
            "WHEN NOT MATCHED THEN INSERT ([Id], [Name], [Qty]) VALUES (source.[Id], source.[Name], source.[Qty]);",
            SqlServerImportDialect.Instance.GenerateInsertSql("t", Columns, Parameters, ConflictStrategy.Skip, "Id"));

    [Fact]
    public void SqlServer_Upsert_UpdatesEveryNonKeyColumn() =>
        Assert.Equal(
            "MERGE [t] WITH (HOLDLOCK) AS target USING (SELECT @p0 AS [Id], @p1 AS [Name], @p2 AS [Qty]) AS source " +
            "ON target.[Id] = source.[Id] " +
            "WHEN MATCHED THEN UPDATE SET target.[Name] = source.[Name], target.[Qty] = source.[Qty] " +
            "WHEN NOT MATCHED THEN INSERT ([Id], [Name], [Qty]) VALUES (source.[Id], source.[Name], source.[Qty]);",
            SqlServerImportDialect.Instance.GenerateInsertSql("t", Columns, Parameters, ConflictStrategy.Upsert, "Id"));

    [Fact]
    public void SqlServer_KeylessUpsert_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            SqlServerImportDialect.Instance.GenerateInsertSql("t", Columns, Parameters, ConflictStrategy.Upsert, null));

        Assert.Equal(KeylessMessage, ex.Message);
    }

    [Fact]
    public void SqlServer_CreateTable() =>
        Assert.Equal(
            "IF OBJECT_ID(N'[t]', N'U') IS NULL CREATE TABLE [t] ([Id] INT PRIMARY KEY, [Name] NVARCHAR(MAX), [Qty] INT NOT NULL)",
            SqlServerImportDialect.Instance.GenerateCreateTableSql("t", TableColumns));

    [Fact]
    public void SqlServer_CreateTable_EscapesAQuoteInsideTheExistenceCheck() =>
        Assert.StartsWith(
            "IF OBJECT_ID(N'[o''t]', N'U') IS NULL CREATE TABLE [o't] (",
            SqlServerImportDialect.Instance.GenerateCreateTableSql("o't", TableColumns));

    [Theory]
    [InlineData(typeof(string), "NVARCHAR(MAX)")]
    [InlineData(typeof(int), "INT")]
    [InlineData(typeof(long), "BIGINT")]
    [InlineData(typeof(short), "SMALLINT")]
    [InlineData(typeof(byte), "TINYINT")]
    [InlineData(typeof(float), "REAL")]
    [InlineData(typeof(double), "FLOAT")]
    [InlineData(typeof(decimal), "DECIMAL(38,9)")]
    [InlineData(typeof(bool), "BIT")]
    [InlineData(typeof(DateTime), "DATETIME2")]
    [InlineData(typeof(DateTimeOffset), "DATETIMEOFFSET")]
    [InlineData(typeof(Guid), "UNIQUEIDENTIFIER")]
    [InlineData(typeof(byte[]), "VARBINARY(MAX)")]
    [InlineData(typeof(DateOnly), "DATE")]
    [InlineData(typeof(TimeOnly), "TIME")]
    [InlineData(typeof(TimeSpan), "TIME")]
    [InlineData(typeof(char), "NCHAR(1)")]
    [InlineData(typeof(uint), "BIGINT")]
    [InlineData(typeof(ulong), "DECIMAL(20,0)")]
    [InlineData(typeof(sbyte), "SMALLINT")]
    [InlineData(typeof(ushort), "INT")]
    public void SqlServer_TypeMap(Type clrType, string expected) =>
        Assert.Equal(expected, SqlServerImportDialect.Instance.MapClrTypeToSqlType(clrType));

    // ------------------------------------------------------------------
    // PostgreSQL
    // ------------------------------------------------------------------

    [Fact]
    public void PostgreSql_Insert() =>
        Assert.Equal(
            "INSERT INTO \"t\" (\"Id\", \"Name\", \"Qty\") VALUES (@p0, @p1, @p2)",
            PostgreSqlImportDialect.Instance.GenerateInsertSql("t", Columns, Parameters, ConflictStrategy.Error, "Id"));

    [Fact]
    public void PostgreSql_Skip() =>
        Assert.Equal(
            "INSERT INTO \"t\" (\"Id\", \"Name\", \"Qty\") VALUES (@p0, @p1, @p2) ON CONFLICT (\"Id\") DO NOTHING",
            PostgreSqlImportDialect.Instance.GenerateInsertSql("t", Columns, Parameters, ConflictStrategy.Skip, "Id"));

    [Fact]
    public void PostgreSql_Upsert() =>
        Assert.Equal(
            "INSERT INTO \"t\" (\"Id\", \"Name\", \"Qty\") VALUES (@p0, @p1, @p2) " +
            "ON CONFLICT (\"Id\") DO UPDATE SET \"Name\" = EXCLUDED.\"Name\", \"Qty\" = EXCLUDED.\"Qty\"",
            PostgreSqlImportDialect.Instance.GenerateInsertSql("t", Columns, Parameters, ConflictStrategy.Upsert, "Id"));

    [Fact]
    public void PostgreSql_KeylessUpsert_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            PostgreSqlImportDialect.Instance.GenerateInsertSql("t", Columns, Parameters, ConflictStrategy.Upsert, null));

        Assert.Equal(KeylessMessage, ex.Message);
    }

    [Fact]
    public void PostgreSql_CreateTable() =>
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS \"t\" (\"Id\" INTEGER PRIMARY KEY, \"Name\" TEXT, \"Qty\" INTEGER NOT NULL)",
            PostgreSqlImportDialect.Instance.GenerateCreateTableSql("t", TableColumns));

    [Theory]
    [InlineData(typeof(string), "TEXT")]
    [InlineData(typeof(int), "INTEGER")]
    [InlineData(typeof(long), "BIGINT")]
    [InlineData(typeof(short), "SMALLINT")]
    [InlineData(typeof(byte), "SMALLINT")]
    [InlineData(typeof(float), "REAL")]
    [InlineData(typeof(double), "DOUBLE PRECISION")]
    [InlineData(typeof(decimal), "NUMERIC")]
    [InlineData(typeof(bool), "BOOLEAN")]
    [InlineData(typeof(DateTime), "TIMESTAMP")]
    [InlineData(typeof(DateTimeOffset), "TIMESTAMPTZ")]
    [InlineData(typeof(Guid), "UUID")]
    [InlineData(typeof(byte[]), "BYTEA")]
    [InlineData(typeof(DateOnly), "DATE")]
    [InlineData(typeof(TimeOnly), "TIME")]
    [InlineData(typeof(TimeSpan), "INTERVAL")]
    [InlineData(typeof(char), "CHAR(1)")]
    [InlineData(typeof(uint), "BIGINT")]
    [InlineData(typeof(ulong), "NUMERIC(20,0)")]
    [InlineData(typeof(sbyte), "SMALLINT")]
    [InlineData(typeof(ushort), "INTEGER")]
    public void PostgreSql_TypeMap(Type clrType, string expected) =>
        Assert.Equal(expected, PostgreSqlImportDialect.Instance.MapClrTypeToSqlType(clrType));

    // ------------------------------------------------------------------
    // SQLite
    // ------------------------------------------------------------------

    [Fact]
    public void Sqlite_Insert() =>
        Assert.Equal(
            "INSERT INTO \"t\" (\"Id\", \"Name\", \"Qty\") VALUES (@p0, @p1, @p2)",
            SqliteImportDialect.Instance.GenerateInsertSql("t", Columns, Parameters, ConflictStrategy.Error, "Id"));

    [Fact]
    public void Sqlite_Upsert() =>
        Assert.Equal(
            "INSERT INTO \"t\" (\"Id\", \"Name\", \"Qty\") VALUES (@p0, @p1, @p2) " +
            "ON CONFLICT (\"Id\") DO UPDATE SET \"Name\" = excluded.\"Name\", \"Qty\" = excluded.\"Qty\"",
            SqliteImportDialect.Instance.GenerateInsertSql("t", Columns, Parameters, ConflictStrategy.Upsert, "Id"));

    [Fact]
    public void Sqlite_KeylessUpsert_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            SqliteImportDialect.Instance.GenerateInsertSql("t", Columns, Parameters, ConflictStrategy.Upsert, null));

        Assert.Equal(KeylessMessage, ex.Message);
    }

    [Fact]
    public void Sqlite_CreateTable_KeepsNotNullOnTheKey() =>
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS \"t\" (\"Id\" INTEGER PRIMARY KEY NOT NULL, \"Name\" TEXT, \"Qty\" INTEGER NOT NULL)",
            SqliteImportDialect.Instance.GenerateCreateTableSql("t", TableColumns));

    [Theory]
    [InlineData(typeof(string), "TEXT")]
    [InlineData(typeof(int), "INTEGER")]
    [InlineData(typeof(long), "INTEGER")]
    [InlineData(typeof(short), "INTEGER")]
    [InlineData(typeof(byte), "INTEGER")]
    [InlineData(typeof(float), "REAL")]
    [InlineData(typeof(double), "REAL")]
    [InlineData(typeof(decimal), "REAL")]
    [InlineData(typeof(bool), "INTEGER")]
    [InlineData(typeof(DateTime), "TEXT")]
    [InlineData(typeof(DateTimeOffset), "TEXT")]
    [InlineData(typeof(Guid), "TEXT")]
    [InlineData(typeof(byte[]), "BLOB")]
    [InlineData(typeof(DateOnly), "TEXT")]
    [InlineData(typeof(TimeOnly), "TEXT")]
    [InlineData(typeof(TimeSpan), "TEXT")]
    [InlineData(typeof(char), "TEXT")]
    [InlineData(typeof(uint), "INTEGER")]
    [InlineData(typeof(ulong), "TEXT")]
    [InlineData(typeof(sbyte), "INTEGER")]
    [InlineData(typeof(ushort), "INTEGER")]
    public void Sqlite_TypeMap(Type clrType, string expected) =>
        Assert.Equal(expected, SqliteImportDialect.Instance.MapClrTypeToSqlType(clrType));

    [Fact]
    public void Sqlite_OnlyAnUnsignedLongIsTransformedForBinding()
    {
        var transform = (IImportValueTransform)SqliteImportDialect.Instance;

        Assert.False(transform.TryTransformForBinding(5L, out object same));
        Assert.Equal(5L, same);
        Assert.True(transform.TryTransformForBinding(ulong.MaxValue, out object text));
        Assert.Equal("18446744073709551615", text);
    }

    // ------------------------------------------------------------------
    // Shared
    // ------------------------------------------------------------------

    [Fact]
    public void AnUnsupportedType_NamesTheTypeAndTheEngine()
    {
        var ex = Assert.Throws<NotSupportedException>(() => SqlServerImportDialect.Instance.MapClrTypeToSqlType(typeof(Uri)));

        Assert.Equal(
            "SQL Server import has no column type for 'System.Uri'. " +
            "The import pipeline creates the target table from the entity, so every mapped " +
            "property needs a column type it can be inserted into. Map the property to a " +
            "supported type, exclude it from the entity, or create the target table yourself " +
            "before importing.",
            ex.Message);
    }
}
