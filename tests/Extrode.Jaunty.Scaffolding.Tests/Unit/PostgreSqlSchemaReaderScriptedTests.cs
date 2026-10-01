using System.Data;
using System.Data.Common;
using Extrode.Jaunty.Scaffolding.Abstractions;
using Extrode.Jaunty.Scaffolding.Providers.PostgreSql;
using Extrode.Jaunty.Scaffolding.Schema;
using Extrode.Jaunty.Scaffolding.Tests.Helpers;
using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Unit;

public class PostgreSqlSchemaReaderScriptedTests
{
    private static DataTable Tables(params (string Schema, string Table)[] rows) =>
        ReaderTables.Build(("SchemaName", typeof(string)), ("TableName", typeof(string)))
            .With(rows.Select(r => new object?[] { r.Schema, r.Table }).ToArray());

    private static DataTable Columns(params object?[][] rows) =>
        ReaderTables.Build(
                ("ColumnName", typeof(string)), ("DataType", typeof(string)), ("IsNullable", typeof(bool)),
                ("IsIdentity", typeof(bool)), ("IsComputed", typeof(bool)), ("MaxLength", typeof(int)),
                ("Precision", typeof(int)), ("Scale", typeof(int)), ("DefaultValue", typeof(string)),
                ("OrdinalPosition", typeof(int)))
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
            if (sql.Contains("contype = 'f'"))
                return (foreignKeys ?? (_ => ForeignKeys())).Invoke(command);
            if (sql.Contains("contype = 'p'"))
                return (primaryKey ?? (_ => EmptyPrimaryKey)).Invoke(command);
            if (sql.Contains("FROM information_schema.columns"))
                return (columns ?? (_ => Columns())).Invoke(command);
            return tables;
        }, state, "AppDb", completeAsync);

    private static Task<DatabaseSchema> Read(ScriptedConnection connection, SchemaReaderOptions? options = null, string connectionString = "cs") =>
        new PostgreSqlSchemaReader(_ => connection).ReadSchemaAsync(connectionString, options ?? new SchemaReaderOptions());

    [Fact]
    public async Task ReadSchemaAsync_MapsTablesColumnsKeysAndDatabaseName()
    {
        ScriptedConnection connection = Connection(
            Tables(("dbo", "Orders"), ("sales", "Items")),
            columns: cmd => cmd.ParameterValues["@TableName"] as string == "Orders"
                ? Columns(
                    ["Id", "int", false, true, false, null, 10, 0, null, 1],
                    ["Note", "nvarchar", true, false, false, 100, null, null, "('x')", 2],
                    ["Total", "decimal", false, false, true, 9, 18, 4, null, 3])
                : Columns(["ItemId", "int", false, false, false, null, null, null, null, 1]),
            primaryKey: cmd => cmd.ParameterValues["@TableName"] as string == "Orders"
                ? PrimaryKey(("PK_Orders", "Id"))
                : PrimaryKey(),
            foreignKeys: cmd => cmd.ParameterValues["@TableName"] as string == "Items"
                ? ForeignKeys(["FK_Items_Orders", "OrderId", "dbo", "Orders", "Id"], ["FK_Items_Other", "OtherId", null, "Other", "Key"])
                : ForeignKeys());

        DatabaseSchema schema = await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true });

        Assert.Equal("AppDb", schema.DatabaseName);
        Assert.Equal(["Orders", "Items"], schema.Tables.Select(t => t.TableName));
        TableSchema orders = schema.Tables[0];
        Assert.Equal("dbo", orders.SchemaName);
        Assert.Equal(["Id", "Note", "Total"], orders.Columns.Select(c => c.ColumnName));

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

        ColumnSchema note = orders.Columns[1];
        Assert.True(note.IsNullable);
        Assert.False(note.IsIdentity);
        Assert.False(note.IsPrimaryKey);
        Assert.Equal(100, note.MaxLength);
        Assert.Null(note.Precision);
        Assert.Null(note.Scale);
        Assert.Equal("('x')", note.DefaultValue);
        Assert.Equal(2, note.OrdinalPosition);

        ColumnSchema total = orders.Columns[2];
        Assert.True(total.IsComputed);
        Assert.Equal(9, total.MaxLength);
        Assert.Equal(18, total.Precision);
        Assert.Equal(4, total.Scale);
        Assert.Equal(3, total.OrdinalPosition);

        Assert.NotNull(orders.PrimaryKey);
        Assert.Equal("PK_Orders", orders.PrimaryKey!.ConstraintName);
        Assert.Equal(["Id"], orders.PrimaryKey.Columns);
        Assert.Empty(orders.ForeignKeys);

        TableSchema items = schema.Tables[1];
        Assert.Equal("sales", items.SchemaName);
        Assert.Null(items.PrimaryKey);
        Assert.Equal(2, items.ForeignKeys.Count);
        Assert.Equal("FK_Items_Orders", items.ForeignKeys[0].ConstraintName);
        Assert.Equal("OrderId", items.ForeignKeys[0].ForeignKeyColumn);
        Assert.Equal("dbo", items.ForeignKeys[0].ReferencedSchema);
        Assert.Equal("Orders", items.ForeignKeys[0].ReferencedTable);
        Assert.Equal("Id", items.ForeignKeys[0].ReferencedColumn);
        Assert.Null(items.ForeignKeys[1].ReferencedSchema);
        Assert.Equal("Key", items.ForeignKeys[1].ReferencedColumn);
    }

    [Fact]
    public async Task ReadSchemaAsync_CompositePrimaryKey_KeepsFirstConstraintNameAndOrder()
    {
        ScriptedConnection connection = Connection(
            Tables(("dbo", "Link")),
            columns: _ => Columns(
                ["A", "int", false, false, false, null, null, null, null, 1],
                ["B", "int", false, false, false, null, null, null, null, 2]),
            primaryKey: _ => PrimaryKey(("PK_Link", "A"), ("PK_Link", "B")));

        DatabaseSchema schema = await Read(connection);

        PrimaryKeyInfo key = schema.Tables[0].PrimaryKey!;
        Assert.Equal("PK_Link", key.ConstraintName);
        Assert.Equal(["A", "B"], key.Columns);
        Assert.All(schema.Tables[0].Columns, c => Assert.True(c.IsPrimaryKey));
    }

    [Fact]
    public async Task ReadSchemaAsync_PrimaryKeyRowsNamingDifferentConstraints_KeepsTheFirstName()
    {
        ScriptedConnection connection = Connection(
            Tables(("dbo", "T")),
            columns: _ => Columns(["A", "int", false, false, false, null, null, null, null, 1]),
            primaryKey: _ => PrimaryKey(("PK_First", "A"), ("PK_Second", "B")));

        DatabaseSchema schema = await Read(connection);

        Assert.Equal("PK_First", schema.Tables[0].PrimaryKey!.ConstraintName);
    }

    [Fact]
    public async Task ReadSchemaAsync_SendsSchemaAndTableParametersOnEveryCommand()
    {
        ScriptedConnection connection = Connection(Tables(("sales", "Items")));

        await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true });

        List<ScriptedCommand> perTable = connection.Commands.Skip(1).ToList();
        Assert.Equal(3, perTable.Count);
        Assert.All(perTable, c =>
        {
            Assert.Equal(2, c.ParameterValues.Count);
            Assert.Equal("sales", c.ParameterValues["@SchemaName"]);
            Assert.Equal("Items", c.ParameterValues["@TableName"]);
        });
    }

    [Fact]
    public async Task ReadSchemaAsync_ForeignKeysDisabled_NeverQueriesThem()
    {
        ScriptedConnection connection = Connection(Tables(("dbo", "T")));

        DatabaseSchema schema = await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = false });

        Assert.DoesNotContain(connection.Commands, c => c.CommandText == PostgreSqlSchemaReader.ForeignKeysSql);
        Assert.Empty(schema.Tables[0].ForeignKeys);
    }

    [Fact]
    public async Task ReadSchemaAsync_ForeignKeysEnabled_QueriesThem()
    {
        ScriptedConnection connection = Connection(Tables(("dbo", "T")));

        await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true });

        Assert.Contains(connection.Commands, c => c.CommandText == PostgreSqlSchemaReader.ForeignKeysSql);
    }

    [Fact]
    public async Task ReadSchemaAsync_IssuesEachQueryWithItsOwnText()
    {
        ScriptedConnection connection = Connection(Tables(("dbo", "T")));

        await Read(connection, new SchemaReaderOptions { IncludeForeignKeys = true });

        Assert.Equal(4, connection.Commands.Count);
        Assert.Contains("FROM information_schema.tables", connection.Commands[0].CommandText);
        Assert.Contains("FROM information_schema.columns c", connection.Commands[1].CommandText);
        Assert.Equal(PostgreSqlSchemaReader.PrimaryKeysSql, connection.Commands[2].CommandText);
        Assert.Equal(PostgreSqlSchemaReader.ForeignKeysSql, connection.Commands[3].CommandText);
    }

    public static TheoryData<SchemaReaderOptions, string[]> FilterCases => new()
    {
        { new SchemaReaderOptions(), ["dbo.A", "dbo.B", "sales.A", "sales.C"] },
        { new SchemaReaderOptions { IncludeSchemas = [], IncludeTables = [], ExcludeTables = [] }, ["dbo.A", "dbo.B", "sales.A", "sales.C"] },
        { new SchemaReaderOptions { IncludeSchemas = ["SALES"] }, ["sales.A", "sales.C"] },
        { new SchemaReaderOptions { IncludeTables = ["a"] }, ["dbo.A", "sales.A"] },
        { new SchemaReaderOptions { ExcludeTables = ["A"] }, ["dbo.B", "sales.C"] },
        { new SchemaReaderOptions { IncludeSchemas = ["sales"], IncludeTables = ["A", "B"] }, ["sales.A"] },
        { new SchemaReaderOptions { IncludeTables = ["A", "B"], ExcludeTables = ["b"] }, ["dbo.A", "sales.A"] },
        { new SchemaReaderOptions { IncludeSchemas = ["missing"] }, [] },
    };

    [Theory]
    [MemberData(nameof(FilterCases))]
    public async Task ReadSchemaAsync_AppliesTheFilters(SchemaReaderOptions options, string[] expected)
    {
        ScriptedConnection connection = Connection(Tables(("dbo", "A"), ("dbo", "B"), ("sales", "A"), ("sales", "C")));

        DatabaseSchema schema = await Read(connection, options);

        Assert.Equal(expected, schema.Tables.Select(t => $"{t.SchemaName}.{t.TableName}"));
    }

    [Fact]
    public async Task ReadSchemaAsync_ClosedConnection_OpensItOnce()
    {
        ScriptedConnection connection = Connection(Tables(("dbo", "T")));

        await Read(connection);

        Assert.Equal(1, connection.OpenCalls);
    }

    [Fact]
    public async Task ReadSchemaAsync_OpenConnection_DoesNotReopenIt()
    {
        ScriptedConnection connection = Connection(Tables(("dbo", "T")), state: ConnectionState.Open);

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
        var reader = new PostgreSqlSchemaReader(cs =>
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
            Tables(("dbo", "T")),
            columns: _ => Columns(
                ["A", "int", false, false, false, null, null, null, null, 1],
                ["B", "int", false, false, false, null, null, null, null, 2]),
            primaryKey: _ => PrimaryKey(("PK_T", "A"), ("PK_T", "B")),
            foreignKeys: _ => ForeignKeys(["FK", "A", "dbo", "U", "Id"], ["FK2", "B", "dbo", "U", "Id2"]),
            completeAsync: completeAsync);

    [Fact]
    public void CreateConnection_ReturnsTheNpgsqlConnectionWithTheConnectionString()
    {
        using DbConnection connection = PostgreSqlSchemaReader.CreateConnection("Host=example;Database=probe");

        Assert.Equal("Npgsql.NpgsqlConnection", connection.GetType().FullName);
        Assert.Contains("example", connection.DataSource);
    }
}
