using System.Data;

using System.Data.Common;
using Jaunty.Core;
using Jaunty.Interfaces;

namespace Jaunty;

public static partial class Jaunty
{
    public static T? Get<T>(this IDbConnection connection, object id) where T : new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        return GetByIdSimpleCore<T>(connection, id, default);
    }

    public static T? Get<T>(this IDbConnection connection, object id, CommandOptions<T> options) where T : new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        return GetByIdSimpleCore<T>(connection, id, options);
    }

    public static T? Get<T, TId>(this IDbConnection connection, TId id) where T : IEntity<TId>, new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        return GetByIdTypedCore<T, TId>(connection, id, default);
    }

    public static T? Get<T, TId>(this IDbConnection connection, TId id, CommandOptions<T> options) where T : IEntity<TId>, new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        return GetByIdTypedCore<T, TId>(connection, id, options);
    }

    public static T GetRequired<T>(this IDbConnection connection, object id) where T : new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        return GetByIdSimpleCore<T>(connection, id, default) ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
    }

    public static T GetRequired<T>(this IDbConnection connection, object id, CommandOptions<T> options) where T : new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        return GetByIdSimpleCore<T>(connection, id, options) ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
    }

    public static T GetRequired<T, TId>(this IDbConnection connection, TId id) where T : IEntity<TId>, new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        return GetByIdTypedCore<T, TId>(connection, id, default) ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
    }

    public static T GetRequired<T, TId>(this IDbConnection connection, TId id, CommandOptions<T> options) where T : IEntity<TId>, new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        return GetByIdTypedCore<T, TId>(connection, id, options) ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
    }

    public static ValueTask<T?> GetAsync<T>(this IDbConnection connection, object id, CancellationToken cancellationToken = default) where T : new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        if (connection is not DbConnection dbConnection)
            throw new ArgumentException("DbConnection required for async", nameof(connection));
        return GetByIdSimpleCoreAsync<T>(dbConnection, id, default, cancellationToken);
    }

    public static ValueTask<T?> GetAsync<T>(this IDbConnection connection, object id, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        if (connection is not DbConnection dbConnection)
            throw new ArgumentException("DbConnection required for async", nameof(connection));
        return GetByIdSimpleCoreAsync<T>(dbConnection, id, options, cancellationToken);
    }

    public static ValueTask<T?> GetAsync<T, TId>(this IDbConnection connection, TId id, CancellationToken cancellationToken = default) where T : IEntity<TId>, new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        if (connection is not DbConnection dbConnection)
            throw new ArgumentException("DbConnection required for async", nameof(connection));
        return GetByIdTypedCoreAsync<T, TId>(dbConnection, id, default, cancellationToken);
    }

    public static ValueTask<T?> GetAsync<T, TId>(this IDbConnection connection, TId id, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : IEntity<TId>, new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        if (connection is not DbConnection dbConnection)
            throw new ArgumentException("DbConnection required for async", nameof(connection));
        return GetByIdTypedCoreAsync<T, TId>(dbConnection, id, options, cancellationToken);
    }

    public static async ValueTask<T> GetRequiredAsync<T>(this IDbConnection connection, object id, CancellationToken cancellationToken = default) where T : new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        if (connection is not DbConnection dbConnection)
            throw new ArgumentException("DbConnection required for async", nameof(connection));
        var result = await GetByIdSimpleCoreAsync<T>(dbConnection, id, default, cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
    }

    public static async ValueTask<T> GetRequiredAsync<T>(this IDbConnection connection, object id, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        if (connection is not DbConnection dbConnection)
            throw new ArgumentException("DbConnection required for async", nameof(connection));
        var result = await GetByIdSimpleCoreAsync<T>(dbConnection, id, options, cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
    }

    public static async ValueTask<T> GetRequiredAsync<T, TId>(this IDbConnection connection, TId id, CancellationToken cancellationToken = default) where T : IEntity<TId>, new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        if (connection is not DbConnection dbConnection)
            throw new ArgumentException("DbConnection required for async", nameof(connection));
        var result = await GetByIdTypedCoreAsync<T, TId>(dbConnection, id, default, cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
    }

    public static async ValueTask<T> GetRequiredAsync<T, TId>(this IDbConnection connection, TId id, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : IEntity<TId>, new()
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
        if (connection is not DbConnection dbConnection)
            throw new ArgumentException("DbConnection required for async", nameof(connection));
        var result = await GetByIdTypedCoreAsync<T, TId>(dbConnection, id, options, cancellationToken).ConfigureAwait(false);
        return result ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
    }
}
