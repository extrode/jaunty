using System.Data;
using System.Reflection;
using Jaunty.Interfaces;
using Jaunty.Configuration;

namespace Jaunty.Internals.Write;

/// <summary>
/// Caches compiled binders for high-performance write operations.
/// </summary>
internal static class WriteParameterCache<T> where T : class
{
    public static readonly Action<IDbCommand, T>? InsertBinder;
    public static readonly Action<IDbCommand, T>? UpdateBinder;
    public static readonly Action<IDbCommand, T>? DeleteBinder;
    public static readonly Action<T, long>? IdSetter;

    static WriteParameterCache()
    {
        InsertBinder = TryGetGeneratedBinder("BindInsert") ?? TryGetReflectionBinder();
        UpdateBinder = TryGetGeneratedBinder("BindUpdate");
        DeleteBinder = TryGetGeneratedBinder("BindDelete");
        IdSetter = CreateIdSetter();
    }

    private static Action<IDbCommand, T>? TryGetGeneratedBinder(string methodName)
    {
        var method = typeof(T).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, [typeof(IDbCommand), typeof(T)], null);
        return (Action<IDbCommand, T>?)method?.CreateDelegate(typeof(Action<IDbCommand, T>));
    }

    private static Action<IDbCommand, T>? TryGetReflectionBinder()
    {
        if (JauntyConfig.ReflectionInsertBinderResolver?.Invoke(typeof(T)) is Action<IDbCommand, object> binder)
        {
            return (cmd, entity) => binder(cmd, entity);
        }
        return null;
    }

    private static Action<T, long>? CreateIdSetter()
    {
        if (typeof(IEntity).IsAssignableFrom(typeof(T)))
        {
            return static (target, value) => ((IEntity)target).Id = value;
        }
        return null;
    }
}
