namespace Jaunty.Internals.Parameters;

internal static class SqlParameterParserCache
{
    // Size-capped so callers that embed literals or build SQL dynamically cannot leak memory
    // through an ever-growing set of distinct SQL-text keys.
    private static readonly BoundedCache<string, string[]> Cache = new(StringComparer.Ordinal);

    // AUD-R34-014: the same SQL text parses differently under MySQL/MariaDB, where a backslash
    // escapes the next character inside a string literal. Two caches rather than one composite
    // key: the flag is fixed per engine, so a process talking to a single engine still fills
    // exactly one of them.
    private static readonly BoundedCache<string, string[]> BackslashEscapedCache = new(StringComparer.Ordinal);

    public static string[] GetOrAdd(string sql, bool backslashEscapes = false)
    {
        return backslashEscapes
            ? BackslashEscapedCache.GetOrAdd(sql, static s => SqlParameterParser.ExtractParameterNames(s, backslashEscapes: true))
            : Cache.GetOrAdd(sql, static s => SqlParameterParser.ExtractParameterNames(s));
    }
}