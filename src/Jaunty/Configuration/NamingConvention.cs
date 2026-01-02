using System.Text;

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

        var sb = new StringBuilder(name.Length + 4);

        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (char.IsUpper(c))
            {
                if (i > 0)
                    sb.Append('_');

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
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

        return name.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
            name.Length > 1 && !IsVowel(name[name.Length - 2])
            ? name.Substring(0, name.Length - 1) + "ies"
            : name.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("sh", StringComparison.OrdinalIgnoreCase)
                        ? name + "es"
                        : name + "s";
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
        c = char.ToLowerInvariant(c);
        return c is 'a' or 'e' or 'i' or 'o' or 'u';
    }
}
