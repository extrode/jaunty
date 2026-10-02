using System.Data;
using System.Text;

using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Fluent.Internals;
using Extrode.Jaunty.Fluent.SourceGen.Tests.Entities;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.Fluent.SourceGen.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NamingResolverCollection
{
    public const string Name = "Naming resolvers";
}

/// <summary>
/// docs/plans/2026-10-02-010: the <c>JauntyConfig</c> naming resolvers apply to a source-generated
/// entity in the reflection order - a non-empty attribute name, then the resolver, then the C#
/// name - across CRUD SQL, the generated binders and reader, Fluent SQL and the generated statics.
/// This project has no reference to Extrode.Jaunty.Extensions.Reflection, so every name here comes
/// from the generated path.
/// </summary>
[Collection(NamingResolverCollection.Name)]
public sealed class GeneratedNamingResolverTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GeneratedNamingResolverTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        Execute("""
            CREATE TABLE resolved_name_widgets (widget_id INTEGER PRIMARY KEY, display_name TEXT NOT NULL, fixed_price NUMERIC NOT NULL);
            CREATE TABLE ResolvedNameWidget (WidgetId INTEGER PRIMARY KEY, DisplayName TEXT NOT NULL, fixed_price NUMERIC NOT NULL);
            """);
    }

    public void Dispose()
    {
        JauntyConfig.TableNameResolver = null;
        JauntyConfig.SchemaNameResolver = null;
        JauntyConfig.ColumnNameResolver = null;
        _connection.Dispose();
    }

    private static string Snake(string name)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }

    private static void UseSnakeCase()
    {
        JauntyConfig.TableNameResolver = type => Snake(type.Name) + "s";
        JauntyConfig.ColumnNameResolver = Snake;
    }

    private void Execute(string sql)
    {
        using IDbCommand cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string[] ColumnNames() => ResolvedNameWidget.EntityColumns.Select(c => c.ColumnName).ToArray();

    [Fact]
    public void WithoutResolvers_TheGeneratedNamesAreTheAttributeOrCSharpNames()
    {
        Assert.Equal("ResolvedNameWidget", ResolvedNameWidget.TableName);
        Assert.Null(ResolvedNameWidget.SchemaName);
        Assert.Equal(["WidgetId", "DisplayName", "fixed_price"], ColumnNames());
    }

    [Fact]
    public void Resolvers_RenameTheDefaultedNames_AndTheAttributeNameWins()
    {
        UseSnakeCase();

        Assert.Equal("resolved_name_widgets", ResolvedNameWidget.TableName);
        Assert.Equal(["widget_id", "display_name", "fixed_price"], ColumnNames());
        Assert.Equal(["widget_id"], ResolvedNameWidget.PrimaryKeyColumnNames);
        Assert.Equal(["display_name", "fixed_price"], ResolvedNameWidget.InsertColumns.Select(c => c.ColumnName));
        Assert.Equal(["display_name", "fixed_price"], ResolvedNameWidget.UpdateColumns.Select(c => c.ColumnName));
        Assert.Equal(["widget_id"], ResolvedNameWidget.DeleteColumns.Select(c => c.ColumnName));
        Assert.True(ResolvedNameWidget.ParameterMap.ContainsKey("display_name"));
        Assert.False(ResolvedNameWidget.ParameterMap.ContainsKey("DisplayName"));
    }

    [Fact]
    public void AResolverReturningNull_FallsBackToTheCSharpName()
    {
        JauntyConfig.TableNameResolver = _ => null!;
        JauntyConfig.ColumnNameResolver = name => name == "DisplayName" ? null! : Snake(name);

        Assert.Equal("ResolvedNameWidget", ResolvedNameWidget.TableName);
        Assert.Equal(["widget_id", "DisplayName", "fixed_price"], ColumnNames());
    }

    [Fact]
    public void TheSchemaResolver_SuppliesTheSchema()
    {
        JauntyConfig.SchemaNameResolver = _ => "main";

        Assert.Equal("main", ResolvedNameWidget.SchemaName);
        Assert.Equal("main", FluentMetadataCache.GetMetadata<ResolvedNameWidget>().SchemaName);
    }

    [Fact]
    public void TheGeneratedBinders_UseTheResolvedParameterNames()
    {
        UseSnakeCase();
        using IDbCommand insert = _connection.CreateCommand();
        using IDbCommand update = _connection.CreateCommand();
        var widget = new ResolvedNameWidget { WidgetId = 4, DisplayName = "a", UnitPrice = 1m };

        ResolvedNameWidget.BindInsert(insert, widget);
        ResolvedNameWidget.BindUpdate(update, widget);

        Assert.Equal(["@display_name", "@fixed_price"], insert.Parameters.Cast<IDataParameter>().Select(p => p.ParameterName));
        Assert.Equal(["@display_name", "@fixed_price", "@widget_id"], update.Parameters.Cast<IDataParameter>().Select(p => p.ParameterName));
    }

    [Fact]
    public void Crud_RoundTripsThroughTheResolvedTableAndColumns()
    {
        UseSnakeCase();
        var widget = new ResolvedNameWidget { DisplayName = "Gadget", UnitPrice = 9.5m };

        widget.WidgetId = (int)_connection.Insert(widget);
        widget.DisplayName = "Renamed";
        Assert.Equal(1, _connection.Update(widget));

        ResolvedNameWidget? fetched = _connection.Get<ResolvedNameWidget>(widget.WidgetId);
        Assert.NotNull(fetched);
        Assert.Equal("Renamed", fetched.DisplayName);
        Assert.Equal(9.5m, fetched.UnitPrice);

        Assert.Equal(1, _connection.Delete(widget));
        Assert.Null(_connection.Get<ResolvedNameWidget>(widget.WidgetId));
    }

    [Fact]
    public void TheGeneratedReader_FindsTheResolvedColumns()
    {
        UseSnakeCase();
        Execute("INSERT INTO resolved_name_widgets VALUES (7, 'Gizmo', 2.5)");

        ResolvedNameWidget widget = Assert.Single(_connection.Query<ResolvedNameWidget>("SELECT widget_id, display_name, fixed_price FROM resolved_name_widgets"));

        Assert.Equal(7, widget.WidgetId);
        Assert.Equal("Gizmo", widget.DisplayName);
        Assert.Equal(2.5m, widget.UnitPrice);
    }

    [Fact]
    public void FluentSql_FiltersOnTheResolvedColumn()
    {
        UseSnakeCase();
        Execute("INSERT INTO resolved_name_widgets VALUES (1, 'Gadget', 1), (2, 'Gizmo', 2)");

        ResolvedNameWidget widget = Assert.Single(_connection.From<ResolvedNameWidget>().Where(w => w.DisplayName == "Gizmo").Select());

        Assert.Equal(2, widget.WidgetId);
    }

    [Fact]
    public void ChangingTheResolvers_SwitchesEveryPathToTheNewNames()
    {
        UseSnakeCase();
        _connection.Insert(new ResolvedNameWidget { DisplayName = "Snake", UnitPrice = 1m });

        JauntyConfig.TableNameResolver = null;
        JauntyConfig.ColumnNameResolver = null;
        _connection.Insert(new ResolvedNameWidget { DisplayName = "Pascal", UnitPrice = 2m });

        ResolvedNameWidget pascal = Assert.Single(_connection.Query<ResolvedNameWidget>("SELECT * FROM ResolvedNameWidget"));
        Assert.Equal("Pascal", pascal.DisplayName);
        Assert.Equal(1L, _connection.QueryScalar<long>("SELECT COUNT(*) FROM resolved_name_widgets"));
    }

    [Fact]
    public void ChangingTheResolvers_MidRead_ResolvesTheReaderAgainstTheNewNames()
    {
        using IDbCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 AS WidgetId, 'pascal' AS DisplayName, 1.5 AS fixed_price, 2 AS widget_id, 'snake' AS display_name";
        using IDataReader reader = cmd.ExecuteReader();
        Assert.True(reader.Read());

        ResolvedNameWidget before = ResolvedNameWidget.ReadEntity(reader);
        UseSnakeCase();
        ResolvedNameWidget after = ResolvedNameWidget.ReadEntity(reader);

        Assert.Equal((1, "pascal"), (before.WidgetId, before.DisplayName));
        Assert.Equal((2, "snake"), (after.WidgetId, after.DisplayName));
    }

    [Fact]
    public void AResolverMappingTwoPropertiesToOneColumn_IsRejectedOnEveryPath()
    {
        JauntyConfig.ColumnNameResolver = name => name == "DisplayName" ? "WIDGET_ID" : Snake(name);
        Execute("INSERT INTO ResolvedNameWidget VALUES (1, 'a', 1)");

        ArgumentException read = Assert.Throws<ArgumentException>(() => _connection.Query<ResolvedNameWidget>("SELECT * FROM ResolvedNameWidget").ToList());
        Assert.Contains("'WidgetId' and 'DisplayName'", read.Message);
        Assert.Throws<ArgumentException>(() => _connection.Insert(new ResolvedNameWidget { DisplayName = "b", UnitPrice = 1m }));
        Assert.Throws<ArgumentException>(() => ResolvedNameWidget.TableName);
    }

    [Fact]
    public void AUserStaticInitializer_CanReadTheGeneratedTableName()
        => Assert.Equal("SELECT * FROM static_init_widgets", StaticInitWidget.SelectAll);
}
