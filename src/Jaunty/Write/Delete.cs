using System.Data;

using Jaunty.Core;
using Jaunty.Internals;
using Jaunty.Interfaces;

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
        return DeleteByIdSimpleCore<T>(connection, id, default);
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
        return DeleteByIdSimpleCore<T>(connection, id, options);
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
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        KeyGuard.ThrowIfNull(id, nameof(id));
        return DeleteByIdCore<T, TId>(connection, id, default);
    }

    /// <summary>
    /// Deletes an entity of type <typeparamref name="T"/> by its primary key value with command options.
    /// </summary>
    public static int Delete<T, TId>(this IDbConnection connection, TId id, CommandOptions options) where T : IEntity<TId>, new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif
        KeyGuard.ThrowIfNull(id, nameof(id));
        return DeleteByIdCore<T, TId>(connection, id, options);
    }

    #endregion
}