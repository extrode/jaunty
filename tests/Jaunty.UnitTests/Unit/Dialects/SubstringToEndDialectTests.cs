using System.Reflection;

using Jaunty.Dialects;
using Jaunty.Extensions.Reflection.Dialects;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// AUD-R26. <see cref="ISubstringToEndDialect"/> is an <em>optional</em> companion to
/// <see cref="ISqlDialect"/>, chosen over a new interface member because <c>ISqlDialect</c> is
/// public and this assembly targets netstandard2.0, where a default interface implementation is not
/// available to keep existing external implementers compiling.
///
/// <para>
/// That choice has a cost the compiler cannot cover. A new <c>ISqlDialect</c> member is a build
/// error in every implementation - which is exactly why round 26 recorded the four hand-written
/// <c>*DialectWithBulkCopy</c> wrappers as safe despite ~40 forwards apiece. An <em>optional</em>
/// interface has no such guarantee: a dialect that quietly does not implement it still compiles,
/// still works, and silently falls back to a sentinel length - which is the defect this interface
/// exists to remove. These tests are what replaces the compiler here.
/// </para>
/// </summary>
public class SubstringToEndDialectTests
{
    /// <summary>
    /// Every shipping <see cref="ISqlDialect"/> implementation - dialects and wrappers alike -
    /// must also implement <see cref="ISubstringToEndDialect"/>. Discovered by reflection rather
    /// than listed, so a dialect added later is covered without anyone remembering to add it here.
    /// </summary>
    [Fact]
    public void EveryShippingDialect_ImplementsTheOptionalInterface()
    {
        Assembly[] shipping =
        [
            typeof(ISqlDialect).Assembly,                                   // Jaunty
            typeof(global::Jaunty.Extensions.Reflection.JauntyReflectionExtensions).Assembly,
        ];

        List<Type> dialects = [];

        foreach (Assembly assembly in shipping)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(ISqlDialect).IsAssignableFrom(type)) continue;

                dialects.Add(type);
            }
        }

        // The premise: if this finds nothing the test would pass vacuously.
        Assert.True(dialects.Count >= 8, $"expected the 4 dialects and 4 bulk-copy wrappers, found {dialects.Count}");

        string[] missing = [.. dialects
            .Where(t => !typeof(ISubstringToEndDialect).IsAssignableFrom(t))
            .Select(t => t.FullName!)];

        Assert.True(
            missing.Length == 0,
            "these ISqlDialect implementations do not implement ISubstringToEndDialect, so they " +
            "silently fall back to a sentinel length:\n  " + string.Join("\n  ", missing));
    }

    // ------------------------------------------------------------------
    // Each dialect's own form
    // ------------------------------------------------------------------

    [Fact]
    public void SqlServer_HasNoTwoArgumentForm_SoItSuppliesALengthThatCannotTruncate()
        => Assert.Equal("SUBSTRING(col, 2, 2147483647)", new SqlServerDialect().GenerateSubstringToEnd("col", "2"));

    [Fact]
    public void Postgres_OmitsFor()
        => Assert.Equal("SUBSTRING(col FROM 2)", new PostgreSqlDialect().GenerateSubstringToEnd("col", "2"));

    [Fact]
    public void MySql_UsesTheTwoArgumentForm()
        => Assert.Equal("SUBSTRING(col, 2)", new MySqlDialect().GenerateSubstringToEnd("col", "2"));

    [Fact]
    public void Sqlite_UsesTheTwoArgumentForm()
        => Assert.Equal("SUBSTR(col, 2)", new SQLiteDialect().GenerateSubstringToEnd("col", "2"));

    /// <summary>
    /// No dialect may emit the sentinel that started this. SQL Server's 2147483647 is not it - the
    /// point of the finding was never "a number appears" but that <em>8000</em> appeared, from a
    /// dialect-neutral visitor, for engines it means nothing to, and small enough to truncate.
    /// </summary>
    [Fact]
    public void NoDialect_EmitsTheHardcoded8000()
    {
        ISubstringToEndDialect[] dialects =
        [
            new SqlServerDialect(), new PostgreSqlDialect(), new MySqlDialect(), new SQLiteDialect(),
        ];

        foreach (ISubstringToEndDialect dialect in dialects)
            Assert.DoesNotContain("8000", dialect.GenerateSubstringToEnd("col", "2"), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // The bulk-copy wrappers
    // ------------------------------------------------------------------

    /// <summary>
    /// A wrapper that dropped this would make the dialect it wraps look like one that never had it.
    /// Since the wrappers are what <c>UseNativeBulkCopy()</c> substitutes, that would silently
    /// reintroduce the sentinel for anyone who had turned bulk copy on - the same shape as
    /// AUD-R25-001, where dispatching on the concrete dialect type broke CSV import for all four
    /// engines the moment a wrapper was in play.
    /// </summary>
    [Theory]
    [InlineData("sqlserver", "SUBSTRING(col, 2, 2147483647)")]
    [InlineData("postgres", "SUBSTRING(col FROM 2)")]
    [InlineData("mysql", "SUBSTRING(col, 2)")]
    [InlineData("sqlite", "SUBSTR(col, 2)")]
    public void TheBulkCopyWrappers_ForwardToTheWrappedDialect(string engine, string expected)
    {
        // Constructed directly rather than through BulkCopyDialectFactory, which is gated on
        // process-wide state that UseNativeBulkCopy() flips - the wrappers are what is under test
        // here, not the switch that selects them.
        ISqlDialect wrapped = engine switch
        {
            "sqlserver" => new SqlServerDialectWithBulkCopy(new SqlServerDialect()),
            "postgres" => new PostgreSqlDialectWithBulkCopy(new PostgreSqlDialect()),
            "mysql" => new MySqlDialectWithBulkCopy(new MySqlDialect()),
            "sqlite" => new SQLiteDialectWithBulkCopy(new SQLiteDialect()),
            _ => throw new ArgumentOutOfRangeException(nameof(engine)),
        };

        Assert.Equal(expected, SubstringToEnd.Generate(wrapped, "col", "2"));
    }

    // ------------------------------------------------------------------
    // The shared helper
    // ------------------------------------------------------------------

    [Fact]
    public void Generate_PrefersTheNativeForm_WhenTheDialectHasOne()
        => Assert.Equal("SUBSTR(col, 2)", SubstringToEnd.Generate(new SQLiteDialect(), "col", "2"));

    [Fact]
    public void Generate_RejectsANullDialect()
        => Assert.Throws<ArgumentNullException>(() => SubstringToEnd.Generate(null!, "col", "2"));
}
