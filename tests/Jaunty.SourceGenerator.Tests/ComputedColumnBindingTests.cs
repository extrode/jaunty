using System.Linq;

using Jaunty.SourceGenerator.Tests.Entities;

using Microsoft.Data.Sqlite;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// Round 15 audit finding: BindInsert/BindUpdate only excluded IsIdentity properties, unlike
/// InsertColumns/UpdateColumns (and EntityColumns) which already excluded IsComputed too - a
/// [DatabaseGenerated(Computed)] property would be bound as an INSERT/UPDATE parameter even
/// though the database computes its value, causing a runtime failure against a real computed
/// column. Uses the same GenComputedEntity fixture as EntityMetadataSourceEmissionTests.
/// </summary>
public sealed class ComputedColumnBindingTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ComputedColumnBindingTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void BindInsert_ComputedColumn_IsExcludedFromParameters()
    {
        using var cmd = _connection.CreateCommand();
        var entity = new GenComputedEntity { EntityId = 1, Label = "a", ComputedValue = "should-not-bind" };

        GenComputedEntity.BindInsert(cmd, entity);

        var paramNames = cmd.Parameters.Cast<SqliteParameter>().Select(p => p.ParameterName).ToList();
        Assert.Contains("@label", paramNames);
        Assert.DoesNotContain("@computed_value", paramNames);
    }

    [Fact]
    public void BindUpdate_ComputedColumn_IsExcludedFromParameters()
    {
        using var cmd = _connection.CreateCommand();
        var entity = new GenComputedEntity { EntityId = 1, Label = "a", ComputedValue = "should-not-bind" };

        GenComputedEntity.BindUpdate(cmd, entity);

        var paramNames = cmd.Parameters.Cast<SqliteParameter>().Select(p => p.ParameterName).ToList();
        Assert.Contains("@label", paramNames);
        Assert.Contains("@entity_id", paramNames);
        Assert.DoesNotContain("@computed_value", paramNames);
    }
}
