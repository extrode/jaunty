using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Data.Persistence;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.PublicApi.AuthEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.eShopWeb.FunctionalTests.PublicApi;

public class TestApiApplication : WebApplicationFactory<AuthenticateEndpoint>
{
    private readonly string _environment = "Testing";

    // Isolated per-fixture shared-cache in-memory SQLite database, kept open for the fixture
    // lifetime so schema + seed data persist across DI scopes / requests.
    private readonly string _sqliteDataSource = $"ApiTests_{System.Guid.NewGuid():N}";

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
                    .UseInMemoryDatabase("IdentityDbForPublicApi")
                    .UseApplicationServiceProvider(sp)
                    .Options);

            // Replace the catalog connection registered by Infrastructure.Dependencies with a
            // Jaunty-backed SQLite connection.
            var connectionDescriptors = services.Where(d => d.ServiceType == typeof(IDbConnection)).ToList();
            foreach (var descriptor in connectionDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IDbConnection>(_ =>
                TortureDatabase.CreateOpenConnection(_sqliteDataSource));
        });

        return base.CreateHost(builder);
    }
}
