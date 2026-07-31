using System;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Converts the provider's 64-bit COUNT result to the <c>int</c> the <c>Count</c>/<c>CountAsync</c>
/// overloads return. R28: the unchecked <c>(int)</c> cast used to truncate silently past
/// <see cref="int.MaxValue"/>; a count that large must surface as an error steering the caller
/// to <c>LongCount</c>/<c>LongCountAsync</c>.
/// </summary>
internal static class CountConversion
{
    internal static int ToInt32(long result)
    {
        if (result > int.MaxValue)
            throw new OverflowException(
                $"COUNT returned {result}, which exceeds Int32.MaxValue. Use LongCount()/LongCountAsync() instead.");
        return (int)result;
    }
}
