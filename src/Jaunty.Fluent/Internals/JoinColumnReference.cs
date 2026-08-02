using System;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// AUD-R34-022. Guards the two arguments of the column-pair <c>On(leftColumn, rightColumn)</c>
/// join overload.
///
/// <para>
/// That overload collides with <c>On&lt;TValue&gt;(string condition, TValue value)</c> whenever
/// <c>TValue</c> is <c>string</c>: C# prefers the non-generic candidate, so a call written against
/// the documented raw-condition-plus-parameter overload -
/// <c>.On("p.category_id = c.id AND c.name = @value", "Widget")</c> - bound here instead, with no
/// warning. The condition and the value were then concatenated into
/// <c>"{leftColumn} = {rightColumn}"</c>, no parameter was ever added, <c>@value</c> was left
/// unbound, and the value reached the SQL text unescaped - an injection path for any caller whose
/// string comes from input. Only an explicit <c>On&lt;string&gt;(...)</c> selected the intended
/// overload.
/// </para>
///
/// <para>
/// Rejecting anything that is not a plain (optionally table-qualified, optionally quoted) column
/// reference makes the mis-binding loud at the call site instead. It does not change what a
/// correct column-pair call does.
/// </para>
/// </summary>
internal static class JoinColumnReference
{
    // Anything that cannot appear in a bare or quoted column reference but does appear in a
    // condition, a literal or an injected fragment.
    private static readonly char[] Disallowed =
        ['=', '<', '>', '(', ')', ',', ';', '\'', '@', ':', '?', '*', '-', '+', '/', '|'];

    internal static void Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A join column reference must not be empty.", parameterName);

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsWhiteSpace(c) || Array.IndexOf(Disallowed, c) >= 0)
            {
                throw new ArgumentException(
                    $"'{value}' is not a column reference. On(leftColumn, rightColumn) joins two " +
                    "columns and concatenates them into '<left> = <right>' - it does not accept a " +
                    "condition or a value. For a raw condition with a parameter, call " +
                    "On<string>(condition, value) or On(condition, parameterName, value); for a " +
                    "raw condition alone, call On(condition).",
                    parameterName);
            }
        }
    }
}
