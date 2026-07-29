using System.Text;

namespace Jaunty.Scaffolding.CodeGeneration;

/// <summary>
/// Helper methods for naming convention transformations.
/// </summary>
public static class NamingHelper
{
    // Common irregular plurals. Static so Singularize doesn't allocate/populate a
    // fresh dictionary on every call.
    private static readonly Dictionary<string, string> IrregularPlurals = new(StringComparer.OrdinalIgnoreCase)
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

    // The f/fe-alternating plurals are a closed class in English (knife/knives, leaf/leaves,
    // wolf/wolves, ...). R24: the previous -ves rule applied the alternation to *every* word
    // ending in -ves, mangling the far larger class of ordinary -ve nouns that pluralize
    // regularly - Waves -> "Waf", Drives -> "Drife", Archives -> "Archife". Enumerating the
    // closed class instead lets everything else fall through to the generic -s rule.
    // Ordered longest-suffix-first so "Shelves" matches "shelves" before "elves".
    private static readonly (string Plural, string Singular)[] FAlternatingPlurals =
    [
        ("sheaves", "sheaf"),
        ("wharves", "wharf"),
        ("scarves", "scarf"),
        ("dwarves", "dwarf"),
        ("shelves", "shelf"),
        ("thieves", "thief"),
        ("knives", "knife"),
        ("leaves", "leaf"),
        ("loaves", "loaf"),
        ("calves", "calf"),
        ("halves", "half"),
        ("selves", "self"),
        ("wolves", "wolf"),
        ("hooves", "hoof"),
        ("turves", "turf"),
        ("wives", "wife"),
        ("lives", "life"),
        ("elves", "elf"),
    ];

    // AUD-R26: singulars that already end in a sibilant, so their plural adds -es.
    //
    // The old "-ses (but not -sses) -> strip -es" rule could not tell these from the far larger
    // class of ordinary -se nouns, because both produce the same ending: Bus+es and Database+s
    // are both "...ses". It stripped two characters from every one of them, so a great many
    // ordinary table names scaffolded to nonsense class names - measured: Databases -> "Databas",
    // Cases -> "Cas", Purchases -> "Purchas", Licenses -> "Licens", Warehouses -> "Warehous",
    // Expenses -> "Expens", Responses -> "Respons", Courses -> "Cours", Houses -> "Hous",
    // Releases -> "Releas", Phases -> "Phas", Leases -> "Leas", Clauses -> "Claus",
    // Causes -> "Caus". Fourteen of the most ordinary table names imaginable.
    //
    // Narrowing the rule to "-uses" does not work either - Houses, Warehouses, Clauses and
    // Causes all end in -uses too. There is no shape that separates them; it is a lexical fact
    // about each word. So the closed class is enumerated and everything else falls through to
    // the generic -s rule, exactly as R24 did for the f/fe-alternating -ves plurals.
    // Ordered longest-suffix-first.
    private static readonly (string Plural, string Singular)[] SibilantSingulars =
    [
        ("surpluses", "surplus"),
        ("campuses", "campus"),
        ("censuses", "census"),
        ("canvases", "canvas"),
        ("statuses", "status"),
        ("corpuses", "corpus"),
        ("quizzes", "quiz"),
        ("aliases", "alias"),
        ("atlases", "atlas"),
        ("viruses", "virus"),
        ("bonuses", "bonus"),
        ("biases", "bias"),
        ("irises", "iris"),
        ("lenses", "lens"),
        ("gases", "gas"),
        ("buses", "bus"),
    ];

    // AUD-R26: the same problem one class over. The "-ches -> strip -es" rule is right for the
    // large -ch class (Branches, Matches, Batches, Searches, Watches, Patches) and wrong for the
    // small -che class, which it truncated: measured Caches -> "Cach", Niches -> "Nich". Here the
    // regular class is the bigger one, so the exceptions are enumerated rather than the rule
    // being inverted.
    private static readonly (string Plural, string Singular)[] CheSingulars =
    [
        ("avalanches", "avalanche"),
        ("moustaches", "moustache"),
        ("mustaches", "mustache"),
        ("quiches", "quiche"),
        ("niches", "niche"),
        ("caches", "cache"),
        ("aches", "ache"),
    ];

    /// <summary>
    /// Derives a generated entity class name from a table name, applying PascalCase
    /// conversion, optional singularization, prefix/suffix, and identifier escaping.
    /// </summary>
    /// <param name="tableName">The source table name.</param>
    /// <param name="singularize">Whether to singularize the PascalCase name.</param>
    /// <param name="classPrefix">An optional prefix to prepend to the class name.</param>
    /// <param name="classSuffix">An optional suffix to append to the class name.</param>
    /// <returns>The generated class name.</returns>
    public static string ToClassName(string tableName, bool singularize, string? classPrefix, string? classSuffix)
    {
        var className = ToPascalCase(tableName);

        if (singularize)
            className = Singularize(className);

        if (!string.IsNullOrEmpty(classPrefix))
            className = classPrefix + className;

        if (!string.IsNullOrEmpty(classSuffix))
            className += classSuffix;

        return EscapeIdentifier(className);
    }

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

        if (IrregularPlurals.TryGetValue(word, out var singular))
            return singular;

        // Rules in order of specificity
        // -ies -> -y (e.g., categories -> category)
        if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && word.Length > 4)
            return word[..^3] + "y";

        // The closed classes first - a word in either of these would be truncated by the
        // sibilant rules below (Statuses -> "Statuse" without the first, Caches -> "Cach"
        // without the second).
        var sibilant = TrySingularizeBySuffix(word, SibilantSingulars);
        if (sibilant != null)
            return sibilant;

        var che = TrySingularizeBySuffix(word, CheSingulars);
        if (che != null)
            return che;

        // -sses, -shes, -ches, -xes -> remove -es. These four are safe as shape rules: -sse,
        // -she and -xe singulars are vanishingly rare, and the -che exceptions are enumerated
        // above.
        if (word.Length > 3)
        {
            if (word.EndsWith("sses", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("shes", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
                word.EndsWith("xes", StringComparison.OrdinalIgnoreCase))
                return word[..^2];
        }

        // AUD-R26: -zzes, not -zes. A bare -zes rule truncated the ordinary -ze nouns, which are
        // effectively the whole class - measured Sizes -> "Siz", Prizes -> "Priz",
        // Bronzes -> "Bronz". English singulars that genuinely end in a bare -z are so rare that
        // the ones which exist (quiz) double the z when pluralized, so requiring -zzes both
        // fixes the -ze nouns and still handles Buzzes -> Buzz.
        if (word.Length > 4 && word.EndsWith("zzes", StringComparison.OrdinalIgnoreCase))
            return word[..^2];

        // Note there is deliberately no general "-ses -> strip -es" rule: see the comment on
        // SibilantSingulars. Databases, Cases, Houses and their kind reach the generic -s rule
        // at the end of this method, which is the correct answer for all of them.

        // -ves -> -f/-fe, but only for the closed class of words that genuinely alternate
        // (leaves -> leaf, knives -> knife). Ordinary -ve nouns (Waves, Drives, Archives)
        // deliberately fall through to the generic -s rule below.
        var fAlternating = TrySingularizeBySuffix(word, FAlternatingPlurals);
        if (fAlternating != null)
            return fAlternating;

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
    /// Matches one of a closed class of irregular plurals, either as the whole word or as the
    /// trailing PascalCase segment of a compound (BookShelves -> BookShelf, OrderStatuses ->
    /// OrderStatus). Requiring a capital at the segment boundary stops ordinary words that
    /// merely *contain* one of these endings from matching - "Olives" must stay "Olive", not
    /// become "Olife".
    /// </summary>
    /// <param name="word">The candidate plural word.</param>
    /// <param name="table">The closed class to match against, longest suffix first.</param>
    /// <returns>The singular form, or null if the word is not in the class.</returns>
    private static string? TrySingularizeBySuffix(string word, (string Plural, string Singular)[] table)
    {
        foreach ((string plural, string singular) in table)
        {
            if (word.Length < plural.Length ||
                !word.EndsWith(plural, StringComparison.OrdinalIgnoreCase))
                continue;

            var start = word.Length - plural.Length;
            if (start > 0 && !char.IsUpper(word[start]))
                continue;

            var replacement = char.IsUpper(word[start])
                ? char.ToUpperInvariant(singular[0]) + singular[1..]
                : singular;

            return word[..start] + replacement;
        }

        return null;
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
    /// Determines whether a string is a well-formed C# namespace - a dot-separated sequence of
    /// valid identifiers, each optionally verbatim-escaped with <c>@</c>.
    /// </summary>
    /// <remarks>
    /// AUD-R26: the namespace is interpolated straight into <c>namespace {0};</c> and was never
    /// checked. Malformed values produced a file the user could not compile, with only a Roslyn
    /// error to work backwards from - measured: "My Namespace" gives CS1514, "123Bad" gives
    /// CS1001, "Foo-Bar" gives CS0116. Worse, a value containing a semicolon injects arbitrary
    /// top-level C# into **every** generated file and still parses cleanly: the namespace
    /// <c>Foo.Bar;public class Pwn{}//</c> emitted
    /// <c>namespace Foo.Bar;public class Pwn{}//;</c> with zero parse errors. That matters
    /// because <c>ScaffoldOptions</c> is public library API, so a host application may pass a
    /// namespace it derived from configuration or from a request, not only from a developer
    /// typing <c>--namespace</c>.
    /// </remarks>
    /// <param name="ns">The candidate namespace.</param>
    /// <returns>True if the string can be emitted as a namespace declaration.</returns>
    public static bool IsValidNamespace(string ns)
    {
        if (string.IsNullOrWhiteSpace(ns))
            return false;

        foreach (var part in ns.Split('.'))
        {
            // A verbatim identifier is legal here, and is the only way to name a segment that
            // collides with a keyword.
            var isVerbatim = part.Length > 0 && part[0] == '@';
            var identifier = isVerbatim ? part[1..] : part;

            if (identifier.Length == 0)
                return false;

            if (!char.IsLetter(identifier[0]) && identifier[0] != '_')
                return false;

            for (int i = 1; i < identifier.Length; i++)
            {
                if (!char.IsLetterOrDigit(identifier[i]) && identifier[i] != '_')
                    return false;
            }

            // Unescaped keywords are rejected; the @-escaped form above is accepted.
            if (!isVerbatim && IsCSharpKeyword(identifier))
                return false;
        }

        return true;
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