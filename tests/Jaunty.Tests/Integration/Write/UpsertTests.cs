using System.Data;
using System.Data.Common;

using FluentAssertions;

using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Tests Upsert and UpsertAsync against Northwind categories.
/// Writes are always rolled back.
/// </summary>
public class UpsertTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public UpsertTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static void ExecuteSql(IDbConnection connection, IDbTransaction transaction, string sql, object? parameters = null)
    {
        using var cmd = connection.CreateCommand();
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

    private static string? QueryScalarString(IDbConnection connection, IDbTransaction transaction, string sql, object? parameters = null)
    {
        using var cmd = connection.CreateCommand();
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

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Upsert_NewEntity_InsertsRecord(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var transaction = connection.BeginTransaction();
        try
        {
            var maxId = connection.QueryScalar<long>(
                "SELECT MAX(category_id) FROM categories",
                CommandOptions<long>.WithTransaction(transaction));
            var newId = (int)(maxId + 100);

            var category = new Category
            {
                CategoryId = newId,
                CategoryName = "UpsertTest",
                Description = "Test category for upsert"
            };

            var result = connection.Upsert(category, CommandOptions.WithTransaction(transaction));
            result.Should().Be(1);

            var insertedName = QueryScalarString(connection, transaction,
                "SELECT category_name FROM categories WHERE category_id = @id",
                new { id = newId });

            insertedName.Should().Be("UpsertTest");
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Upsert_ExistingEntity_UpdatesRecord(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var transaction = connection.BeginTransaction();
        try
        {
            var maxId = connection.QueryScalar<long>(
                "SELECT MAX(category_id) FROM categories",
                CommandOptions<long>.WithTransaction(transaction));
            var testId = (int)(maxId + 101);

            ExecuteSql(connection, transaction,
                "INSERT INTO categories (category_id, category_name, description) VALUES (@id, @name, @desc)",
                new { id = testId, name = "OriginalName", desc = "Original description" });

            var category = new Category
            {
                CategoryId = testId,
                CategoryName = "UpdatedName",
                Description = "Updated description"
            };

            var result = connection.Upsert(category, CommandOptions.WithTransaction(transaction));
            result.Should().Be(1);

            var updatedName = QueryScalarString(connection, transaction,
                "SELECT category_name FROM categories WHERE category_id = @id",
                new { id = testId });

            updatedName.Should().Be("UpdatedName");
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task UpsertAsync_NewEntity_InsertsRecord(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var transaction = connection.BeginTransaction();
        try
        {
            var maxId = connection.QueryScalar<long>(
                "SELECT MAX(category_id) FROM categories",
                CommandOptions<long>.WithTransaction(transaction));
            var newId = (int)(maxId + 102);

            var category = new Category
            {
                CategoryId = newId,
                CategoryName = "AsyncUpsertTest",
                Description = "Async test category"
            };

            var result = await connection.UpsertAsync(category,
                CommandOptions.WithTransaction(transaction));

            result.Should().Be(1);

            var insertedName = QueryScalarString(connection, transaction,
                "SELECT category_name FROM categories WHERE category_id = @id",
                new { id = newId });

            insertedName.Should().Be("AsyncUpsertTest");
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task UpsertAsync_ExistingEntity_UpdatesRecord(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var transaction = connection.BeginTransaction();
        try
        {
            var maxId = connection.QueryScalar<long>(
                "SELECT MAX(category_id) FROM categories",
                CommandOptions<long>.WithTransaction(transaction));
            var testId = (int)(maxId + 103);

            ExecuteSql(connection, transaction,
                "INSERT INTO categories (category_id, category_name, description) VALUES (@id, @name, @desc)",
                new { id = testId, name = "AsyncOriginal", desc = "Async original" });

            var category = new Category
            {
                CategoryId = testId,
                CategoryName = "AsyncUpdated",
                Description = "Async updated"
            };

            var result = await connection.UpsertAsync(category,
                CommandOptions.WithTransaction(transaction));

            result.Should().Be(1);

            var updatedName = QueryScalarString(connection, transaction,
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
