namespace Jaunty.Helpers;

internal static class StringExtensions
{
    public static bool IsNullOrWhiteSpace(this string? str) => string.IsNullOrWhiteSpace(str);

    public static bool Equals(this string? str, string? other, StringComparison comparison)
    {
        if (str is null && other is null) return true;
        if (str is null || other is null) return false;
        return str.Equals(other, comparison);
    } 
}
