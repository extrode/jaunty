using DuckDB.NET.Data;

using Jaunty.Dialects;
using Jaunty.Fluent;
using Jaunty.FlatFiles.DuckDB.Dialects;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Integration;

/// <summary>
/// AUD-R25 (B4-3): <c>ISqlDialect.ParameterPrefix</c> is a public extension point whose documented
/// purpose is provider-specific placeholder syntax, and <c>Jaunty.Fluent</c> honours it across ~20
/// call sites. Jaunty core's CRUD and by-id paths never consult it - <c>CrudSqlCache</c>,
/// <c>MultiRowInsertCache</c>, <c>Upsert</c>, <c>DeleteCore</c>, <c>GetCore</c> and
/// <c>WriteParameterHelper</c> all emit a literal <c>"@"</c>, as do both entity binders (the
/// generated <c>BindInsert</c>/<c>BindUpdate</c>/<c>BindDelete</c> and the reflection equivalents).
///
/// <para>
/// The finding offered two resolutions: route core through the property the way Fluent does, or
/// document it as Fluent-only. The second was taken, because the first is not an internal change.
/// The generated binders bake the prefix in at <b>compile</b> time, when no connection and therefore
/// no dialect exists, so routing core CRUD through the dialect requires the generated binder
/// contract to accept a prefix at runtime - a breaking change to the generated API surface. That is
/// a scoped piece of work, not an audit fix.
/// </para>
///
/// <para>
/// These tests exist so the limitation is executable rather than only prose. The finding said the
/// write path's divergence was untested and suspected it was broken; it is broken, and this is the
/// reproduction. <b>They deliberately assert a failure.</b> When the binder contract is changed,
/// they will fail - which is the intent: whoever fixes it is told exactly where the record of the
/// old behaviour lives, and should replace these with the round-trip assertions the fix enables.
/// </para>
/// </summary>
public class ParameterPrefixLimitationTests : IDisposable
{
    private readonly DuckDBConnection _connection;

    public ParameterPrefixLimitationTests()
    {
        SqlDialectFactory.RegisterDialect("DuckDBConnection", DuckDbDialect.Instance);

        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE customers (
                CustomerId INTEGER PRIMARY KEY,
                Name VARCHAR,
                Email VARCHAR,
                Phone VARCHAR,
                CreatedAt TIMESTAMP
            )
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TheDuckDbDialect_DeclaresADollarPrefix()
    {
        // The premise. If this ever changes the rest of the class is describing nothing.
        Assert.Equal("$", DuckDbDialect.Instance.ParameterPrefix);
    }

    [Fact]
    public void CoreInsert_DoesNotHonourTheDialectPrefix_AndFails()
    {
        var entity = new CustomerProfile
        {
            CustomerId = 1,
            Name = "Ada",
            Email = "ada@example.com",
            CreatedAt = DateTime.UnixEpoch
        };

        var exception = Assert.Throws<DuckDBException>(() => _connection.Insert(entity));

        // DuckDB parses the "@CustomerId" core emits as a column reference, not a placeholder.
        Assert.Contains("not found in FROM clause", exception.Message);
    }

    [Fact]
    public void FluentHonoursThePrefixInTheSqlButStillFails_ForADifferentReason()
    {
        // Worth stating plainly, because the obvious advice - "use Jaunty.Fluent for writes against
        // a non-@ dialect" - is WRONG, and this test is what established that rather than assuming
        // it. Fluent does honour ParameterPrefix, so the SQL it emits parses: DuckDB gets past the
        // Binder Error the core path dies on. It then fails at bind time instead, because the names
        // Fluent gives its parameters are not the ones DuckDB matches against the $placeholders.
        //
        // So DuckDB writes do not work through either path today, for two different reasons, and
        // fixing B4-3 in core would not by itself make them work. Recorded here so the next person
        // does not fix half of it and believe they are done.
        var entity = new CustomerProfile
        {
            CustomerId = 2,
            Name = "Grace",
            Email = "grace@example.com",
            CreatedAt = DateTime.UnixEpoch
        };

        var exception = Assert.Throws<DuckDBException>(
            () => _connection.Into<CustomerProfile>().Values(entity).Insert());

        Assert.Contains("Values were not provided", exception.Message);
        Assert.DoesNotContain("not found in FROM clause", exception.Message);
    }

    [Fact]
    public void ReadsAreUnaffected_BecauseTheyCarryNoParameters()
    {
        // Bounding the limitation: the DuckDB read path is real and covered elsewhere
        // (JauntyMaterializationTests). It is writes and by-id lookups - the paths that bind
        // parameters - that the prefix divergence breaks.
        using var seed = _connection.CreateCommand();
        seed.CommandText = "INSERT INTO customers VALUES (3, 'Alan', 'alan@example.com', NULL, '1970-01-01')";
        seed.ExecuteNonQuery();

        List<CustomerProfile> customers = [.. _connection.Query<CustomerProfile>("SELECT * FROM customers")];

        Assert.Equal("Alan", Assert.Single(customers).Name);
    }
}
