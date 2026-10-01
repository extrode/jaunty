namespace Extrode.Jaunty.Internals.Parameters;

internal static class SqlParameterParserCache
{
    // AUD-R34-014, AUD-R38-057 and round 38's bracket follow-up: the same SQL text parses
    // differently per engine - MySQL/MariaDB escape with a backslash, SQL Server and MySQL never
    // take '$' as a sigil, and only SQL Server and SQLite read '[' as a quoted identifier. One
    // cache per flag combination rather than a composite key: the flags are fixed per engine, so a
    // process talking to a single engine still fills exactly one of them. Each slot is
    // size-capped so callers that embed literals or build SQL dynamically cannot leak memory
    // through an ever-growing set of distinct SQL-text keys, and holds its parse delegate so the
    // hit path allocates nothing.
    private static readonly Slot?[] Slots = new Slot?[8];

    public static string[] GetOrAdd(string sql, bool backslashEscapes = false, bool dollarSigil = true, bool bracketIdentifiers = true)
    {
        // MySQL/MariaDB is the only backslash-escaping engine and is also dollar-free.
        if (backslashEscapes)
            dollarSigil = false;

        int index = (backslashEscapes ? 4 : 0) | (dollarSigil ? 2 : 0) | (bracketIdentifiers ? 1 : 0);
        Slot slot = Volatile.Read(ref Slots[index])
            ?? Interlocked.CompareExchange(ref Slots[index], new Slot(backslashEscapes, dollarSigil, bracketIdentifiers), null)
            ?? Slots[index]!;

        return slot.Cache.GetOrAdd(sql, slot.Parse);
    }

    private sealed class Slot(bool backslashEscapes, bool dollarSigil, bool bracketIdentifiers)
    {
        public readonly BoundedCache<string, string[]> Cache = new(StringComparer.Ordinal);

        public readonly Func<string, string[]> Parse =
            s => SqlParameterParser.ExtractParameterNames(s, backslashEscapes, dollarSigil, bracketIdentifiers);
    }
}
