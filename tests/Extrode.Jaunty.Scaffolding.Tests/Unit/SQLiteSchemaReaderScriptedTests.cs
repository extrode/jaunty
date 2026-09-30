using System.Data;
using System.Data.Common;
using Extrode.Jaunty.Scaffolding.Abstractions;
using Extrode.Jaunty.Scaffolding.Providers.SQLite;
using Extrode.Jaunty.Scaffolding.Schema;
using Extrode.Jaunty.Scaffolding.Tests.Helpers;
using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Unit;

public class SQLiteSchemaReaderScriptedTests
{
    private sealed class FakeTable
    {
        public string? CreateSql { get; init; }

        public object?[][] Columns { get; init; } = [];

        public object?[][] ForeignKeys { get; init; } = [];
    }

    private static object?[] Col(int cid, string name, string? type, int notNull, string? dflt, int pk, int hidden = 0) =>
        [cid, name, type, notNull, dflt, pk, hidden];

    private static object?[] Fk(int seq, string table, string from, string? to) =>
        [0, seq, table, from, to, "NO ACTION", "NO ACTION", "NONE"];

    private static string NameIn(string sql)
    {
        int start = sql.IndexOf('\'');
        int end = sql.LastIndexOf('\'');
        return sql[(start + 1)..end].Replace("''", "'");
    }

    private static DataTable ColumnTable(object?[][] rows, bool includeHidden)
    {
        DataTable table = includeHidden
            ? ReaderTables.Build(
                ("cid", typeof(int)), ("name", typeof(string)), ("type", typeof(string)), ("notnull", typeof(int)),
                ("dflt_value", typeof(string)), ("pk", typeof(int)), ("hidden", typeof(int)))
            : ReaderTables.Build(
                ("cid", typeof(int)), ("name", typeof(string)), ("type", typeof(string)), ("notnull", typeof(int)),
                ("dflt_value", typeof(string)), ("pk", typeof(int)));
        return table.With(includeHidden ? rows : rows.Select(r => r[..6]).ToArray());
    }

    private static ScriptedConnection Connection(
        Dictionary<string, FakeTable> db,
        ConnectionState state = ConnectionState.Closed,
        Func<int, bool>? completeAsync = null) =>
        new(command =>
        {
            string sql = command.CommandText;
            if (sql.StartsWith("PRAGMA table_xinfo(", StringComparison.Ordinal))
                return ColumnTable(db[NameIn(sql)].Columns, includeHidden: true);
            if (sql.StartsWith("PRAGMA table_info(", StringComparison.Ordinal))
                return ColumnTable(db[NameIn(sql)].Columns, includeHidden: false);
            if (sql.StartsWith("PRAGMA foreign_key_list(", StringComparison.Ordinal))
                return ReaderTables.Build(
                        ("id", typeof(int)), ("seq", typeof(int)), ("table", typeof(string)), ("from", typeof(string)),
                        ("to", typeof(string)), ("on_update", typeof(string)), ("on_delete", typeof(string)), ("match", typeof(string)))
                    .With(db[NameIn(sql)].ForeignKeys);
            if (sql.Contains("SELECT sql FROM sqlite_master"))
                return ReaderTables.Build(("sql", typeof(string)))
                    .With([[db[(string)command.ParameterValues["@TableName"]!].CreateSql]]);
            return ReaderTables.Build(("name", typeof(string))).With(db.Keys.Select(k => new object?[] { k }).ToArray());
        }, state, "main", completeAsync);

    private static Task<DatabaseSchema> Read(
        ScriptedConnection connection,
        SchemaReaderOptions? options = null,
        string connectionString = "Data Source=/data/app.db") =>
        new SQLiteSchemaReader(_ => connection).ReadSchemaAsync(connectionString, options ?? new SchemaReaderOptions());

    private static Dictionary<string, FakeTable> Simple(params string[] names) =>
        names.ToDictionary(n => n, _ => new FakeTable { Columns = [Col(0, "id", "TEXT", 0, null, 0)] });

    [Fact]
    public async Task ReadSchemaAsync_MapsColumnsKeysForeignKeysAndDatabaseName()
    {
        var db = new Dictionary<string, FakeTable>
        {
            ["orders"] = new()
            {
                CreateSql = "CREATE TABLE orders (id INTEGER PRIMARY KEY, note TEXT NOT NULL DEFAULT 'x', v TEXT, gen TEXT GENERATED ALWAYS AS (note) STORED)",
                Columns =
                [
                    Col(0, "id", "INTEGER", 0, null, 1),
                    Col(1, "note", "TEXT", 1, "'x'", 0),
                    Col(2, "vt", "TEXT", 0, null, 0, hidden: 1),
                    Col(3, "v", null, 0, null, 0),
                    Col(4, "gen", "TEXT", 0, null, 0, hidden: 3),
                    Col(5, "gen2", "TEXT", 0, null, 0, hidden: 2)
                ]
            },
            ["items"] = new()
            {
                Columns = [Col(0, "order_id", "integer", 0, null, 0)],
                ForeignKeys = [Fk(0, "orders", "order_id", "id")]
            }
        };

        DatabaseSchema schema = await Read(Connection(db), new SchemaReaderOptions { IncludeForeignKeys = true });

        Assert.Equal("app", schema.DatabaseName);
        Assert.Equal(["orders", "items"], schema.Tables.Select(t => t.TableName));
        TableSchema orders = schema.Tables[0];
        Assert.Equal(string.Empty, orders.SchemaName);
        Assert.Equal(["id", "note", "v", "gen", "gen2"], orders.Columns.Select(c => c.ColumnName));
        Assert.Equal([1, 2, 3, 4, 5], orders.Columns.Select(c => c.OrdinalPosition));

        ColumnSchema id = orders.Columns[0];
        Assert.Equal("INTEGER", id.DataType);
        Assert.True(id.IsPrimaryKey);
        Assert.True(id.IsIdentity);
        Assert.False(id.IsNullable);
        Assert.False(id.IsComputed);
        Assert.Null(id.DefaultValue);

        ColumnSchema note = orders.Columns[1];
        Assert.False(note.IsNullable);
        Assert.False(note.IsPrimaryKey);
        Assert.False(note.IsIdentity);
        Assert.Equal("'x'", note.DefaultValue);

        ColumnSchema v = orders.Columns[2];
        Assert.True(v.IsNullable);
        Assert.False(v.IsComputed);

        Assert.True(orders.Columns[3].IsComputed);
        Assert.True(orders.Columns[4].IsComputed);

        Assert.Equal("pk_orders", orders.PrimaryKey!.ConstraintName);
        Assert.Equal(["id"], orders.PrimaryKey.Columns);
        Assert.Empty(orders.ForeignKeys);

        TableSchema items = schema.Tables[1];
        Assert.Null(items.PrimaryKey);
        ForeignKeyInfo fk = Assert.Single(items.ForeignKeys);
        Assert.Equal("fk_items_order_id", fk.ConstraintName);
        Assert.Equal("order_id", fk.ForeignKeyColumn);
        Assert.Null(fk.ReferencedSchema);
        Assert.Equal("orders", fk.ReferencedTable);
        Assert.Equal("id", fk.ReferencedColumn);
    }

    [Theory]
    [InlineData("INTEGER", "CREATE TABLE t (id INTEGER PRIMARY KEY)", true, false)]
    [InlineData("integer", "CREATE TABLE t (id integer PRIMARY KEY)", true, false)]
    [InlineData("INT", "CREATE TABLE t (id INT PRIMARY KEY)", false, true)]
    [InlineData("TEXT", "CREATE TABLE t (id TEXT PRIMARY KEY)", false, true)]
    [InlineData("INTEGER", "CREATE TABLE t (id INTEGER PRIMARY KEY) WITHOUT ROWID", false, false)]
    [InlineData("TEXT", "CREATE TABLE t (id TEXT PRIMARY KEY) WITHOUT ROWID", false, false)]
    [InlineData("INTEGER", null, true, false)]
    public async Task ReadSchemaAsync_SingleColumnKey_IdentityAndNullability(
        string type, string? createSql, bool identity, bool nullable)
    {
        var db = new Dictionary<string, FakeTable>
        {
            ["t"] = new() { CreateSql = createSql, Columns = [Col(0, "id", type, 0, null, 1)] }
        };

        DatabaseSchema schema = await Read(Connection(db));

        ColumnSchema id = schema.Tables[0].Columns[0];
        Assert.Equal(identity, id.IsIdentity);
        Assert.Equal(nullable, id.IsNullable);
    }

    [Fact]
    public async Task ReadSchemaAsync_WithoutRowIdTable_KeepsNonKeyColumnsNullable()
    {
        var db = new Dictionary<string, FakeTable>
        {
            ["t"] = new()
            {
                CreateSql = "CREATE TABLE t (id TEXT PRIMARY KEY, v TEXT, w TEXT NOT NULL) WITHOUT ROWID",
                Columns = [Col(0, "id", "TEXT", 0, null, 1), Col(1, "v", "TEXT", 0, null, 0), Col(2, "w", "TEXT", 1, null, 0)]
            }
        };

        DatabaseSchema schema = await Read(Connection(db));

        Assert.Equal([false, true, false], schema.Tables[0].Columns.Select(c => c.IsNullable));
    }

    [Fact]
    public async Task ReadSchemaAsync_CompositeKey_ReportsColumnsInKeyOrderAndNoIdentity()
    {
        var db = new Dictionary<string, FakeTable>
        {
            ["t"] = new()
            {
                Columns = [Col(0, "a", "INTEGER", 0, null, 2), Col(1, "b", "INTEGER", 0, null, 1), Col(2, "c", "TEXT", 0, null, 0)]
            }
        };

        DatabaseSchema schema = await Read(Connection(db));

        TableSchema table = schema.Tables[0];
        Assert.Equal(["b", "a"], table.PrimaryKey!.Columns);
        Assert.All(table.Columns.Take(2), c =>
        {
            Assert.True(c.IsPrimaryKey);
            Assert.False(c.IsIdentity);
            Assert.True(c.IsNullable);
        });
        Assert.False(table.Columns[2].IsPrimaryKey);
    }

    [Fact]
    public async Task ReadSchemaAsync_NoKeyColumns_HasNoPrimaryKey()
    {
        DatabaseSchema schema = await Read(Connection(Simple("t")));

        Assert.Null(schema.Tables[0].PrimaryKey);
    }

    [Fact]
    public async Task ReadSchemaAsync_ImplicitForeignKeyTarget_ResolvesThroughTheParentKeyInOrder()
    {
        var db = new Dictionary<string, FakeTable>
        {
            ["parent"] = new()
            {
                Columns = [Col(0, "x", "TEXT", 0, null, 2), Col(1, "y", "TEXT", 0, null, 1), Col(2, "z", "TEXT", 0, null, 0)]
            },
            ["child"] = new()
            {
                Columns = [Col(0, "a", "TEXT", 0, null, 0), Col(1, "b", "TEXT", 0, null, 0)],
                ForeignKeys = [Fk(0, "parent", "a", null), Fk(1, "Parent", "b", null)]
            }
        };
        ScriptedConnection connection = Connection(db);

        DatabaseSchema schema = await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true, IncludeTables = ["child"] });

        IReadOnlyList<ForeignKeyInfo> fks = schema.Tables.Single().ForeignKeys;
        Assert.Equal(["y", "x"], fks.Select(f => f.ReferencedColumn));
        Assert.Equal(["parent", "Parent"], fks.Select(f => f.ReferencedTable));
        Assert.Equal(1, connection.Commands.Count(c => c.CommandText.StartsWith("PRAGMA table_info(", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ReadSchemaAsync_ImplicitForeignKeyTarget_DropsRowsBeyondTheParentKey()
    {
        var db = new Dictionary<string, FakeTable>
        {
            ["parent"] = new() { Columns = [Col(0, "id", "TEXT", 0, null, 1)] },
            ["keyless"] = new() { Columns = [Col(0, "id", "TEXT", 0, null, 0)] },
            ["child"] = new()
            {
                Columns = [Col(0, "a", "TEXT", 0, null, 0)],
                ForeignKeys = [Fk(0, "parent", "a", null), Fk(1, "parent", "b", null), Fk(0, "keyless", "c", null)]
            }
        };

        DatabaseSchema schema = await Read(Connection(db), new SchemaReaderOptions { IncludeForeignKeys = true, IncludeTables = ["child"] });

        ForeignKeyInfo only = Assert.Single(schema.Tables.Single().ForeignKeys);
        Assert.Equal("a", only.ForeignKeyColumn);
        Assert.Equal("id", only.ReferencedColumn);
    }

    [Fact]
    public async Task ReadSchemaAsync_ExplicitForeignKeyTargets_IssueNoParentLookup()
    {
        var db = new Dictionary<string, FakeTable>
        {
            ["parent"] = new() { Columns = [Col(0, "id", "TEXT", 0, null, 1)] },
            ["child"] = new()
            {
                Columns = [Col(0, "a", "TEXT", 0, null, 0)],
                ForeignKeys = [Fk(0, "parent", "a", "id")]
            }
        };
        ScriptedConnection connection = Connection(db);

        await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true, IncludeTables = ["child"] });

        Assert.DoesNotContain(connection.Commands, c => c.CommandText.StartsWith("PRAGMA table_info(", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadSchemaAsync_ForeignKeysDisabled_NeverQueriesThem()
    {
        var db = new Dictionary<string, FakeTable>
        {
            ["t"] = new() { Columns = [Col(0, "a", "TEXT", 0, null, 0)], ForeignKeys = [Fk(0, "t", "a", "a")] }
        };
        ScriptedConnection connection = Connection(db);

        DatabaseSchema schema = await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = false });

        Assert.DoesNotContain(connection.Commands, c => c.CommandText.StartsWith("PRAGMA foreign_key_list(", StringComparison.Ordinal));
        Assert.Empty(schema.Tables[0].ForeignKeys);
    }

    [Fact]
    public async Task ReadSchemaAsync_TableNameWithQuote_IsEscapedInPragmaAndRawInTheParameter()
    {
        var db = new Dictionary<string, FakeTable> { ["order's notes"] = new() { Columns = [Col(0, "id", "TEXT", 0, null, 0)] } };
        ScriptedConnection connection = Connection(db);

        await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true });

        Assert.Contains(connection.Commands, c => c.CommandText == "PRAGMA table_xinfo('order''s notes')");
        Assert.Contains(connection.Commands, c => c.CommandText == "PRAGMA foreign_key_list('order''s notes')");
        ScriptedCommand createSql = connection.Commands.Single(c => c.CommandText.Contains("SELECT sql FROM sqlite_master"));
        Assert.Equal("order's notes", createSql.ParameterValues["@TableName"]);
        Assert.Single(createSql.ParameterValues);
    }

    public static TheoryData<SchemaReaderOptions, string[]> FilterCases => new()
    {
        { new SchemaReaderOptions(), ["a", "b", "c"] },
        { new SchemaReaderOptions { IncludeTables = [], ExcludeTables = [] }, ["a", "b", "c"] },
        { new SchemaReaderOptions { IncludeTables = ["B"] }, ["b"] },
        { new SchemaReaderOptions { ExcludeTables = ["B"] }, ["a", "c"] },
        { new SchemaReaderOptions { IncludeTables = ["a", "b"], ExcludeTables = ["A"] }, ["b"] },
    };

    [Theory]
    [MemberData(nameof(FilterCases))]
    public async Task ReadSchemaAsync_AppliesTheFilters(SchemaReaderOptions options, string[] expected)
    {
        DatabaseSchema schema = await Read(Connection(Simple("a", "b", "c")), options);

        Assert.Equal(expected, schema.Tables.Select(t => t.TableName));
    }

    [Fact]
    public async Task ReadSchemaAsync_ClosedConnection_OpensItOnce()
    {
        ScriptedConnection connection = Connection(Simple("t"));

        await Read(connection);

        Assert.Equal(1, connection.OpenCalls);
    }

    [Fact]
    public async Task ReadSchemaAsync_OpenConnection_DoesNotReopenIt()
    {
        ScriptedConnection connection = Connection(Simple("t"), state: ConnectionState.Open);

        await Read(connection);

        Assert.Equal(0, connection.OpenCalls);
    }

    [Fact]
    public async Task ReadSchemaAsync_DisposesTheConnection()
    {
        ScriptedConnection connection = Connection(Simple());

        await Read(connection);

        Assert.True(connection.Disposed);
    }

    [Fact]
    public async Task ReadSchemaAsync_PassesTheConnectionStringToTheFactory()
    {
        ScriptedConnection connection = Connection(Simple());
        string? received = null;
        var reader = new SQLiteSchemaReader(cs =>
        {
            received = cs;
            return connection;
        });

        await reader.ReadSchemaAsync("Data Source=x.db", new SchemaReaderOptions());

        Assert.Equal("Data Source=x.db", received);
    }

    [Fact]
    public void ReadSchemaAsync_NeverPostsBackToTheCallersContext_AtAnyAwaitPoint()
    {
        int points = CountPoints();
        Assert.True(points > 10);

        for (int point = 0; point < points; point++)
        {
            int target = point;
            var context = new CountingContext();
            ScriptedConnection connection = FullConnection(completeAsync: i => i == target);

            DatabaseSchema schema = CountingContext.Run(context, () => Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true, IncludeTables = ["child"] }));

            Assert.Single(schema.Tables);
            Assert.True(context.Posts == 0, $"await point {point} posted {context.Posts} time(s) to the caller's context");
        }
    }

    private static int CountPoints()
    {
        ScriptedConnection connection = FullConnection(completeAsync: null);
        Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true, IncludeTables = ["child"] }).GetAwaiter().GetResult();
        return connection.AsyncPoints;
    }

    private static ScriptedConnection FullConnection(Func<int, bool>? completeAsync) =>
        Connection(
            new Dictionary<string, FakeTable>
            {
                ["parent"] = new()
                {
                    CreateSql = "CREATE TABLE parent (id INTEGER PRIMARY KEY)",
                    Columns = [Col(0, "id", "INTEGER", 0, null, 1)]
                },
                ["child"] = new()
                {
                    CreateSql = "CREATE TABLE child (a TEXT, b TEXT)",
                    Columns = [Col(0, "a", "TEXT", 0, null, 0), Col(1, "b", "TEXT", 0, null, 0)],
                    ForeignKeys = [Fk(0, "parent", "a", null), Fk(0, "parent", "b", "id")]
                }
            },
            completeAsync: completeAsync);

    [Fact]
    public void CreateConnection_ReturnsTheSqliteConnectionWithTheConnectionString()
    {
        using DbConnection connection = SQLiteSchemaReader.CreateConnection("Data Source=probe.db");

        Assert.Equal("Microsoft.Data.Sqlite.SqliteConnection", connection.GetType().FullName);
        Assert.Contains("probe.db", connection.DataSource);
    }
}
