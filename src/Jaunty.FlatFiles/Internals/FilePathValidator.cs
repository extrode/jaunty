using System.Collections.ObjectModel;

namespace Jaunty.FlatFiles.Internals;

/// <summary>
/// Validates the path array a file source is constructed with.
/// </summary>
/// <remarks>
/// AUD-R26: every multi-path source used to check only <c>filePaths[0]</c> while consuming the
/// whole array, so a null at index 1 or later constructed successfully and then surfaced as a
/// <see cref="NullReferenceException"/> inside <c>DuckDbDialect.GenerateReadFunction</c>'s
/// <c>p.Replace("'", "''")</c> - no parameter name, no index, and at SQL-build time rather than
/// at the constructor written to catch it. Shared here rather than repeated so the five
/// multi-path sources cannot drift apart again. <c>DeltaLakeFileSource</c> and
/// <c>IcebergFileSource</c> reject a multi-element array outright, so their single-element check
/// is already complete and they deliberately do not use this.
/// </remarks>
internal static class FilePathValidator
{
    /// <summary>
    /// Throws when <paramref name="filePaths"/> is null, empty, or contains a null entry at any
    /// index. Deliberately does not reject empty or whitespace entries: that would be a new
    /// restriction rather than a fix, and a remote path scheme this library does not know about
    /// is not ours to second-guess.
    /// </summary>
    /// <param name="filePaths">The caller-supplied paths.</param>
    /// <param name="argumentName">The parameter name to report on failure.</param>
    public static void ThrowIfInvalid(string[] filePaths, string argumentName)
    {
        if (filePaths is null)
            throw new ArgumentNullException(argumentName);

        if (filePaths.Length == 0)
            throw new ArgumentException("At least one file path is required.", argumentName);

        for (int i = 0; i < filePaths.Length; i++)
        {
            if (filePaths[i] is null)
                throw new ArgumentNullException(
                    argumentName,
                    $"File path at index {i} must not be null.");
        }
    }

    /// <summary>
    /// Returns a read-only defensive copy of <paramref name="filePaths"/> for a source to expose
    /// as its <c>FilePaths</c>.
    /// </summary>
    /// <remarks>
    /// AUD-R35-234. Every source validated the array and then stored it by reference, so the
    /// caller kept a live handle: <c>arr[1] = null</c> after construction re-opened exactly the
    /// failure <see cref="ThrowIfInvalid"/> was written to close, and the declared
    /// <c>IReadOnlyList&lt;string&gt;</c> was castable straight back to <c>string[]</c>. The clone
    /// closes the first hole and the <c>ReadOnlyCollection&lt;string&gt;</c> wrapper closes the
    /// second. Paths themselves are strings, so nothing deeper needs copying.
    /// </remarks>
    /// <param name="filePaths">The caller-supplied paths, already validated.</param>
    public static IReadOnlyList<string> Snapshot(string[] filePaths)
        => new ReadOnlyCollection<string>((string[])filePaths.Clone());
}
