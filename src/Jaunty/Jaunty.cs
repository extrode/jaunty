using System.Data;
using System.Reflection;

using Jaunty.Interfaces;
using Jaunty.Internal.Execution;
using Jaunty.Internal.Mapping;
using Jaunty.Readers;

namespace Jaunty;

public static partial class Jaunty
{
    internal static object[] CombineParams(object param1, object param2, object[] rest)
    {
        var result = new object[2 + rest.Length];
        result[0] = param1;
        result[1] = param2;

        for (int i = 0; i < rest.Length; i++)
            result[i + 2] = rest[i];

        return result;
    }

    internal static List<T> QueryCore<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T : new()
    {
        return CommandExecutor.ExecuteReader(connection, sql, parameters, options.Transaction, options.CommandTimeout, reader =>
        {
            var results = new List<T>();
            var setters = MetadataCache<T>.GetSetters(reader, mode);

            while (reader.Read())
            {
                var entity = new T();

                for (int i = 0; i < setters.Length; i++)
                    setters[i].Set(entity, reader);

                results.Add(entity);
            }

            return results;
        });
    }

    internal static IEnumerable<T> QueryInternal<T>(IDbConnection connection, string sql, object? parameters, CommandOptions options, MappingMode mode) where T : new()
    {
        return CommandExecutor.ExecuteReader(connection, sql, parameters, options.Transaction, options.CommandTimeout,
            reader => DispatchRead<T>(reader, mode));
    }

    private static IEnumerable<T> ReadMappedEntities<T>(IDataReader reader) where T : IMapped<T>, new()
    {
        return EntityReader.ReadEntities<T>(reader);
    }

    private static IEnumerable<T> DispatchRead<T>(IDataReader reader, MappingMode mode) where T : new()
    {
        // 1. Source generated
        if (GeneratedEntityReader<T>.Exists)
            return GeneratedEntityReader<T>.Read(reader);

        // 2. User mapped
        if (ImplementsIMapped<T>())
            return ReadMappedViaTrampoline<T>(reader);

        // 3. Fallback
        return MetadataEntityReader.ReadEntities<T>(reader, mode);
    }

    private static IEnumerable<T> ReadMappedViaTrampoline<T>(IDataReader reader)
    {
        var method = typeof(Jaunty)
            .GetMethod(nameof(ReadMappedEntities), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(T));

        return (IEnumerable<T>)method.Invoke(null, [reader])!;
    }

    private static bool ImplementsIMapped<T>()
    {
        var type = typeof(T);
        foreach (var i in type.GetInterfaces())
        {
            if (i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IMapped<>))
                return true;
        }
        return false;
    }
}
