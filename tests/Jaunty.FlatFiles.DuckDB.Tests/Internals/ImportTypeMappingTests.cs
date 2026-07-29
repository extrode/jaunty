using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.FlatFiles.DuckDB.Internals.Import;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R26 (batch 7, medium/bug). All three import dialects mapped thirteen CLR types and sent
/// everything else silently to a text column - <c>_ =&gt; "NVARCHAR(MAX)"</c> on SQL Server,
/// <c>_ =&gt; "TEXT"</c> on PostgreSQL and SQLite. The unmapped set was ordinary for a modern
/// entity: any enum, <see cref="DateOnly"/>, <see cref="TimeOnly"/>, <see cref="TimeSpan"/>,
/// <see cref="char"/>, <see cref="uint"/>, <see cref="ulong"/>, <see cref="sbyte"/>,
/// <see cref="ushort"/>.
///
/// <para>
/// The import pipeline then binds a value of the real CLR type into that text column. SQLite's
/// dynamic typing absorbs it, so the defect is invisible there; PostgreSQL does not, and the
/// failure lands at insert time with no mention of the column type - one step removed from the DDL
/// that caused it.
/// </para>
/// </summary>
public class ImportTypeMappingTests : IDisposable
{
    private readonly EnumStorage _originalEnumStorage = JauntyConfig.DefaultEnumStorage;

    /// <summary>
    /// Restores <em>only</em> the setting these tests change.
    /// </summary>
    /// <remarks>
    /// Emphatically not <c>JauntyConfig.Reset()</c>, which since AUD-R26-048 also calls
    /// <c>SqlDialectFactory.ResetRegistrations()</c> - and this project has no
    /// <c>xunit.runner.json</c>, so its collections run in parallel. Resetting here drops the
    /// <c>DuckDBConnection</c> to <c>DuckDbDialect</c> registration that <c>DuckDb</c>'s
    /// constructor installs, out from under every other test still running. The first draft of this
    /// file did exactly that and took 141 unrelated tests down with it - a red suite that had
    /// nothing to do with the code under test.
    /// </remarks>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.DefaultEnumStorage = _originalEnumStorage;
    }

    private enum Priority
    {
        Low = 0,
        High = 1
    }

    private enum ByteBackedPriority : byte
    {
        Low = 0
    }

    public static TheoryData<IImportDialect, string> Dialects() => new()
    {
        { new SqlServerImportDialect(), "SQL Server" },
        { new PostgreSqlImportDialect(), "PostgreSQL" },
        { new SqliteImportDialect(), "SQLite" },
    };

    // ------------------------------------------------------------------
    // The types that used to fall through
    // ------------------------------------------------------------------

    /// <summary>
    /// Every previously-unmapped type now has a real column type on every engine. Asserted as
    /// "not the engine's catch-all text type" rather than by naming each SQL type, so the test
    /// states the property that was broken - a type-specific column - without pinning choices like
    /// <c>INTERVAL</c> vs <c>TIME</c> that are engine judgement calls.
    /// </summary>
    [Theory]
    [MemberData(nameof(Dialects))]
    public void PreviouslyUnmappedTypesNoLongerCollapseToText(IImportDialect dialect, string engine)
    {
        _ = engine;

        Type[] wereSilentlyText =
        [
            typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan), typeof(char),
            typeof(uint), typeof(ulong), typeof(sbyte), typeof(ushort),
        ];

        string textType = dialect.MapClrTypeToSqlType(typeof(string));

        foreach (Type type in wereSilentlyText)
        {
            string mapped = dialect.MapClrTypeToSqlType(type);

            // SQLite is the exception and deliberately so: it has four storage classes, and TEXT
            // genuinely is where a DateOnly belongs there. What matters on SQLite is that the type
            // is recognised at all rather than reaching the catch-all - proved by the throwing test
            // below, which is what the catch-all became.
            if (dialect is SqliteImportDialect)
            {
                Assert.False(string.IsNullOrWhiteSpace(mapped));
                continue;
            }

            Assert.NotEqual(textType, mapped);
        }
    }

    /// <summary>
    /// A type with no sensible column is now a named error rather than a text column. This is the
    /// behaviour the catch-all used to swallow, and the message has to carry the type - a table
    /// with several bad columns is otherwise a guessing game.
    /// </summary>
    [Theory]
    [MemberData(nameof(Dialects))]
    public void AnUnsupportedTypeThrowsNamingTheTypeAndEngine(IImportDialect dialect, string engine)
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => dialect.MapClrTypeToSqlType(typeof(System.Text.StringBuilder)));

        Assert.Contains("StringBuilder", exception.Message, StringComparison.Ordinal);
        Assert.Contains(engine, exception.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Enums follow the library's own storage rule
    // ------------------------------------------------------------------

    /// <summary>
    /// An enum stored numerically gets the column its underlying integral type would get, not a
    /// text column. <see cref="ByteBackedPriority"/> is separate because the underlying type is the
    /// one that decides - an enum is not always <see cref="int"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Dialects))]
    public void ANumericEnumMapsAsItsUnderlyingType(IImportDialect dialect, string engine)
    {
        _ = engine;
        JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;

        Assert.Equal(dialect.MapClrTypeToSqlType(typeof(int)), dialect.MapClrTypeToSqlType(typeof(Priority)));
        Assert.Equal(dialect.MapClrTypeToSqlType(typeof(byte)), dialect.MapClrTypeToSqlType(typeof(ByteBackedPriority)));
    }

    /// <summary>
    /// And an enum stored as its name gets a string column - which is what the old catch-all
    /// happened to produce, but for the wrong reason and regardless of the setting.
    /// </summary>
    [Theory]
    [MemberData(nameof(Dialects))]
    public void AStringEnumMapsAsAString(IImportDialect dialect, string engine)
    {
        _ = engine;
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        Assert.Equal(dialect.MapClrTypeToSqlType(typeof(string)), dialect.MapClrTypeToSqlType(typeof(Priority)));
    }

    // ------------------------------------------------------------------
    // The undocumented Nullable precondition
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>MapClrTypeToSqlType</c> is a public member of a public interface, and the only caller
    /// that unwrapped <see cref="Nullable{T}"/> was <c>TargetDdlGenerator</c>, one line before the
    /// call, with nothing documenting that as a precondition. Any other caller got a text column
    /// for an <c>int?</c>. It now unwraps for itself.
    /// </summary>
    [Theory]
    [MemberData(nameof(Dialects))]
    public void ANullableValueTypeMapsAsItsUnderlyingType(IImportDialect dialect, string engine)
    {
        _ = engine;

        Assert.Equal(dialect.MapClrTypeToSqlType(typeof(int)), dialect.MapClrTypeToSqlType(typeof(int?)));
        Assert.Equal(dialect.MapClrTypeToSqlType(typeof(DateOnly)), dialect.MapClrTypeToSqlType(typeof(DateOnly?)));
    }

    // ------------------------------------------------------------------
    // The mappings that already worked must not have moved
    // ------------------------------------------------------------------

    /// <summary>
    /// The thirteen types that were always mapped keep their exact column types. This fix is only
    /// about the ones that fell through, and a shipped table's DDL should not change underneath
    /// anyone.
    /// </summary>
    [Fact]
    public void TheAlreadyMappedTypesAreUnchanged()
    {
        var sqlServer = new SqlServerImportDialect();
        Assert.Equal("NVARCHAR(MAX)", sqlServer.MapClrTypeToSqlType(typeof(string)));
        Assert.Equal("INT", sqlServer.MapClrTypeToSqlType(typeof(int)));
        Assert.Equal("DECIMAL(18,4)", sqlServer.MapClrTypeToSqlType(typeof(decimal)));
        Assert.Equal("VARBINARY(MAX)", sqlServer.MapClrTypeToSqlType(typeof(byte[])));

        var postgres = new PostgreSqlImportDialect();
        Assert.Equal("TEXT", postgres.MapClrTypeToSqlType(typeof(string)));
        Assert.Equal("DOUBLE PRECISION", postgres.MapClrTypeToSqlType(typeof(double)));
        Assert.Equal("UUID", postgres.MapClrTypeToSqlType(typeof(Guid)));

        var sqlite = new SqliteImportDialect();
        Assert.Equal("INTEGER", sqlite.MapClrTypeToSqlType(typeof(long)));
        Assert.Equal("BLOB", sqlite.MapClrTypeToSqlType(typeof(byte[])));
    }
}
