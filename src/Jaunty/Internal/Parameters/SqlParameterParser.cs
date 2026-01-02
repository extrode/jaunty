namespace Jaunty.Internal.Parameters;

internal static class SqlParameterParser
{
    public static string[] ExtractParameterNames(string sql)
    {
        var names = new List<string>();
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
                i = SkipStringLiteral(sql, i + 1, '\'');
                continue;
            }

            // Skip identifier (double quote or brackets)
            if (c == '"')
            {
                i = SkipStringLiteral(sql, i + 1, '"');
                continue;
            }

            if (c == '[')
            {
                i = SkipStringLiteral(sql, i + 1, ']');
                continue;
            }

            // Found parameter
            if (c == '@')
            {
                var name = ExtractParameterName(sql, i + 1, out var end);

                if (name.Length > 0)
                    names.Add(name);

                i = end;
                continue;
            }

            i++;
        }

        return [.. names];
    }

    private static int SkipToEndOfLine(string sql, int start)
    {
        var i = start;

        while (i < sql.Length && sql[i] != '\n' && sql[i] != '\r')
            i++;

        return i;
    }

    private static int SkipBlockComment(string sql, int start)
    {
        var i = start;

        while (i + 1 < sql.Length)
        {
            if (sql[i] == '*' && sql[i + 1] == '/')
                return i + 2;

            i++;
        }

        return sql.Length;
    }

    private static int SkipStringLiteral(string sql, int start, char terminator)
    {
        var i = start;

        while (i < sql.Length)
        {
            if (sql[i] == terminator)
            {
                // Handle escaped terminator (doubled)
                if (i + 1 < sql.Length && sql[i + 1] == terminator)
                {
                    i += 2;
                    continue;
                }

                return i + 1;
            }

            i++;
        }

        return sql.Length;
    }

    private static string ExtractParameterName(string sql, int start, out int end)
    {
        var i = start;

        while (i < sql.Length && IsParameterChar(sql[i]))
            i++;

        end = i;
        return sql.Substring(start, i - start);
    }

    private static bool IsParameterChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }
}
