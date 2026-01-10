namespace Jaunty.Internals.Parameters;

internal static class SqlParameterParser
{
    internal static string[] ExtractParameterNames(string sql)
    {
        // Pre-size list - most queries have 0-5 parameters
        var names = new List<string>(4);
        var i = 0;
        var len = sql.Length;

        while (i < len)
        {
            var c = sql[i];

            // Skip single-line comment
            if (c == '-' && i + 1 < len && sql[i + 1] == '-')
            {
                i = SkipToEndOfLine(sql, i + 2, len);
                continue;
            }

            // Skip block comment
            if (c == '/' && i + 1 < len && sql[i + 1] == '*')
            {
                i = SkipBlockComment(sql, i + 2, len);
                continue;
            }

            // Skip string literal (single quote)
            if (c == '\'')
            {
                i = SkipQuoted(sql, i + 1, len, '\'');
                continue;
            }

            // Skip identifier (double quote or brackets)
            if (c == '"')
            {
                i = SkipQuoted(sql, i + 1, len, '"');
                continue;
            }

            if (c == '[')
            {
                i = SkipQuoted(sql, i + 1, len, ']');
                continue;
            }

            // Found parameter
            if (c == '@')
            {
                i = ExtractParameterName(sql, i + 1, len, names);
                continue;
            }

            i++;
        }

        return [.. names];
    }

    private static int SkipToEndOfLine(string sql, int i, int len)
    {
        while (i < len)
        {
            var c = sql[i];
            if (c is '\n' or '\r') break;
            i++;
        }
        return i;
    }

    private static int SkipBlockComment(string sql, int i, int len)
    {
        while (i + 1 < len)
        {
            if (sql[i] == '*' && sql[i + 1] == '/')
                return i + 2;
            i++;
        }
        return len;
    }

    private static int SkipQuoted(string sql, int i, int len, char terminator)
    {
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

    private static int ExtractParameterName(string sql, int start, int len, List<string> names)
    {
        var i = start;

        while (i < len && IsParameterChar(sql[i]))
            i++;

        if (i > start)
            names.Add(sql.Substring(start, i - start));

        return i;
    }

    private static bool IsParameterChar(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';
}
