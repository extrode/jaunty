using System.Collections.Immutable;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R35-224 and AUD-R35-225: nothing named <c>EquatableArray</c> anywhere in the suite, so
/// <c>Count</c>, the indexer, <c>Equals(object?)</c>, the operators and the default-vs-empty
/// asymmetry had no caller at all, and the enumerator's boxing had nothing pinning it.
/// </summary>
public class EquatableArrayTests
{
    private static EquatableArray<int> Of(params int[] values) => new(ImmutableArray.Create(values));

    [Fact]
    public void Count_ReportsTheElementCount()
    {
        Assert.Equal(3, Of(1, 2, 3).Count);
        Assert.Equal(0, Of().Count);
    }

    [Fact]
    public void Count_OnADefaultInstance_IsZeroRatherThanThrowing()
    {
        EquatableArray<int> value = default;

        Assert.Equal(0, value.Count);
    }

    [Fact]
    public void Indexer_ReturnsTheElement()
    {
        var array = Of(5, 6, 7);

        Assert.Equal(5, array[0]);
        Assert.Equal(7, array[2]);
    }

    [Fact]
    public void Equals_SameContents_AreEqual()
    {
        Assert.True(Of(1, 2).Equals(Of(1, 2)));
        Assert.True(Of(1, 2) == Of(1, 2));
        Assert.False(Of(1, 2) != Of(1, 2));
    }

    [Fact]
    public void Equals_DifferentContentsOrLength_AreNotEqual()
    {
        Assert.False(Of(1, 2).Equals(Of(1, 3)));
        Assert.False(Of(1, 2).Equals(Of(1)));
        Assert.True(Of(1, 2) != Of(1));
    }

    [Fact]
    public void Equals_Object_MatchesTheTypedOverload()
    {
        object boxed = Of(1, 2);

        Assert.True(Of(1, 2).Equals(boxed));
        Assert.False(Of(1, 3).Equals(boxed));
        Assert.False(Of(1, 2).Equals("not an array"));
        Assert.False(Of(1, 2).Equals(null));
    }

    [Fact]
    public void Equals_ADefaultInstance_MatchesOnlyAnotherDefault()
    {
        EquatableArray<int> first = default;
        EquatableArray<int> second = default;

        Assert.True(first.Equals(second));
        Assert.False(first.Equals(Of()));
        Assert.False(Of().Equals(first));
    }

    [Fact]
    public void GetHashCode_SameContents_Agree()
    {
        Assert.Equal(Of(1, 2, 3).GetHashCode(), Of(1, 2, 3).GetHashCode());
        Assert.Equal(0, ((EquatableArray<int>)default).GetHashCode());
    }

    [Fact]
    public void GetEnumerator_IsTheStructEnumerator()
    {
        var array = Of(1, 2, 3);

        Assert.IsType<ImmutableArray<int>.Enumerator>(array.GetEnumerator());
    }

    [Fact]
    public void GetEnumerator_YieldsEveryElementInOrder()
    {
        var seen = new List<int>();

        foreach (int value in Of(1, 2, 3))
            seen.Add(value);

        Assert.Equal(new[] { 1, 2, 3 }, seen);
    }

    [Fact]
    public void GetEnumerator_OnADefaultInstance_YieldsNothingRatherThanThrowing()
    {
        EquatableArray<int> value = default;
        var seen = new List<int>();

        foreach (int element in value)
            seen.Add(element);

        Assert.Empty(seen);
    }

    [Fact]
    public void TheInterfaceEnumerators_StillWork()
    {
        IReadOnlyList<int> asList = Of(1, 2, 3);
        IEnumerable<int> asGeneric = asList;
        System.Collections.IEnumerable asUntyped = asList;

        Assert.Equal(new[] { 1, 2, 3 }, asGeneric.ToArray());
        Assert.Equal(3, asList.Count);
        Assert.Equal(2, asList[1]);
        Assert.Equal(3, asUntyped.Cast<int>().Count());
    }

    [Fact]
    public void TheInterfaceEnumerator_OnADefaultInstance_YieldsNothing()
    {
        IEnumerable<int> value = (EquatableArray<int>)default;

        Assert.Empty(value);
    }
}
