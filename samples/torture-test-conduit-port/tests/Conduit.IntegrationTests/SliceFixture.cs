using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;

using Conduit.Infrastructure;

using MediatR;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Conduit.IntegrationTests;

/// <summary>
/// Jaunty-based replacement for the former EF Core InMemory-backed fixture. Jaunty has no
/// in-memory provider, so this points at a real, disposable per-test SQLite file - the same
/// substitution the eShopOnWeb torture-test port used for its own EF-InMemory test fixture.
/// </summary>
public class SliceFixture : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ServiceProvider _provider;
    private readonly string _dbName = Guid.NewGuid() + ".db";

    public SliceFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConduit();

        var connectionString = $"Filename={_dbName};Pooling=False";
        services.AddSingleton<IDbConnection>(new SqliteConnection(connectionString));
        services.AddSingleton<ConduitDb>();

        _provider = services.BuildServiceProvider();

        GetConduitDb().EnsureCreated();
        _scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
    }

    public ConduitDb GetConduitDb() => _provider.GetRequiredService<ConduitDb>();

    public void Dispose()
    {
        GetConduitDb().Connection.Close();
        _provider.Dispose();
        File.Delete(_dbName);
    }

    public async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        await action(scope.ServiceProvider);
    }

    public async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request) =>
        ExecuteScopeAsync(sp =>
        {
            var mediator = sp.GetRequiredService<IMediator>();

            return mediator.Send(request);
        });

    public Task SendAsync(IRequest request) =>
        ExecuteScopeAsync(sp =>
        {
            var mediator = sp.GetRequiredService<IMediator>();

            return mediator.Send(request);
        });

    public Task ExecuteConduitDbAsync(Func<ConduitDb, Task> action) =>
        ExecuteScopeAsync(sp => action(sp.GetRequiredService<ConduitDb>()));

    public Task<T> ExecuteConduitDbAsync<T>(Func<ConduitDb, Task<T>> action) =>
        ExecuteScopeAsync(sp => action(sp.GetRequiredService<ConduitDb>()));
}
