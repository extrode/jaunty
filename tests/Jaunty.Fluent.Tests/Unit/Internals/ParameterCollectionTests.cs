using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent.Tests.Unit.Internals;

/// <summary>
/// Unit tests for ParameterCollection's duplicate-name handling and prefix stripping.
/// </summary>
public class ParameterCollectionTests
{
    [Fact]
    public void Add_DuplicateName_Throws()
    {
        var parameters = new ParameterCollection();
        parameters.Add("@p0", 1);

        var ex = Assert.Throws<ArgumentException>(() => parameters.Add("@p0", 2));
        Assert.Contains("@p0", ex.Message);
    }

    [Fact]
    public void AddRange_WithDuplicateAgainstExisting_Throws()
    {
        var parameters = new ParameterCollection();
        parameters.Add("@p0", 1);

        Assert.Throws<ArgumentException>(() =>
            parameters.AddRange(new List<(string Name, object? Value)> { ("@p0", 2) }));
    }

    [Fact]
    public void Add_DistinctNames_DoesNotThrow()
    {
        var parameters = new ParameterCollection();
        parameters.Add("@p0", 1);
        parameters.Add("@p1", 2);

        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void Clear_AllowsReAddingPreviouslyUsedName()
    {
        var parameters = new ParameterCollection();
        parameters.Add("@p0", 1);
        parameters.Clear();

        parameters.Add("@p0", 2);

        Assert.Equal(1, parameters.Count);
    }

    [Fact]
    public void Clone_PreservesDuplicateDetection()
    {
        var parameters = new ParameterCollection();
        parameters.Add("@p0", 1);

        ParameterCollection clone = parameters.Clone();

        Assert.Throws<ArgumentException>(() => clone.Add("@p0", 2));
    }

    [Theory]
    [InlineData("@p0", "p0")]
    [InlineData("$p0", "p0")]
    [InlineData(":p0", "p0")]
    public void ToParameterObject_StripsKnownPrefixes(string paramName, string expectedKey)
    {
        var parameters = new ParameterCollection();
        parameters.Add(paramName, 42);

        var result = parameters.ToParameterObject() as IDictionary<string, object?>;

        Assert.NotNull(result);
        Assert.True(result!.ContainsKey(expectedKey));
        Assert.Equal(42, result[expectedKey]);
    }
}
