using Microsoft.Data.Sqlite;

using Jaunty.FlatFiles.DuckDB.Internals.Import;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R35-029. All three import dialects emitted NOT NULL only when the column was neither
/// nullable nor the primary key, on the assumption that PRIMARY KEY implies NOT NULL. It does on
/// PostgreSQL and SQL Server. It does not on SQLite: outside an INTEGER PRIMARY KEY rowid alias,
/// SQLite accepts NULLs in a PRIMARY KEY column - a documented bug it keeps for backwards
/// compatibility. A non-nullable string/Guid/ulong key therefore got <c>TEXT PRIMARY KEY</c> and a
/// source row with an empty key column imported as NULL.
///
/// <para>
/// Asserted by executing the DDL and inserting a NULL key, not by matching the SQL text: the
/// question is what SQLite does with the statement, which is exactly what a text assertion cannot
/// say.
/// </para>
/// </summary>
public class ImportDialectKeyNullabilityTests
{
    private static SqliteConnection OpenWith(string sql)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();

        return connection;
    }

    private static int InsertNullKey(SqliteConnection connection)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO t (Code, Name) VALUES (NULL, 'x')";
        return cmd.ExecuteNonQuery();
    }

    [Fact]
    public void SqliteRejectsANullTextKey()
    {
        string sql = SqliteImportDialect.Instance.GenerateCreateTableSql("t",
        [
            ("Code", typeof(string), true, false),
            ("Name", typeof(string), false, true),
        ]);

        using SqliteConnection connection = OpenWith(sql);

        SqliteException ex = Assert.Throws<SqliteException>(() => InsertNullKey(connection));
        Assert.Contains("NOT NULL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqliteRejectsANullGuidKey()
    {
        string sql = SqliteImportDialect.Instance.GenerateCreateTableSql("t",
        [
            ("Code", typeof(Guid), true, false),
            ("Name", typeof(string), false, true),
        ]);

        using SqliteConnection connection = OpenWith(sql);

        Assert.Throws<SqliteException>(() => InsertNullKey(connection));
    }

    /// <summary>
    /// The rowid alias must survive the change: NOT NULL on an INTEGER PRIMARY KEY does not stop the
    /// column being an alias for the rowid, so an omitted key still autoassigns.
    /// </summary>
    [Fact]
    public void AnIntegerKeyIsStillARowidAliasAndStillAutoassigns()
    {
        string sql = SqliteImportDialect.Instance.GenerateCreateTableSql("t",
        [
            ("Id", typeof(int), true, false),
            ("Name", typeof(string), false, true),
        ]);

        using SqliteConnection connection = OpenWith(sql);
        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO t (Name) VALUES ('a'); INSERT INTO t (Name) VALUES ('b'); SELECT group_concat(Id) FROM t";

        Assert.Equal("1,2", insert.ExecuteScalar()?.ToString());
    }

    /// <summary>
    /// A key the entity declares as nullable stays nullable. The fix keys off the property, not off
    /// the column being a key.
    /// </summary>
    [Fact]
    public void ANullableKeyStillAcceptsNull()
    {
        string sql = SqliteImportDialect.Instance.GenerateCreateTableSql("t",
        [
            ("Code", typeof(string), true, true),
            ("Name", typeof(string), false, true),
        ]);

        using SqliteConnection connection = OpenWith(sql);

        Assert.Equal(1, InsertNullKey(connection));
    }

    [Fact]
    public void ANonKeyColumnIsUnaffected()
    {
        string sql = SqliteImportDialect.Instance.GenerateCreateTableSql("t",
        [
            ("Code", typeof(string), true, false),
            ("Name", typeof(string), false, false),
        ]);

        Assert.Contains("\"Name\" TEXT NOT NULL", sql, StringComparison.Ordinal);
    }
}
