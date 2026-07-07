using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Data.Persistence;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.eShopWeb.FunctionalTests.Web;

public class TestApplication : WebApplicationFactory<IBasketViewModelService>
{
    private readonly string _environment = "Development";

    // Each fixture instance gets its own uniquely-named shared-cache in-memory SQLite database so
    // functional test runs are isolated from one another. The connection is held open for the
    // lifetime of the fixture so the schema and seeded data persist across DI scopes / requests.
    private readonly string _sqliteDataSource = $"WebTests_{System.Guid.NewGuid():N}";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

        builder.ConfigureServices(services =>
        {
            // Identity continues to use EF Core InMemory.
            var identityDescriptors = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppIdentityDbContext>))
                .ToList();
            foreach (var descriptor in identityDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddScoped(sp =>
                new DbContextOptionsBuilder<AppIdentityDbContext>()
                    .UseInMemoryDatabase("Identity")
                    .UseApplicationServiceProvider(sp)
                    .Options);

            // Replace the SQL Server catalog connection registered by Infrastructure.Dependencies
            // with a Jaunty-backed SQLite connection.
            var connectionDescriptors = services.Where(d => d.ServiceType == typeof(IDbConnection)).ToList();
            foreach (var descriptor in connectionDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IDbConnection>(_ =>
            {
                var connection = new SqliteConnection(
                    $"DataSource={_sqliteDataSource};Mode=Memory;Cache=Shared");
                connection.Open();
                SqliteSchema.EnsureCreated(connection);
                return connection;
            });
        });

        return base.CreateHost(builder);
    }
}
