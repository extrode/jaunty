using System.Text.RegularExpressions;

namespace Jaunty.Dialects;

/// <summary>
/// Validates that a string is safe to interpolate directly into generated SQL as a table,
/// schema, or column identifier. Rejects anything that could break out of dialect quoting
/// (quotes, brackets, backticks, whitespace, statement separators, etc.).
/// </summary>
internal static class SqlIdentifierValidator
{
    private static readonly Regex ValidIdentifierPattern =
        new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static void Validate(string identifier, string paramName)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !ValidIdentifierPattern.IsMatch(identifier))
            throw new ArgumentException($"'{identifier}' is not a valid SQL identifier.", paramName);
    }
}
