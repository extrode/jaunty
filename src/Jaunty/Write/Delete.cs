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
    #region Delete by Entity

    /// <summary>
    /// Deletes an entity from the database using its primary key.
    /// </summary>
    public static int Delete<T>(this IDbConnection connection, T entity) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
        return DeleteByEntityCore(connection, entity, default);
    }

    /// <summary>
    /// Deletes an entity from the database using its primary key with command options.
    /// </summary>
    public static int Delete<T>(this IDbConnection connection, T entity, CommandOptions options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entity);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (entity is null) throw new ArgumentNullException(nameof(entity));
#endif
        return DeleteByEntityCore(connection, entity, options);
    }

    #endregion

    #region Delete by ID

    /// <summary>
    /// Deletes an entity from the database by its primary key value.
    /// </summary>
    public static int Delete<T>(this IDbConnection connection, object id) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        // Internal decision: DeleteByIdCore requires T : IEntity<TId>.
        // For simple Delete<T>(object id), we use reflection to invoke the generic method.
        var iEntityInterface = typeof(T).GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntity<>));
        if (iEntityInterface is null)
            throw new InvalidOperationException($"Type '{typeof(T).Name}' must implement IEntity to use Delete by ID.");
        
        var idType = iEntityInterface.GetGenericArguments()[0];
        var method = typeof(Jaunty).GetMethod(nameof(DeleteByIdCore), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DeleteByIdCore method not found.");
        var genericMethod = method.MakeGenericMethod(typeof(T), idType);
        return (int)genericMethod.Invoke(null, new object[] { connection, id, default(CommandOptions) })!;
    }

    /// <summary>
    /// Deletes an entity from the database by its primary key value with command options.
    /// </summary>
    public static int Delete<T>(this IDbConnection connection, object id, CommandOptions options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        var iEntityInterface = typeof(T).GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntity<>));
        if (iEntityInterface is null)
            throw new InvalidOperationException($"Type '{typeof(T).Name}' must implement IEntity to use Delete by ID.");
        
        var idType = iEntityInterface.GetGenericArguments()[0];
        var method = typeof(Jaunty).GetMethod(nameof(DeleteByIdCore), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DeleteByIdCore method not found.");
        var genericMethod = method.MakeGenericMethod(typeof(T), idType);
        return (int)genericMethod.Invoke(null, new object[] { connection, id, options })!;
    }

    #endregion

    #region Delete by IEntity<T>

    /// <summary>
    /// Deletes an entity of type <typeparamref name="T"/> by its primary key value.
    /// </summary>
    public static int Delete<T, TId>(this IDbConnection connection, TId id) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        return DeleteByIdCore<T, TId>(connection, id, default);
    }

    /// <summary>
    /// Deletes an entity of type <typeparamref name="T"/> by its primary key value with command options.
    /// </summary>
    public static int Delete<T, TId>(this IDbConnection connection, TId id, CommandOptions options) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(id);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (id is null) throw new ArgumentNullException(nameof(id));
#endif
        return DeleteByIdCore<T, TId>(connection, id, options);
    }

    #endregion
}
