using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Interfaces;
using Jaunty.Internals;

namespace Jaunty.Readers;

/// <summary>
/// Internal helper for reading entities from data readers using cached mappers.
/// </summary>
internal static class EntityReader
{
    /// <summary>
    /// Reads all entities from the data reader using the cached mapper for type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IMapped{T}"/> and have a parameterless constructor.</typeparam>
    /// <param name="reader">The data reader positioned before the first row.</param>
    /// <returns>An enumerable of entities that reads rows on-demand.</returns>
    internal static IEnumerable<T> ReadEntities<T>(IDataReader reader) where T : IMapped<T>, new()
    {
        var mapper = MappedCache<T>.Mapper!;
        while (reader.Read())
            yield return mapper(reader);
    }

#if ASYNC_ENUMERABLE_SUPPORT
    /// <summary>
    /// Asynchronously reads all entities from the data reader using the cached mapper for type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IMapped{T}"/> and have a parameterless constructor.</typeparam>
    /// <param name="reader">The data reader positioned before the first row.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous read operations.</param>
    /// <returns>An async enumerable of entities that reads rows on-demand.</returns>
    /// <remarks>
    /// <para>
    /// This method is only available when <c>ASYNC_ENUMERABLE_SUPPORT</c> is defined (typically .NET 8+).
    /// It returns <see cref="IAsyncEnumerable{T}"/> for true async streaming.
    /// </para>
    /// <para>
    /// When <c>ASYNC_ENUMERABLE_SUPPORT</c> is not defined, use the fallback overload that returns <see cref="ValueTask{List{T}}"/>.
    /// </para>
    /// </remarks>
    public static async IAsyncEnumerable<T> ReadEntitiesAsync<T>(DbDataReader reader, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : IMapped<T>, new()
    {
        var mapper = MappedCache<T>.Mapper!;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return mapper(reader);
    }
#else
    /// <summary>
    /// Asynchronously reads all entities from the data reader and returns them as a list.
    /// </summary>
    /// <typeparam name="T">The entity type. Must implement <see cref="IMapped{T}"/> and have a parameterless constructor.</typeparam>
    /// <param name="reader">The data reader positioned before the first row.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous read operations.</param>
    /// <returns>A task containing a list of all entities read from the data reader.</returns>
    /// <remarks>
    /// <para>
    /// This is a fallback implementation for platforms that do not support <see cref="IAsyncEnumerable{T}"/>.
    /// All results are buffered into a list before returning.
    /// </para>
    /// <para>
    /// For true async streaming on supported platforms, use the <c>ASYNC_ENUMERABLE_SUPPORT</c> overload.
    /// </para>
    /// </remarks>
    public static async ValueTask<List<T>> ReadEntitiesAsync<T>(DbDataReader reader, CancellationToken cancellationToken = default)
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


