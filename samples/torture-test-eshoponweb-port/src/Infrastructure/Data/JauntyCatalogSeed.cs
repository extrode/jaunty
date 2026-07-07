using System;
using System.Data;
using System.Threading.Tasks;
using Jaunty;
using Microsoft.eShopWeb.Infrastructure.Data.Persistence;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Data;

/// <summary>
/// Jaunty-based replacement for the former EF Core <c>CatalogContextSeed</c>. Ensures the schema
/// exists (for SQLite-backed runs, since there is no EF migration path anymore) and seeds the
/// preconfigured catalog brands, types and items. Idempotent: seeding is skipped for any table
/// that already contains rows.
/// </summary>
public static class JauntyCatalogSeed
{
    public static async Task SeedAsync(IDbConnection connection, ILogger logger, int retry = 0)
    {
        var retryForAvailability = retry;
        try
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }

            // No EF migrations any more; create the schema for whichever dialect this connection
            // targets (idempotent create-if-not-exists across all supported providers).
            CatalogSchema.EnsureCreated(connection);

            if (Count(connection, "CatalogBrands") == 0)
            {
                SeedBrands(connection);
            }

            if (Count(connection, "CatalogTypes") == 0)
            {
                SeedTypes(connection);
            }

            if (Count(connection, "Catalog") == 0)
            {
                SeedItems(connection);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            if (retryForAvailability >= 10) throw;

            retryForAvailability++;
            logger.LogError(ex.Message);
            await SeedAsync(connection, logger, retryForAvailability);
            throw;
        }
    }

    private static int Count(IDbConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        var result = command.ExecuteScalar();
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private static void SeedBrands(IDbConnection connection)
    {
        foreach (var brand in new[] { "Azure", ".NET", "Visual Studio", "SQL Server", "Other" })
        {
            connection.Insert(new CatalogBrandRow { Brand = brand });
        }
    }

    private static void SeedTypes(IDbConnection connection)
    {
        foreach (var type in new[] { "Mug", "T-Shirt", "Sheet", "USB Memory Stick" })
        {
            connection.Insert(new CatalogTypeRow { Type = type });
        }
    }

    private static void SeedItems(IDbConnection connection)
    {
        // (catalogTypeId, catalogBrandId, name, description, price, pictureUri) mirroring the
        // original CatalogContextSeed.GetPreconfiguredItems().
        var items = new (int TypeId, int BrandId, string Name, decimal Price, string Uri)[]
        {
            (2, 2, ".NET Bot Black Sweatshirt", 19.5M, "http://catalogbaseurltobereplaced/images/products/1.png"),
            (1, 2, ".NET Black & White Mug", 8.50M, "http://catalogbaseurltobereplaced/images/products/2.png"),
            (2, 5, "Prism White T-Shirt", 12M, "http://catalogbaseurltobereplaced/images/products/3.png"),
            (2, 2, ".NET Foundation Sweatshirt", 12M, "http://catalogbaseurltobereplaced/images/products/4.png"),
            (3, 5, "Roslyn Red Sheet", 8.5M, "http://catalogbaseurltobereplaced/images/products/5.png"),
            (2, 2, ".NET Blue Sweatshirt", 12M, "http://catalogbaseurltobereplaced/images/products/6.png"),
            (2, 5, "Roslyn Red T-Shirt", 12M, "http://catalogbaseurltobereplaced/images/products/7.png"),
            (2, 5, "Kudu Purple Sweatshirt", 8.5M, "http://catalogbaseurltobereplaced/images/products/8.png"),
            (1, 5, "Cup<T> White Mug", 12M, "http://catalogbaseurltobereplaced/images/products/9.png"),
            (3, 2, ".NET Foundation Sheet", 12M, "http://catalogbaseurltobereplaced/images/products/10.png"),
            (3, 2, "Cup<T> Sheet", 8.5M, "http://catalogbaseurltobereplaced/images/products/11.png"),
            (2, 5, "Prism White TShirt", 12M, "http://catalogbaseurltobereplaced/images/products/12.png"),
        };

        foreach (var i in items)
        {
            connection.Insert(new CatalogItemRow
            {
                CatalogTypeId = i.TypeId,
                CatalogBrandId = i.BrandId,
                Name = i.Name,
                Description = i.Name,
                Price = i.Price,
                PictureUri = i.Uri,
            });
        }
    }
}
