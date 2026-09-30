using System.Reflection;

using Extrode.Jaunty.FlatFiles.DuckDB.Internals;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

public class MappedPropertyFilterOrderingTests
{
    private class Shadowed
    {
        public string Item { get; set; } = "";
    }

    private sealed class ShadowedByAnIndexer : Shadowed
    {
#pragma warning disable CS0108
        public int this[int index] => index;
#pragma warning restore CS0108
    }

    private class Parent
    {
        public int Value { get; set; }
    }

    private sealed class Child : Parent
    {
        public new int Value { get; set; }
    }

    private static readonly MethodInfo IsMoreDerivedThan =
        typeof(MappedPropertyFilter).GetMethod("IsMoreDerivedThan", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static bool MoreDerived(PropertyInfo candidate, PropertyInfo incumbent) =>
        (bool)IsMoreDerivedThan.Invoke(null, [candidate, incumbent])!;

    private static PropertyInfo ValueOn(Type type) =>
        type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;

    [Fact]
    public void AnIndexerNamedLikeABaseProperty_DoesNotDisplaceIt()
    {
        List<PropertyInfo> mapped = MappedPropertyFilter.GetMappedProperties(typeof(ShadowedByAnIndexer));

        PropertyInfo item = Assert.Single(mapped);
        Assert.Equal(typeof(Shadowed), item.DeclaringType);
        Assert.Empty(item.GetIndexParameters());
    }

    [Fact]
    public void AnIndexer_IsNeverMapped()
    {
        PropertyInfo indexer = typeof(ShadowedByAnIndexer).GetProperties().Single(p => p.GetIndexParameters().Length > 0);

        Assert.False(MappedPropertyFilter.IsMapped(indexer));
    }

    [Fact]
    public void ADerivedDeclaration_IsMoreDerivedThanTheBaseOne() =>
        Assert.True(MoreDerived(ValueOn(typeof(Child)), ValueOn(typeof(Parent))));

    [Fact]
    public void ABaseDeclaration_IsNotMoreDerivedThanTheDerivedOne() =>
        Assert.False(MoreDerived(ValueOn(typeof(Parent)), ValueOn(typeof(Child))));

    [Fact]
    public void ADeclaration_IsNotMoreDerivedThanItself() =>
        Assert.False(MoreDerived(ValueOn(typeof(Child)), ValueOn(typeof(Child))));

    [Fact]
    public void UnrelatedDeclarations_AreNotMoreDerivedThanEachOther() =>
        Assert.False(MoreDerived(ValueOn(typeof(Parent)), typeof(Shadowed).GetProperty("Item")!));
}
