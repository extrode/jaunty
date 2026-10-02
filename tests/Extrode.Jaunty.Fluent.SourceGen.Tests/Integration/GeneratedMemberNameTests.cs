using System.Data;

using Extrode.Jaunty.Fluent.SourceGen.Tests.Entities;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.Fluent.SourceGen.Tests.Integration;

/// <summary>
/// AUD-R38 Q3: the generated members live in the nested <c>Jaunty</c> class, so an entity with a
/// <c>TableName</c> column is mapped by the generator. This project has no reference to
/// Extrode.Jaunty.Extensions.Reflection, so the reflection fallback cannot be what makes it work.
/// </summary>
public sealed class GeneratedMemberNameTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public GeneratedMemberNameTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using IDbCommand cmd = _connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE audit_entries (Id INTEGER PRIMARY KEY, TableName TEXT NOT NULL, SchemaName TEXT NULL, ParameterMap TEXT NOT NULL)";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void TheNestedClass_DescribesTheMapping_AndTheEntityKeepsItsOwnMembers()
    {
        var entry = new AuditEntry { TableName = "orders", SchemaName = "sales", ParameterMap = "{}" };

        Assert.Equal("audit_entries", AuditEntry.Jaunty.TableName);
        Assert.Null(AuditEntry.Jaunty.SchemaName);
        Assert.Equal(["Id", "TableName", "SchemaName", "ParameterMap"], AuditEntry.Jaunty.EntityColumns.Select(c => c.ColumnName));
        Assert.Equal("orders", entry.TableName);
        Assert.Equal("sales", entry.SchemaName);
    }

    [Fact]
    public void Crud_RoundTripsTheCollidingColumns()
    {
        var entry = new AuditEntry { TableName = "orders", SchemaName = "sales", ParameterMap = "{\"id\":1}" };

        entry.Id = (int)_connection.Insert(entry);
        entry.TableName = "invoices";
        Assert.Equal(1, _connection.Update(entry));

        AuditEntry? fetched = _connection.Get<AuditEntry>(entry.Id);
        Assert.NotNull(fetched);
        Assert.Equal("invoices", fetched.TableName);
        Assert.Equal("sales", fetched.SchemaName);
        Assert.Equal("{\"id\":1}", fetched.ParameterMap);

        Assert.Equal(1, _connection.Delete(entry));
        Assert.Null(_connection.Get<AuditEntry>(entry.Id));
    }

    [Fact]
    public void Fluent_FiltersOnACollidingColumn()
    {
        _connection.Insert(new AuditEntry { TableName = "orders", ParameterMap = "a" });
        _connection.Insert(new AuditEntry { TableName = "invoices", ParameterMap = "b" });

        AuditEntry match = Assert.Single(_connection.From<AuditEntry>().Where(e => e.TableName == "invoices").Select());

        Assert.Equal("b", match.ParameterMap);
        Assert.Null(match.SchemaName);
    }
}
