using System.Collections.Concurrent;

namespace Jaunty.InternalApi.Parameters;

internal static class SqlParameterParserCache
{
    private static readonly ConcurrentDictionary<string, string[]> Cache = new(StringComparer.Ordinal);

    public static string[] GetOrAdd(string sql)
    {
        return Cache.GetOrAdd(sql, static s => SqlParameterParser.ExtractParameterNames(s));
    }
}
