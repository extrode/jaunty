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
    /// Invokes <see cref="ITypeHandler.ToDbValue"/>, rewrapping anything it throws as an
    /// <see cref="InvalidOperationException"/> naming the handler and the value type.
    /// </summary>
    /// <remarks>
    /// AUD-R35-115 (round-35 batch 04c). Three write paths call the same handler registry -
    /// <c>ParameterBinder.ApplyTypeHandlerIfNeeded</c> for ad-hoc parameters,
    /// <c>GeneratedBindingSupport.ToDbValue</c>/<c>ToDbEnumValue</c> for the source-generated
    /// binders, and <c>JauntyReflectionExtensions.ConvertWithTypeHandlerOrElse</c> for the
    /// reflection entity binders - and only the first wrapped a throwing handler. The same faulty
    /// handler therefore produced a diagnosable error on one path and a raw handler-internal
    /// exception naming neither Jaunty, the handler nor the value on the other two. One failure
    /// mode, three call sites, so one contract: they all come through here now.
    /// </remarks>
    internal static object? ToDbValueOrThrow(ITypeHandler handler, object value)
    {
        try
        {
            return handler.ToDbValue(value);
        }
        catch (Exception ex)
        {
            // Surface conversion failures rather than silently binding unconverted data.
            throw new InvalidOperationException(
                $"Type handler '{handler.GetType().Name}' failed to convert a value of type '{value.GetType().Name}' to its database representation.",
                ex);
        }
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
    /// Clears all registered handlers.
    /// </summary>
    /// <remarks>
    /// AUD-R35-177. This said "intended for testing cleanup", but its only non-test caller is the
    /// public production API <c>JauntyConfig.Reset()</c>, which the surrounding code treats as a real
    /// runtime operation - it deliberately bumps <c>ConfigurationGeneration</c> afterwards, because
    /// this method does not bump it itself. Anyone reasoning about handler lifetime from the old text
    /// would have concluded handlers outlive a <c>Reset()</c>.
    /// </remarks>
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
    /// <remarks>
    /// AUD-R35-176. This method and its sibling <see cref="TryConvertToDb{T}"/> have no caller
    /// anywhere in <c>src/</c>. All four production consumers - <c>ParameterBinder</c>,
    /// <c>GeneratedBindingSupport</c>, <c>JauntyReflectionExtensions</c> and
    /// <c>MetadataCache.CreateSetter</c> - call <see cref="TryGetHandler"/> and convert themselves.
    /// They also key on the <em>static</em> <c>typeof(T)</c>, where every write-path consumer keys on
    /// the runtime <c>value.GetType()</c>, so the two lookups disagree whenever the static type is
    /// looser than the value's (an <c>object</c>-typed call, for instance). The consequence worth
    /// stating plainly: the exception-wrapping and null-return contracts these two methods define,
    /// and the tests that pin them, are not the contracts the shipped paths execute. Kept rather than
    /// removed - that is a deletion decision, not an audit one - and recorded in
    /// <c>work/todo.md</c>. First filed in round 27 and never registered either way.
    /// </remarks>
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
                // R27 batch 7: a null from the handler cannot represent a non-nullable value
                // type; reporting success would hand back default(T) (e.g. 0 for int),
                // indistinguishable from real data.
                return default(T) is null;
            }
            result = (T)converted;
            return true;
        }
        catch (Exception ex)
        {
            // Surface conversion failures rather than silently reporting "no conversion".
            throw new InvalidOperationException(
                $"Type handler '{handler.GetType().Name}' failed to parse a database value into type '{typeof(T).Name}'.",
                ex);
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
        catch (Exception ex)
        {
            // Surface conversion failures rather than silently reporting "no conversion".
            throw new InvalidOperationException(
                $"Type handler '{handler.GetType().Name}' failed to convert a value of type '{typeof(T).Name}' to its database representation.",
                ex);
        }
    }
}
