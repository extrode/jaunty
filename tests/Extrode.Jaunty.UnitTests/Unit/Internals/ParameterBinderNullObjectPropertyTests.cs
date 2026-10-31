using Extrode.Jaunty.Internals.Parameters;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.Tests.Unit.Internals;

public class ParameterBinderNullObjectPropertyTests
{
    private sealed class ObjectFilter
    {
        public object? Ids { get; set; }
    }

    private sealed class CloneableFilter
    {
        public ICloneable? Ids { get; set; }
    }

    private static SqliteCommand Command(string sql) => new(sql, new SqliteConnection("Data Source=:memory:"));

    [Fact]
    public void AnObjectProperty_Null_BindsDbNullInsteadOfAnEmptySet()
    {
        const string sql = "SELECT 1 FROM r39_object_null WHERE Id = @Ids";
        using SqliteCommand command = Command(sql);
        ParameterBinder.Bind(command, new ObjectFilter { Ids = null });

        Assert.Equal(sql, command.CommandText);
        Assert.Equal(DBNull.Value, command.Parameters["Ids"].Value);
    }

    [Fact]
    public void AnInterfaceProperty_Null_BindsDbNullInsteadOfAnEmptySet()
    {
        const string sql = "SELECT 1 FROM r39_iface_null WHERE Id = @Ids";
        using SqliteCommand command = Command(sql);
        ParameterBinder.Bind(command, new CloneableFilter { Ids = null });

        Assert.Equal(sql, command.CommandText);
        Assert.Equal(DBNull.Value, command.Parameters["Ids"].Value);
    }

    [Fact]
    public void AnObjectProperty_HoldingAList_StillExpands()
    {
        const string sql = "SELECT 1 FROM r39_object_list WHERE Id IN @Ids";
        using SqliteCommand command = Command(sql);
        ParameterBinder.Bind(command, new ObjectFilter { Ids = new List<int> { 4, 5 } });

        Assert.Equal("SELECT 1 FROM r39_object_list WHERE Id IN (@Ids0, @Ids1)", command.CommandText);
        Assert.Equal(2, command.Parameters.Count);
    }
}
