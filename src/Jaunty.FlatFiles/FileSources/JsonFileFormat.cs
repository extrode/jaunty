namespace Jaunty.FlatFiles.FileSources;

/// <summary>
/// The JSON file structure format.
/// </summary>
public enum JsonFileFormat
{
    /// <summary>Auto-detect the JSON format.</summary>
    Auto,

    /// <summary>JSON array of objects: [{...}, {...}].</summary>
    Array,

    /// <summary>Newline-delimited JSON (one object per line).</summary>
    NewlineDelimited
}