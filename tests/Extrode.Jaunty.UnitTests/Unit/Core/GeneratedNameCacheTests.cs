using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Core;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Core;

/// <summary>
/// <see cref="GeneratedNameCache{TState}"/> resolves a source-generated entity's names in the
/// shared order and rebuilds what the generated code derives from them once per configuration
/// generation.
/// </summary>
[Collection(ConfigurationGenerationCollection.Name)]
public sealed class GeneratedNameCacheTests : IDisposable
{
    private sealed class Widget
    {
    }

    private sealed class State
    {
        public State(GeneratedNames names) => Names = names;

        public GeneratedNames Names { get; }
    }

    private int _builds;

    public void Dispose()
    {
        JauntyConfig.Reconfigure(jc => jc.TableNameResolver = null);
        JauntyConfig.Reconfigure(jc => jc.SchemaNameResolver = null);
        JauntyConfig.Reconfigure(jc => jc.ColumnNameResolver = null);
    }

    private GeneratedNameCache<State> Cache(string? table = null, string? schema = null)
        => new(typeof(Widget), table, schema, ["WidgetId", "DisplayName"], [null, "fixed"], n => { _builds++; return new State(n); });

    [Fact]
    public void WithoutResolvers_TheNamesAreTheAttributeOrCSharpNames()
    {
        GeneratedNames names = Cache().Current.Names;

        Assert.Equal("Widget", names.TableName);
        Assert.Null(names.SchemaName);
        Assert.Equal("WidgetId", names.Column(0));
        Assert.Equal("fixed", names.Column(1));
    }

    [Fact]
    public void TheAttributeTableAndSchema_WinOverTheResolvers()
    {
        JauntyConfig.Reconfigure(jc => jc.TableNameResolver = _ => "resolved");
        JauntyConfig.Reconfigure(jc => jc.SchemaNameResolver = _ => "resolved");

        GeneratedNames names = Cache("t", "s").Current.Names;

        Assert.Equal("t", names.TableName);
        Assert.Equal("s", names.SchemaName);
    }

    [Fact]
    public void TheResolvers_NameWhatNoAttributeNames()
    {
        JauntyConfig.Reconfigure(jc => jc.TableNameResolver = t => t.Name + "s");
        JauntyConfig.Reconfigure(jc => jc.SchemaNameResolver = _ => "dbo");
        JauntyConfig.Reconfigure(jc => jc.ColumnNameResolver = name => name.ToLowerInvariant());

        GeneratedNames names = Cache().Current.Names;

        Assert.Equal("Widgets", names.TableName);
        Assert.Equal("dbo", names.SchemaName);
        Assert.Equal("widgetid", names.Column(0));
        Assert.Equal("fixed", names.Column(1));
    }

    [Fact]
    public void ParameterNames_AreTheResolvedColumnNamesWithAnAtSign()
    {
        JauntyConfig.Reconfigure(jc => jc.ColumnNameResolver = name => name.ToLowerInvariant());

        GeneratedNames names = Cache().Current.Names;

        Assert.Equal("@widgetid", names.Parameter(0));
        Assert.Equal("@fixed", names.Parameter(1));
    }

    [Fact]
    public void TheState_IsBuiltOnce_WhileTheConfigurationIsUnchanged()
    {
        GeneratedNameCache<State> cache = Cache();

        State first = cache.Current;
        State second = cache.Current;

        Assert.Same(first, second);
        Assert.Equal(1, _builds);
    }

    [Fact]
    public void SettingAResolver_RebuildsTheStateUnderTheNewNames()
    {
        GeneratedNameCache<State> cache = Cache();
        State before = cache.Current;

        JauntyConfig.Reconfigure(jc => jc.ColumnNameResolver = name => "c_" + name);
        State after = cache.Current;

        Assert.NotSame(before, after);
        Assert.Equal(2, _builds);
        Assert.Equal("WidgetId", before.Names.Column(0));
        Assert.Equal("c_WidgetId", after.Names.Column(0));
        Assert.Same(after, cache.Current);
    }

    [Fact]
    public void TheConstructor_RejectsMissingArguments()
    {
        Assert.Throws<ArgumentNullException>("entityType", () => new GeneratedNameCache<State>(null!, null, null, [], [], n => new State(n)));
        Assert.Throws<ArgumentNullException>("propertyNames", () => new GeneratedNameCache<State>(typeof(Widget), null, null, null!, [], n => new State(n)));
        Assert.Throws<ArgumentNullException>("attributeColumns", () => new GeneratedNameCache<State>(typeof(Widget), null, null, [], null!, n => new State(n)));
        Assert.Throws<ArgumentNullException>("build", () => new GeneratedNameCache<State>(typeof(Widget), null, null, [], [], null!));
    }

    [Theory]
    [InlineData("x", "x")]
    [InlineData("x", "X")]
    public void AResolverMergingTwoColumns_IsRejected(string first, string second)
    {
        JauntyConfig.Reconfigure(jc => jc.ColumnNameResolver = name => name == "A" ? first : second);
        var cache = new GeneratedNameCache<State>(typeof(Widget), null, null, ["A", "B"], [null, null], n => new State(n));

        ArgumentException ex = Assert.Throws<ArgumentException>("columns", () => cache.Current);

        Assert.Contains("'Widget'", ex.Message);
        Assert.Contains("'A' and 'B'", ex.Message);
    }

    [Fact]
    public void AResolverNamingAColumnAfterAnAttributeColumn_IsRejected()
    {
        JauntyConfig.Reconfigure(jc => jc.ColumnNameResolver = _ => "fixed");
        GeneratedNameCache<State> cache = Cache();

        Assert.Throws<ArgumentException>("columns", () => cache.Current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACollisionAlreadyInTheAttributeNames_IsLeftToTheGenerator(bool withResolver)
    {
        if (withResolver)
            JauntyConfig.Reconfigure(jc => jc.ColumnNameResolver = name => name.ToLowerInvariant());
        var cache = new GeneratedNameCache<State>(typeof(Widget), null, null, ["A", "B", "C"], ["dup", "DUP", null], n => new State(n));

        GeneratedNames names = cache.Current.Names;

        Assert.Equal("dup", names.Column(0));
        Assert.Equal("DUP", names.Column(1));
    }

    [Fact]
    public void TheConstructor_RejectsMismatchedColumnArrays()
        => Assert.Throws<ArgumentException>("attributeColumns", () => new GeneratedNameCache<State>(typeof(Widget), null, null, ["A"], [], n => new State(n)));
}
