using System.Data;
using System.Data.Common;
using Extrode.Jaunty.Scaffolding.Abstractions;
using Extrode.Jaunty.Scaffolding.Providers.MySql;
using Extrode.Jaunty.Scaffolding.Schema;
using Extrode.Jaunty.Scaffolding.Tests.Helpers;
using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Unit;

public class MySqlSchemaReaderScriptedTests
{
    private static DataTable Tables(params string[] names) =>
        ReaderTables.Build(("SchemaName", typeof(string)), ("TableName", typeof(string)))
            .With(names.Select(n => new object?[] { "", n }).ToArray());

    private static DataTable Columns(params object?[][] rows) =>
        ReaderTables.Build(
                ("ColumnName", typeof(string)), ("DataType", typeof(string)), ("IsNullable", typeof(bool)),
                ("IsIdentity", typeof(bool)), ("IsComputed", typeof(bool)), ("MaxLength", typeof(ulong)),
                ("Precision", typeof(ulong)), ("Scale", typeof(ulong)), ("DefaultValue", typeof(string)),
                ("OrdinalPosition", typeof(uint)), ("ColumnType", typeof(string)))
            .With(rows);

    private static DataTable PrimaryKey(params (string Constraint, string Column)[] rows) =>
        ReaderTables.Build(("ConstraintName", typeof(string)), ("ColumnName", typeof(string)), ("KeyOrdinal", typeof(int)))
            .With(rows.Select((r, i) => new object?[] { r.Constraint, r.Column, i + 1 }).ToArray());

    private static DataTable ForeignKeys(params object?[][] rows) =>
        ReaderTables.Build(
                ("ConstraintName", typeof(string)), ("ForeignKeyColumn", typeof(string)), ("ReferencedSchema", typeof(string)),
                ("ReferencedTable", typeof(string)), ("ReferencedColumn", typeof(string)))
            .With(rows);

    private static readonly DataTable EmptyPrimaryKey = PrimaryKey();

    private static ScriptedConnection Connection(
        DataTable tables,
        Func<ScriptedCommand, DataTable>? columns = null,
        Func<ScriptedCommand, DataTable>? primaryKey = null,
        Func<ScriptedCommand, DataTable>? foreignKeys = null,
        ConnectionState state = ConnectionState.Closed,
        Func<int, bool>? completeAsync = null) =>
        new(command =>
        {
            string sql = command.CommandText;
            if (sql.Contains("REFERENCED_TABLE_NAME IS NOT NULL"))
                return (foreignKeys ?? (_ => ForeignKeys())).Invoke(command);
            if (sql.Contains("CONSTRAINT_NAME = 'PRIMARY'"))
                return (primaryKey ?? (_ => EmptyPrimaryKey)).Invoke(command);
            if (sql.Contains("FROM INFORMATION_SCHEMA.COLUMNS"))
                return (columns ?? (_ => Columns())).Invoke(command);
            return tables;
        }, state, "AppDb", completeAsync);

    private static Task<DatabaseSchema> Read(ScriptedConnection connection, SchemaReaderOptions? options = null, string connectionString = "cs") =>
        new MySqlSchemaReader(_ => connection).ReadSchemaAsync(connectionString, options ?? new SchemaReaderOptions());

    [Fact]
    public async Task ReadSchemaAsync_MapsTablesColumnsKeysAndDatabaseName()
    {
        ScriptedConnection connection = Connection(
            Tables("orders", "items"),
            columns: cmd => cmd.ParameterValues["@TableName"] as string == "orders"
                ? Columns(
                    ["id", "int", false, true, false, null, 10UL, 0UL, null, 1U, "int(11)"],
                    ["note", "varchar", true, false, false, 100UL, null, null, "'x'", 2U, "varchar(100)"],
                    ["total", "decimal", false, false, true, null, 18UL, 4UL, null, 3U, null],
                    ["body", "longtext", true, false, false, 4294967295UL, null, null, null, 4U, "longtext"])
                : Columns(["item_id", "int", false, false, false, null, null, null, null, 1U, null]),
            primaryKey: cmd => cmd.ParameterValues["@TableName"] as string == "orders"
                ? PrimaryKey(("PRIMARY", "id"))
                : PrimaryKey(),
            foreignKeys: cmd => cmd.ParameterValues["@TableName"] as string == "items"
                ? ForeignKeys(["fk_items_orders", "order_id", "", "orders", "id"], ["fk_items_other", "other_id", "otherdb", "other", "key"])
                : ForeignKeys());

        DatabaseSchema schema = await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true });

        Assert.Equal("AppDb", schema.DatabaseName);
        Assert.Equal(["orders", "items"], schema.Tables.Select(t => t.TableName));
        TableSchema orders = schema.Tables[0];
        Assert.Equal(string.Empty, orders.SchemaName);
        Assert.Equal(["id", "note", "total", "body"], orders.Columns.Select(c => c.ColumnName));

        ColumnSchema id = orders.Columns[0];
        Assert.Equal("int", id.DataType);
        Assert.False(id.IsNullable);
        Assert.True(id.IsIdentity);
        Assert.False(id.IsComputed);
        Assert.True(id.IsPrimaryKey);
        Assert.Null(id.MaxLength);
        Assert.Equal(10, id.Precision);
        Assert.Equal(0, id.Scale);
        Assert.Null(id.DefaultValue);
        Assert.Equal(1, id.OrdinalPosition);
        Assert.Equal("int(11)", id.ColumnType);

        ColumnSchema note = orders.Columns[1];
        Assert.True(note.IsNullable);
        Assert.False(note.IsIdentity);
        Assert.False(note.IsPrimaryKey);
        Assert.Equal(100, note.MaxLength);
        Assert.Null(note.Precision);
        Assert.Null(note.Scale);
        Assert.Equal("'x'", note.DefaultValue);
        Assert.Equal(2, note.OrdinalPosition);
        Assert.Equal("varchar(100)", note.ColumnType);

        ColumnSchema total = orders.Columns[2];
        Assert.True(total.IsComputed);
        Assert.Equal(18, total.Precision);
        Assert.Equal(4, total.Scale);
        Assert.Equal(3, total.OrdinalPosition);
        Assert.Null(total.ColumnType);

        Assert.Equal(int.MaxValue, orders.Columns[3].MaxLength);

        Assert.NotNull(orders.PrimaryKey);
        Assert.Equal("PRIMARY", orders.PrimaryKey!.ConstraintName);
        Assert.Equal(["id"], orders.PrimaryKey.Columns);
        Assert.Empty(orders.ForeignKeys);

        TableSchema items = schema.Tables[1];
        Assert.Null(items.PrimaryKey);
        Assert.Equal(2, items.ForeignKeys.Count);
        Assert.Equal("fk_items_orders", items.ForeignKeys[0].ConstraintName);
        Assert.Equal("order_id", items.ForeignKeys[0].ForeignKeyColumn);
        Assert.Equal("", items.ForeignKeys[0].ReferencedSchema);
        Assert.Equal("orders", items.ForeignKeys[0].ReferencedTable);
        Assert.Equal("id", items.ForeignKeys[0].ReferencedColumn);
        Assert.Equal("otherdb", items.ForeignKeys[1].ReferencedSchema);
        Assert.Equal("key", items.ForeignKeys[1].ReferencedColumn);
    }

    [Fact]
    public async Task ReadSchemaAsync_NullReferencedSchema_StaysNull()
    {
        ScriptedConnection connection = Connection(
            Tables("t"),
            foreignKeys: _ => ForeignKeys(["fk", "a", null, "u", "id"]));

        DatabaseSchema schema = await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true });

        Assert.Null(schema.Tables[0].ForeignKeys[0].ReferencedSchema);
    }

    [Fact]
    public async Task ReadSchemaAsync_CompositePrimaryKey_KeepsFirstConstraintNameAndOrder()
    {
        ScriptedConnection connection = Connection(
            Tables("link"),
            columns: _ => Columns(
                ["a", "int", false, false, false, null, null, null, null, 1U, null],
                ["b", "int", false, false, false, null, null, null, null, 2U, null]),
            primaryKey: _ => PrimaryKey(("PRIMARY", "a"), ("PRIMARY", "b")));

        DatabaseSchema schema = await Read(connection);

        PrimaryKeyInfo key = schema.Tables[0].PrimaryKey!;
        Assert.Equal("PRIMARY", key.ConstraintName);
        Assert.Equal(["a", "b"], key.Columns);
        Assert.All(schema.Tables[0].Columns, c => Assert.True(c.IsPrimaryKey));
    }

    [Fact]
    public async Task ReadSchemaAsync_PrimaryKeyRowsNamingDifferentConstraints_KeepsTheFirstName()
    {
        ScriptedConnection connection = Connection(
            Tables("t"),
            columns: _ => Columns(["a", "int", false, false, false, null, null, null, null, 1U, null]),
            primaryKey: _ => PrimaryKey(("PK_First", "a"), ("PK_Second", "b")));

        DatabaseSchema schema = await Read(connection);

        Assert.Equal("PK_First", schema.Tables[0].PrimaryKey!.ConstraintName);
    }

    [Fact]
    public async Task ReadSchemaAsync_SendsTheTableParameterOnEveryCommand()
    {
        ScriptedConnection connection = Connection(Tables("items"));

        await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true });

        List<ScriptedCommand> perTable = connection.Commands.Skip(1).ToList();
        Assert.Equal(3, perTable.Count);
        Assert.All(perTable, c =>
        {
            KeyValuePair<string, object?> only = Assert.Single(c.ParameterValues);
            Assert.Equal("@TableName", only.Key);
            Assert.Equal("items", only.Value);
        });
    }

    [Fact]
    public async Task ReadSchemaAsync_ForeignKeysDisabled_NeverQueriesThem()
    {
        ScriptedConnection connection = Connection(Tables("t"));

        DatabaseSchema schema = await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = false });

        Assert.DoesNotContain(connection.Commands, c => c.CommandText == MySqlSchemaReader.ForeignKeysSql);
        Assert.Empty(schema.Tables[0].ForeignKeys);
    }

    [Fact]
    public async Task ReadSchemaAsync_ForeignKeysEnabled_QueriesThem()
    {
        ScriptedConnection connection = Connection(Tables("t"));

        await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true });

        Assert.Contains(connection.Commands, c => c.CommandText == MySqlSchemaReader.ForeignKeysSql);
    }

    [Fact]
    public async Task ReadSchemaAsync_IssuesEachQueryWithItsOwnText()
    {
        ScriptedConnection connection = Connection(Tables("t"));

        await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true });

        Assert.Equal(4, connection.Commands.Count);
        Assert.Contains("FROM INFORMATION_SCHEMA.TABLES", connection.Commands[0].CommandText);
        Assert.Contains("FROM INFORMATION_SCHEMA.COLUMNS", connection.Commands[1].CommandText);
        Assert.Contains("CONSTRAINT_NAME = 'PRIMARY'", connection.Commands[2].CommandText);
        Assert.Equal(MySqlSchemaReader.ForeignKeysSql, connection.Commands[3].CommandText);
    }

    public static TheoryData<SchemaReaderOptions, string[]> FilterCases => new()
    {
        { new SchemaReaderOptions(), ["a", "b", "c"] },
        { new SchemaReaderOptions { IncludeSchemas = [], IncludeTables = [], ExcludeTables = [] }, ["a", "b", "c"] },
        { new SchemaReaderOptions { IncludeSchemas = ["APPDB"] }, ["a", "b", "c"] },
        { new SchemaReaderOptions { IncludeTables = ["B"] }, ["b"] },
        { new SchemaReaderOptions { ExcludeTables = ["B"] }, ["a", "c"] },
        { new SchemaReaderOptions { IncludeTables = ["a", "b"], ExcludeTables = ["A"] }, ["b"] },
    };

    [Theory]
    [MemberData(nameof(FilterCases))]
    public async Task ReadSchemaAsync_AppliesTheFilters(SchemaReaderOptions options, string[] expected)
    {
        ScriptedConnection connection = Connection(Tables("a", "b", "c"));

        DatabaseSchema schema = await Read(connection, options);

        Assert.Equal(expected, schema.Tables.Select(t => t.TableName));
    }

    [Fact]
    public async Task ReadSchemaAsync_SchemaFilterExcludingTheDatabase_ReturnsNoTablesAndRunsNoQuery()
    {
        ScriptedConnection connection = Connection(Tables("a", "b"));

        DatabaseSchema schema = await Read(connection, new SchemaReaderOptions { IncludeSchemas = ["otherdb"] });

        Assert.Empty(schema.Tables);
        Assert.Empty(connection.Commands);
        Assert.Equal("AppDb", schema.DatabaseName);
    }

    [Fact]
    public async Task ReadSchemaAsync_ClosedConnection_OpensItOnce()
    {
        ScriptedConnection connection = Connection(Tables("t"));

        await Read(connection);

        Assert.Equal(1, connection.OpenCalls);
    }

    [Fact]
    public async Task ReadSchemaAsync_OpenConnection_DoesNotReopenIt()
    {
        ScriptedConnection connection = Connection(Tables("t"), state: ConnectionState.Open);

        await Read(connection);

        Assert.Equal(0, connection.OpenCalls);
    }

    [Fact]
    public async Task ReadSchemaAsync_DisposesTheConnection()
    {
        ScriptedConnection connection = Connection(Tables());

        await Read(connection);

        Assert.True(connection.Disposed);
    }

    [Fact]
    public async Task ReadSchemaAsync_PassesTheConnectionStringToTheFactory()
    {
        ScriptedConnection connection = Connection(Tables());
        string? received = null;
        var reader = new MySqlSchemaReader(cs =>
        {
            received = cs;
            return connection;
        });

        await reader.ReadSchemaAsync("Server=x;Database=y", new SchemaReaderOptions());

        Assert.Equal("Server=x;Database=y", received);
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

            DatabaseSchema schema = CountingContext.Run(context, () => Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true }));

            Assert.Single(schema.Tables);
            Assert.True(context.Posts == 0, $"await point {point} posted {context.Posts} time(s) to the caller's context");
        }
    }

    private static int CountPoints()
    {
        ScriptedConnection connection = FullConnection(completeAsync: null);
        Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true }).GetAwaiter().GetResult();
        return connection.AsyncPoints;
    }

    private static ScriptedConnection FullConnection(Func<int, bool>? completeAsync) =>
        Connection(
            Tables("t"),
            columns: _ => Columns(
                ["a", "int", false, false, false, null, null, null, null, 1U, null],
                ["b", "int", false, false, false, null, null, null, null, 2U, null]),
            primaryKey: _ => PrimaryKey(("PRIMARY", "a"), ("PRIMARY", "b")),
            foreignKeys: _ => ForeignKeys(["fk", "a", "", "u", "id"], ["fk2", "b", "", "u", "id2"]),
            completeAsync: completeAsync);

    [Fact]
    public void CreateConnection_ReturnsTheMySqlConnectorConnectionWithTheConnectionString()
    {
        using DbConnection connection = MySqlSchemaReader.CreateConnection("Server=example;Database=probe");

        Assert.Equal("MySqlConnector.MySqlConnection", connection.GetType().FullName);
        Assert.Equal("probe", connection.Database);
        Assert.Contains("example", connection.DataSource);
    }
}
