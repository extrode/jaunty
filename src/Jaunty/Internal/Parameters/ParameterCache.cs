using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Jaunty.Internal.Parameters;

internal static class ParameterCache
{
    private static readonly ConcurrentDictionary<Type, ParameterMetadata[]> Cache = new();

    public static ParameterMetadata[] Get(Type type)
    {
        return Cache.GetOrAdd(type, BuildMetadata);
    }

    private static ParameterMetadata[] BuildMetadata(Type type)
    {
        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var result = new ParameterMetadata[props.Length];

        for (int i = 0; i < props.Length; i++)
        {
            var p = props[i];
            result[i] = new ParameterMetadata(p.Name, CreateGetter(p));
        }

        return result;
    }

    private static Func<object, object?> CreateGetter(PropertyInfo prop)
    {
        var obj = Expression.Parameter(typeof(object), "o");
        var cast = Expression.Convert(obj, prop.DeclaringType!);
        var access = Expression.Property(cast, prop);
        var box = Expression.Convert(access, typeof(object));

        return Expression.Lambda<Func<object, object?>>(box, obj).Compile();
    }
}
