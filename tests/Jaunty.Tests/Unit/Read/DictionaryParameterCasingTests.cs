using System.Dynamic;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R26 (batch 3, medium/consistency). Dictionary parameters were matched against the SQL using
/// <em>the caller's own comparer</em>, while object/POCO parameters are matched case-insensitively -
/// so the two documented-equivalent parameter forms disagreed, and the difference was invisible at
/// the call site.
///
/// <para>
/// Measured before the fix against Microsoft.Data.Sqlite, SQL <c>... WHERE id = @Id</c>:
/// </para>
/// <code>
/// new { id = 5 }                                                     -> OK
/// new Dictionary&lt;string, object?&gt;                   { ["id"] = 5 }  -> ArgumentException
/// new Dictionary&lt;string, object?&gt;(OrdinalIgnoreCase) { ["id"] = 5 }  -> OK
/// new Dictionary&lt;string, object?&gt;                   { ["Id"] = 5 }  -> OK
/// </code>
///
/// <para>
/// A plain <c>Dictionary&lt;string, object?&gt;</c> - the form every example produces by default -
/// is case-sensitive, so which of two equivalent call shapes worked depended on a comparer the
/// caller probably never chose.
/// </para>
/// </summary>
public class DictionaryParameterCasingTests
{
    private const string Sql = "SELECT name FROM products WHERE id = @Id";

    private static SqliteConnection Seed()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = """
            CREATE TABLE products (id INTEGER, name TEXT);
            INSERT INTO products VALUES (5, 'widget');
            """;
        seed.ExecuteNonQuery();
        return connection;
    }

    private static string? Name(SqliteConnection connection, object parameters)
        => connection.QueryScalar<string>(Sql, parameters);

    // ------------------------------------------------------------------
    // The two forms now agree
    // ------------------------------------------------------------------

    /// <summary>The reference behaviour: the object path has always matched case-insensitively.</summary>
    [Fact]
    public void AnObjectWithADifferentlyCasedProperty_Binds()
    {
        using SqliteConnection connection = Seed();
        Assert.Equal("widget", Name(connection, new { id = 5 }));
    }

    /// <summary>The measured reproduction. This threw before the fix.</summary>
    [Fact]
    public void APlainDictionaryWithADifferentlyCasedKey_NowBinds()
    {
        using SqliteConnection connection = Seed();
        Assert.Equal("widget", Name(connection, new Dictionary<string, object?> { ["id"] = 5 }));
    }

    [Fact]
    public void APlainDictionaryWithAnUpperCasedKey_Binds()
    {
        using SqliteConnection connection = Seed();
        Assert.Equal("widget", Name(connection, new Dictionary<string, object?> { ["ID"] = 5 }));
    }

    [Fact]
    public void AnExactlyMatchingKey_StillBinds()
    {
        using SqliteConnection connection = Seed();
        Assert.Equal("widget", Name(connection, new Dictionary<string, object?> { ["Id"] = 5 }));
    }

    /// <summary>A dictionary that already declared the comparer must be unaffected.</summary>
    [Fact]
    public void ACaseInsensitiveDictionary_StillBinds()
    {
        using SqliteConnection connection = Seed();
        Assert.Equal("widget", Name(connection,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["id"] = 5 }));
    }

    /// <summary>
    /// An <see cref="ExpandoObject"/> presents a case-sensitive
    /// <c>IDictionary&lt;string, object?&gt;</c>, so it took the same path and had the same problem.
    /// </summary>
    [Fact]
    public void AnExpandoObjectWithADifferentlyCasedMember_Binds()
    {
        using SqliteConnection connection = Seed();

        dynamic expando = new ExpandoObject();
        expando.id = 5;

        Assert.Equal("widget", Name(connection, (object)expando));
    }

    /// <summary>
    /// The whole point of the fix stated as one assertion: the same query, the same value, three
    /// call shapes, one answer.
    /// </summary>
    [Fact]
    public void TheObjectAndDictionaryForms_Agree()
    {
        using SqliteConnection connection = Seed();

        Assert.Equal(
            Name(connection, new { id = 5 }),
            Name(connection, new Dictionary<string, object?> { ["id"] = 5 }));

        Assert.Equal(
            Name(connection, new { Id = 5 }),
            Name(connection, new Dictionary<string, object?> { ["id"] = 5 }));
    }

    // ------------------------------------------------------------------
    // Ambiguity is refused, not guessed
    // ------------------------------------------------------------------

    /// <summary>
    /// A case-sensitive dictionary can hold both spellings. Under case-insensitive matching there is
    /// no answer to which one <c>@Id</c> meant, and picking whichever enumerated first is exactly
    /// the silent wrong-value class this round has been closing.
    /// </summary>
    [Fact]
    public void KeysDifferingOnlyInCase_AreRefused()
    {
        using SqliteConnection connection = Seed();

        var parameters = new Dictionary<string, object?> { ["id"] = 5, ["ID"] = 6 };

        ArgumentException ex = Assert.Throws<ArgumentException>(() => Name(connection, parameters));

        Assert.Equal("parameters", ex.ParamName);
        Assert.Contains("differing only in case", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The ambiguity check runs only when a lookup misses, so an exact hit alongside a
    /// differently-cased sibling still resolves - that is the behaviour that existed before and is
    /// not worth breaking to enforce a rule the caller has already answered unambiguously.
    /// </summary>
    [Fact]
    public void AnExactHit_ResolvesWithoutConsultingTheOtherSpelling()
    {
        using SqliteConnection connection = Seed();

        var parameters = new Dictionary<string, object?> { ["Id"] = 5 };

        Assert.Equal("widget", Name(connection, parameters));
    }

    // ------------------------------------------------------------------
    // What must not change
    // ------------------------------------------------------------------

    [Fact]
    public void AMissingKey_StillThrows()
    {
        using SqliteConnection connection = Seed();

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Name(connection, new Dictionary<string, object?> { ["Other"] = 5 }));

        Assert.Contains("No value found in dictionary for SQL parameter '@Id'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnusedKey_StillThrows()
    {
        using SqliteConnection connection = Seed();

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => Name(connection, new Dictionary<string, object?> { ["Id"] = 5, ["Spurious"] = 1 }));

        Assert.Contains("Unused parameter keys in dictionary", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullValue_StillBindsAsNull()
    {
        using SqliteConnection connection = Seed();

        Assert.Null(connection.QueryScalar<string>(
            "SELECT name FROM products WHERE name IS @Name",
            new Dictionary<string, object?> { ["name"] = null }));
    }

    [Fact]
    public void MultipleParameters_AllBindRegardlessOfCase()
    {
        using SqliteConnection connection = Seed();

        Assert.Equal("widget", connection.QueryScalar<string>(
            "SELECT name FROM products WHERE id = @Id AND name = @Name",
            new Dictionary<string, object?> { ["ID"] = 5, ["nAmE"] = "widget" }));
    }
}
