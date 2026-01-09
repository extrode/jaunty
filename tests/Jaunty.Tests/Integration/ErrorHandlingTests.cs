using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration;

public class ErrorHandlingTests : IDisposable
{
    private readonly Database _db;

    public ErrorHandlingTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    #region Null/Invalid Arguments

    [Fact]
    public void Query_NullConnection_ThrowsArgumentNullException()
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Query<Category>("SELECT * FROM categories"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void Query_NullSql_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.Query<Category>(null!));

        Assert.Contains("SQL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_EmptySql_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.Query<Category>(""));

        Assert.Contains("SQL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_WhitespaceSql_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.Query<Category>("   \t\n  "));

        Assert.Contains("SQL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QueryScalar_NullConnection_ThrowsArgumentNullException()
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.QueryScalar<int>("SELECT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryScalar_NullSql_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.QueryScalar<int>(null!));
    }

    #endregion

    #region SQL Errors

    [Fact]
    public void Query_InvalidSql_ThrowsSqlException()
    {
        Assert.ThrowsAny<Exception>(() =>
            _db.Connection.Query<Category>("SELECT * FROM nonexistent_table"));
    }

    [Fact]
    public void Query_SyntaxError_ThrowsSqlException()
    {
        Assert.ThrowsAny<Exception>(() =>
            _db.Connection.Query<Category>("SELEC * FRO categories"));
    }

    [Fact]
    public void QueryScalar_InvalidSql_ThrowsSqlException()
    {
        Assert.ThrowsAny<Exception>(() =>
            _db.Connection.QueryScalar<int>("SELECT COUNT(*) FROM nonexistent_table"));
    }

    #endregion

    #region Type Conversion Errors

    [Fact]
    public void QueryScalar_TypeMismatch_ThrowsException()
    {
        // Trying to read a string as a DateTime
        Assert.ThrowsAny<Exception>(() =>
            _db.Connection.QueryScalar<DateTime>("SELECT product_name FROM products WHERE product_id = 1"));
    }

    #endregion

    #region Parameter Binding Errors

    [Fact]
    public void Query_MissingParameter_ThrowsParameterCountException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.Query<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id AND category_name = @Name",
                new { Id = 1 })); // Missing Name parameter

        Assert.Contains("@Name", ex.Message);
    }

    [Fact]
    public void Query_ExtraParameter_Throws()
    {
        // Extra parameters in the anonymous object cause an error
        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.Query<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
                new { Id = 1, Extra1 = 2, Extra2 = 3 }));

        Assert.Contains("Unused parameter", ex.Message);
    }

    #endregion
}
