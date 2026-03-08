using System.Text;

namespace Jaunty.Scaffolding.CodeGeneration;

/// <summary>
/// Helper methods for naming convention transformations.
/// </summary>
public static class NamingHelper
{
    /// <summary>
    /// Converts snake_case or kebab-case to PascalCase.
    /// If the name has no delimiters, capitalizes the first letter and preserves the rest.
    /// </summary>
    /// <param name="name">The name to convert.</param>
    /// <returns>The name in PascalCase.</returns>
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Check if name contains any delimiters
        bool hasDelimiters = name.Contains('_') || name.Contains('-') || name.Contains(' ');

        if (!hasDelimiters)
        {
            // No delimiters - just capitalize first letter and preserve the rest
            return char.ToUpperInvariant(name[0]) + name[1..];
        }

        // Has delimiters - convert to PascalCase
        var sb = new StringBuilder(name.Length);
        bool capitalizeNext = true;

        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];

            if (c is '_' or '-' or ' ')
            {
                capitalizeNext = true;
                continue;
            }

            if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts snake_case or kebab-case to camelCase.
    /// </summary>
    /// <param name="name">The name to convert.</param>
    /// <returns>The name in camelCase.</returns>
    public static string ToCamelCase(string name)
    {
        var pascal = ToPascalCase(name);
        if (pascal.Length == 0)
            return pascal;

        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    /// <summary>
    /// Attempts to singularize a plural word using common English rules.
    /// </summary>
    /// <param name="word">The word to singularize.</param>
    /// <returns>The singular form of the word.</returns>
    public static string Singularize(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        // Common irregular plurals
        var irregulars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["People"] = "Person",
            ["Children"] = "Child",
            ["Men"] = "Man",
            ["Women"] = "Woman",
            ["Teeth"] = "Tooth",
            ["Feet"] = "Foot",
            ["Mice"] = "Mouse",
            ["Geese"] = "Goose",
            ["Analyses"] = "Analysis",
            ["Diagnoses"] = "Diagnosis",
            ["Theses"] = "Thesis",
            ["Crises"] = "Crisis",
            ["Phenomena"] = "Phenomenon",
            ["Criteria"] = "Criterion",
            ["Data"] = "Datum",
            ["Media"] = "Medium",
            ["Indices"] = "Index",
            ["Appendices"] = "Appendix",
            ["Matrices"] = "Matrix",
            ["Vertices"] = "Vertex",
        };

        if (irregulars.TryGetValue(word, out var singular))
            return singular;

        // Rules in order of specificity
        // -ies -> -y (e.g., categories -> category)
        if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && word.Length > 4)
            return word[..^3] + "y";

        // -sses, -shes, -ches, -xes, -zes -> remove -es
        if (word.Length > 3)
        {
            if (word.EndsWith("sses", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("shes", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("xes", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("zes", StringComparison.OrdinalIgnoreCase))
                return word[..^2];
        }

        // -ses (but not -sses) -> remove -es (e.g., Buses -> Bus, Gases -> Gas)
        if (word.Length > 3 &&
            word.EndsWith("ses", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("sses", StringComparison.OrdinalIgnoreCase))
            return word[..^2];

        // -ves -> -f or -fe (e.g., leaves -> leaf, knives -> knife)
        if (word.EndsWith("ves", StringComparison.OrdinalIgnoreCase) && word.Length > 4)
        {
            var withF = word[..^3] + "f";
            var withFe = word[..^3] + "fe";
            // Common -fe words
            if (word.EndsWith("ives", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("aves", StringComparison.OrdinalIgnoreCase))
                return withFe;
            return withF;
        }

        // -s -> remove s (but not for words ending in -ss, -us, -is)
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("us", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("is", StringComparison.OrdinalIgnoreCase) &&
            word.Length > 1)
            return word[..^1];

        return word;
    }

    /// <summary>
    /// Determines if a string is a valid C# identifier.
    /// </summary>
    /// <param name="name">The name to check.</param>
    /// <returns>True if the name is a valid C# identifier.</returns>
    public static bool IsValidCSharpIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        for (int i = 1; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                return false;
        }

        return !IsCSharpKeyword(name);
    }

    /// <summary>
    /// Makes a string a valid C# identifier by escaping it if necessary.
    /// </summary>
    /// <param name="name">The name to escape.</param>
    /// <returns>A valid C# identifier.</returns>
    public static string EscapeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "_";

        // If it starts with a digit, prefix with underscore
        if (char.IsDigit(name[0]))
            name = "_" + name;

        // Replace invalid characters with underscores
        var sb = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }

        var result = sb.ToString();

        // If it's a keyword, prefix with @
        if (IsCSharpKeyword(result))
            return "@" + result;

        return result;
    }

    private static bool IsCSharpKeyword(string name)
    {
        return name switch
        {
            "abstract" or "as" or "base" or "bool" or "break" or "byte" or "case" or "catch" or
            "char" or "checked" or "class" or "const" or "continue" or "decimal" or "default" or
            "delegate" or "do" or "double" or "else" or "enum" or "event" or "explicit" or
            "extern" or "false" or "finally" or "fixed" or "float" or "for" or "foreach" or
            "goto" or "if" or "implicit" or "in" or "int" or "interface" or "internal" or "is" or
            "lock" or "long" or "namespace" or "new" or "null" or "object" or "operator" or
            "out" or "override" or "params" or "private" or "protected" or "public" or "readonly" or
            "ref" or "return" or "sbyte" or "sealed" or "short" or "sizeof" or "stackalloc" or
            "static" or "string" or "struct" or "switch" or "this" or "throw" or "true" or "try" or
            "typeof" or "uint" or "ulong" or "unchecked" or "unsafe" or "ushort" or "using" or
            "virtual" or "void" or "volatile" or "while" => true,
            _ => false
        };
    }
}