using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Fluent.SourceGen.Tests.Entities;
using Extrode.Jaunty.Internals;

namespace Extrode.Jaunty.Fluent.SourceGen.Tests.Integration;

[Collection(NamingResolverCollection.Name)]
public sealed class ConfigureAfterGeneratedReadTests : IDisposable
{
    public ConfigureAfterGeneratedReadTests() => JauntyConfig.Reset();

    public void Dispose() => JauntyConfig.Reset();

    [Fact]
    public void ReadingAGeneratedTableName_CountsAsFirstUse()
    {
        _ = AuditEntry.Jaunty.TableName;

        Assert.True(ConfigurationGeneration.HasBeenRead);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JauntyConfig.Configure(c => c.TableNameResolver = t => "x_" + t.Name));
        Assert.Contains("already read its settings", ex.Message);
        Assert.Equal("audit_entries", AuditEntry.Jaunty.TableName);
    }

    [Fact]
    public void ConfigureFirst_ThenTheGeneratedTableNameFollowsIt()
    {
        JauntyConfig.Configure(c => c.TableNameResolver = t => "cfg_" + t.Name);

        Assert.True(JauntyConfig.IsConfigured);
        Assert.Equal("cfg_ResolvedNameWidget", ResolvedNameWidget.Jaunty.TableName);
    }
}
