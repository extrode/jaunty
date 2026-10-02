using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.FlatFiles.Core;
using Extrode.Jaunty.FlatFiles.Internals;

namespace Extrode.Jaunty.FlatFiles.Tests.Internals;

[Collection(GlobalConfigStateCollection.Name)]
public sealed class TableNameResolverConfigTests : IDisposable
{
    private readonly Func<Type, string?>? _originalTableResolver = JauntyConfig.TableNameResolver;

    public void Dispose() => JauntyConfig.Reconfigure(jc => jc.TableNameResolver = _originalTableResolver);

    public class Unattributed
    {
        public int Id { get; set; }
    }

    [Table("named")]
    public class Attributed
    {
        public int Id { get; set; }
    }

    [System.ComponentModel.DataAnnotations.Schema.Table("da_named")]
    public class DataAnnotated
    {
        public int Id { get; set; }
    }

    [Table("")]
    public class EmptyName
    {
        public int Id { get; set; }
    }

    [Fact]
    public void AResolver_NamesAnUnattributedEntity()
    {
        JauntyConfig.Reconfigure(jc => jc.TableNameResolver = t => "tbl_" + t.Name);

        Assert.Equal("tbl_Unattributed", TableNameResolver.Resolve<Unattributed>());
    }

    [Fact]
    public void AResolver_NamesAnEntityWhoseAttributeNameIsEmpty()
    {
        JauntyConfig.Reconfigure(jc => jc.TableNameResolver = t => "tbl_" + t.Name);

        Assert.Equal("tbl_EmptyName", TableNameResolver.Resolve<EmptyName>());
    }

    [Theory]
    [InlineData(typeof(Attributed), "named")]
    [InlineData(typeof(DataAnnotated), "da_named")]
    public void AnAttributeName_WinsAndTheResolverIsNotCalled(Type entity, string expected)
    {
        int calls = 0;
        JauntyConfig.Reconfigure(jc => jc.TableNameResolver = _ => { calls++; return "resolved"; });

        Assert.Equal(expected, TableNameResolver.Resolve(entity));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void AResolverReturningNull_FallsBackToTheLowercasedClassName()
    {
        JauntyConfig.Reconfigure(jc => jc.TableNameResolver = _ => null);

        Assert.Equal("unattributed", TableNameResolver.Resolve<Unattributed>());
    }

    [Fact]
    public void TheRegisteredSourceTakesTheResolvedName()
    {
        JauntyConfig.Reconfigure(jc => jc.TableNameResolver = t => "tbl_" + t.Name);
        var options = new FlatFileOptions();

        options.AddCsv<Unattributed>("rows.csv");

        Assert.Equal("tbl_Unattributed", Assert.Single(options.Sources).TableName);
    }
}
