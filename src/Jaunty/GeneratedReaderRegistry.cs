using System.Data;

namespace Jaunty;

internal static class GeneratedReaderRegistry
{
    private static readonly Dictionary<Type, Delegate> _map = [];

    public static void Register<T>(Func<IDataReader, T> reader) => _map[typeof(T)] = reader;

    public static bool TryGet<T>(out Func<IDataReader, T> reader)
    {
        if (_map.TryGetValue(typeof(T), out var d))
        {
            reader = (Func<IDataReader, T>)d;
            return true;
        }

        reader = null!;
        return false;
    }
}
