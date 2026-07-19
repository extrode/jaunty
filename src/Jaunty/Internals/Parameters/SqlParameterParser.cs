using System.Text;

using JauntyConfig = Jaunty.Configuration.JauntyConfig;

namespace Jaunty.Internals.Parameters;

internal static class SqlParameterParser
{
    internal static string[] ExtractParameterNames(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return [];

#if NET8_0_OR_GREATER
        return ExtractParameterNamesSpan(sql.AsSpan());
#else
        return ExtractParameterNamesClassic(sql);
#endif
    }

#if NET8_0_OR_GREATER
    private static string[] ExtractParameterNamesSpan(ReadOnlySpan<char> sql)
    {
        var names = new List<string>(JauntyConfig.ParameterParsingCapacity);
        var i = 0;
        var len = sql.Length;

        while (i < len)
        {
            var c = sql[i];

            // Skip single-line comment
            if (c == '-' && i + 1 < len && sql[i + 1] == '-')
            {
                i = SkipToEndOfLine(sql, i + 2);
                continue;
            }

            // Skip block comment
            if (c == '/' && i + 1 < len && sql[i + 1] == '*')
            {
                i = SkipBlockComment(sql, i + 2);
                continue;
            }

            // Skip string literal (single quote)
            if (c == '\'')
            {
                i = SkipQuoted(sql, i + 1, '\'');
                continue;
            }

            // Skip identifier (double quote or brackets)
            if (c == '"')
            {
                i = SkipQuoted(sql, i + 1, '"');
                continue;
            }

            if (c == '[')
            {
                i = SkipQuoted(sql, i + 1, ']');
                continue;
            }

            // Skip SQL Server global/system variable (@@IDENTITY, @@ROWCOUNT, ...)
            if (c == '@' && i + 1 < len && sql[i + 1] == '@')
            {
                i += 2;
                while (i < len && IsParameterChar(sql[i]))
                    i++;
                continue;
            }

            // PostgreSQL/DuckDB dollar-quoted string ($$...$$ or $tag$...$tag$): must be
            // recognized before generic parameter extraction below, or its body gets scanned
            // as if it were ordinary SQL text and any identifier-shaped run inside it (e.g.
            // "SELECT" in "$$SELECT 1$$") is mistaken for a parameter name.
            if (c == '$')
            {
                int dollarQuoteEnd = TrySkipDollarQuoted(sql, i);
                if (dollarQuoteEnd >= 0)
                {
                    i = dollarQuoteEnd;
                    continue;
                }
            }

            // Found parameter (@ for SQL Server/SQLite, $ for DuckDB/PostgreSQL)
            if (c is '@' or '$')
            {
                i = ExtractAndAddParameterName(sql, i + 1, names);
                continue;
            }

            i++;
        }

        return [.. names];
    }

    // A dollar-quote opening tag is '$' + zero-or-more identifier chars + '$' (e.g. "$$" or
    // "$tag$"). A bare "$name" parameter reference never has a second unescaped '$' immediately
    // after its name chars in that position, so probing for the closing '$' safely disambiguates
    // the two without needing full context. Returns the index just past the closing delimiter,
    // or -1 if 'sql[dollarPos]' is not the start of a dollar-quote.
    private static int TrySkipDollarQuoted(ReadOnlySpan<char> sql, int dollarPos)
    {
        int len = sql.Length;
        int tagEnd = dollarPos + 1;
        while (tagEnd < len && IsParameterChar(sql[tagEnd]))
            tagEnd++;

        if (tagEnd >= len || sql[tagEnd] != '$')
            return -1;

        int delimLen = tagEnd + 1 - dollarPos;
        int searchFrom = tagEnd + 1;
        while (searchFrom + delimLen <= len)
        {
            if (sql.Slice(searchFrom, delimLen).SequenceEqual(sql.Slice(dollarPos, delimLen)))
                return searchFrom + delimLen;
            searchFrom++;
        }

        return len; // Unterminated dollar-quote: skip to end rather than mis-scan the remainder.
    }

    private static int SkipToEndOfLine(ReadOnlySpan<char> sql, int i)
    {
        int len = sql.Length;
        while (i < len)
        {
            var c = sql[i];
            if (c is '\n' or '\r') break;
            i++;
        }
        return i;
    }

    private static int SkipBlockComment(ReadOnlySpan<char> sql, int i)
    {
        int len = sql.Length;
        while (i + 1 < len)
        {
            if (sql[i] == '*' && sql[i + 1] == '/')
                return i + 2;
            i++;
        }
        return len;
    }

    private static int SkipQuoted(ReadOnlySpan<char> sql, int i, char terminator)
    {
        int len = sql.Length;
        while (i < len)
        {
            if (sql[i] == terminator)
            {
                // Handle escaped terminator (doubled)
                if (i + 1 < len && sql[i + 1] == terminator)
                {
                    i += 2;
                    continue;
                }
                return i + 1;
            }
            i++;
        }
        return len;
    }

    private static int ExtractAndAddParameterName(ReadOnlySpan<char> sql, int start, List<string> names)
    {
        int i = start;
        int len = sql.Length;

        while (i < len && IsParameterChar(sql[i]))
            i++;

        if (i > start)
        {
            names.Add(sql.Slice(start, i - start).ToString());
        }

        return i;
    }
#endif

    private static string[] ExtractParameterNamesClassic(string sql)
    {
        var names = new List<string>(JauntyConfig.ParameterParsingCapacity);
        var i = 0;
        var len = sql.Length;

        while (i < len)
        {
            var c = sql[i];

            if (c == '-' && i + 1 < len && sql[i + 1] == '-')
            {
                i = SkipToEndOfLineClassic(sql, i + 2, len);
                continue;
            }

            if (c == '/' && i + 1 < len && sql[i + 1] == '*')
            {
                i = SkipBlockCommentClassic(sql, i + 2, len);
                continue;
            }

            if (c == '\'')
            {
                i = SkipQuotedClassic(sql, i + 1, len, '\'');
                continue;
            }

            if (c == '"')
            {
                i = SkipQuotedClassic(sql, i + 1, len, '"');
                continue;
            }

            if (c == '[')
            {
                i = SkipQuotedClassic(sql, i + 1, len, ']');
                continue;
            }

            if (c == '@' && i + 1 < len && sql[i + 1] == '@')
            {
                i += 2;
                while (i < len && IsParameterChar(sql[i]))
                    i++;
                continue;
            }

            if (c == '$')
            {
                int dollarQuoteEnd = TrySkipDollarQuotedClassic(sql, i, len);
                if (dollarQuoteEnd >= 0)
                {
                    i = dollarQuoteEnd;
                    continue;
                }
            }

            if (c is '@' or '$')
            {
                i = ExtractAndAddParameterNameClassic(sql, i + 1, len, names);
                continue;
            }

            i++;
        }

        return [.. names];
    }

    private static int TrySkipDollarQuotedClassic(string sql, int dollarPos, int len)
    {
        int tagEnd = dollarPos + 1;
        while (tagEnd < len && IsParameterChar(sql[tagEnd]))
            tagEnd++;

        if (tagEnd >= len || sql[tagEnd] != '$')
            return -1;

        int delimLen = tagEnd + 1 - dollarPos;
        int searchFrom = tagEnd + 1;
        while (searchFrom + delimLen <= len)
        {
            if (string.CompareOrdinal(sql, searchFrom, sql, dollarPos, delimLen) == 0)
                return searchFrom + delimLen;
            searchFrom++;
        }

        return len;
    }

    private static int SkipToEndOfLineClassic(string sql, int i, int len)
    {
        while (i < len)
        {
            var c = sql[i];
            if (c is '\n' or '\r') break;
            i++;
        }
        return i;
    }

    private static int SkipBlockCommentClassic(string sql, int i, int len)
    {
        while (i + 1 < len)
        {
            if (sql[i] == '*' && sql[i + 1] == '/')
                return i + 2;
            i++;
        }
        return len;
    }

    private static int SkipQuotedClassic(string sql, int i, int len, char terminator)
    {
        while (i < len)
        {
            if (sql[i] == terminator)
            {
                if (i + 1 < len && sql[i + 1] == terminator)
                {
                    i += 2;
                    continue;
                }
                return i + 1;
            }
            i++;
        }
        return len;
    }

    private static int ExtractAndAddParameterNameClassic(string sql, int start, int len, List<string> names)
    {
        var i = start;
        while (i < len && IsParameterChar(sql[i]))
            i++;

        if (i > start)
        {
            names.Add(sql.Substring(start, i - start));
        }

        return i;
    }

    private static bool IsParameterChar(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';
}