using System.Text.RegularExpressions;

namespace Jaunty.Dialects;

/// <summary>
/// The set of characters a dialect accepts in an identifier it will emit <em>unquoted</em>.
/// </summary>
/// <remarks>
/// AUD-R35-007. The dialects quote only keywords, so every identifier reaching
/// <see cref="SqlIdentifierValidator"/> may be emitted verbatim. The whitelist is therefore the
/// engine's own unquoted-identifier grammar and nothing wider: anything outside it is rejected
/// rather than quoted, because the caller supplying it is supplying a string that is about to be
/// interpolated into SQL.
/// </remarks>
internal enum SqlIdentifierFlavor
{
    /// <summary>The intersection every engine accepts. The default, and what SQLite uses.</summary>
    Common,

    /// <summary>T-SQL: <c>#</c> and <c>$</c> in addition, <c>#</c> leading for temp tables.</summary>
    SqlServer,

    /// <summary>MySQL/MariaDB: <c>$</c> in addition, and a leading digit is legal.</summary>
    MySql,

    /// <summary>PostgreSQL: <c>$</c> in addition, but never leading.</summary>
    PostgreSql,
}

/// <summary>
/// Validates that a string is safe to interpolate directly into generated SQL as a table,
/// schema, or column identifier. Rejects anything that could break out of dialect quoting
/// (quotes, brackets, backticks, whitespace, statement separators, etc.).
/// </summary>
/// <remarks>
/// <para>
/// AUD-R35-007 widened the pattern from <c>^[A-Za-z_][A-Za-z0-9_]*$</c>, which rejected identifiers
/// every supported engine accepts: any non-ASCII letter at all (so a column named <c>preço</c> or
/// <c>名前</c> could not be queried), <c>#</c>-prefixed SQL Server temp tables, and the <c>$</c>
/// that appears in generated PostgreSQL and MySQL names. The widening is a whitelist per flavour,
/// not a blacklist - the rejected set still contains every character that could terminate a
/// quoted identifier or a statement.
/// </para>
/// <para>
/// <c>@</c> is deliberately <em>not</em> accepted for <see cref="SqlIdentifierFlavor.SqlServer"/>
/// even though T-SQL allows it in an unquoted identifier, because an identifier beginning with
/// <c>@</c> is indistinguishable from a parameter reference in the emitted SQL.
/// </para>
/// </remarks>
internal static class SqlIdentifierValidator
{
    // \p{L} letters, \p{Nd} decimal digits, \p{Mn}/\p{Mc} combining marks - a decomposed accent is
    // part of its base letter, not a separate character to reject.
    private const string Letter = @"\p{L}\p{Mn}\p{Mc}";
    private const string LetterOrDigit = Letter + @"\p{Nd}";

    private static readonly Regex CommonPattern =
        new($"^[{Letter}_][{LetterOrDigit}_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SqlServerPattern =
        new($"^[{Letter}_#][{LetterOrDigit}_#$]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // MySQL permits a leading digit, but not an all-digit identifier - that would be a number.
    private static readonly Regex MySqlPattern =
        new($"^(?![\\p{{Nd}}]+$)[{LetterOrDigit}_$]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PostgreSqlPattern =
        new($"^[{Letter}_][{LetterOrDigit}_$]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void Validate(string identifier, string paramName)
        => Validate(identifier, paramName, SqlIdentifierFlavor.Common);

    public static void Validate(string identifier, string paramName, SqlIdentifierFlavor flavor)
    {
        if (!IsValid(identifier, flavor))
            throw new ArgumentException($"'{identifier}' is not a valid SQL identifier.", paramName);
    }

    public static bool IsValid(string? identifier, SqlIdentifierFlavor flavor)
        => !string.IsNullOrWhiteSpace(identifier) && PatternFor(flavor).IsMatch(identifier!);

    private static Regex PatternFor(SqlIdentifierFlavor flavor) => flavor switch
    {
        SqlIdentifierFlavor.SqlServer => SqlServerPattern,
        SqlIdentifierFlavor.MySql => MySqlPattern,
        SqlIdentifierFlavor.PostgreSql => PostgreSqlPattern,
        _ => CommonPattern,
    };
}
