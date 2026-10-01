using Extrode.Jaunty.Internals.Parameters;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R38-130: an object-typed property bound null or scalar first cached a plain template, so a
/// later call with an array bound it as one value instead of expanding the IN clause.
/// </summary>
public class ParameterBinderObjectTypedCollectionTests
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
    public void AnObjectProperty_NullFirst_StillExpandsALaterArray()
    {
        const string sql = "SELECT 1 FROM r38_130_null WHERE Id IN @Ids";
        using SqliteCommand first = Command(sql);
        ParameterBinder.Bind(first, new ObjectFilter { Ids = null });

        using SqliteCommand second = Command(sql);
        ParameterBinder.Bind(second, new ObjectFilter { Ids = new[] { 1, 2 } });

        Assert.NotEqual(sql, second.CommandText);
        Assert.Equal(2, second.Parameters.Count);
    }

    [Fact]
    public void AnObjectProperty_ScalarFirst_StillExpandsALaterArray()
    {
        const string sql = "SELECT 1 FROM r38_130_scalar WHERE Id IN @Ids";
        using SqliteCommand first = Command(sql);
        ParameterBinder.Bind(first, new ObjectFilter { Ids = 7 });
        Assert.Equal(7, first.Parameters["Ids"].Value);

        using SqliteCommand second = Command(sql);
        ParameterBinder.Bind(second, new ObjectFilter { Ids = new[] { 1, 2, 3 } });

        Assert.Equal(3, second.Parameters.Count);
    }

    [Fact]
    public void AnInterfacePropertyAnArrayImplements_NullFirst_StillExpandsALaterArray()
    {
        const string sql = "SELECT 1 FROM r38_130_iface WHERE Id IN @Ids";
        using SqliteCommand first = Command(sql);
        ParameterBinder.Bind(first, new CloneableFilter { Ids = null });

        using SqliteCommand second = Command(sql);
        ParameterBinder.Bind(second, new CloneableFilter { Ids = new[] { 1, 2 } });

        Assert.Equal(2, second.Parameters.Count);
    }
}
