using DuckDB.NET.Data;

using Extrode.Jaunty.FlatFiles.DuckDB.Internals;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

public class DuckDbObservationDescribeTests
{
    private static IDictionary<string, object?> Keyed(object described) => (IDictionary<string, object?>)described;

    [Fact]
    public void ANamedParameter_IsKeyedByItsNameAlone()
    {
        IDictionary<string, object?> described = Keyed(DuckDbObservation.Describe([new DuckDBParameter("a", 1), new DuckDBParameter("b", 2)]));

        Assert.Equal(["a", "b"], described.Keys);
        Assert.Equal([1, 2], described.Values);
    }

    [Fact]
    public void ARepeatedParameterName_KeepsEveryValueUnderTheIndexedKey()
    {
        IDictionary<string, object?> described = Keyed(DuckDbObservation.Describe(
            [new DuckDBParameter("a", 1), new DuckDBParameter("b", 2), new DuckDBParameter("a", 3)]));

        Assert.Equal(["a", "b", "a#2"], described.Keys);
        Assert.Equal(3, described["a#2"]);
    }

    [Fact]
    public void AParameterWithoutAName_FallsBackToItsPosition()
    {
        var unnamed = new DuckDBParameter { Value = 7 };
        string expected = unnamed.ParameterName ?? "0";

        IDictionary<string, object?> described = Keyed(DuckDbObservation.Describe([unnamed]));

        Assert.Equal([expected], described.Keys);
        Assert.Equal(7, described[expected]);
    }

    [Fact]
    public void ATupleName_IsReportedWithoutItsDollarPrefix()
    {
        IDictionary<string, object?> described = Keyed(DuckDbObservation.Describe([("$a", (object?)1), ("b", 2)]));

        Assert.Equal(["a", "b"], described.Keys);
    }

    [Fact]
    public void ARepeatedTupleName_KeepsEveryValueUnderTheIndexedKey()
    {
        IDictionary<string, object?> described = Keyed(DuckDbObservation.Describe([("$a", (object?)1), ("a", 2), ("$a", 3)]));

        Assert.Equal(["a", "a#1", "a#2"], described.Keys);
        Assert.Equal([1, 2, 3], described.Values);
    }

    [Fact]
    public void NoParameters_ShareOneReadOnlySet()
    {
        object fromList = DuckDbObservation.Describe(new List<DuckDBParameter>());
        object fromTuples = DuckDbObservation.Describe(Array.Empty<(string, object?)>());

        Assert.Same(fromList, fromTuples);
        Assert.Throws<NotSupportedException>(() => Keyed(fromList).Add("x", 1));
    }
}
