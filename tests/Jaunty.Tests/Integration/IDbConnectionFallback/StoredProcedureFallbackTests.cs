using System.Data.SQLite;

using Jaunty.StoredProcedure;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.IDbConnectionFallback;

/// <summary>
/// Tests that the three async stored-procedure core methods (ExecuteWithOutputParametersAsync,
/// ExecuteScalarWithOutputParametersAsync, ExecuteNonQueryWithOutputParametersAsync) correctly
/// reject non-DbConnection wrappers instead of silently falling back to blocking, synchronous
/// ADO.NET calls inside an async API. The async public API requires DbConnection and throws
/// InvalidOperationException for IDbConnection-only implementations.
/// </summary>
public class StoredProcedureFallbackTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;
    private readonly IDbConnectionWrapper _wrapper;

    public StoredProcedureFallbackTests()
    {
        _realConnection = new SQLiteConnection("Data Source=:memory:");
        _realConnection.Open();
        _wrapper = new IDbConnectionWrapper(_realConnection);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _realConnection.Dispose();
    }

    [Fact]
    public async Task ExecuteStoredProcedureAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        var parameters = new SpParameters().AddInput("CategoryId", 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _wrapper.ExecuteStoredProcedureAsync<Product>("GetProductsByCategory", parameters).AsTask());
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        var parameters = new SpParameters().AddInput("CategoryId", 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _wrapper.ExecuteStoredProcedureScalarAsync<long>("GetProductCount", parameters).AsTask());
    }

    [Fact]
    public async Task ExecuteStoredProcedureNonQueryAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        var parameters = new SpParameters().AddInput("CategoryId", 1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _wrapper.ExecuteStoredProcedureNonQueryAsync("UpdatePricesByCategory", parameters).AsTask());
    }
}
