using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R35-247. The empty parameter set returned for every parameterless command was a single
/// shared, mutable <c>Dictionary&lt;string, object?&gt;</c>, handed straight to user code as
/// <c>CommandContext.Parameters</c> and to <c>JauntyConfig.Logger</c>. An interceptor that wrote to
/// the dictionary it received - or a logger that cached it and mutated it later - corrupted the
/// audit record of every subsequent parameterless command in the process.
/// </summary>
public class DuckDbObservationTests
{
    [Fact]
    public void TheEmptySet_IsTheSameInstanceForBothOverloads()
    {
        object fromList = DuckDbObservation.Describe(new List<DuckDBParameter>());
        object fromTuples = DuckDbObservation.Describe(Array.Empty<(string, object?)>());

        Assert.Same(fromList, fromTuples);
        Assert.Empty((IReadOnlyDictionary<string, object?>)fromList);
    }

    [Fact]
    public void TheEmptySet_RejectsAWrite()
    {
        var shared = (IDictionary<string, object?>)DuckDbObservation.Describe(new List<DuckDBParameter>());

        Assert.Throws<NotSupportedException>(() => shared["injected"] = 1);
        Assert.Throws<NotSupportedException>(() => shared.Clear());
        Assert.Throws<NotSupportedException>(() => shared.Remove("anything"));
    }

    [Fact]
    public void AFailedWrite_LeavesTheNextCommandsRecordClean()
    {
        var first = (IDictionary<string, object?>)DuckDbObservation.Describe(new List<DuckDBParameter>());
        try { first["injected"] = 1; } catch (NotSupportedException) { }

        Assert.Empty((IReadOnlyDictionary<string, object?>)DuckDbObservation.Describe(Array.Empty<(string, object?)>()));
    }

    [Fact]
    public void TheEmptySet_StillPresentsAsADictionary()
    {
        object described = DuckDbObservation.Describe(new List<DuckDBParameter>());

        // LoggingInterceptor.FormatParameters looks for IDictionary<string, object?>; if the shared
        // instance stopped satisfying that, a parameterless command would log reflected properties
        // instead of an empty parameter set.
        Assert.IsAssignableFrom<IDictionary<string, object?>>(described);
        Assert.Equal(0, ((IDictionary<string, object?>)described).Count);
    }

    [Fact]
    public void ANullParameterList_AlsoYieldsTheEmptySet()
    {
        Assert.Same(
            DuckDbObservation.Describe(new List<DuckDBParameter>()),
            DuckDbObservation.Describe((List<DuckDBParameter>)null!));
    }

    [Fact]
    public void APopulatedSet_IsStillAFreshWritableDictionary()
    {
        object described = DuckDbObservation.Describe(new[] { ("$id", (object?)7) });
        var asDictionary = (IDictionary<string, object?>)described;

        Assert.Equal(7, asDictionary["id"]);

        asDictionary["extra"] = 1;

        Assert.Equal(2, asDictionary.Count);
        Assert.Empty((IReadOnlyDictionary<string, object?>)DuckDbObservation.Describe(Array.Empty<(string, object?)>()));
    }
}
