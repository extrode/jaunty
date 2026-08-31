using System.Data;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Data.Queries;

public class BasketQueryService : IBasketQueryService
{
    private readonly IDbConnection _connection;

    public BasketQueryService(IDbConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// This method performs the sum on the database rather than in memory.
    /// </summary>
    public Task<int> CountTotalBasketItems(string username)
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        using var command = _connection.CreateCommand();
        command.CommandText =
            @"SELECT COALESCE(SUM(bi.Quantity), 0)
              FROM Baskets b
              INNER JOIN BasketItems bi ON bi.BasketId = b.Id
              WHERE b.BuyerId = @buyerId";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@buyerId";
        parameter.Value = username;
        command.Parameters.Add(parameter);

        var result = command.ExecuteScalar();
        var total = result is null or System.DBNull ? 0 : System.Convert.ToInt32(result);

        return Task.FromResult(total);
    }
}
