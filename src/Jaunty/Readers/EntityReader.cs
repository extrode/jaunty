using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Interfaces;
using Jaunty.Internals;
using Jaunty.Internals.Enums;

namespace Jaunty.Readers;

/// <summary>
/// Internal helper for reading entities from data readers using cached mappers.
/// </summary>
internal static class EntityReader
{
    public static IEnumerable<T> Read<T>(this IDataReader reader, MappingMode mode = MappingMode.Strict) where T : new()
    {
        var mapper = MappedCache<T>.Mapper ?? throw new InvalidOperationException($"No mapper found for type '{typeof(T).Name}'.");
        while (reader.Read())
            yield return mapper(reader);
    }

    public static async IAsyncEnumerable<T> ReadAsync<T>(this DbDataReader reader, MappingMode mode = MappingMode.Strict, [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : new()
    {
        var mapper = MappedCache<T>.Mapper ?? throw new InvalidOperationException($"No mapper found for type '{typeof(T).Name}'.");
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return mapper(reader);
    }

    /// <summary>
    /// Reads all entities from the data reader using the cached mapper for type <typeparamref name="T"/>.
    /// </summary>
    internal static IEnumerable<T> ReadEntities<T>(IDataReader reader) where T : new()
    {
        var mapper = MappedCache<T>.Mapper ?? throw new InvalidOperationException($"No mapper found for type '{typeof(T).Name}'.");
        while (reader.Read())
            yield return mapper(reader);
    }

#if ASYNC_ENUMERABLE_SUPPORT
    /// <summary>
    /// Asynchronously reads all entities from the data reader using the cached mapper for type <typeparamref name="T"/>.
    /// </summary>
    public static async IAsyncEnumerable<T> ReadEntitiesAsync<T>(DbDataReader reader, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : new()
    {
        var mapper = MappedCache<T>.Mapper ?? throw new InvalidOperationException($"No mapper found for type '{typeof(T).Name}'.");
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return mapper(reader);
    }
#else
    /// <summary>
    /// Asynchronously reads all entities from the data reader and returns them as a list.
    /// </summary>
    public static async ValueTask<List<T>> ReadEntitiesAsync<T>(DbDataReader reader, CancellationToken cancellationToken = default)
        where T : new()
    {
        var mapper = MappedCache<T>.Mapper ?? throw new InvalidOperationException($"No mapper found for type '{typeof(T).Name}'.");
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            results.Add(mapper(reader));
        return results;
    }
#endif
}
