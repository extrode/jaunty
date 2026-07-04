using System.Collections.Concurrent;

namespace Jaunty.TypeHandlers;

/// <summary>
/// Internal registry for type handlers. Provides fast, NativeAOT-compatible lookups
/// and thread-safe registration and removal of type handlers.
/// </summary>
internal static class TypeHandlerRegistry
{
    private static readonly ConcurrentDictionary<Type, ITypeHandler> Handlers = new();
    private static readonly object MutationSync = new();
    private static volatile int _handlerCount;

    /// <summary>
    /// Gets a value indicating whether any type handlers have been registered.
    /// Used to fast-path queries and parameter binding when no custom handlers are needed.
    /// </summary>
    internal static bool HasHandlers => _handlerCount > 0;

    /// <summary>
    /// Registers a type handler for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to register a handler for.</typeparam>
    /// <param name="handler">The handler instance.</param>
    internal static void Register<T>(ITypeHandler handler)
    {
        Type key = typeof(T);
        lock (MutationSync)
        {
            Handlers.AddOrUpdate(key, handler, (_, __) => handler);
            _handlerCount = Handlers.Count;
        }
    }

    /// <summary>
    /// Attempts to retrieve a registered type handler for the specified type.
    /// </summary>
    /// <param name="type">The type to look up.</param>
    /// <param name="handler">The handler, if found; otherwise null.</param>
    /// <returns>True if a handler was found; otherwise false.</returns>
    internal static bool TryGetHandler(Type type, out ITypeHandler? handler)
    {
        return Handlers.TryGetValue(type, out handler);
    }

    /// <summary>
    /// Removes a registered type handler for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to remove the handler for.</typeparam>
    /// <returns>True if a handler was removed; otherwise false.</returns>
    internal static bool Remove<T>()
    {
        Type key = typeof(T);
        lock (MutationSync)
        {
            bool removed = Handlers.TryRemove(key, out _);
            if (removed)
            {
                _handlerCount = Handlers.Count;
            }
            return removed;
        }
    }

    /// <summary>
    /// Clears all registered handlers. Intended for testing cleanup.
    /// </summary>
    internal static void Clear()
    {
        lock (MutationSync)
        {
            Handlers.Clear();
            _handlerCount = 0;
        }
    }

    /// <summary>
    /// Attempts to convert a database value to a CLR value using a registered handler.
    /// </summary>
    /// <typeparam name="T">The target CLR type.</typeparam>
    /// <param name="dbValue">The database value to convert.</param>
    /// <param name="result">The converted value, if conversion succeeded.</param>
    /// <returns>True if a handler was found and conversion succeeded; otherwise false.</returns>
    internal static bool TryConvertFromDb<T>(object? dbValue, out T result)
    {
        result = default!;

        if (!TryGetHandler(typeof(T), out ITypeHandler? handler) || handler is null)
            return false;

        try
        {
            object? converted = handler.Parse(dbValue);
            if (converted is null)
            {
                result = default!;
            }
            else
            {
                result = (T)converted;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to convert a CLR value to a database value using a registered handler.
    /// </summary>
    /// <typeparam name="T">The CLR value type.</typeparam>
    /// <param name="value">The CLR value to convert.</param>
    /// <param name="dbValue">The converted database value, if conversion succeeded.</param>
    /// <returns>True if a handler was found and conversion succeeded; otherwise false.</returns>
    internal static bool TryConvertToDb<T>(T value, out object? dbValue)
    {
        dbValue = null;

        if (!TryGetHandler(typeof(T), out ITypeHandler? handler) || handler is null)
            return false;

        try
        {
            dbValue = handler.ToDbValue(value);
            return true;
        }
        catch
        {
            dbValue = null;
            return false;
        }
    }
}
