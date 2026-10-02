using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Internals.Entity;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Internals;

/// <summary>
/// The one name order both mapping modes share: a non-empty attribute name, then the resolver when
/// it returns non-null, then the C# name. docs/02-architecture/metadata-system-spec.md diagrams it.
/// </summary>
[Collection(ConfigurationGenerationCollection.Name)]
public sealed class NameResolutionTests : IDisposable
{
    private sealed class Widget
    {
    }

    public void Dispose()
    {
        JauntyConfig.TableNameResolver = null;
        JauntyConfig.SchemaNameResolver = null;
        JauntyConfig.ColumnNameResolver = null;
    }

    [Theory]
    [InlineData("attr", "resolved", "attr")]
    [InlineData("", "resolved", "resolved")]
    [InlineData(null, "resolved", "resolved")]
    [InlineData(null, null, "Widget")]
    [InlineData(null, "", "")]
    public void Table_FollowsTheOrder(string? attribute, string? resolved, string expected)
    {
        JauntyConfig.TableNameResolver = _ => resolved!;

        Assert.Equal(expected, NameResolution.Table(typeof(Widget), attribute));
    }

    [Fact]
    public void Table_WithoutAResolver_IsTheTypeName()
        => Assert.Equal("Widget", NameResolution.Table(typeof(Widget), null));

    [Fact]
    public void Table_PassesTheEntityTypeToTheResolver()
    {
        Type? seen = null;
        JauntyConfig.TableNameResolver = t => { seen = t; return "x"; };

        NameResolution.Table(typeof(Widget), null);

        Assert.Equal(typeof(Widget), seen);
    }

    [Theory]
    [InlineData("attr", "resolved", "attr")]
    [InlineData("", "resolved", "resolved")]
    [InlineData(null, "resolved", "resolved")]
    [InlineData(null, null, null)]
    [InlineData(null, "", "")]
    public void Schema_FollowsTheOrder(string? attribute, string? resolved, string? expected)
    {
        JauntyConfig.SchemaNameResolver = _ => resolved!;

        Assert.Equal(expected, NameResolution.Schema(typeof(Widget), attribute));
    }

    [Fact]
    public void Schema_WithoutAResolver_IsNone()
        => Assert.Null(NameResolution.Schema(typeof(Widget), ""));

    [Theory]
    [InlineData("attr", "resolved", "attr")]
    [InlineData("", "resolved", "resolved")]
    [InlineData(null, "resolved", "resolved")]
    [InlineData(null, null, "DisplayName")]
    [InlineData(null, "", "")]
    public void Column_FollowsTheOrder(string? attribute, string? resolved, string expected)
    {
        JauntyConfig.ColumnNameResolver = _ => resolved!;

        Assert.Equal(expected, NameResolution.Column("DisplayName", attribute));
    }

    [Fact]
    public void Column_PassesThePropertyNameToTheResolver()
    {
        JauntyConfig.ColumnNameResolver = name => name + "_col";

        Assert.Equal("DisplayName_col", NameResolution.Column("DisplayName", null));
    }

    [Fact]
    public void AnAttributeName_IsNeverPassedToTheResolvers()
    {
        int calls = 0;
        JauntyConfig.TableNameResolver = _ => { calls++; return "t"; };
        JauntyConfig.SchemaNameResolver = _ => { calls++; return "s"; };
        JauntyConfig.ColumnNameResolver = _ => { calls++; return "c"; };

        NameResolution.Table(typeof(Widget), "attr");
        NameResolution.Schema(typeof(Widget), "attr");
        NameResolution.Column("DisplayName", "attr");

        Assert.Equal(0, calls);
    }
}
