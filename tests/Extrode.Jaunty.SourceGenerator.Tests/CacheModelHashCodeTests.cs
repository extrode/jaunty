using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using Extrode.Jaunty.SourceGenerator;

namespace Extrode.Jaunty.SourceGenerator.Tests;

public sealed class CacheModelHashCodeTests
{
    private static readonly Type Generator = typeof(JauntyGenerator);

    private static int H(string? s) => s?.GetHashCode() ?? 0;

    private static Type Nested(string name)
        => Generator.GetNestedType(name, BindingFlags.NonPublic) ?? throw new InvalidOperationException(name);

    private static LocationInfo SomeLocation()
        => new("a.cs", new TextSpan(3, 4), new LinePositionSpan(new LinePosition(1, 2), new LinePosition(1, 6)));

    private static int Mapper(string? typeName, string? supplies, string? members, LocationInfo? location)
    {
        object mapper = Activator.CreateInstance(Nested("HandWrittenMapper"), typeName, supplies, members, location)!;
        return mapper.GetHashCode();
    }

    private static int ExpectedMapper(string? typeName, string? supplies, string? members, LocationInfo? location)
    {
        unchecked
        {
            int hash = H(typeName);
            hash = (hash * 397) ^ H(supplies);
            hash = (hash * 397) ^ H(members);
            return (hash * 397) ^ (location?.GetHashCode() ?? 0);
        }
    }

    [Fact]
    public void HandWrittenMapper_HashCombinesEveryField()
    {
        LocationInfo location = SomeLocation();

        Assert.Equal(ExpectedMapper("Type", "supplies", "members", location), Mapper("Type", "supplies", "members", location));
    }

    [Fact]
    public void HandWrittenMapper_HashWithoutLocation()
    {
        Assert.Equal(ExpectedMapper("Type", "supplies", "members", null), Mapper("Type", "supplies", "members", null));
    }

    [Fact]
    public void HandWrittenMapper_HashOfTheDefaultValueIsZero()
    {
        object mapper = Activator.CreateInstance(Nested("HandWrittenMapper"))!;

        Assert.Equal(0, mapper.GetHashCode());
    }

    [Theory]
    [InlineData("Type", "supplies", "members")]
    [InlineData("Other", "supplies", "members")]
    [InlineData("Type", "other", "members")]
    [InlineData("Type", "supplies", "other")]
    public void HandWrittenMapper_HashDependsOnEachString(string typeName, string supplies, string members)
    {
        Assert.Equal(ExpectedMapper(typeName, supplies, members, null), Mapper(typeName, supplies, members, null));
    }

    private static int Root(string? expression, bool isWitness)
        => Activator.CreateInstance(Nested("ParameterRoot"), expression, isWitness)!.GetHashCode();

    private static int ExpectedRoot(string? expression, bool isWitness)
        => unchecked(H(expression) * 397) ^ (isWitness ? 1 : 0);

    [Theory]
    [InlineData("global::A", false)]
    [InlineData("global::A", true)]
    [InlineData("new { X = default(int) }", true)]
    public void ParameterRoot_HashCombinesExpressionAndWitnessFlag(string expression, bool isWitness)
    {
        Assert.Equal(ExpectedRoot(expression, isWitness), Root(expression, isWitness));
    }

    [Fact]
    public void ParameterRoot_HashOfTheDefaultValueIsZero()
    {
        Assert.Equal(0, Activator.CreateInstance(Nested("ParameterRoot"))!.GetHashCode());
    }

    [Fact]
    public void ParameterRoot_WitnessFlagChangesTheHash()
    {
        Assert.NotEqual(Root("global::A", false), Root("global::A", true));
    }

    private static object Site(string method, params object?[] args)
        => Nested("ParameterSite").GetMethod(method, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, args)!;

    [Fact]
    public void ParameterSite_RootableHashesTheRoot()
    {
        object site = Site("Rootable", "global::A", false);
        int root = ExpectedRoot("global::A", false);
        int expected = unchecked(((root * 397) ^ 0) * 397) ^ 0;

        Assert.Equal(expected, site.GetHashCode());
    }

    [Fact]
    public void ParameterSite_UnrootableHashesTypeAndLocation()
    {
        Location location = Location.Create("a.cs", new TextSpan(0, 1), new LinePositionSpan(default, new LinePosition(0, 1)));
        object site = Site("Unrootable", "System.Object", location);
        int expected = unchecked(((0 * 397) ^ H("System.Object")) * 397) ^ location.GetHashCode();

        Assert.Equal(expected, site.GetHashCode());
    }

    [Fact]
    public void ParameterSite_HashOfTheDefaultValueIsZero()
    {
        Assert.Equal(0, Activator.CreateInstance(Nested("ParameterSite"))!.GetHashCode());
    }
}
