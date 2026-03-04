using System.Data;

namespace Jaunty.FlatFiles;

/// <summary>
/// Represents an embedded database engine that can query flat files.
/// </summary>
public interface IFlatFileDatabase : IDisposable
#if !NETSTANDARD2_0
    , IAsyncDisposable
#endif
{
    /// <summary>
    /// Gets the underlying ADO.NET connection to the embedded database.
    /// </summary>
    IDbConnection Connection { get; }

    /// <summary>
    /// Registers a file source so it can be queried as a table.
    /// </summary>
    /// <param name="source">The file source to register.</param>
    void RegisterSource(IFileSource source);

    /// <summary>
    /// Registers a file source asynchronously.
    /// </summary>
    /// <param name="source">The file source to register.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    ValueTask RegisterSourceAsync(IFileSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the file source metadata for a registered entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <returns>The file source info, or null if not registered.</returns>
    IFileSource? GetSource<T>() where T : class, new();

    /// <summary>
    /// Gets whether any mutations have been made to the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    bool IsModified<T>() where T : class, new();
}
