using Jaunty.Internals.Entity;

namespace Jaunty.Tests.Unit.Internals.Entity;

public class EntityMetadataTests
{
    private static ColumnMetadata Column(string columnName, bool isPrimaryKey = false) =>
        new("Prop_" + columnName, typeof(string), columnName, isPrimaryKey, isIdentity: false, isComputed: false,
            getter: _ => columnName, setter: (_, _) => { });

    [Fact]
    public void TheConstructor_CopiesTheCallersList_SoALaterAddCannotReachColumns()
    {
        var columns = new List<ColumnMetadata> { Column("id", isPrimaryKey: true), Column("name") };

        var metadata = new EntityMetadata("t", null, columns);
        columns.Add(Column("added_after"));

        Assert.Equal(2, metadata.Columns.Count);
        Assert.DoesNotContain(metadata.Columns, c => c.ColumnName == "added_after");
    }

    [Fact]
    public void TheConstructor_CopiesTheCallersList_SoALaterAddCannotDesynchroniseTheDerivedCollections()
    {
        var columns = new List<ColumnMetadata> { Column("id", isPrimaryKey: true), Column("name") };

        var metadata = new EntityMetadata("t", null, columns);
        columns.Add(Column("added_after"));

        Assert.Equal(metadata.Columns.Count, metadata.PrimaryKeys.Count + metadata.NonPrimaryKeyColumns.Count);
        Assert.Equal(2, metadata.NonIdentityColumns.Count);
        Assert.Equal(2, metadata.InsertColumns.Count);
        Assert.Equal(2, metadata.ParameterMap.Count);
        Assert.False(metadata.ParameterMap.ContainsKey("added_after"));
    }

    [Fact]
    public void TheConstructor_CopiesTheCallersList_SoALaterAddCannotBypassTheDuplicateColumnGuard()
    {
        var columns = new List<ColumnMetadata> { Column("id", isPrimaryKey: true), Column("name") };
        var metadata = new EntityMetadata("t", null, columns);

        columns.Add(Column("NAME"));

        Assert.Single(metadata.Columns, c => string.Equals(c.ColumnName, "name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheConstructor_StillThrowsOnDuplicateColumnNames_RegardlessOfCasing()
    {
        var columns = new List<ColumnMetadata> { Column("name"), Column("NAME") };

        ArgumentException ex = Assert.Throws<ArgumentException>(() => new EntityMetadata("t", null, columns));

        Assert.Contains("NAME", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheConstructor_AcceptsANonListSequence()
    {
        IEnumerable<ColumnMetadata> columns = new[] { Column("id", isPrimaryKey: true), Column("name") }.AsEnumerable();

        var metadata = new EntityMetadata("t", "dbo", columns);

        Assert.Equal("t", metadata.TableName);
        Assert.Equal("dbo", metadata.SchemaName);
        Assert.Equal(2, metadata.Columns.Count);
        Assert.Single(metadata.PrimaryKeys);
    }
}
