using System.Linq.Expressions;
using System.Reflection;

namespace Jaunty.Internal.Parameters;

internal static class ParameterCache
{
    private static readonly Dictionary<Type, ParameterMetadata[]> _cache = [];

    public static ParameterMetadata[] Get(Type type)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(type, out var meta))
                return meta;

            var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var result = new ParameterMetadata[props.Length];

            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                var getter = CreateGetter(p);
                result[i] = new ParameterMetadata(p.Name, getter);
            }

            _cache[type] = result;
            return result;
        }
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
