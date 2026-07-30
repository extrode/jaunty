using System;

using Jaunty.Internals.Read;

namespace Jaunty.Extensions.Reflection;

/// <summary>
/// Converts raw ADO.NET values to CLR target types, working around <see cref="Convert.ChangeType(object, Type)"/>'s
/// inability to convert to <see cref="Nullable{T}"/>, enum, <see cref="Guid"/> or <see cref="char"/>
/// target types.
/// </summary>
/// <remarks>
/// AUD-R26-062: this was one of three implementations of the same contract, and they disagreed on
/// Guid, char, nullable unwrapping and the date/time types. The behaviour now lives in
/// <see cref="DbValueConversion"/>, which documents each divergence and which way it was settled;
/// this type remains as the assembly's entry point so its call sites read unchanged.
/// </remarks>
internal static class DbValueConverter
{
    public static object ChangeType(object value, Type targetType) =>
        DbValueConversion.Convert(value, targetType);
}
