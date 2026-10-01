namespace Extrode.Jaunty.Internals.Parameters;

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

    // AUD-R38-057: likewise for engines where '$' is never a sigil. MySQL/MariaDB is the only
    // backslash-escaping engine and is also dollar-free, so it keeps its own cache unchanged.
    private static readonly BoundedCache<string, string[]> DollarFreeCache = new(StringComparer.Ordinal);

    public static string[] GetOrAdd(string sql, bool backslashEscapes = false, bool dollarSigil = true)
    {
        if (backslashEscapes)
            return BackslashEscapedCache.GetOrAdd(sql, static s => SqlParameterParser.ExtractParameterNames(s, backslashEscapes: true, dollarSigil: false));

        return dollarSigil
            ? Cache.GetOrAdd(sql, static s => SqlParameterParser.ExtractParameterNames(s))
            : DollarFreeCache.GetOrAdd(sql, static s => SqlParameterParser.ExtractParameterNames(s, dollarSigil: false));
    }
}