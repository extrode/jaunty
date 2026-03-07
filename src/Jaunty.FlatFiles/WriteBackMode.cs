namespace Jaunty.FlatFiles;

/// <summary>
/// Specifies how modified data should be written back to disk.
/// </summary>
public enum WriteBackMode
{
    /// <summary>
    /// Replace the original file with the modified data (atomic: temp file + rename).
    /// </summary>
    Overwrite,

    /// <summary>
    /// Write to a new file specified by the output path.
    /// </summary>
    NewFile
}
