using System.Data;
using Jaunty;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Data.Persistence;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.Infrastructure;

public static class Dependencies
{
    public static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        // Ensure Jaunty's reflection-based mapping is active for the flat Row POCOs (they carry no
        // source-generated mappers in this project). Jaunty auto-discovers the extension assembly,
        // but calling it explicitly is safe and idempotent.
        Jaunty.Extensions.Reflection.JauntyReflectionExtensions.UseReflectionMapping();

        // Historically named "UseOnlyInMemoryDatabase"; now selects a SQLite catalog store
        // (used by the functional test fixtures). Identity continues to use EF Core.
        bool useSqliteCatalog = false;
        if (configuration["UseOnlyInMemoryDatabase"] != null)
        {
            useSqliteCatalog = bool.Parse(configuration["UseOnlyInMemoryDatabase"]!);
        }

        if (useSqliteCatalog)
        {
            // A single shared in-memory SQLite database kept open for the app lifetime so the
            // schema and seed data survive across DI scopes / HTTP requests.
            services.AddSingleton<IDbConnection>(_ =>
            {
                var connection = new SqliteConnection("DataSource=CatalogSqlite;Mode=Memory;Cache=Shared");
                connection.Open();
                SqliteSchema.EnsureCreated(connection);
                return connection;
            });

            services.AddDbContext<AppIdentityDbContext>(options =>
                options.UseInMemoryDatabase("Identity"));
        }
        else
        {
            // Real SQL Server catalog store via Jaunty. The connection string is the same
            // "CatalogConnection" previously consumed by EF Core.
            services.AddScoped<IDbConnection>(_ =>
                new SqlConnection(configuration.GetConnectionString("CatalogConnection")));

            // Add Identity DbContext (still EF Core).
            services.AddDbContext<AppIdentityDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("IdentityConnection")));
        }
    }
}
