using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.FlatFiles.DuckDB.Internals;
using Extrode.Jaunty.FlatFiles.DuckDB.Internals.Import;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// Full messages for the type-level mapping guards: an entity with nothing mapped, and an entity
/// with two <c>[Key]</c> properties. Also the null-key guard on <c>ImportDialectResolver.Register</c>.
/// </summary>
public class MappingGuardMessageTests
{
    public class NothingMapped
    {
        public int ReadOnly => 1;
    }

    public class TwoKeys
    {
        [Key] public int First { get; set; }
        [Key] public int Second { get; set; }
    }

    [Fact]
    public void AnEntityWithNoMappedProperties_SaysWhatCountsAsMapped()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ColumnMappingCache.Get(typeof(NothingMapped)));

        Assert.Equal(
            $"Entity type '{typeof(NothingMapped).FullName}' has no mapped properties, so it cannot be read " +
            "from or written to a flat file. A property is mapped when it is public, has " +
            "both a getter and a setter, and carries neither [Ignore] nor [NotMapped].",
            ex.Message);
    }

    [Fact]
    public void TwoKeyProperties_AreNamedAndRejected()
    {
        var ex = Assert.Throws<NotSupportedException>(() => TargetDdlGenerator.GetKeyColumnName(typeof(TwoKeys)));

        Assert.Equal(
            "Entity type 'TwoKeys' has more than one [Key] property ('First' and 'Second'). Composite keys " +
            "are not currently supported for import conflict resolution or PRIMARY KEY DDL generation.",
            ex.Message);
    }

    [Fact]
    public void Register_ANullKey_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ImportDialectResolver.Register(null!, new SqliteImportDialect()));

        Assert.Equal("connectionTypeNameContains", ex.ParamName);
    }
}
