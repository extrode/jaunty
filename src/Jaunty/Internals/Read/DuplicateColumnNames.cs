namespace Jaunty.Internals.Read;

/// <summary>
/// Makes a result set's column names unique before they are used as dictionary keys.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26. The untyped-row paths built each row with the indexer - <c>row[columnNames[i]] = value</c>
/// - so a result set carrying two columns of the same name silently kept only the last. No
/// exception, no diagnostic, and nothing in the XML docs mentioning it.
/// <c>SELECT o.Id, c.Id FROM Orders o JOIN Customers c ...</c> is the ordinary join shape, and the
/// caller received a dictionary one key short with no way to notice. Measured against
/// Microsoft.Data.Sqlite: <c>keys=[Id] values=[1]</c> - the order row's <c>Id</c> of 10 was gone.
/// </para>
/// <para>
/// AUD-R25 made this strictly worse. It changed the comparer from ordinal to
/// <see cref="StringComparer.OrdinalIgnoreCase"/> so the two untyped-row APIs would agree on lookup,
/// which was right on its own terms - but under the old comparer <c>Id</c> and <c>id</c> were two
/// distinct keys and both values survived, and under the new one they collide. So
/// <c>SELECT o.Id AS Id, c.Id AS id</c> - what an unaliased join across differently-cased schemas
/// produces - lost a value it used to keep. The round-25 finding recorded the duplicate-name problem
/// as a parenthetical and the fix did not address it, so the comparer change widened the failure
/// without the widening being noticed. Both cases are covered here.
/// </para>
/// <para>
/// <strong>Why renaming rather than throwing.</strong> These are the untyped "give me whatever the
/// query returned" APIs; their whole point is that the caller does not have to declare a shape.
/// Throwing on a result set the database returned happily would turn working queries - including
/// generated SQL the caller does not control - into crashes, to fix a problem they may not have.
/// Renaming loses nothing: every value is present, and it is discoverable by enumerating the keys.
/// </para>
/// <para>
/// <strong>Why the first occurrence keeps the bare name.</strong> <c>row["Id"]</c> has to resolve to
/// something, and the first column is the better answer than today's last: it matches Dapper, whose
/// <c>DapperTable</c> name lookup resolves duplicates to the lowest ordinal, and it matches how the
/// SQL reads - in <c>SELECT o.Id, c.Id FROM Orders o JOIN ...</c> the driving table comes first.
/// Note that this does change which value an existing caller gets for the bare name; there is no
/// option that does not, because today's answer is the one being called a bug.
/// </para>
/// </remarks>
internal static class DuplicateColumnNames
{
    /// <summary>
    /// Returns column names with duplicates disambiguated: the first occurrence of a name is left
    /// alone and each later one gains an <c>_N</c> suffix, where <c>N</c> is its occurrence number.
    /// </summary>
    /// <param name="columnNames">
    /// The names as read from the data reader. Not modified - a duplicate-free set is returned as-is.
    /// </param>
    /// <returns>
    /// <paramref name="columnNames"/> itself when every name is already unique under
    /// <see cref="StringComparer.OrdinalIgnoreCase"/>, so the overwhelmingly common case allocates
    /// nothing; otherwise a new array.
    /// </returns>
    /// <remarks>
    /// A generated suffix can itself collide with a real column - <c>SELECT Id, Id_2, Id</c> - so the
    /// occurrence number keeps rising until the candidate is free. Comparisons use
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> throughout, matching the comparer the row
    /// dictionaries are built with; disambiguating under a different comparer than the dictionary
    /// uses would leave collisions the dictionary still sees.
    /// </remarks>
    public static string[] Disambiguate(string[] columnNames)
    {
        // One pass to answer "is there anything to do at all", and it doubles as the reserved set.
        // Nearly every result set has unique column names, and that case must not pay for this.
        HashSet<string>? reserved = FindDuplicates(columnNames);

        if (reserved is null)
            return columnNames;

        var result = new string[columnNames.Length];
        HashSet<string> used = NewSet(columnNames.Length);

        for (int i = 0; i < columnNames.Length; i++)
        {
            string name = columnNames[i];

            if (used.Add(name))
            {
                result[i] = name;
                continue;
            }

            // Second occurrence becomes _2, third _3, and so on - the occurrence number, not the
            // ordinal, so the suffix means something to a reader of the output.
            //
            // A candidate is rejected both when it is already used and when it is the real name of
            // *any* column in this set, including one that has not been reached yet. Checking only
            // the former would let a generated name steal a real column's identity: on
            // SELECT Id, Id, Id_2 the second Id would take "Id_2" and the genuine Id_2 column would
            // be pushed out to "Id_2_2" - so row["Id_2"] would silently return the wrong value,
            // which is the same class of bug this whole change removes.
            int occurrence = 2;
            string candidate = Suffix(name, occurrence);

            while (reserved.Contains(candidate) || !used.Add(candidate))
                candidate = Suffix(name, ++occurrence);

            result[i] = candidate;
        }

        return result;
    }

    private static string Suffix(string name, int occurrence)
        => name + "_" + occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns every name in <paramref name="columnNames"/> when at least two of them collide, or
    /// <see langword="null"/> when they are all distinct.
    /// </summary>
    /// <remarks>
    /// One method rather than a separate "any duplicates?" predicate and a second set-building pass:
    /// the caller needs the full set only in the duplicate case, and the set is what detects the
    /// duplicate, so building it once answers both questions.
    /// </remarks>
    private static HashSet<string>? FindDuplicates(string[] columnNames)
    {
        if (columnNames.Length < 2)
            return null;

        HashSet<string> seen = NewSet(columnNames.Length);
        bool duplicate = false;

        for (int i = 0; i < columnNames.Length; i++)
        {
            if (!seen.Add(columnNames[i]))
                duplicate = true;
        }

        return duplicate ? seen : null;
    }

    /// <remarks>
    /// netstandard2.0's <see cref="HashSet{T}"/> has no (capacity, comparer) constructor - it arrived
    /// in .NET Core 2.0 - so the capacity hint is available on one TFM only. The behaviour is
    /// identical either way; only the initial allocation differs.
    /// </remarks>
    private static HashSet<string> NewSet(int capacity)
#if NET8_0_OR_GREATER
        => new(capacity, StringComparer.OrdinalIgnoreCase);
#else
        => new HashSet<string>(StringComparer.OrdinalIgnoreCase);
#endif
}
