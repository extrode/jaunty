using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Interfaces;
namespace Jaunty.Readers;

public static class EntityReader
{
    // Sync version - always available
    public static IEnumerable<T> ReadEntities<T>(IDataReader reader)
        where T : IMapped<T>, new()
    {
#if NET7_0_OR_GREATER
        var columnNames = T.ColumnNames;
        var ordinals = new int[columnNames.Length];
        for (int i = 0; i < columnNames.Length; i++)
        {
            ordinals[i] = reader.GetOrdinal(columnNames[i]);
        }
#else
        var mapper = new T();
        var columnNames = mapper.GetColumnNames();
        var ordinals = new int[columnNames.Length];
        for (int i = 0; i < columnNames.Length; i++)
        {
            ordinals[i] = reader.GetOrdinal(columnNames[i]);
        }
#endif
        while (reader.Read())
        {
#if NET7_0_OR_GREATER
            yield return T.ReadEntity(reader, ordinals);
#else
            yield return mapper.ReadEntity(reader, ordinals);
#endif
        }
    }

#if NET7_0_OR_GREATER || ASYNC_ENUMERABLE_SUPPORT
    public static async IAsyncEnumerable<T> ReadEntitiesAsync<T>(DbDataReader reader, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : IMapped<T>, new()
    {
#if NET7_0_OR_GREATER
        var columnNames = T.ColumnNames;
        var ordinals = new int[columnNames.Length];
        for (int i = 0; i < columnNames.Length; i++)
        {
            ordinals[i] = reader.GetOrdinal(columnNames[i]);
        }
#else
        var mapper = new T();
        var columnNames = mapper.GetColumnNames();
        var ordinals = new int[columnNames.Length];
        for (int i = 0; i < columnNames.Length; i++)
        {
            ordinals[i] = reader.GetOrdinal(columnNames[i]);
        }
#endif
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
#if NET7_0_OR_GREATER
            yield return T.ReadEntity(reader, ordinals);
#else
            yield return mapper.ReadEntity(reader, ordinals);
#endif
        }
    }
#endif

#if !NET7_0_OR_GREATER && !ASYNC_ENUMERABLE_SUPPORT
    public static async Task<List<T>> ReadEntitiesAsync<T>(
        DbDataReader reader,
        CancellationToken cancellationToken = default)
        where T : IMapped<T>, new()
    {
        var mapper = new T();
        var columnNames = mapper.GetColumnNames();
        var ordinals = new int[columnNames.Length];
        for (int i = 0; i < columnNames.Length; i++)
        {
            ordinals[i] = reader.GetOrdinal(columnNames[i]);
        }

        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(mapper.ReadEntity(reader, ordinals));
        }
        return results;
    }
#endif
}