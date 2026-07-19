using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Retrieves all rows from the table mapped to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <returns>A list of all entities in the table.</returns>
    public static List<T> GetAll<T>(this IDbConnection connection) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        return GetAllCore<T>(connection, default);
    }

    /// <summary>
    /// Retrieves all rows from the table mapped to <typeparamref name="T"/> with command options.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="options">Command options for transaction, timeout, or custom mapper.</param>
    /// <returns>A list of all entities in the table.</returns>
    public static List<T> GetAll<T>(this IDbConnection connection, CommandOptions<T> options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        return GetAllCore<T>(connection, options);
    }

    /// <summary>
    /// Asynchronously retrieves all rows from the table mapped to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing a list of all entities in the table.</returns>
    public static ValueTask<List<T>> GetAllAsync<T>(this IDbConnection connection, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");
        return GetAllCoreAsync<T>(dbConnection, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously retrieves all rows from the table mapped to <typeparamref name="T"/> with command options.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="options">Command options for transaction, timeout, or custom mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing a list of all entities in the table.</returns>
    public static ValueTask<List<T>> GetAllAsync<T>(this IDbConnection connection, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");
        return GetAllCoreAsync<T>(dbConnection, options, cancellationToken);
    }

    /// <summary>
    /// Streams all rows from the table mapped to <typeparamref name="T"/>, yielding entities lazily.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> that streams results row by row.</returns>
    public static IEnumerable<T> GetAllStream<T>(this IDbConnection connection) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        return GetAllStreamCore<T>(connection, default);
    }

    /// <summary>
    /// Streams all rows from the table mapped to <typeparamref name="T"/> with command options, yielding entities lazily.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="options">Command options for transaction or timeout.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> that streams results row by row.</returns>
    public static IEnumerable<T> GetAllStream<T>(this IDbConnection connection, CommandOptions<T> options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        return GetAllStreamCore<T>(connection, options);
    }

#if ASYNC_ENUMERABLE_SUPPORT
    /// <summary>
    /// Asynchronously streams all rows from the table mapped to <typeparamref name="T"/>, yielding entities lazily.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that streams results row by row.</returns>
    public static IAsyncEnumerable<T> GetAllStreamAsync<T>(this IDbConnection connection, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");
        return GetAllStreamCoreAsync<T>(dbConnection, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously streams all rows from the table mapped to <typeparamref name="T"/> with command options, yielding entities lazily.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="options">Command options for transaction or timeout.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that streams results row by row.</returns>
    public static IAsyncEnumerable<T> GetAllStreamAsync<T>(this IDbConnection connection, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");
        return GetAllStreamCoreAsync<T>(dbConnection, options, cancellationToken);
    }
#else
    /// <summary>
    /// Asynchronously retrieves all rows from the table mapped to <typeparamref name="T"/>.
    /// On .NET Framework (netstandard2.0), returns a completed task with the buffered results.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing an enumerable of all entities.</returns>
    public static ValueTask<IEnumerable<T>> GetAllStreamAsync<T>(this IDbConnection connection, CancellationToken cancellationToken = default) where T : new()
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");
        return GetAllStreamCoreAsync<T>(dbConnection, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously retrieves all rows from the table mapped to <typeparamref name="T"/> with command options.
    /// On .NET Framework (netstandard2.0), returns a completed task with the buffered results.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="options">Command options for transaction or timeout.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task containing an enumerable of all entities.</returns>
    public static ValueTask<IEnumerable<T>> GetAllStreamAsync<T>(this IDbConnection connection, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");
        return GetAllStreamCoreAsync<T>(dbConnection, options, cancellationToken);
    }
#endif
}
