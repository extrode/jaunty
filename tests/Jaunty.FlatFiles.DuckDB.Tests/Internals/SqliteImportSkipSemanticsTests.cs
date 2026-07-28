using Jaunty.FlatFiles.DuckDB.Internals.Import;
using Jaunty.FlatFiles.Import;

using Microsoft.Data.Sqlite;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R25: SqliteImportDialect implemented <see cref="ConflictStrategy.Skip"/> as
/// <c>INSERT OR IGNORE</c>, which suppresses <em>every</em> constraint violation on the row - NOT
/// NULL, CHECK, foreign key, and any other UNIQUE index - not just the duplicate key. A source row
/// that violated a NOT NULL or CHECK constraint was therefore dropped on the floor and counted as a
/// skipped duplicate, with nothing surfaced to the caller.
///
/// <para>
/// The two sibling dialects were already key-scoped: PostgreSqlImportDialect emits
/// <c>ON CONFLICT (key) DO NOTHING</c> and SqlServerImportDialect matches its MERGE on the key
/// column. SQLite now emits <c>ON CONFLICT (key) DO NOTHING</c> too - supported since SQLite 3.24,
/// the same release that gave it the DO UPDATE form the Upsert path already relies on.
/// </para>
/// </summary>
public class SqliteImportSkipSemanticsTests
{
    // ------------------------------------------------------------------
    // Generated SQL
    // ------------------------------------------------------------------

    [Fact]
    public void Skip_EmitsKeyScopedDoNothing_NotOrIgnore()
    {
        string sql = SqliteImportDialect.Instance.GenerateInsertSql(
            "orders", ["id", "total"], ["@p0", "@p1"], ConflictStrategy.Skip, keyColumnName: "id");

        Assert.DoesNotContain("OR IGNORE", sql);
        Assert.Contains("INSERT INTO \"orders\"", sql);
        Assert.Contains("ON CONFLICT (\"id\") DO NOTHING", sql);
    }

    [Fact]
    public void Skip_QuotesTheConflictTarget()
    {
        string sql = SqliteImportDialect.Instance.GenerateInsertSql(
            "orders", ["we\"ird"], ["@p0"], ConflictStrategy.Skip, keyColumnName: "we\"ird");

        Assert.Contains("ON CONFLICT (\"we\"\"ird\") DO NOTHING", sql);
    }

    [Fact]
    public void Error_EmitsAPlainInsert()
    {
        string sql = SqliteImportDialect.Instance.GenerateInsertSql(
            "orders", ["id"], ["@p0"], ConflictStrategy.Error, keyColumnName: "id");

        Assert.DoesNotContain("ON CONFLICT", sql);
        Assert.DoesNotContain("OR IGNORE", sql);
    }

    // ------------------------------------------------------------------
    // Executed against a real SQLite database
    // ------------------------------------------------------------------

    [Fact]
    public void Skip_StillSkipsDuplicateKeys()
    {
        using SqliteConnection connection = OpenSeeded();

        int inserted = Execute(connection, SkipSql(), id: 1, name: "duplicate", qty: 5);

        Assert.Equal(0, inserted);
        Assert.Equal("original", ScalarName(connection, 1));
    }

    [Fact]
    public void Skip_StillInsertsNonConflictingRows()
    {
        using SqliteConnection connection = OpenSeeded();

        int inserted = Execute(connection, SkipSql(), id: 2, name: "fresh", qty: 7);

        Assert.Equal(1, inserted);
        Assert.Equal("fresh", ScalarName(connection, 2));
    }

    [Fact]
    public void Skip_NoLongerSwallowsACheckConstraintViolation()
    {
        // The point of the fix. Under INSERT OR IGNORE this row vanished silently and the import
        // reported it as skipped; the caller had no way to tell a duplicate from bad data.
        using SqliteConnection connection = OpenSeeded();

        SqliteException ex = Assert.Throws<SqliteException>(() =>
            Execute(connection, SkipSql(), id: 3, name: "negative qty", qty: -1));

        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Skip_NoLongerSwallowsANotNullViolation()
    {
        using SqliteConnection connection = OpenSeeded();

        Assert.Throws<SqliteException>(() =>
            Execute(connection, SkipSql(), id: 4, name: null, qty: 1));
    }

    [Fact]
    public void Skip_NoLongerSwallowsAViolationOfADifferentUniqueIndex()
    {
        // A second UNIQUE index is not the conflict target, so a collision on it is a genuine error
        // rather than a duplicate to skip.
        using SqliteConnection connection = OpenSeeded();

        Assert.Throws<SqliteException>(() =>
            Execute(connection, SkipSql(), id: 5, name: "original", qty: 1));
    }

    private static string SkipSql() => SqliteImportDialect.Instance.GenerateInsertSql(
        "items", ["id", "name", "qty"], ["@p0", "@p1", "@p2"], ConflictStrategy.Skip, keyColumnName: "id");

    private static SqliteConnection OpenSeeded()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE items (
                id   INTEGER PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                qty  INTEGER NOT NULL CHECK (qty >= 0)
            );
            INSERT INTO items (id, name, qty) VALUES (1, 'original', 10);
            """;
        create.ExecuteNonQuery();

        return connection;
    }

    private static int Execute(SqliteConnection connection, string sql, int id, string? name, int qty)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@p0", id);
        command.Parameters.AddWithValue("@p1", (object?)name ?? DBNull.Value);
        command.Parameters.AddWithValue("@p2", qty);
        return command.ExecuteNonQuery();
    }

    private static string? ScalarName(SqliteConnection connection, int id)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM items WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);
        return command.ExecuteScalar() as string;
    }
}
