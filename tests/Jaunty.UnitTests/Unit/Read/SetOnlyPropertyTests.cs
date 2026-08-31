using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R26 (batch 3, low/bug). A public <strong>set-only</strong> property on a parameters object
/// crashed binding with an exception naming neither Jaunty, the type, nor the property.
///
/// <para>
/// <c>ParameterCache.BuildMetadata</c> enumerates <c>GetProperties(Instance | Public)</c> and calls
/// <c>CreateGetter</c> for each, which builds <c>Expression.Property(cast, prop)</c> — and that
/// throws for a property with no <c>get</c> accessor. Measured before the fix:
/// </para>
/// <code>
/// conn.QueryPartialList("SELECT id FROM t WHERE id = @Id", new SetOnly { Id = 5 })
///   -> ArgumentException: Expression must be readable (Parameter 'expression')
/// </code>
///
/// <para>
/// The same method already skips indexed properties for exactly this reason — the R16 comment
/// cites Dapper's precedent — so the defect class was recognised and closed one variant at a time.
/// This is the second variant.
/// </para>
/// </summary>
public class SetOnlyPropertyTests
{
    private sealed class SetOnlyParameters
    {
        private string _secret = "";

        public int Id { get; set; }

        /// <summary>Write-only: the whole point. <c>Expression.Property</c> cannot read it.</summary>
        public string Secret { set => _secret = value; }

        public string Reveal() => _secret;
    }

    private sealed class WriteOnlyOnly
    {
        private int _x;

        public int X { set => _x = value; }

        public int Reveal() => _x;
    }

    private static SqliteConnection Seed()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = """
            CREATE TABLE t (id INTEGER, name TEXT);
            INSERT INTO t VALUES (5, 'widget');
            """;
        seed.ExecuteNonQuery();
        return connection;
    }

    /// <summary>The measured reproduction.</summary>
    [Fact]
    public void ASetOnlyProperty_IsSkippedRatherThanCrashingTheBind()
    {
        using SqliteConnection connection = Seed();

        var parameters = new SetOnlyParameters { Id = 5, Secret = "unreadable" };

        Assert.Equal("widget", connection.QueryScalar<string>("SELECT name FROM t WHERE id = @Id", parameters));
    }

    [Fact]
    public void ASetOnlyProperty_DoesNotBecomeAParameter()
    {
        using SqliteConnection connection = Seed();

        var parameters = new SetOnlyParameters { Id = 5, Secret = "unreadable" };

        // If Secret were still in the metadata it would be reported as an unused key, since the
        // SQL never mentions it. Skipping means it is not a parameter at all.
        Assert.Equal("widget", connection.QueryScalar<string>("SELECT name FROM t WHERE id = @Id", parameters));
        Assert.Equal("unreadable", parameters.Reveal());
    }

    /// <summary>
    /// A set-only property the SQL actually asks for must still fail — but as "no value found",
    /// which names the parameter, not as an expression-tree error that names nothing.
    /// </summary>
    [Fact]
    public void ASetOnlyPropertyTheSqlAsksFor_FailsNamingTheParameter()
    {
        using SqliteConnection connection = Seed();

        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => connection.QueryScalar<string>(
                "SELECT name FROM t WHERE name = @Secret", new SetOnlyParameters { Secret = "x" }));

        Assert.Contains("Secret", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Expression must be readable", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>An object whose every property is set-only leaves no bindable parameters at all.</summary>
    [Fact]
    public void AnObjectWithOnlySetOnlyProperties_BindsNothing()
    {
        using SqliteConnection connection = Seed();

        Assert.Equal("widget", connection.QueryScalar<string>(
            "SELECT name FROM t WHERE id = 5", new WriteOnlyOnly { X = 1 }));
    }

    // ------------------------------------------------------------------
    // What must not change
    // ------------------------------------------------------------------

    [Fact]
    public void AnOrdinaryPoco_StillBinds()
    {
        using SqliteConnection connection = Seed();
        Assert.Equal("widget", connection.QueryScalar<string>(
            "SELECT name FROM t WHERE id = @Id", new { Id = 5 }));
    }

    /// <summary>
    /// A get-only property is readable and must remain bindable — the fix keys on the missing
    /// getter, not on the missing setter.
    /// </summary>
    [Fact]
    public void AGetOnlyProperty_StillBinds()
    {
        using SqliteConnection connection = Seed();

        Assert.Equal("widget", connection.QueryScalar<string>(
            "SELECT name FROM t WHERE id = @Id", new GetOnlyParameters(5)));
    }

    private sealed class GetOnlyParameters(int id)
    {
        public int Id { get; } = id;
    }
}
