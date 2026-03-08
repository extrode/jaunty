using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.FileSources;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB;

/// <summary>
/// Static factory for opening flat file databases backed by DuckDB.
/// </summary>
public static class FlatFile
{
    private static readonly Dictionary<string, Func<string, string, Type, IFileSource>> _extensionRegistry = new(StringComparer.OrdinalIgnoreCase)
    {
        [".csv"] = (tableName, fullPath, entityType) => new CsvFileSource(tableName, fullPath, entityType),
        [".tsv"] = (tableName, fullPath, entityType) => new TsvFileSource(tableName, fullPath, entityType),
        [".parquet"] = (tableName, fullPath, entityType) => new ParquetFileSource(tableName, fullPath, entityType),
        [".json"] = (tableName, fullPath, entityType) => new JsonFileSource(tableName, fullPath, entityType),
        [".ndjson"] = (tableName, fullPath, entityType) => new JsonFileSource(tableName, fullPath, entityType) { JsonFormat = JsonFileFormat.NewlineDelimited },
        [".xlsx"] = (tableName, fullPath, entityType) => new ExcelFileSource(tableName, fullPath, entityType),
        [".xls"] = (tableName, fullPath, entityType) => new ExcelFileSource(tableName, fullPath, entityType),
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

        string resolvedPath;
        string extension;
        string tableName;

        if (IsRemoteUri(filePath, out var scheme))
        {
            if (!_allowedSchemes.Contains(scheme))
                throw new ArgumentException(
                    $"URI scheme '{scheme}://' is not allowed. Allowed schemes: {string.Join(", ", _allowedSchemes.OrderBy(s => s).Select(s => s + "://"))}.",
                    nameof(filePath));

            resolvedPath = filePath;
            extension = Path.GetExtension(GetFileNameFromUri(filePath)).ToLowerInvariant();
            tableName = Path.GetFileNameWithoutExtension(GetFileNameFromUri(filePath)).ToLowerInvariant();
        }
        else if (IsGlobPattern(filePath))
        {
            // Glob patterns — DuckDB resolves them, so pass through as-is
            resolvedPath = filePath;
            extension = InferExtensionFromGlob(filePath);
            tableName = SanitizeTableName(Path.GetFileNameWithoutExtension(filePath));
        }
        else
        {
            // Local file — validate existence
            resolvedPath = Path.GetFullPath(filePath);
            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException($"Flat file not found: {resolvedPath}", resolvedPath);

            extension = Path.GetExtension(resolvedPath).ToLowerInvariant();
            tableName = Path.GetFileNameWithoutExtension(resolvedPath).ToLowerInvariant();
        }

        var options = new FlatFileOptions();
        var source = CreateSourceFromExtension(extension, tableName, resolvedPath, typeof(object));
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

    internal static IFileSource CreateSourceFromExtension(string extension, string tableName, string fullPath, Type entityType)
    {
        if (_extensionRegistry.TryGetValue(extension, out var factory))
            return factory(tableName, fullPath, entityType);

        var supported = string.Join(", ", _extensionRegistry.Keys.OrderBy(k => k));
        throw new ArgumentException(
            $"Unsupported file extension '{extension}'. Supported extensions: {supported}. " +
            $"Use {nameof(FlatFile)}.RegisterExtension() to add custom file types.",
            nameof(extension));
    }

    private static bool IsRemoteUri(string path, out string scheme)
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
        // Remove glob characters and normalize for use as a SQL table name
        var sanitized = name.Replace("*", "").Replace("?", "").Trim('.', '_', '-');
        return string.IsNullOrEmpty(sanitized) ? "data" : sanitized.ToLowerInvariant();
    }
}