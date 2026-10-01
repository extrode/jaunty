using System.Text;

using JauntyConfig = Extrode.Jaunty.Configuration.JauntyConfig;

namespace Extrode.Jaunty.Internals.Parameters;

internal static class SqlParameterParser
{
    /// <param name="sql">The SQL text to scan for parameter placeholders.</param>
    /// <param name="backslashEscapes">
    /// Whether a backslash escapes the next character inside a string literal (AUD-R34-014). True
    /// only for MySQL/MariaDB, which accept backslash escapes by default; for every other engine a
    /// literal ending in a backslash is complete, and applying the rule there would swallow its
    /// terminator.
    /// </param>
    /// <param name="dollarSigil">
    /// Whether <c>$</c> can open a parameter or a dollar-quoted string (AUD-R38-057). False for SQL
    /// Server, where <c>$action</c>, <c>$IDENTITY</c>, <c>$ROWGUID</c> and <c>$PARTITION</c> are
    /// pseudo-columns and <c>$5.00</c> is a money literal, and for MySQL/MariaDB, where <c>$</c> is
    /// only ever an identifier character.
    /// </param>
    internal static string[] ExtractParameterNames(string sql, bool backslashEscapes = false, bool dollarSigil = true)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return [];

#if NET8_0_OR_GREATER
        return ExtractParameterNamesSpan(sql.AsSpan(), backslashEscapes, dollarSigil);
#else
        return ExtractParameterNamesClassic(sql, backslashEscapes, dollarSigil);
#endif
    }

#if NET8_0_OR_GREATER
    private static string[] ExtractParameterNamesSpan(ReadOnlySpan<char> sql, bool backslashEscapes, bool dollarSigil)
    {
        // Deferred until the first sigil is found. Opening with a sized List cost every
        // parameterless statement a List plus its backing array - measured at 120 bytes/call by
        // AllocationBudgetTests - for a result that is always empty. Most CRUD SQL Extrode.Jaunty
        // generates has parameters, but every hand-written SELECT without a WHERE clause paid it.
        List<string>? names = null;
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
                i = SkipQuoted(sql, i + 1, '\'', backslashEscapes);
                continue;
            }

            // Skip double-quoted run. What this delimits is dialect-dependent: an identifier
            // under ANSI_QUOTES and everywhere else, but a string literal in MySQL's default
            // mode, which is the only mode that sets backslashEscapes. So the flag flows
            // through here exactly as it does for the single quote above, matching
            // ParameterBinder's own literal scanners, which both test `c is '\'' or '"'`.
            // Bracket and backtick runs below are identifiers in every dialect and never take
            // an escape.
            if (c == '"')
            {
                i = SkipQuoted(sql, i + 1, '"', backslashEscapes);
                continue;
            }

            // Skip bracket-quoted identifier (SQL Server)
            if (c == '[')
            {
                i = SkipQuoted(sql, i + 1, ']', backslashEscapes: false);
                continue;
            }

            // MySQL/MariaDB quote identifiers with backticks, doubling an embedded one. Without
            // this, `it's` starts a phantom string literal that swallows the rest of the statement
            // (losing every later parameter), and `@col` yields a parameter that does not exist.
            if (c == '`')
            {
                i = SkipQuoted(sql, i + 1, '`', backslashEscapes: false);
                continue;
            }

            // Skip SQL Server global/system variable (@@IDENTITY, @@ROWCOUNT, ...)
            if (c == '@' && i + 1 < len && sql[i + 1] == '@')
            {
                i += 2;
                continue;
            }

            // PostgreSQL/DuckDB dollar-quoted string ($$...$$ or $tag$...$tag$): must be
            // recognized before generic parameter extraction below, or its body gets scanned
            // as if it were ordinary SQL text and any identifier-shaped run inside it (e.g.
            // "SELECT" in "$$SELECT 1$$") is mistaken for a parameter name.
            if (c == '$')
            {
                if (!dollarSigil || IsSigilInsideIdentifier(sql, i))
                {
                    i = SkipDollarRun(sql, i);
                    continue;
                }

                int dollarQuoteEnd = TrySkipDollarQuoted(sql, i);
                if (dollarQuoteEnd != -1)
                {
                    i = dollarQuoteEnd;
                    continue;
                }
            }

            // Found parameter (@ for SQL Server/SQLite, $ for DuckDB/PostgreSQL). A sigil preceded
            // by an identifier character is part of that identifier, not a placeholder - see
            // IsSigilInsideIdentifier.
            if (c is '@' or '$' && !IsSigilInsideIdentifier(sql, i))
            {
                i = ExtractAndAddParameterName(sql, i + 1, ref names);
                continue;
            }

            i++;
        }

        return names is null ? [] : [.. names];
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
        int found = sql.Slice(searchFrom).IndexOf(sql.Slice(dollarPos, delimLen));
        return found == -1 ? len : searchFrom + found + delimLen; // Unterminated dollar-quote: skip to end rather than mis-scan the remainder.
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

    private static int SkipQuoted(ReadOnlySpan<char> sql, int i, char terminator, bool backslashEscapes)
    {
        int len = sql.Length;
        while (i < len)
        {
            // AUD-R34-014: MySQL/MariaDB only, and inside string literals only - which means
            // the caller decides, by passing the flag on for ' and " and hard-coding it false
            // for the bracket and backtick identifier runs. See the backslashEscapes parameter
            // on ExtractParameterNames.
            if (backslashEscapes && sql[i] == '\\')
            {
                i += 2;
                continue;
            }

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

    private static int ExtractAndAddParameterName(ReadOnlySpan<char> sql, int start, ref List<string>? names)
    {
        int i = start;
        int len = sql.Length;

        while (i < len && IsParameterChar(sql[i]))
            i++;

        if (i > start)
        {
            names ??= new List<string>(JauntyConfig.ParameterParsingCapacity);
            names.Add(sql.Slice(start, i - start).ToString());
        }

        return i;
    }
#endif

    private static string[] ExtractParameterNamesClassic(string sql, bool backslashEscapes, bool dollarSigil)
    {
        List<string>? names = null;
        var i = 0;
        var len = sql.Length;

        while (i < len)
        {
            var c = sql[i];

            if (c == '-' && i + 1 < len && sql[i + 1] == '-')
            {
                i = SkipToEndOfLineClassic(sql, i + 2, len);
                // Stryker disable once Statement : the walk sits on the newline (or the end) here, and the fall-through i++ only steps over it
                continue;
            }

            if (c == '/' && i + 1 < len && sql[i + 1] == '*')
            {
                i = SkipBlockCommentClassic(sql, i + 2, len);
                continue;
            }

            if (c == '\'')
            {
                i = SkipQuotedClassic(sql, i + 1, len, '\'', backslashEscapes);
                continue;
            }

            if (c == '"')
            {
                i = SkipQuotedClassic(sql, i + 1, len, '"', backslashEscapes);
                continue;
            }

            if (c == '[')
            {
                i = SkipQuotedClassic(sql, i + 1, len, ']', backslashEscapes: false);
                continue;
            }

            // See the span walker: backtick-quoted MySQL identifiers must be skipped too.
            if (c == '`')
            {
                i = SkipQuotedClassic(sql, i + 1, len, '`', backslashEscapes: false);
                continue;
            }

            if (c == '@' && i + 1 < len && sql[i + 1] == '@')
            {
                i += 2;
                continue;
            }

            if (c == '$')
            {
                if (!dollarSigil || IsSigilInsideIdentifier(sql, i))
                {
                    i = SkipDollarRun(sql.AsSpan(), i);
                    continue;
                }

                int dollarQuoteEnd = TrySkipDollarQuotedClassic(sql, i, len);
                if (dollarQuoteEnd != -1)
                {
                    i = dollarQuoteEnd;
                    continue;
                }
            }

            if (c is '@' or '$' && !IsSigilInsideIdentifier(sql, i))
            {
                i = ExtractAndAddParameterNameClassic(sql, i + 1, len, ref names);
                continue;
            }

            i++;
        }

        return names is null ? [] : [.. names];
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
        int found = sql.IndexOf(sql.Substring(dollarPos, delimLen), searchFrom, StringComparison.Ordinal);
        return found == -1 ? len : found + delimLen;
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

    private static int SkipQuotedClassic(string sql, int i, int len, char terminator, bool backslashEscapes)
    {
        while (i < len)
        {
            // AUD-R34-014: see the span twin.
            if (backslashEscapes && sql[i] == '\\')
            {
                i += 2;
                continue;
            }

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

    private static int ExtractAndAddParameterNameClassic(string sql, int start, int len, ref List<string>? names)
    {
        var i = start;
        while (i < len && IsParameterChar(sql[i]))
            i++;

        if (i > start)
        {
            names ??= new List<string>(JauntyConfig.ParameterParsingCapacity);
            names.Add(sql.Substring(start, i - start));
        }

        return i;
    }

    /// <summary>
    /// The characters that may appear in a parameter name - and, identically, the characters that
    /// may appear in the body of an unquoted SQL identifier.
    /// </summary>
    /// <remarks>
    /// Internal rather than private because <see cref="ParameterBinder"/> applies the same two rules
    /// and previously kept its own byte-identical copy. AUD-R26 required all four sigil sites to
    /// agree; sharing the predicate is what makes that structural instead of a convention.
    /// </remarks>
    internal static bool IsParameterChar(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    /// <summary>
    /// Whether a <c>@</c> or <c>$</c> at <paramref name="sigilPos"/> is part of the identifier it
    /// sits in rather than the start of a parameter placeholder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26. Both sigils are legal <em>inside</em> identifiers - <c>$</c> in SQL Server,
    /// PostgreSQL, Oracle and SQLite, <c>@</c> in SQL Server - and the parser had no positional
    /// check at all, so it treated any occurrence as a placeholder. Measured against a real
    /// Microsoft.Data.Sqlite connection: a table named <c>sales$2024</c> made
    /// <c>SELECT * FROM sales$2024 WHERE id = @Id</c> emit a phantom parameter <c>2024</c>, and
    /// <c>BuildTemplate</c> then threw because no property matched it. Ordinary SQL against a legacy
    /// or generated schema simply did not work.
    /// </para>
    /// <para>
    /// A placeholder is always preceded by something that is not an identifier character -
    /// whitespace, an operator, a comma, an opening paren, or the start of the statement. So the
    /// preceding character alone disambiguates the two, with no need to track identifier state.
    /// </para>
    /// </remarks>
    internal static bool IsSigilInsideIdentifier(string sql, int sigilPos)
        => sigilPos > 0 && IsParameterChar(sql[sigilPos - 1]);

    /// <inheritdoc cref="IsSigilInsideIdentifier(string, int)"/>
    internal static bool IsSigilInsideIdentifier(ReadOnlySpan<char> sql, int sigilPos)
        => sigilPos > 0 && IsParameterChar(sql[sigilPos - 1]);

    /// <summary>
    /// Index just past a <c>$</c> that is not a sigil and the identifier characters and further
    /// <c>$</c>s that follow it.
    /// </summary>
    /// <remarks>
    /// AUD-R38-056. Inside an identifier, a second <c>$</c> (<c>sales$q1$2024</c>) used to be taken
    /// for a dollar-quote opening tag that never closed, which swallowed the rest of the statement.
    /// PostgreSQL's own lexer never opens a dollar quote inside an identifier.
    /// </remarks>
    internal static int SkipDollarRun(ReadOnlySpan<char> sql, int dollarPos)
    {
        int i = dollarPos + 1;
        while (i < sql.Length && (IsParameterChar(sql[i]) || sql[i] == '$'))
            i++;
        return i;
    }
}