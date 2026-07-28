using System;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Normalizes the parameter name used by the raw-condition <c>On(condition, ..., value)</c>
/// join overloads.
/// </summary>
internal static class JoinParameterName
{
    /// <summary>
    /// The name the two-argument <c>On(condition, value)</c> overload has always used. Kept as
    /// the default so existing callers writing "@value" in their condition keep working.
    /// </summary>
    internal const string Default = "value";

    /// <summary>
    /// Qualifies <paramref name="name"/> with the dialect's parameter prefix. The name is
    /// accepted either bare ("orderId") or already prefixed ("@orderId"/"$orderId"/":orderId"),
    /// so a caller can pass whichever form matches the placeholder they wrote in the condition
    /// without having to know the dialect's prefix.
    /// </summary>
    /// <param name="prefix">The dialect's parameter prefix (e.g. "@", "$", ":").</param>
    /// <param name="name">The caller-supplied parameter name.</param>
    /// <param name="argumentName">The parameter name to report on a validation failure.</param>
    /// <returns>The prefix-qualified parameter name.</returns>
    internal static string Qualify(string prefix, string name, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Join condition parameter name must not be null or whitespace.", argumentName);

        string bare = name[0] is '@' or '$' or ':' ? name.Substring(1) : name;

        if (bare.Length == 0)
            throw new ArgumentException($"Join condition parameter name '{name}' is only a parameter prefix.", argumentName);

        return prefix + bare;
    }

    /// <summary>
    /// Builds the error thrown when a join chain reuses a parameter name. Raised ahead of
    /// <see cref="ParameterCollection.Add"/>'s own duplicate check so the message can name the
    /// remedy - the three-argument overload - rather than only stating the collision.
    /// </summary>
    internal static ArgumentException DuplicateError(string qualifiedName, string argumentName) =>
        new(
            $"A join condition parameter named '{qualifiedName}' has already been added to this query. " +
            "Each raw-condition On(...) call in a join chain needs its own parameter name - use the " +
            "On(condition, parameterName, value) overload to name them distinctly.",
            argumentName);
}
