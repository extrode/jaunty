using System.Collections.Concurrent;

using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB;

/// <summary>
/// Static factory for opening flat file databases backed by DuckDB.
/// </summary>
public static class FlatFile
{
    private static readonly ConcurrentDictionary<string, Func<string, string, Type, IFileSource>> _extensionRegistry = new(StringComparer.OrdinalIgnoreCase)
    {
        [".csv"] = (tableName, fullPath, entityType) => new CsvFileSource(tableName, fullPath, entityType),
        [".tsv"] = (tableName, fullPath, entityType) => new TsvFileSource(tableName, fullPath, entityType),
        [".parquet"] = (tableName, fullPath, entityType) => new ParquetFileSource(tableName, fullPath, entityType),
        [".json"] = (tableName, fullPath, entityType) => new JsonFileSource(tableName, fullPath, entityType),
        [".ndjson"] = (tableName, fullPath, entityType) => new JsonFileSource(tableName, fullPath, entityType) { JsonFormat = JsonFileFormat.NewlineDelimited },
        [".xlsx"] = (tableName, fullPath, entityType) => new ExcelFileSource(tableName, fullPath, entityType),
        // NOTE: ".xls" (legacy binary Excel) is intentionally NOT registered here. ExcelFileSource
        // reads via DuckDB's read_xlsx, which only supports the modern ".xlsx" format. Opening a
        // ".xls" file falls through to the "unsupported extension" error below.
    };

    /// <summary>
    /// URI schemes that DuckDB supports for remote and local file access.
    /// </summary>
    private static readonly HashSet<string> _allowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "file",                 // Local file access
        "http", "https",        // httpfs extension
        "s3", "s3a", "s3n",     // AWS S3
        "az", "abfss",          // Azure Blob Storage
        "r2",                   // Cloudflare R2
        "gs",                   // Google Cloud Storage
        "hf",                   // Hugging Face
    };

    /// <summary>
    /// Registers a custom file extension so that <see cref="Open(string)"/> can handle it.
    /// The factory receives (tableName, fullPath, entityType) and must return an <see cref="IFileSource"/>.
    /// </summary>
    /// <param name="extension">The file extension including the dot (e.g. ".xlsx").</param>
    /// <param name="factory">A factory that creates an <see cref="IFileSource"/> from a table name, file path, and entity type.</param>
    public static void RegisterExtension(string extension, Func<string, string, Type, IFileSource> factory)
    {
        ArgumentNullException.ThrowIfNull(extension);
        ArgumentNullException.ThrowIfNull(factory);
        _extensionRegistry[extension.StartsWith('.') ? extension : "." + extension] = factory;
    }

    /// <summary>
    /// Opens a single flat file as a queryable database. The file format is inferred from the extension,
    /// and the view name is derived from the filename (without extension).
    /// Supports local files, glob patterns (e.g. <c>data/*.csv</c>), and remote URLs
    /// (e.g. <c>s3://bucket/file.parquet</c>, <c>https://example.com/data.csv</c>).
    /// </summary>
    /// <param name="filePath">Path to the flat file, a glob pattern, or a remote URL.</param>
    /// <returns>A flat file database with the file registered as a queryable view.</returns>
    /// <exception cref="ArgumentException">Thrown when the file extension is not supported or the URI scheme is not allowed.</exception>
    /// <exception cref="FileNotFoundException">Thrown when a local file does not exist.</exception>
    public static IFlatFile Open(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        (string resolvedPath, string extension, string tableName) = ResolvePath(filePath, nameof(filePath));

        var options = new FlatFileOptions();
        IFileSource source = CreateSourceFromExtension(extension, tableName, resolvedPath, typeof(object));
        options.Sources.Add(source);

        return new DuckDb(options);
    }

    /// <summary>
    /// Opens a flat file database with the specified configuration.
    /// Use the builder to register multiple sources with per-source options.
    /// </summary>
    /// <param name="configure">Action to configure the database options and register file sources.</param>
    /// <returns>A flat file database with all configured sources registered.</returns>
    public static IFlatFile Open(Action<FlatFileOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new FlatFileOptions();
        configure(options);

        return new DuckDb(options);
    }

    /// <summary>
    /// Resolves a path string into the path DuckDB should read, the extension the source type is
    /// chosen from, and a table name derived from the file name.
    /// </summary>
    /// <remarks>
    /// AUD-R35-245. This was inline in <see cref="Open(string)"/>, so <c>FlatFileImporter</c>'s
    /// single-path overload - the other entry point that takes a path string over the same pipeline
    /// - had its own <c>Path.GetFullPath</c> plus <c>File.Exists</c> and accepted neither a glob
    /// nor any of the eight remote schemes, failing with a <c>FileNotFoundException</c> naming a
    /// missing file rather than an unsupported path form. Shared here so the two path-string entry
    /// points cannot accept different path languages again.
    /// </remarks>
    /// <param name="filePath">A local path, a glob pattern, or a remote URL.</param>
    /// <param name="argumentName">The caller's parameter name, for the exceptions.</param>
    internal static (string ResolvedPath, string Extension, string TableName) ResolvePath(string filePath, string argumentName)
    {
        if (IsRemoteUri(filePath, out var scheme))
        {
            if (!_allowedSchemes.Contains(scheme))
                throw new ArgumentException(
                    $"URI scheme '{scheme}://' is not allowed. Allowed schemes: {string.Join(", ", _allowedSchemes.OrderBy(s => s).Select(s => s + "://"))}.",
                    argumentName);

            string fileName = GetFileNameFromUri(filePath);
            return (filePath,
                Path.GetExtension(fileName).ToLowerInvariant(),
                SanitizeTableName(Path.GetFileNameWithoutExtension(fileName)));
        }

        if (IsGlobPattern(filePath))
        {
            // Glob patterns - DuckDB resolves them, so pass through as-is
            return (filePath,
                InferExtensionFromGlob(filePath),
                SanitizeTableName(Path.GetFileNameWithoutExtension(filePath)));
        }

        // Local file - validate existence
        string fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Flat file not found: {fullPath}", fullPath);

        return (fullPath,
            Path.GetExtension(fullPath).ToLowerInvariant(),
            SanitizeTableName(Path.GetFileNameWithoutExtension(fullPath)));
    }

    internal static IFileSource CreateSourceFromExtension(string extension, string tableName, string fullPath, Type entityType)
    {
        if (_extensionRegistry.TryGetValue(extension, out Func<string, string, Type, IFileSource>? factory))
            return factory(tableName, fullPath, entityType);

        var supported = string.Join(", ", _extensionRegistry.Keys.OrderBy(k => k));
        throw new ArgumentException(
            $"Unsupported file extension '{extension}'. Supported extensions: {supported}. " +
            $"Use {nameof(FlatFile)}.RegisterExtension() to add custom file types.",
            nameof(extension));
    }

    internal static bool IsRemoteUri(string path, out string scheme)
    {
        // Check for scheme:// pattern without using Uri.TryCreate (which accepts file:// and relative paths)
        var schemeEnd = path.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd > 0)
        {
            scheme = path[..schemeEnd];
            return true;
        }

        scheme = string.Empty;
        return false;
    }

    private static bool IsGlobPattern(string path)
        => path.Contains('*') || path.Contains('?');

    private static string GetFileNameFromUri(string uri)
    {
        // Extract filename from URI path (e.g. "s3://bucket/path/file.csv" → "file.csv")
        var queryStart = uri.IndexOf('?');
        var pathPart = queryStart >= 0 ? uri[..queryStart] : uri;
        var lastSlash = pathPart.LastIndexOf('/');
        return lastSlash >= 0 ? pathPart[(lastSlash + 1)..] : pathPart;
    }

    private static string InferExtensionFromGlob(string glob)
    {
        // Extract extension from glob (e.g. "data/*.csv" → ".csv", "logs/**/*.json" → ".json")
        // Strip glob characters to find the actual extension
        var cleaned = glob.Replace("*", "").Replace("?", "");
        var ext = Path.GetExtension(cleaned).ToLowerInvariant();
        return string.IsNullOrEmpty(ext)
            ? throw new ArgumentException(
                $"Cannot infer file format from glob pattern '{glob}'. Use FlatFile.Open(configure) with an explicit file source instead.",
                nameof(glob))
            : ext;
    }

    private static string SanitizeTableName(string name)
    {
        // Replace anything that isn't a plain identifier character so a filename
        // (which may contain quotes, glob characters, or other SQL metacharacters)
        // can never break out of the generated SQL identifier when interpolated downstream.
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (char c in name)
        {
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        var sanitized = sb.ToString().Trim('_');
        if (string.IsNullOrEmpty(sanitized))
            return "data";

        sanitized = sanitized.ToLowerInvariant();
        return char.IsDigit(sanitized[0]) ? "_" + sanitized : sanitized;
    }
}