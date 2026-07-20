using Jaunty.Attributes;
using Jaunty.FlatFiles.DuckDB.Internals.Import;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// Regression tests for <see cref="TargetDdlGenerator"/>: single-key resolution, the composite-key
/// guard rail (AUD-R9), and column definition generation.
/// </summary>
public class TargetDdlGeneratorTests
{
    private sealed class SingleKeyEntity
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class NoKeyEntity
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class CompositeKeyEntity
    {
        [Key]
        public int TenantId { get; set; }

        [Key]
        public int OrderId { get; set; }

        public string Description { get; set; } = string.Empty;
    }

    private sealed class CompositeKeyWithColumnAttributeEntity
    {
        [Key]
        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Key]
        public int OrderId { get; set; }
    }

    [Fact]
    public void GetKeyColumnName_SingleKeyProperty_ReturnsPropertyName()
    {
        var result = TargetDdlGenerator.GetKeyColumnName(typeof(SingleKeyEntity));

        Assert.Equal("Id", result);
    }

    [Fact]
    public void GetKeyColumnName_NoKeyProperty_ReturnsNull()
    {
        var result = TargetDdlGenerator.GetKeyColumnName(typeof(NoKeyEntity));

        Assert.Null(result);
    }

    [Fact]
    public void GetKeyColumnName_CompositeKey_ThrowsNotSupportedException()
    {
        var ex = Assert.Throws<NotSupportedException>(
            () => TargetDdlGenerator.GetKeyColumnName(typeof(CompositeKeyEntity)));

        Assert.Contains(nameof(CompositeKeyEntity), ex.Message);
        Assert.Contains("TenantId", ex.Message);
        Assert.Contains("OrderId", ex.Message);
    }

    [Fact]
    public void GetKeyColumnName_CompositeKeyWithColumnAttribute_ThrowsNotSupportedException()
    {
        // Verifies the exception fires before silently truncating to the first [Key] property,
        // even when that property also carries a [Column] attribute.
        var ex = Assert.Throws<NotSupportedException>(
            () => TargetDdlGenerator.GetKeyColumnName(typeof(CompositeKeyWithColumnAttributeEntity)));

        Assert.Contains(nameof(CompositeKeyWithColumnAttributeEntity), ex.Message);
    }

    [Fact]
    public void GetColumnDefinitions_SingleKeyEntity_MarksKeyColumnAsPrimaryKey()
    {
        var columns = TargetDdlGenerator.GetColumnDefinitions(typeof(SingleKeyEntity));

        var idColumn = columns.Single(c => c.Name == "Id");
        Assert.True(idColumn.IsPrimaryKey);

        var nameColumn = columns.Single(c => c.Name == "Name");
        Assert.False(nameColumn.IsPrimaryKey);
    }
}
