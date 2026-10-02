using System.Collections.Concurrent;

using Extrode.Jaunty.Internals;

namespace Extrode.Jaunty.TypeHandlers;

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
    internal static bool HasHandlers
    {
        get
        {
            ConfigurationGeneration.MarkRead();
            return _handlerCount > 0;
        }
    }

    /// <summary>
    /// Registers a type handler for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to register a handler for.</typeparam>
    /// <param name="handler">The handler instance.</param>
    /// <remarks>
    /// AUD-R38-061: a handler for <c>Nullable&lt;X&gt;</c> is keyed on <c>X</c>, since every read and
    /// write lookup asks for the underlying type. Registering one for <c>X</c> and one for
    /// <c>X?</c> leaves whichever came last.
    /// </remarks>
    internal static void Register<T>(ITypeHandler handler) => Register(KeyFor<T>(), handler);

    /// <summary>
    /// Registers <paramref name="handler"/> under a key already produced by <see cref="KeyFor{T}"/>.
    /// </summary>
    internal static void Register(Type key, ITypeHandler handler)
    {
        lock (MutationSync)
        {
            Handlers.AddOrUpdate(key, handler, (_, __) => handler);
            _handlerCount = Handlers.Count;
        }
    }

    internal static Type KeyFor<T>() => Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

    /// <summary>
    /// Attempts to retrieve a registered type handler for the specified type.
    /// </summary>
    /// <param name="type">The type to look up.</param>
    /// <param name="handler">The handler, if found; otherwise null.</param>
    /// <returns>True if a handler was found; otherwise false.</returns>
    internal static bool TryGetHandler(Type type, out ITypeHandler? handler)
    {
        ConfigurationGeneration.MarkRead();
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
    /// exception naming neither Extrode.Jaunty, the handler nor the value on the other two. One failure
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
        Type key = KeyFor<T>();
        lock (MutationSync)
        {
            if (!Handlers.TryRemove(key, out _))
                return false;
            _handlerCount = Handlers.Count;
        }

        Configuration.JauntyConfig.ForgetTypeHandler(key);
        return true;
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
}
