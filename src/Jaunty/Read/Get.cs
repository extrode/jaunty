using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Interfaces;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Retrieves an entity by its primary key value.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor and a single primary key property.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="id">The primary key value.</param>
    /// <returns>The entity if found; otherwise <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <typeparamref name="T"/> has no single primary key.</exception>
    public static T? Get<T>(this IDbConnection connection, object id) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        return GetByIdSimpleCore<T>(connection, id, default);
    }

    /// <summary>
    /// Retrieves an entity by its primary key value with command options.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor and a single primary key property.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="options">Command options for transaction, timeout, or custom mapper.</param>
    /// <returns>The entity if found; otherwise <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <typeparamref name="T"/> has no single primary key.</exception>
    public static T? Get<T>(this IDbConnection connection, object id, CommandOptions<T> options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        return GetByIdSimpleCore<T>(connection, id, options);
    }

    /// <summary>
    /// Retrieves a typed-key entity by its primary key value.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/> and have a parameterless constructor.</typeparam>
    /// <typeparam name="TId">The primary key type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="id">The primary key value.</param>
    /// <returns>The entity if found; otherwise <see langword="null"/>.</returns>
    public static T? Get<T, TId>(this IDbConnection connection, TId id) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        KeyGuard.ThrowIfNull(id, nameof(id));
        return GetByIdTypedCore<T, TId>(connection, id, default);
    }

    /// <summary>
    /// Retrieves a typed-key entity by its primary key value with command options.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/> and have a parameterless constructor.</typeparam>
    /// <typeparam name="TId">The primary key type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="options">Command options for transaction, timeout, or custom mapper.</param>
    /// <returns>The entity if found; otherwise <see langword="null"/>.</returns>
    public static T? Get<T, TId>(this IDbConnection connection, TId id, CommandOptions<T> options) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        KeyGuard.ThrowIfNull(id, nameof(id));
        return GetByIdTypedCore<T, TId>(connection, id, options);
    }

    /// <summary>
    /// Retrieves an entity by its primary key value, throwing if not found.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor and a single primary key property.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="id">The primary key value.</param>
    /// <returns>The entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity with the specified key exists.</exception>
    public static T GetRequired<T>(this IDbConnection connection, object id) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        return GetByIdSimpleCore<T>(connection, id, default) ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
    }

    /// <summary>
    /// Retrieves an entity by its primary key value with command options, throwing if not found.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor and a single primary key property.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="options">Command options for transaction, timeout, or custom mapper.</param>
    /// <returns>The entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity with the specified key exists.</exception>
    public static T GetRequired<T>(this IDbConnection connection, object id, CommandOptions<T> options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        return GetByIdSimpleCore<T>(connection, id, options) ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
    }

    /// <summary>
    /// Retrieves a typed-key entity by its primary key value, throwing if not found.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/> and have a parameterless constructor.</typeparam>
    /// <typeparam name="TId">The primary key type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="id">The primary key value.</param>
    /// <returns>The entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity with the specified key exists.</exception>
    public static T GetRequired<T, TId>(this IDbConnection connection, TId id) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        KeyGuard.ThrowIfNull(id, nameof(id));
        return GetByIdTypedCore<T, TId>(connection, id, default) ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
    }

    /// <summary>
    /// Retrieves a typed-key entity by its primary key value with command options, throwing if not found.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/> and have a parameterless constructor.</typeparam>
    /// <typeparam name="TId">The primary key type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="options">Command options for transaction, timeout, or custom mapper.</param>
    /// <returns>The entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity with the specified key exists.</exception>
    public static T GetRequired<T, TId>(this IDbConnection connection, TId id, CommandOptions<T> options) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        KeyGuard.ThrowIfNull(id, nameof(id));
        return GetByIdTypedCore<T, TId>(connection, id, options) ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
    }

    /// <summary>
    /// Asynchronously retrieves an entity by its primary key value.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor and a single primary key property.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing the entity if found; otherwise <see langword="null"/>.</returns>
    public static ValueTask<T?> GetAsync<T>(this IDbConnection connection, object id, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");
        return GetByIdSimpleCoreAsync<T>(dbConnection, id, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously retrieves an entity by its primary key value with command options.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor and a single primary key property.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="options">Command options for transaction, timeout, or custom mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing the entity if found; otherwise <see langword="null"/>.</returns>
    public static ValueTask<T?> GetAsync<T>(this IDbConnection connection, object id, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");
        return GetByIdSimpleCoreAsync<T>(dbConnection, id, options, cancellationToken);
    }

    /// <summary>
    /// Asynchronously retrieves a typed-key entity by its primary key value.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/> and have a parameterless constructor.</typeparam>
    /// <typeparam name="TId">The primary key type.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing the entity if found; otherwise <see langword="null"/>.</returns>
    public static ValueTask<T?> GetAsync<T, TId>(this IDbConnection connection, TId id, CancellationToken cancellationToken = default) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        KeyGuard.ThrowIfNull(id, nameof(id));
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");
        return GetByIdTypedCoreAsync<T, TId>(dbConnection, id, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously retrieves a typed-key entity by its primary key value with command options.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/> and have a parameterless constructor.</typeparam>
    /// <typeparam name="TId">The primary key type.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="options">Command options for transaction, timeout, or custom mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing the entity if found; otherwise <see langword="null"/>.</returns>
    public static ValueTask<T?> GetAsync<T, TId>(this IDbConnection connection, TId id, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        KeyGuard.ThrowIfNull(id, nameof(id));
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");
        return GetByIdTypedCoreAsync<T, TId>(dbConnection, id, options, cancellationToken);
    }

    /// <summary>
    /// Asynchronously retrieves an entity by its primary key value, throwing if not found.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor and a single primary key property.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing the entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity with the specified key exists.</exception>
    /// <remarks>
    /// AUD-R35-047: split into a non-<c>async</c> validating wrapper over an <c>async</c>
    /// local, so the argument checks throw at the call site rather than surfacing as a faulted
    /// <see cref="ValueTask"/> on await. Every other async entry point in this file already
    /// does this, the <c>*EagerValidationTests</c> family pins it as a contract, and
    /// AUD-R25 B2-1 fixed the same shape on <c>ExecuteAsync</c>/<c>ExecuteBatchAsync</c>.
    /// </remarks>
    public static ValueTask<T> GetRequiredAsync<T>(this IDbConnection connection, object id, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        return AwaitAsync();

        async ValueTask<T> AwaitAsync()
        {
            var result = await GetByIdSimpleCoreAsync<T>(dbConnection, id, default, cancellationToken).ConfigureAwait(false);
            return result ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
        }
    }

    /// <summary>
    /// Asynchronously retrieves an entity by its primary key value with command options, throwing if not found.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor and a single primary key property.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="options">Command options for transaction, timeout, or custom mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing the entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity with the specified key exists.</exception>
    /// <remarks>
    /// AUD-R35-047: split into a non-<c>async</c> validating wrapper over an <c>async</c>
    /// local, so the argument checks throw at the call site rather than surfacing as a faulted
    /// <see cref="ValueTask"/> on await. Every other async entry point in this file already
    /// does this, the <c>*EagerValidationTests</c> family pins it as a contract, and
    /// AUD-R25 B2-1 fixed the same shape on <c>ExecuteAsync</c>/<c>ExecuteBatchAsync</c>.
    /// </remarks>
    public static ValueTask<T> GetRequiredAsync<T>(this IDbConnection connection, object id, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        return AwaitAsync();

        async ValueTask<T> AwaitAsync()
        {
            var result = await GetByIdSimpleCoreAsync<T>(dbConnection, id, options, cancellationToken).ConfigureAwait(false);
            return result ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
        }
    }

    /// <summary>
    /// Asynchronously retrieves a typed-key entity by its primary key value, throwing if not found.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/> and have a parameterless constructor.</typeparam>
    /// <typeparam name="TId">The primary key type.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing the entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity with the specified key exists.</exception>
    /// <remarks>
    /// AUD-R35-047: split into a non-<c>async</c> validating wrapper over an <c>async</c>
    /// local, so the argument checks throw at the call site rather than surfacing as a faulted
    /// <see cref="ValueTask"/> on await. Every other async entry point in this file already
    /// does this, the <c>*EagerValidationTests</c> family pins it as a contract, and
    /// AUD-R25 B2-1 fixed the same shape on <c>ExecuteAsync</c>/<c>ExecuteBatchAsync</c>.
    /// </remarks>
    public static ValueTask<T> GetRequiredAsync<T, TId>(this IDbConnection connection, TId id, CancellationToken cancellationToken = default) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        KeyGuard.ThrowIfNull(id, nameof(id));
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        return AwaitAsync();

        async ValueTask<T> AwaitAsync()
        {
            var result = await GetByIdTypedCoreAsync<T, TId>(dbConnection, id, default, cancellationToken).ConfigureAwait(false);
            return result ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
        }
    }

    /// <summary>
    /// Asynchronously retrieves a typed-key entity by its primary key value with command options, throwing if not found.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IEntity{TId}"/> and have a parameterless constructor.</typeparam>
    /// <typeparam name="TId">The primary key type.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="id">The primary key value.</param>
    /// <param name="options">Command options for transaction, timeout, or custom mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing the entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity with the specified key exists.</exception>
    /// <remarks>
    /// AUD-R35-047: split into a non-<c>async</c> validating wrapper over an <c>async</c>
    /// local, so the argument checks throw at the call site rather than surfacing as a faulted
    /// <see cref="ValueTask"/> on await. Every other async entry point in this file already
    /// does this, the <c>*EagerValidationTests</c> family pins it as a contract, and
    /// AUD-R25 B2-1 fixed the same shape on <c>ExecuteAsync</c>/<c>ExecuteBatchAsync</c>.
    /// </remarks>
    public static ValueTask<T> GetRequiredAsync<T, TId>(this IDbConnection connection, TId id, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        KeyGuard.ThrowIfNull(id, nameof(id));
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        return AwaitAsync();

        async ValueTask<T> AwaitAsync()
        {
            var result = await GetByIdTypedCoreAsync<T, TId>(dbConnection, id, options, cancellationToken).ConfigureAwait(false);
            return result ?? throw new InvalidOperationException($"Entity of type '{typeof(T).Name}' with ID '{id}' not found.");
        }
    }
}
