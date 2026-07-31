using System.ComponentModel;

using Jaunty.Attributes;
using Jaunty.Configuration;
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
}
