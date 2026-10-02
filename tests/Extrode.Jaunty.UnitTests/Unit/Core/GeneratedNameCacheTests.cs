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
        JauntyConfig.TableNameResolver = null;
        JauntyConfig.SchemaNameResolver = null;
        JauntyConfig.ColumnNameResolver = null;
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
        JauntyConfig.TableNameResolver = _ => "resolved";
        JauntyConfig.SchemaNameResolver = _ => "resolved";

        GeneratedNames names = Cache("t", "s").Current.Names;

        Assert.Equal("t", names.TableName);
        Assert.Equal("s", names.SchemaName);
    }

    [Fact]
    public void TheResolvers_NameWhatNoAttributeNames()
    {
        JauntyConfig.TableNameResolver = t => t.Name + "s";
        JauntyConfig.SchemaNameResolver = _ => "dbo";
        JauntyConfig.ColumnNameResolver = name => name.ToLowerInvariant();

        GeneratedNames names = Cache().Current.Names;

        Assert.Equal("Widgets", names.TableName);
        Assert.Equal("dbo", names.SchemaName);
        Assert.Equal("widgetid", names.Column(0));
        Assert.Equal("fixed", names.Column(1));
    }

    [Fact]
    public void ParameterNames_AreTheResolvedColumnNamesWithAnAtSign()
    {
        JauntyConfig.ColumnNameResolver = name => name.ToLowerInvariant();

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

        JauntyConfig.ColumnNameResolver = name => "c_" + name;
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

    [Fact]
    public void TheConstructor_RejectsMismatchedColumnArrays()
        => Assert.Throws<ArgumentException>("attributeColumns", () => new GeneratedNameCache<State>(typeof(Widget), null, null, ["A"], [], n => new State(n)));
}
