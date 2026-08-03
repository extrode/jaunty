using System.ComponentModel;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Internals.Read;
using Jaunty.TypeHandlers;

namespace Jaunty.Core;

/// <summary>
/// Write-path value conversion for source-generated binders. Not intended for application code.
/// </summary>
/// <remarks>
/// AUD-R30: the generated <c>BindInsert</c>/<c>BindUpdate</c>/<c>BindDelete</c> handed the raw
/// boxed property value to <c>IDbDataParameter.Value</c>, while the reflection
/// binder (<c>JauntyReflectionExtensions.BuildValueConverter</c>) applies
/// <see cref="TypeHandlerRegistry"/> and <see cref="EnumStorageAttribute"/> /
/// <see cref="JauntyConfig.DefaultEnumStorage"/> conversion. A string-stored enum entity therefore
/// read correctly through the generated mapper but wrote its numeric value back, and registered
/// type handlers were ignored - adding the generator package silently changed write behaviour.
/// Generated code runs in the consumer's assembly, so this bridge must be public;
/// <see cref="TypeHandlerRegistry"/> itself stays internal.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedBindingSupport
{
    /// <summary>
    /// Converts a non-enum property value for parameter binding: a registered type handler's
    /// <c>ToDbValue</c> applies; otherwise the value passes through unchanged.
    /// </summary>
    public static object? ToDbValue(object? value)
    {
        if (value is not null && TypeHandlerRegistry.HasHandlers
            && TypeHandlerRegistry.TryGetHandler(value.GetType(), out ITypeHandler? handler) && handler is not null)
        {
            return handler.ToDbValue(value);
        }
        return value;
    }

    /// <summary>
    /// Converts an enum property value for parameter binding, mirroring the reflection binder's
    /// <c>BuildValueConverter</c>: a registered type handler wins outright; otherwise an explicit
    /// <c>[EnumStorage]</c> (baked in by the generator) or, when <paramref name="storage"/> is
    /// <see langword="null"/>, the mutable <see cref="JauntyConfig.DefaultEnumStorage"/> - read
    /// per call for the same reason the reflection path re-reads it - decides between the enum's
    /// name and its numeric value.
    /// </summary>
    public static object? ToDbEnumValue(object? value, EnumStorage? storage)
    {
        if (value is null)
            return null;

        if (TypeHandlerRegistry.HasHandlers
            && TypeHandlerRegistry.TryGetHandler(value.GetType(), out ITypeHandler? handler) && handler is not null)
        {
            return handler.ToDbValue(value);
        }

        return (storage ?? JauntyConfig.DefaultEnumStorage) == EnumStorage.String
            ? value.ToString()
            : value;
    }

    /// <summary>
    /// Whether any type handler is registered at all. Read once per row by the generated read
    /// path so the no-handler case - which is nearly every case - costs a static field load and a
    /// predictable branch rather than a generic call per column.
    /// </summary>
    /// <remarks>
    /// AUD-R35: the generated <em>write</em> path consults <see cref="TypeHandlerRegistry"/> via
    /// <see cref="ToDbValue"/> (AUD-R30-002), and the reflection read path re-resolves the registry
    /// on every call (<c>MetadataCache.CreateSetter</c>), but the generated <em>read</em> path
    /// never consulted it - emitted <c>ReadEntity</c>/<c>CreateRowMapper</c> went straight to typed
    /// getters or <c>ReadFallback&lt;T&gt;</c>. Since <c>DrDispatcher.Resolve</c> prefers the
    /// generated mapper, referencing the generator package left a registered handler applied on
    /// write and skipped on read for the same entity: a value converted going in and not coming
    /// back out. AUD-R30-002 fixed the binders only and did not touch the read side.
    /// </remarks>
    public static bool HasHandlers => TypeHandlerRegistry.HasHandlers;

    /// <summary>
    /// Whether a handler is registered for <typeparamref name="T"/> specifically. The generated
    /// read path checks this before reaching for <see cref="FromDbValue{T}"/>, so a handler
    /// registered for some unrelated type leaves every other property on the entity reading
    /// through the emitted fast path byte for byte - the two conversions are deliberately not
    /// interchangeable (the emitted <c>ReadFallback&lt;T&gt;</c> accepts provider shapes, such as a
    /// <c>DateTime</c> for a <c>TimeOnly</c> column, that the shared converter does not).
    /// </summary>
    public static bool HasHandlerFor<T>()
        => TypeHandlerRegistry.TryGetHandler(typeof(T), out ITypeHandler? handler) && handler is not null;

    /// <summary>
    /// Read-path counterpart to <see cref="ToDbValue"/>, mirroring the handler branch of
    /// <c>MetadataCache.CreateSetter</c>: a handler registered for <typeparamref name="T"/> parses
    /// the raw value; otherwise the shared <c>DbValueConversion</c> applies, which is what the
    /// generated fast path would have produced.
    /// </summary>
    /// <remarks>
    /// The lookup key is <typeparamref name="T"/>, and the generator passes the property's
    /// <em>underlying</em> (non-nullable) type - the same key the reflection twin uses. Only
    /// reached when <see cref="HasHandlers"/> is true, so a handler registered for some other type
    /// costs the lookup and the conversion but still returns the fast path's answer. NULL columns
    /// never reach here: both the generated read path and the reflection twin's
    /// <c>PropertySetter.Set</c> skip a DBNull column before the setter runs.
    /// </remarks>
    public static T FromDbValue<T>(object dbValue)
    {
        if (TypeHandlerRegistry.TryGetHandler(typeof(T), out ITypeHandler? handler) && handler is not null)
        {
            try
            {
                object? converted = handler.Parse(dbValue);
                return converted is null ? default! : (T)converted;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Type handler '{handler.GetType().Name}' failed to parse the value read for '{typeof(T).Name}'.", ex);
            }
        }

        return dbValue is T typed ? typed : (T)DbValueConversion.Convert(dbValue, typeof(T));
    }
}
