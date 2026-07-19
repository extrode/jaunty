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
}
