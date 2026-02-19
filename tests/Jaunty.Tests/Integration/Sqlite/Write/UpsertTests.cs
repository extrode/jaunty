using System.Data;
using System.Data.Common;
using System.Data.SQLite;

using FluentAssertions;

using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Write;

/// <summary>
/// Tests Upsert and UpsertAsync against the shared Northwind.db.
/// All writes are wrapped in transactions that are ALWAYS rolled back
/// to preserve the database for other tests.
/// </summary>
public class UpsertTests : IDisposable
{
    private readonly Database _db;

    public UpsertTests()
    {
        _db = new Database();
        if (_db.Connection.State == ConnectionState.Closed)
            _db.Connection.Open();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ExecuteSql(IDbTransaction transaction, string sql, object? parameters = null)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = transaction;

        if (parameters != null)
        {
            foreach (var prop in parameters.GetType().GetProperties())
            {
                var param = cmd.CreateParameter();
                param.ParameterName = "@" + prop.Name;
                param.Value = prop.GetValue(parameters) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        }

        cmd.ExecuteNonQuery();
    }

    private string? QueryScalarString(IDbTransaction transaction, string sql, object? parameters = null)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = transaction;

        if (parameters != null)
        {
            foreach (var prop in parameters.GetType().GetProperties())
            {
                var param = cmd.CreateParameter();
                param.ParameterName = "@" + prop.Name;
                param.Value = prop.GetValue(parameters) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        }

        return cmd.ExecuteScalar()?.ToString();
    }

    [Fact]
    public void Upsert_NewEntity_InsertsRecord()
    {
        using var transaction = _db.Connection.BeginTransaction();
        try
        {
            var maxId = _db.Connection.QueryScalar<long>(
                "SELECT MAX(category_id) FROM categories",
                CommandOptions<long>.WithTransaction(transaction));
            var newId = (int)(maxId + 100);

            var category = new Category
            {
                CategoryId = newId,
                CategoryName = "UpsertTest",
                Description = "Test category for upsert"
            };

            var result = _db.Connection.Upsert(category,
                CommandOptions.WithTransaction(transaction));

            result.Should().Be(1);

            var insertedName = QueryScalarString(transaction,
                "SELECT category_name FROM categories WHERE category_id = @id",
                new { id = newId });

            insertedName.Should().Be("UpsertTest");
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Fact]
    public void Upsert_ExistingEntity_UpdatesRecord()
    {
        using var transaction = _db.Connection.BeginTransaction();
        try
        {
            var maxId = _db.Connection.QueryScalar<long>(
                "SELECT MAX(category_id) FROM categories",
                CommandOptions<long>.WithTransaction(transaction));
            var testId = (int)(maxId + 101);

            ExecuteSql(transaction,
                "INSERT INTO categories (category_id, category_name, description) VALUES (@id, @name, @desc)",
                new { id = testId, name = "OriginalName", desc = "Original description" });

            var category = new Category
            {
                CategoryId = testId,
                CategoryName = "UpdatedName",
                Description = "Updated description"
            };

            var result = _db.Connection.Upsert(category,
                CommandOptions.WithTransaction(transaction));

            result.Should().Be(1);

            var updatedName = QueryScalarString(transaction,
                "SELECT category_name FROM categories WHERE category_id = @id",
                new { id = testId });

            updatedName.Should().Be("UpdatedName");
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Fact]
    public async Task UpsertAsync_NewEntity_InsertsRecord()
    {
        var dbConn = (DbConnection)_db.Connection;
        using var transaction = dbConn.BeginTransaction();
        try
        {
            var maxId = _db.Connection.QueryScalar<long>(
                "SELECT MAX(category_id) FROM categories",
                CommandOptions<long>.WithTransaction(transaction));
            var newId = (int)(maxId + 102);

            var category = new Category
            {
                CategoryId = newId,
                CategoryName = "AsyncUpsertTest",
                Description = "Async test category"
            };

            var result = await dbConn.UpsertAsync(category,
                CommandOptions.WithTransaction(transaction));

            result.Should().Be(1);

            var insertedName = QueryScalarString(transaction,
                "SELECT category_name FROM categories WHERE category_id = @id",
                new { id = newId });

            insertedName.Should().Be("AsyncUpsertTest");
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Fact]
    public async Task UpsertAsync_ExistingEntity_UpdatesRecord()
    {
        var dbConn = (DbConnection)_db.Connection;
        using var transaction = dbConn.BeginTransaction();
        try
        {
            var maxId = _db.Connection.QueryScalar<long>(
                "SELECT MAX(category_id) FROM categories",
                CommandOptions<long>.WithTransaction(transaction));
            var testId = (int)(maxId + 103);

            ExecuteSql(transaction,
                "INSERT INTO categories (category_id, category_name, description) VALUES (@id, @name, @desc)",
                new { id = testId, name = "AsyncOriginal", desc = "Async original" });

            var category = new Category
            {
                CategoryId = testId,
                CategoryName = "AsyncUpdated",
                Description = "Async updated"
            };

            var result = await dbConn.UpsertAsync(category,
                CommandOptions.WithTransaction(transaction));

            result.Should().Be(1);

            var updatedName = QueryScalarString(transaction,
                "SELECT category_name FROM categories WHERE category_id = @id",
                new { id = testId });

            updatedName.Should().Be("AsyncUpdated");
        }
        finally
        {
            transaction.Rollback();
        }
    }
}
