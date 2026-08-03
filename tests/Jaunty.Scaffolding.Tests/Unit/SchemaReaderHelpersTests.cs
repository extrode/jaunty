using System.Reflection;

using Jaunty.Scaffolding.Providers;
using Jaunty.Scaffolding.Schema;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// AUD-R35-043. The primary-key marking loop existed three times, each with its own hand-written
/// <see cref="ColumnSchema"/> clone, and the copies had already diverged - MySQL's carried
/// <c>ColumnType</c>, PostgreSQL's and SQL Server's dropped it. One clone now, and a reflection
/// test so a property added to <c>ColumnSchema</c> cannot go missing from it.
/// </summary>
public class SchemaReaderHelpersTests
{
    private static ColumnSchema Populated() => new()
    {
        ColumnName = "Id",
        DataType = "tinyint",
        IsNullable = true,
        IsPrimaryKey = false,
        IsIdentity = true,
        IsComputed = true,
        MaxLength = 17,
        Precision = 5,
        Scale = 3,
        DefaultValue = "42",
        OrdinalPosition = 9,
        ColumnType = "tinyint(1) unsigned"
    };

    /// <summary>
    /// The one that was actually lost.
    /// </summary>
    [Fact]
    public void MarkingAKeyKeepsTheRawColumnType()
    {
        var columns = new List<ColumnSchema> { Populated() };

        SchemaReaderHelpers.MarkPrimaryKeyColumns(columns, new PrimaryKeyInfo { ConstraintName = "pk", Columns = ["Id"] });

        Assert.True(columns[0].IsPrimaryKey);
        Assert.Equal("tinyint(1) unsigned", columns[0].ColumnType);
    }

    /// <summary>
    /// Fails when a property is added to <see cref="ColumnSchema"/> and not to the clone, which is
    /// the failure mode that produced the divergence in the first place. Compares every readable
    /// property except <see cref="ColumnSchema.IsPrimaryKey"/>, which the clone deliberately sets.
    /// </summary>
    [Fact]
    public void EveryOtherPropertySurvivesTheClone()
    {
        ColumnSchema source = Populated();
        ColumnSchema clone = SchemaReaderHelpers.WithPrimaryKey(source);

        PropertyInfo[] properties = typeof(ColumnSchema).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.True(properties.Length >= 12);

        foreach (PropertyInfo property in properties)
        {
            if (property.Name == nameof(ColumnSchema.IsPrimaryKey))
                continue;

            Assert.Equal(property.GetValue(source), property.GetValue(clone));
        }

        Assert.True(clone.IsPrimaryKey);
    }

    /// <summary>
    /// Every value in the fixture differs from the type's default, so a clone that dropped a
    /// property could not pass the comparison above by coincidence.
    /// </summary>
    [Fact]
    public void TheFixtureLeavesNoPropertyAtItsDefault()
    {
        ColumnSchema source = Populated();

        foreach (PropertyInfo property in typeof(ColumnSchema).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name == nameof(ColumnSchema.IsPrimaryKey))
                continue;

            object? value = property.GetValue(source);
            object? fallback = property.PropertyType.IsValueType
                ? Activator.CreateInstance(property.PropertyType)
                : null;

            Assert.NotEqual(fallback, value);
        }
    }

    [Fact]
    public void ANullPrimaryKeyMarksNothing()
    {
        var columns = new List<ColumnSchema> { Populated() };

        SchemaReaderHelpers.MarkPrimaryKeyColumns(columns, null);

        Assert.False(columns[0].IsPrimaryKey);
    }

    [Fact]
    public void OnlyTheNamedColumnsAreMarked()
    {
        var columns = new List<ColumnSchema>
        {
            new() { ColumnName = "Id", DataType = "int" },
            new() { ColumnName = "Name", DataType = "text" }
        };

        SchemaReaderHelpers.MarkPrimaryKeyColumns(columns, new PrimaryKeyInfo { ConstraintName = "pk", Columns = ["Id"] });

        Assert.True(columns[0].IsPrimaryKey);
        Assert.False(columns[1].IsPrimaryKey);
    }

    /// <summary>
    /// Every reader matches key names case-insensitively, because the catalog casing of a key's
    /// column list need not match the column list's.
    /// </summary>
    [Fact]
    public void KeyColumnNamesMatchWithoutRegardToCase()
    {
        var columns = new List<ColumnSchema> { new() { ColumnName = "Id", DataType = "int" } };

        SchemaReaderHelpers.MarkPrimaryKeyColumns(columns, new PrimaryKeyInfo { ConstraintName = "pk", Columns = ["ID"] });

        Assert.True(columns[0].IsPrimaryKey);
    }

    [Fact]
    public void EveryColumnOfACompositeKeyIsMarked()
    {
        var columns = new List<ColumnSchema>
        {
            new() { ColumnName = "OrderId", DataType = "int" },
            new() { ColumnName = "LineNo", DataType = "int" },
            new() { ColumnName = "Note", DataType = "text" }
        };

        SchemaReaderHelpers.MarkPrimaryKeyColumns(
            columns, new PrimaryKeyInfo { ConstraintName = "pk", Columns = ["OrderId", "LineNo"] });

        Assert.True(columns[0].IsPrimaryKey);
        Assert.True(columns[1].IsPrimaryKey);
        Assert.False(columns[2].IsPrimaryKey);
    }
}
