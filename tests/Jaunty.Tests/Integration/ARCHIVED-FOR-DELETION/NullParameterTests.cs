using System.Data;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite;

/// <summary>
/// Tests for null parameter validation across all public APIs.
/// Covers P0 Critical Issue #4: Missing Null Checks in Public APIs
/// </summary>
public class NullParameterTests : IDisposable
{
    private readonly Database _db;

    public NullParameterTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    #region Query Methods

    [Fact]
    public void Query_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Query<Category>("SELECT * FROM categories"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void Query_NullSql_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.Query<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public void Query_EmptySql_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _db.Connection.Query<Category>(""));

        Assert.Contains("sql", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_NullParameters_DoesNotThrow()
    {
        // null parameters is valid - treated as no parameters
        var categories = _db.Connection.QueryPartial<Category>(
            "SELECT * FROM categories LIMIT 1",
            (object?)null);

        Assert.NotNull(categories);
    }

    [Fact]
    public async Task QueryAsync_NullConnection_ThrowsInvalidOperationException()
    {
        IDbConnection? connection = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection!.QueryAsync<Category>("SELECT * FROM categories"));

        Assert.Contains("Async connection requires a DbConnection", ex.Message);
    }

    [Fact]
    public async Task QueryAsync_NullSql_ThrowsArgumentNullException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.QueryAsync<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    #endregion

    #region QueryFirst Methods

    [Fact]
    public void QueryFirst_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.QueryFirst<Category>("SELECT * FROM categories LIMIT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryFirst_NullSql_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.QueryFirst<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public async Task QueryFirstAsync_NullConnection_ThrowsInvalidOperationException()
    {
        IDbConnection? connection = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection!.QueryFirstAsync<Category>("SELECT * FROM categories LIMIT 1"));

        Assert.Contains("Async connection requires a DbConnection", ex.Message);
    }

    [Fact]
    public async Task QueryFirstAsync_NullSql_ThrowsArgumentNullException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.QueryFirstAsync<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    #endregion

    #region QuerySingle Methods

    [Fact]
    public void QuerySingle_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.QuerySingle<Category>("SELECT * FROM categories LIMIT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QuerySingle_NullSql_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.QuerySingle<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public async Task QuerySingleAsync_NullConnection_ThrowsInvalidOperationException()
    {
        IDbConnection? connection = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection!.QuerySingleAsync<Category>("SELECT * FROM categories LIMIT 1"));

        Assert.Contains("Async connection requires a DbConnection", ex.Message);
    }

    [Fact]
    public async Task QuerySingleAsync_NullSql_ThrowsArgumentNullException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.QuerySingleAsync<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    #endregion

    #region QueryFirstOrDefault Methods

    [Fact]
    public void QueryFirstOrDefault_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.QueryFirstOrDefault<Category>("SELECT * FROM categories LIMIT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryFirstOrDefault_NullSql_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.QueryFirstOrDefault<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public async Task QueryFirstOrDefaultAsync_NullConnection_ThrowsInvalidOperationException()
    {
        IDbConnection? connection = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection!.QueryFirstOrDefaultAsync<Category>("SELECT * FROM categories LIMIT 1"));

        Assert.Contains("Async connection requires a DbConnection", ex.Message);
    }

    [Fact]
    public async Task QueryFirstOrDefaultAsync_NullSql_ThrowsArgumentNullException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.QueryFirstOrDefaultAsync<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    #endregion

    #region QueryPartial Methods

    [Fact]
    public void QueryPartial_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.QueryPartial<Category>("SELECT * FROM categories"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryPartial_NullSql_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.QueryPartial<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public async Task QueryPartialAsync_NullConnection_ThrowsInvalidOperationException()
    {
        IDbConnection? connection = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection!.QueryPartialAsync<Category>("SELECT * FROM categories"));

        Assert.Contains("Async connection requires a DbConnection", ex.Message);
    }

    [Fact]
    public async Task QueryPartialAsync_NullSql_ThrowsArgumentNullException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.QueryPartialAsync<Category>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    #endregion

    #region QueryScalar Methods

    [Fact]
    public void QueryScalar_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.QueryScalar<int>("SELECT 1"));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void QueryScalar_NullSql_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.QueryScalar<int>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    [Fact]
    public async Task QueryScalarAsync_NullConnection_ThrowsInvalidOperationException()
    {
        IDbConnection? connection = null;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await connection!.QueryScalarAsync<int>("SELECT 1"));

        Assert.Contains("Async connection requires a DbConnection", ex.Message);
    }

    [Fact]
    public async Task QueryScalarAsync_NullSql_ThrowsArgumentNullException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.QueryScalarAsync<int>(null!));

        Assert.Equal("sql", ex.ParamName);
    }

    #endregion

    #region Insert Methods

    [Fact]
    public void Insert_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var category = new Category { CategoryName = "Test" };

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Insert(category));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void Insert_NullEntity_ThrowsArgumentNullException()
    {
        Category? entity = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.Insert(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    [Fact]
    public async Task InsertAsync_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var category = new Category { CategoryName = "Test" };

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.InsertAsync(category));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public async Task InsertAsync_NullEntity_ThrowsArgumentNullException()
    {
        Category? entity = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.InsertAsync(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    #endregion

    #region Update Methods

    [Fact]
    public void Update_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var category = new Category { CategoryId = 1, CategoryName = "Test" };

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Update(category));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void Update_NullEntity_ThrowsArgumentNullException()
    {
        Category? entity = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.Update(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    [Fact]
    public async Task UpdateAsync_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var category = new Category { CategoryId = 1, CategoryName = "Test" };

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.UpdateAsync(category));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public async Task UpdateAsync_NullEntity_ThrowsArgumentNullException()
    {
        Category? entity = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.UpdateAsync(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    #endregion

    #region Delete Methods

    [Fact]
    public void Delete_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var category = new Category { CategoryId = 1, CategoryName = "Test" };

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Delete(category));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void Delete_NullEntity_ThrowsArgumentNullException()
    {
        Category? entity = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.Delete(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    [Fact]
    public void DeleteById_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Delete<Category>(1));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void DeleteById_NullId_ThrowsArgumentNullException()
    {
        object? id = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.Delete<Category>(id!));

        Assert.Equal("id", ex.ParamName);
    }

    [Fact]
    public async Task DeleteAsync_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var category = new Category { CategoryId = 1, CategoryName = "Test" };

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.DeleteAsync(category));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public async Task DeleteAsync_NullEntity_ThrowsArgumentNullException()
    {
        Category? entity = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.DeleteAsync(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    #endregion

    #region BulkInsert Methods

    [Fact]
    public void BulkInsert_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var categories = new List<Category> { new() { CategoryName = "Test" } };

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.BulkInsert(categories));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void BulkInsert_NullEntities_ThrowsArgumentNullException()
    {
        IEnumerable<Category>? entities = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.BulkInsert(entities!));

        Assert.Equal("entities", ex.ParamName);
    }

    [Fact]
    public async Task BulkInsertAsync_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var categories = new List<Category> { new() { CategoryName = "Test" } };

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.BulkInsertAsync(categories));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public async Task BulkInsertAsync_NullEntities_ThrowsArgumentNullException()
    {
        IEnumerable<Category>? entities = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.BulkInsertAsync(entities!));

        Assert.Equal("entities", ex.ParamName);
    }

    #endregion

    #region BulkUpdate Methods

    [Fact]
    public void BulkUpdate_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var categories = new List<Category> { new() { CategoryId = 1, CategoryName = "Test" } };

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.BulkUpdate(categories));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void BulkUpdate_NullEntities_ThrowsArgumentNullException()
    {
        IEnumerable<Category>? entities = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.BulkUpdate(entities!));

        Assert.Equal("entities", ex.ParamName);
    }

    [Fact]
    public async Task BulkUpdateAsync_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var categories = new List<Category> { new() { CategoryId = 1, CategoryName = "Test" } };

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.BulkUpdateAsync(categories));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public async Task BulkUpdateAsync_NullEntities_ThrowsArgumentNullException()
    {
        IEnumerable<Category>? entities = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.BulkUpdateAsync(entities!));

        Assert.Equal("entities", ex.ParamName);
    }

    #endregion

    #region BulkDelete Methods

    [Fact]
    public void BulkDelete_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var categories = new List<Category> { new() { CategoryId = 1 } };

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.BulkDelete(categories));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void BulkDelete_NullEntities_ThrowsArgumentNullException()
    {
        IEnumerable<Category>? entities = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.BulkDelete(entities!));

        Assert.Equal("entities", ex.ParamName);
    }

    [Fact]
    public async Task BulkDeleteAsync_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var categories = new List<Category> { new() { CategoryId = 1 } };

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.BulkDeleteAsync(categories));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public async Task BulkDeleteAsync_NullEntities_ThrowsArgumentNullException()
    {
        IEnumerable<Category>? entities = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.BulkDeleteAsync(entities!));

        Assert.Equal("entities", ex.ParamName);
    }

    #endregion

    #region Upsert Methods

    [Fact]
    public void Upsert_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var category = new Category { CategoryId = 1, CategoryName = "Test" };

        var ex = Assert.Throws<ArgumentNullException>(() =>
            connection!.Upsert(category));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public void Upsert_NullEntity_ThrowsArgumentNullException()
    {
        Category? entity = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            _db.Connection.Upsert(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    [Fact]
    public async Task UpsertAsync_NullConnection_ThrowsArgumentNullException()
    {
        IDbConnection? connection = null;
        var category = new Category { CategoryId = 1, CategoryName = "Test" };

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await connection!.UpsertAsync(category));

        Assert.Equal("connection", ex.ParamName);
    }

    [Fact]
    public async Task UpsertAsync_NullEntity_ThrowsArgumentNullException()
    {
        Category? entity = null;

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.Connection.UpsertAsync(entity!));

        Assert.Equal("entity", ex.ParamName);
    }

    #endregion
}
