using System.Collections.Concurrent;
using System.Data.Common;

using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB.Internals.Import;

/// <summary>
/// Resolves an <see cref="IImportDialect"/> from a <see cref="DbConnection"/> type.
/// Supports auto-detection for SQLite, PostgreSQL, and SQL Server. Anything else is supplied by
/// the caller as <c>ImportOptions.Dialect</c> - see <see cref="Resolve"/>. <see cref="Register"/>
/// exists for this assembly and its test project only; it is not reachable from outside.
/// </summary>
internal static class ImportDialectResolver
{
    private static readonly ConcurrentDictionary<string, IImportDialect> _registry = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a custom import dialect for a connection type name (or substring).
    /// The key is matched against the connection's <c>GetType().FullName</c> using contains logic.
    /// </summary>
    /// <remarks>
    /// AUD-R35-249: <b>not</b> a public extension point. This class is <c>internal</c>, so only
    /// <c>InternalsVisibleTo</c> assemblies - in practice the test project - can call this. A
    /// consumer of the package supplies a custom dialect through <c>ImportOptions.Dialect</c>,
    /// which is what <see cref="Resolve"/>'s unrecognised-provider message now names. Kept because
    /// the built-in detection is written in terms of the same registry and the tests drive it.
    /// </remarks>
    /// <param name="connectionTypeNameContains">A substring of the connection's full type name (e.g. "MySql", "Oracle").</param>
    /// <param name="dialect">The import dialect to use for matching connections.</param>
    public static void Register(string connectionTypeNameContains, IImportDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(connectionTypeNameContains);
        ArgumentNullException.ThrowIfNull(dialect);
        _registry[connectionTypeNameContains] = dialect;
    }

    /// <summary>
    /// Resolves the import dialect for the given connection.
    /// Checks the explicit option first, then custom registrations, then built-in detection.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The connection type is not recognised and nothing is registered for it. The message names the
    /// type and <see cref="Register"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// AUD-R26 (batch 7). An unrecognised provider used to fall back to
    /// <see cref="SqliteImportDialect"/>, so a MySQL, MariaDB, Oracle or DuckDB target with no
    /// registered dialect was handed SQLite's SQL - SQLite type names (<c>TEXT</c>, <c>INTEGER</c>,
    /// <c>REAL</c>) in the generated DDL and SQLite's <c>INSERT OR IGNORE</c> /
    /// <c>ON CONFLICT ... DO UPDATE</c> conflict syntax. On MySQL every one of those is a syntax
    /// error surfacing from the provider with no hint that dialect detection was the cause. A silent
    /// default is only reasonable when the default is broadly correct, and SQLite's is the narrowest
    /// of the three built in here.
    /// </para>
    /// <para>
    /// The resolver already had the vocabulary to say "I don't recognise this" - <see cref="Register"/>
    /// exists for exactly this case - so it now says so and names it.
    /// </para>
    /// </remarks>
    internal static IImportDialect Resolve(DbConnection connection, IImportDialect? explicitDialect)
    {
        if (explicitDialect is not null)
            return explicitDialect;

        var typeName = connection.GetType().FullName ?? "";

        // Check custom registrations first.
        //
        // AUD-R26: ordered by descending key length rather than by ConcurrentDictionary enumeration
        // order, which is unspecified. When two registered substrings both match a connection type
        // name, which one won used to be arbitrary and could differ between runs; the longer key is
        // the more specific match, so "MySqlConnector" beats "MySql" deterministically.
        foreach ((string key, IImportDialect dialect) in MatchOrder())
        {
            if (typeName.Contains(key, StringComparison.OrdinalIgnoreCase))
                return dialect;
        }

        // Built-in detection
        if (typeName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return SqliteImportDialect.Instance;
        if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            return PostgreSqlImportDialect.Instance;
        // Match the SqlClient "SqlConnection" type precisely (preceded by a namespace dot) so
        // "MySql.Data.MySqlClient.MySqlConnection" doesn't false-positive on the "SqlConnection" substring.
        if (typeName.EndsWith(".SqlConnection", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("Microsoft.Data.SqlClient", StringComparison.OrdinalIgnoreCase))
            return SqlServerImportDialect.Instance;

        // AUD-R35-249: the message used to lead with ImportDialectResolver.Register(...), and
        // Register's own doc presented it as the extension point for custom dialects - but this
        // class is internal, so the only callers are InternalsVisibleTo assemblies. A user
        // following that advice got a compile error, not a fix. ImportOptions.Dialect is the one
        // remedy a real caller has, so it is the one the message names.
        throw new InvalidOperationException(
            $"No import dialect is registered for connection type '{typeName}'. Import auto-detects " +
            "SQLite, PostgreSQL and SQL Server. For anything else, implement IImportDialect and " +
            "pass it as ImportOptions.Dialect: new ImportOptions(dialect: myDialect).");
    }

    /// <summary>
    /// The custom registry in a deterministic order: longest key first, so the most specific
    /// registered substring wins rather than whichever the dictionary happened to yield.
    /// </summary>
    private static List<KeyValuePair<string, IImportDialect>> MatchOrder()
    {
        // Built by enumeration, not by the List(IEnumerable) constructor: that path can take
        // ICollection.CopyTo, which races with a concurrent Register and throws. ConcurrentDictionary's
        // own enumerator is lock-free and safe against concurrent writers, which is the guarantee
        // Resolve_ConcurrentWithRegister_DoesNotThrow exists to hold us to.
        var entries = new List<KeyValuePair<string, IImportDialect>>();
        foreach (KeyValuePair<string, IImportDialect> entry in _registry)
            entries.Add(entry);

        entries.Sort(static (a, b) =>
        {
            int byLength = b.Key.Length.CompareTo(a.Key.Length);
            return byLength != 0 ? byLength : string.CompareOrdinal(a.Key, b.Key);
        });
        return entries;
    }
}