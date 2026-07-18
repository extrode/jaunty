namespace Jaunty.Internals.Parameters;

internal static class SqlParameterParserCache
{
    // Size-capped so callers that embed literals or build SQL dynamically cannot leak memory
    // through an ever-growing set of distinct SQL-text keys.
    private static readonly BoundedCache<string, string[]> Cache = new(StringComparer.Ordinal);

    public static string[] GetOrAdd(string sql)
    {
        return Cache.GetOrAdd(sql, static s => SqlParameterParser.ExtractParameterNames(s));
    }
}