using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Tests for null parameter validation across Write public APIs.
/// </summary>
public class WriteNullParameterTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public WriteNullParameterTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    #region Insert Methods

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Insert_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;
        var category = new Category { CategoryName = "Test" };

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Insert(category));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Insert_NullEntity_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        Category? entity = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.Insert(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task InsertAsync_NullEntity_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        Category? entity = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection.InsertAsync(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task InsertAsync_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;
        var category = new Category { CategoryName = "Test" };

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.InsertAsync(category));

        Assert.Equal("connection", ex.ParamName);
    }

    #endregion

    #region Update Methods

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Update_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;
        var category = new Category { CategoryId = 1, CategoryName = "Test" };

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Update(category));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Update_NullEntity_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        Category? entity = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.Update(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task UpdateAsync_NullEntity_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        Category? entity = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection.UpdateAsync(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task UpdateAsync_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;
        var category = new Category { CategoryId = 1, CategoryName = "Test" };

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.UpdateAsync(category));

        Assert.Equal("connection", ex.ParamName);
    }

    #endregion

    #region Delete Methods

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Delete_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;
        var category = new Category { CategoryId = 1, CategoryName = "Test" };

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Delete(category));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Delete_NullEntity_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        Category? entity = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.Delete(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DeleteById_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Delete<Category>(1));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void DeleteById_NullId_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        object? id = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.Delete<Category>(id!));

        Assert.Equal("id", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsync_NullEntity_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        Category? entity = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection.DeleteAsync(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task DeleteAsync_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;
        var category = new Category { CategoryId = 1, CategoryName = "Test" };

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.DeleteAsync(category));

        Assert.Equal("connection", ex.ParamName);
    }

    #endregion

    #region Upsert Methods

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Upsert_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;
        var category = new Category { CategoryId = 1, CategoryName = "Test" };

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Upsert(category));

        Assert.Equal("connection", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Upsert_NullEntity_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        Category? entity = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection.Upsert(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task UpsertAsync_NullEntity_ThrowsArgumentNullException(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        Category? entity = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection.UpsertAsync(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task UpsertAsync_NullConnection_ThrowsArgumentNullException(DialectInfo dialect)
    {
        System.Data.IDbConnection? connection = null;
        var category = new Category { CategoryId = 1, CategoryName = "Test" };

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.UpsertAsync(category));

        Assert.Equal("connection", ex.ParamName);
    }

    #endregion
}
