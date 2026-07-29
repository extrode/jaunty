using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R26 (batch 1, medium). Four public read APIs documented a parameter-binding mode that does
/// not exist: <em>"Supports both named parameters (via anonymous objects) and positional parameters
/// (via property arrays)"</em>. Jaunty has no positional binding and no property-array form, and
/// <c>Query.cs</c>'s worked example labelled <c>// Positional parameters (values bound in order)</c>
/// showed an anonymous object - named binding, identical in kind to the "Named parameters" block
/// three lines above it.
///
/// <para>
/// These tests pin what the binder actually does, so the corrected documentation cannot drift back:
/// three shapes, all by name, and an array that is none of them. If positional binding is ever
/// implemented, <see cref="AnArray_IsNotPositionalBinding"/> fails and says so.
/// </para>
/// </summary>
public class ParameterBindingShapesTests
{
    private static SqliteConnection Seed()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = """
            CREATE TABLE products (id INTEGER, category_id INTEGER, price REAL, name TEXT);
            INSERT INTO products VALUES (1, 5, 150.0, 'widget');
            INSERT INTO products VALUES (2, 5,  50.0, 'gadget');
            INSERT INTO products VALUES (3, 9, 150.0, 'doohickey');
            """;
        seed.ExecuteNonQuery();
        return connection;
    }

    private const string TwoParameterSql =
        "SELECT name FROM products WHERE category_id = @Id AND price > @Min";

    private const string OneParameterSql =
        "SELECT name FROM products WHERE category_id = @Id";

    // ------------------------------------------------------------------
    // The three shapes that do work
    // ------------------------------------------------------------------

    [Fact]
    public void AnAnonymousObject_BindsByPropertyName()
    {
        using SqliteConnection connection = Seed();

        IDictionary<string, object?> row = Assert.Single(
            connection.QueryPartialList(TwoParameterSql, new { Id = 5, Min = 100.0 }));

        Assert.Equal("widget", row["name"]);
    }

    /// <summary>Property order is irrelevant, which is what "by name" means and "positional" does not.</summary>
    [Fact]
    public void AnAnonymousObject_BindsRegardlessOfPropertyOrder()
    {
        using SqliteConnection connection = Seed();

        IDictionary<string, object?> row = Assert.Single(
            connection.QueryPartialList(TwoParameterSql, new { Min = 100.0, Id = 5 }));

        Assert.Equal("widget", row["name"]);
    }

    [Fact]
    public void ADictionary_BindsByKey()
    {
        using SqliteConnection connection = Seed();

        var parameters = new Dictionary<string, object?> { ["Id"] = 5, ["Min"] = 100.0 };

        IDictionary<string, object?> row = Assert.Single(
            connection.QueryPartialList(TwoParameterSql, parameters));

        Assert.Equal("widget", row["name"]);
    }

    /// <summary>
    /// The one form the old docs called "positional" that actually works - and it is not positional
    /// either. The value goes to whatever single parameter the SQL names.
    /// </summary>
    [Fact]
    public void ASingleScalar_BindsToTheOneParameterTheSqlNames()
    {
        using SqliteConnection connection = Seed();

        List<IDictionary<string, object?>> rows = [.. connection.QueryPartialList(OneParameterSql, 5)];

        Assert.Equal(2, rows.Count);
    }

    // ------------------------------------------------------------------
    // What the documentation claimed, and what it actually does
    // ------------------------------------------------------------------

    /// <summary>
    /// The measured reproduction. An array is neither a dictionary nor a scalar, so it falls to the
    /// by-name path and is reflected over as an ordinary object. The error text enumerating
    /// <c>Rank</c> and <c>SyncRoot</c> is itself evidence the documented path had never been run.
    /// </summary>
    [Fact]
    public void AnArray_IsNotPositionalBinding()
    {
        using SqliteConnection connection = Seed();

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => connection.QueryPartialList(TwoParameterSql, new object[] { 5, 100.0 }).ToList());

        Assert.Contains("No property found on type 'Object[]'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("SyncRoot", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A scalar cannot be "values bound in order" because it is one value, and the binder refuses to
    /// guess: with two parameters in the SQL it throws rather than binding the same value to both.
    /// </summary>
    [Fact]
    public void ASingleScalar_IsRejectedWhenTheSqlNamesMoreThanOneParameter()
    {
        using SqliteConnection connection = Seed();

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => connection.QueryPartialList(TwoParameterSql, 5).ToList());

        Assert.Contains("cannot be bound to SQL containing 2 distinct parameters", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// And it will not silently drop a value the SQL has no home for - the other half of why a
    /// scalar can never behave positionally.
    /// </summary>
    [Fact]
    public void ASingleScalar_IsRejectedWhenTheSqlNamesNoParameters()
    {
        using SqliteConnection connection = Seed();

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => connection.QueryPartialList("SELECT name FROM products", 5).ToList());

        Assert.Contains("Unused scalar parameter value", ex.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // The documentation itself
    // ------------------------------------------------------------------

    /// <summary>
    /// A doc-only fix has nothing in the build that holds it, so this reads the shipped XML
    /// documentation and asserts the retracted claim has not come back. Four files carried it
    /// verbatim; a fifth copy would reintroduce exactly the drift this finding was about.
    /// </summary>
    [Fact]
    public void TheXmlDocumentation_NoLongerClaimsPositionalBinding()
    {
        string xmlPath = Path.ChangeExtension(typeof(Jaunty).Assembly.Location, ".xml");

        Assert.True(File.Exists(xmlPath), $"XML documentation not produced at {xmlPath}.");

        string xml = File.ReadAllText(xmlPath);

        Assert.DoesNotContain("positional parameters</strong> (via property arrays)", xml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Positional parameters (values bound in order)", xml, StringComparison.Ordinal);
        Assert.Contains("There is no positional binding.", xml, StringComparison.Ordinal);
    }
}
