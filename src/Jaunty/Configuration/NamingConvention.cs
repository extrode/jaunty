namespace Jaunty.Configuration;

/// <summary>
/// Built-in naming conventions for table and column resolution.
/// </summary>
public static class NamingConvention
{
    /// <summary>
    /// Converts PascalCase to snake_case.
    /// Example: ProductName -> product_name
    /// </summary>
    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Count uppercase letters to pre-size correctly
        var upperCount = 0;
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]))
                upperCount++;
        }

        // Fast path: no uppercase after first char
        if (upperCount == 0)
            return name.ToLowerInvariant();

        // Build result with exact size
        var result = new char[name.Length + upperCount];
        var j = 0;

        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (char.IsUpper(c))
            {
                if (i > 0)
                    result[j++] = '_';

                result[j++] = char.ToLowerInvariant(c);
            }
            else
            {
                result[j++] = c;
            }
        }

        return new string(result, 0, j);
    }

    /// <summary>
    /// Converts PascalCase to lowercase.
    /// Example: ProductName -> productname
    /// </summary>
    public static string ToLowerCase(string name)
    {
        return name?.ToLowerInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Pluralizes table name (simple English rules).
    /// Example: Product -> Products
    /// </summary>
    public static string Pluralize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var len = name.Length;
        var last = name[len - 1];

        // Words ending in consonant + y: Category -> Categories
        if ((last == 'y' || last == 'Y') && len > 1 && !IsVowel(name[len - 2]))
            return name.Substring(0, len - 1) + "ies";

        // Words ending in s, x, ch, sh: Box -> Boxes
        if (last == 's' || last == 'S' ||
            last == 'x' || last == 'X' ||
            (len >= 2 && (name[len - 2] == 'c' || name[len - 2] == 's') && (last == 'h' || last == 'H')))
            return name + "es";

        // Default: Product -> Products
        return name + "s";
    }

    /// <summary>
    /// Combines snake_case and pluralization for table names.
    /// Example: OrderDetail -> order_details
    /// </summary>
    public static Func<Type, string> SnakeCasePluralTable => type => ToSnakeCase(Pluralize(type.Name));

    /// <summary>
    /// Snake_case column resolver.
    /// Example: ProductName -> product_name
    /// </summary>
    public static Func<string, string> SnakeCaseColumn => ToSnakeCase;

    private static bool IsVowel(char c)
    {
        return (c | 0x20) is 'a' or 'e' or 'i' or 'o' or 'u';
    }
}
