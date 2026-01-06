using System.Data;
using System.Reflection;

using Jaunty.PublicApi.Interfaces;

namespace Jaunty.InternalApi;

internal static class MappedCache<T> where T : new()
{
    internal static readonly Func<IDataReader, T>? Mapper = ResolveMapper();

    internal static Func<IDataReader, T>? ResolveMapper()
    {
        Type entityType = typeof(T);
        if (!typeof(IMapped<>).IsAssignableFrom(entityType))
            return null;

        var readMethod = entityType.GetMethod("ReadEntity", BindingFlags.Public | BindingFlags.Static, null, [typeof(IDataReader)], null);

        if (readMethod is null)
            return null;

#if NET8_0_OR_GREATER
        return readMethod.CreateDelegate<Func<IDataReader, T>>();
#else
        return (Func<IDataReader, T>)Delegate.CreateDelegate(typeof(Func<IDataReader, T>), readMethod);
#endif
    }
}