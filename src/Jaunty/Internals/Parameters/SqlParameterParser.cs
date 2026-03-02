using System.Text;

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
        var names = new List<string>(CommonConstants.InitialParameterCapacity);
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

            // Found parameter
            if (c == '@')
            {
                i = ExtractAndAddParameterName(sql, i + 1, names);
                continue;
            }

            i++;
        }

        return [.. names];
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
        var names = new List<string>(CommonConstants.InitialParameterCapacity);
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

            if (c == '@')
            {
                i = ExtractAndAddParameterNameClassic(sql, i + 1, len, names);
                continue;
            }

            i++;
        }

        return [.. names];
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
