using System.Reflection;

using Jaunty.Extensions.Reflection;
using Jaunty.Internals.Entity;

using Xunit;

namespace Jaunty.Tests.Unit.Reflection;

/// <summary>
/// AUD-R35-068. All three write binders read property values with
/// <c>PropertyInfo.GetValue(entity)</c>, once per column per entity, on the hot
/// insert/update/delete path this package exists to serve. The same package already compiles an
/// expression-tree getter for every column of every entity and exposes it as
/// <c>PropertyContext&lt;T&gt;.Getter</c> - and nothing in <c>src/</c> or <c>tests/</c> ever read
/// it, so the compile cost was paid at snapshot-build time and the benefit never collected. Core
/// Jaunty treats reflection <c>GetValue</c> as the fallback and the compiled getter as the fast
/// path in the equivalent write code.
/// <para>
/// A compiled getter and <c>GetValue</c> return the same value, so the switch is not observable
/// from behaviour - it is observable from the wiring, which is what these assert, in the same shape
/// as <c>EnableStreamingWiringTests</c>.
/// </para>
/// </summary>
public class ReflectionWriteBinderGetterTests
{
    private sealed class Widget
    {
        public int WidgetId { get; set; }
        public string? Name { get; set; }
        public decimal Price { get; set; }
    }

    private static Array BuildConverters(string columnsProperty)
    {
        MethodInfo build = typeof(JauntyReflectionExtensions)
            .GetMethod("BuildColumnConverters", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(Widget));

        object columns = typeof(EntityMetadata).GetProperty(columnsProperty)!
            .GetValue(MetadataCache<Widget>.Metadata)!;

        return (Array)build.Invoke(null, [columns])!;
    }

    private static Delegate GetterOf(Array converters, int index)
    {
        object entry = converters.GetValue(index)!;
        return (Delegate)entry.GetType().GetField("Item2")!.GetValue(entry)!;
    }

    [Theory]
    [InlineData("InsertColumns")]
    [InlineData("UpdateColumns")]
    [InlineData("DeleteColumns")]
    [InlineData("PrimaryKeys")]
    public void EveryBoundColumn_UsesTheSnapshotsCompiledGetter(string columnsProperty)
    {
        Array converters = BuildConverters(columnsProperty);
        PropertyContext<Widget>[] contexts = MetadataCache<Widget>.Properties;

        Assert.NotEqual(0, converters.Length);

        for (int i = 0; i < converters.Length; i++)
        {
            Delegate getter = GetterOf(converters, i);

            // Reference equality, not merely "a delegate that works": a fresh
            // entity => property.GetValue(entity) closure would satisfy any value-based assertion.
            Assert.Contains(contexts, c => ReferenceEquals(c.Getter, getter));
        }
    }

    [Fact]
    public void TheCompiledGetter_ReadsTheSameValueReflectionWould()
    {
        var widget = new Widget { WidgetId = 7, Name = "cog", Price = 12.5m };
        PropertyContext<Widget>[] contexts = MetadataCache<Widget>.Properties;

        foreach (PropertyContext<Widget> context in contexts)
            Assert.Equal(context.Property.GetValue(widget), context.Getter(widget));
    }

    /// <summary>
    /// The fallback path: a column whose property is not in the snapshot still binds, through
    /// reflection, rather than throwing.
    /// </summary>
    [Fact]
    public void APropertyOutsideTheSnapshot_FallsBackToReflection()
    {
        MethodInfo resolve = typeof(JauntyReflectionExtensions)
            .GetMethod("ResolveGetter", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(Widget));

        PropertyInfo stranger = typeof(Widget).GetProperty(nameof(Widget.Name))!;
        var empty = Array.Empty<PropertyContext<Widget>>();

        var getter = (Func<Widget, object?>)resolve.Invoke(null, [empty, stranger])!;

        Assert.Equal("cog", getter(new Widget { Name = "cog" }));
    }
}
