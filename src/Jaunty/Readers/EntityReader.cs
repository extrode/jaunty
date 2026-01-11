using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Interfaces;
using Jaunty.Internals;

namespace Jaunty.Readers;

internal static class EntityReader
{
    internal static IEnumerable<T> ReadEntities<T>(IDataReader reader) where T : IMapped<T>, new()
    {
        var mapper = MappedCache<T>.Mapper!;
        while (reader.Read())
            yield return mapper(reader);
    }

#if ASYNC_ENUMERABLE_SUPPORT
    public static async IAsyncEnumerable<T> ReadEntitiesAsync<T>(DbDataReader reader, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : IMapped<T>, new()
    {
        var mapper = MappedCache<T>.Mapper!;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return mapper(reader);
    }
#else
    public static async Task<List<T>> ReadEntitiesAsync<T>(DbDataReader reader, CancellationToken cancellationToken = default)
        where T : IMapped<T>, new()
    {
        var mapper = MappedCache<T>.Mapper!;
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            results.Add(mapper(reader));
        return results;
    }
#endif
}
