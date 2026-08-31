using Jaunty.Scaffolding.Providers.SqlServer;
using Jaunty.Scaffolding.Schema;
using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

public class SqlServerSchemaReaderTests
{
    [Fact]
    public void MarkPrimaryKeyColumns_MultiColumnTable_DoesNotThrow()
    {
        var columns = new List<ColumnSchema>
        {
            new() { ColumnName = "actor_id", DataType = "int", OrdinalPosition = 1 },
            new() { ColumnName = "first_name", DataType = "varchar", OrdinalPosition = 2 },
            new() { ColumnName = "last_name", DataType = "varchar", OrdinalPosition = 3 },
            new() { ColumnName = "last_update", DataType = "datetime", OrdinalPosition = 4 }
        };
        var primaryKey = new PrimaryKeyInfo { ConstraintName = "PK_actor", Columns = ["actor_id"] };

        var exception = Record.Exception(() => SqlServerSchemaReader.MarkPrimaryKeyColumns(columns, primaryKey));

        Assert.Null(exception);
        Assert.True(columns[0].IsPrimaryKey);
        Assert.False(columns[1].IsPrimaryKey);
    }

    [Fact]
    public void MarkPrimaryKeyColumns_CompositeKeyAcrossNonAdjacentColumns_MarksAllKeyColumns()
    {
        var columns = new List<ColumnSchema>
        {
            new() { ColumnName = "actor_id", DataType = "int", OrdinalPosition = 1 },
            new() { ColumnName = "film_id", DataType = "int", OrdinalPosition = 2 },
            new() { ColumnName = "last_update", DataType = "datetime", OrdinalPosition = 3 }
        };
        var primaryKey = new PrimaryKeyInfo
        {
            ConstraintName = "PK_film_actor",
            Columns = ["actor_id", "film_id"]
        };

        SqlServerSchemaReader.MarkPrimaryKeyColumns(columns, primaryKey);

        Assert.True(columns[0].IsPrimaryKey);
        Assert.True(columns[1].IsPrimaryKey);
        Assert.False(columns[2].IsPrimaryKey);
    }

    [Fact]
    public void MarkPrimaryKeyColumns_NoPrimaryKey_LeavesColumnsUnchanged()
    {
        // IsPrimaryKey starts true (an atypical initial state) so this assertion would fail
        // if a null primaryKey ever caused the method to reset column state instead of
        // taking its early-return no-op path - IsPrimaryKey's own default (false) can't
        // distinguish "the method ran and left it alone" from "the method never ran".
        var columns = new List<ColumnSchema>
        {
            new() { ColumnName = "col1", DataType = "int", OrdinalPosition = 1, IsPrimaryKey = true }
        };

        SqlServerSchemaReader.MarkPrimaryKeyColumns(columns, null);

        Assert.True(columns[0].IsPrimaryKey);
    }

    [Theory]
    [InlineData("nvarchar", (short)100, (short)50)]
    [InlineData("nchar", (short)20, (short)10)]
    public void NormalizeMaxLength_UnicodeCharacterTypes_HalvesByteLength(string dataType, short rawMaxLength, short expected)
    {
        short? result = SqlServerSchemaReader.NormalizeMaxLength(dataType, rawMaxLength);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("varchar", (short)100)]
    [InlineData("char", (short)20)]
    public void NormalizeMaxLength_NonUnicodeCharacterTypes_LeavesByteLengthUnchanged(string dataType, short rawMaxLength)
    {
        short? result = SqlServerSchemaReader.NormalizeMaxLength(dataType, rawMaxLength);

        Assert.Equal(rawMaxLength, result);
    }

    [Fact]
    public void NormalizeMaxLength_UnicodeTypeWithMaxSentinel_LeavesMinusOneUnchanged()
    {
        short? result = SqlServerSchemaReader.NormalizeMaxLength("nvarchar", -1);

        Assert.Equal((short)-1, result);
    }

    [Fact]
    public void NormalizeMaxLength_NullMaxLength_ReturnsNull()
    {
        short? result = SqlServerSchemaReader.NormalizeMaxLength("nvarchar", null);

        Assert.Null(result);
    }

    // AUD-R22: text/ntext/image are legacy LOB types where sys.columns.max_length is always a
    // fixed sentinel of 16 (the internal data pointer size), not a real byte length - halving it
    // (ntext) or passing it through (text/image) produced a nonsensical [MaxLength(8)] or
    // [MaxLength(16)] attribute that silently truncates real data far under the column's true,
    // effectively unbounded, capacity. 16 is the real-world sentinel SQL Server actually reports;
    // the type must be treated as unbounded (null) regardless of the raw value.
    [Theory]
    [InlineData("text", (short)16)]
    [InlineData("ntext", (short)16)]
    [InlineData("image", (short)16)]
    [InlineData("text", (short)32)]
    public void NormalizeMaxLength_LegacyLobTypes_ReturnsNullRegardlessOfRawValue(string dataType, short rawMaxLength)
    {
        short? result = SqlServerSchemaReader.NormalizeMaxLength(dataType, rawMaxLength);

        Assert.Null(result);
    }
}
