using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using Jaunty.Interfaces;
using Jaunty.Internals;
using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    #region Async Delete by Entity

    /// <summary>
    /// Asynchronously deletes an entity from the database using its primary key.
    /// </summary>
    public static ValueTask<int> DeleteAsync<T>(this IDbConnection connection, T entity, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : DeleteByEntityCoreAsync(dbConnection, entity, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes an entity from the database using its primary key with command options.
    /// </summary>
    public static ValueTask<int> DeleteAsync<T>(this IDbConnection connection, T entity, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : DeleteByEntityCoreAsync(dbConnection, entity, options, cancellationToken);
    }

    #endregion

    #region Async Delete by ID

    /// <summary>
    /// Asynchronously deletes an entity by its primary key value.
    /// </summary>
    public static ValueTask<int> DeleteAsync<T>(this IDbConnection connection, object id, CancellationToken cancellationToken = default) where T : new()
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
        
        // Use reflection to invoke DeleteByIdCoreAsync since T doesn't satisfy IEntity constraint at compile time
        var iEntityInterface = typeof(T).GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntity<>));
        if (iEntityInterface is null)
            throw new InvalidOperationException($"Type '{typeof(T).Name}' must implement IEntity to use Delete by ID.");
        
        var idType = iEntityInterface.GetGenericArguments()[0];
        var method = typeof(Jaunty).GetMethod(nameof(DeleteByIdCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DeleteByIdCoreAsync method not found.");
        var genericMethod = method.MakeGenericMethod(typeof(T), idType);
        var result = genericMethod.Invoke(null, new object[] { dbConnection, id, default(CommandOptions), cancellationToken });
        return (ValueTask<int>)result!;
    }

    /// <summary>
    /// Asynchronously deletes an entity by its primary key value with command options.
    /// </summary>
    public static ValueTask<int> DeleteAsync<T>(this IDbConnection connection, object id, CommandOptions options, CancellationToken cancellationToken = default) where T : new()
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
        
        var iEntityInterface = typeof(T).GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntity<>));
        if (iEntityInterface is null)
            throw new InvalidOperationException($"Type '{typeof(T).Name}' must implement IEntity to use Delete by ID.");
        
        var idType = iEntityInterface.GetGenericArguments()[0];
        var method = typeof(Jaunty).GetMethod(nameof(DeleteByIdCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DeleteByIdCoreAsync method not found.");
        var genericMethod = method.MakeGenericMethod(typeof(T), idType);
        var result = genericMethod.Invoke(null, new object[] { dbConnection, id, options, cancellationToken });
        return (ValueTask<int>)result!;
    }

    #endregion

    #region Async Delete by IEntity<T>

    /// <summary>
    /// Asynchronously deletes an entity of type <typeparamref name="T"/> by its primary key value.
    /// </summary>
    public static ValueTask<int> DeleteAsync<T, TId>(this IDbConnection connection, TId id, CancellationToken cancellationToken = default) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : DeleteByIdCoreAsync<T, TId>(dbConnection, id, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously deletes an entity of type <typeparamref name="T"/> by its primary key value with command options.
    /// </summary>
    public static ValueTask<int> DeleteAsync<T, TId>(this IDbConnection connection, TId id, CommandOptions options, CancellationToken cancellationToken = default) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : DeleteByIdCoreAsync<T, TId>(dbConnection, id, options, cancellationToken);
    }

    #endregion
}
