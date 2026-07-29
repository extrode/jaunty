using Jaunty.Internals.Read;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R26 (batch 1, medium). The untyped-row paths built each row with the indexer, so two columns
/// of the same name silently collapsed to one key. Measured before the fix, against a live
/// Microsoft.Data.Sqlite connection, on all three sites - <c>QueryPartialList</c>,
/// <c>QueryPartialListAsync</c> and <c>Query&lt;Dictionary&lt;string, object&gt;&gt;</c>:
/// <c>keys=[Id] values=[1]</c>, with the order row's <c>Id</c> of 10 gone.
/// </summary>
public class DuplicateColumnNameTests
{
    private static SqliteConnection Seed()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = """
            CREATE TABLE Orders (Id INTEGER, CustomerId INTEGER);
            CREATE TABLE Customers (Id INTEGER, Name TEXT);
            INSERT INTO Orders VALUES (10, 1);
            INSERT INTO Customers VALUES (1, 'Acme');
            """;
        seed.ExecuteNonQuery();
        return connection;
    }

    private const string JoinSql =
        "SELECT o.Id, c.Id FROM Orders o JOIN Customers c ON c.Id = o.CustomerId";

    // The round-25 widening: under the old ordinal comparer these were two distinct keys and both
    // values survived; OrdinalIgnoreCase made them collide.
    private const string CaseDifferingSql =
        "SELECT o.Id AS Id, c.Id AS id FROM Orders o JOIN Customers c ON c.Id = o.CustomerId";

    // ------------------------------------------------------------------
    // The disambiguation rule itself
    // ------------------------------------------------------------------

    [Fact]
    public void UniqueNames_AreReturnedUntouched()
    {
        string[] names = ["Id", "Name", "Total"];
        Assert.Same(names, DuplicateColumnNames.Disambiguate(names));
    }

    /// <summary>
    /// Returning the caller's own array for the common case is the point of the fast path - every
    /// result set in the codebase that has no duplicates must not allocate for this.
    /// </summary>
    [Fact]
    public void AnEmptyColumnSet_IsReturnedUntouched()
    {
        string[] names = [];
        Assert.Same(names, DuplicateColumnNames.Disambiguate(names));
    }

    [Fact]
    public void ASingleColumn_IsReturnedUntouched()
    {
        string[] names = ["Id"];
        Assert.Same(names, DuplicateColumnNames.Disambiguate(names));
    }

    [Fact]
    public void TheFirstOccurrence_KeepsTheBareName()
        => Assert.Equal(["Id", "Id_2"], DuplicateColumnNames.Disambiguate(["Id", "Id"]));

    [Fact]
    public void TheSuffix_IsTheOccurrenceNumber_NotTheOrdinal()
        => Assert.Equal(
            ["Id", "Name", "Id_2", "Id_3"],
            DuplicateColumnNames.Disambiguate(["Id", "Name", "Id", "Id"]));

    [Fact]
    public void NamesDifferingOnlyInCase_AreTreatedAsDuplicates()
        => Assert.Equal(["Id", "id_2"], DuplicateColumnNames.Disambiguate(["Id", "id"]));

    /// <summary>The generated name keeps the duplicate's own casing, not the first occurrence's.</summary>
    [Fact]
    public void TheGeneratedName_PreservesTheReadersCasing()
        => Assert.Equal(["ID", "id_2", "Id_3"], DuplicateColumnNames.Disambiguate(["ID", "id", "Id"]));

    /// <summary>
    /// A generated suffix can collide with a real column, so the occurrence number keeps rising until
    /// the candidate is free. Without this the fix would reintroduce the very collapse it removes.
    /// </summary>
    [Fact]
    public void AGeneratedSuffix_DoesNotCollideWithARealColumn()
        => Assert.Equal(["Id", "Id_2", "Id_3"], DuplicateColumnNames.Disambiguate(["Id", "Id_2", "Id"]));

    [Fact]
    public void AGeneratedSuffix_DoesNotCollideWithARealColumnLaterInTheSet()
        => Assert.Equal(["Id", "Id_3", "Id_2"], DuplicateColumnNames.Disambiguate(["Id", "Id", "Id_2"]));

    [Fact]
    public void EveryProducedName_IsUnique()
    {
        string[] result = DuplicateColumnNames.Disambiguate(
            ["Id", "id", "ID", "Id_2", "Name", "name", "Id"]);

        Assert.Equal(result.Length, new HashSet<string>(result, StringComparer.OrdinalIgnoreCase).Count);
    }

    // ------------------------------------------------------------------
    // QueryPartialList - the measured reproduction
    // ------------------------------------------------------------------

    [Fact]
    public void QueryPartialList_KeepsBothValuesOfADuplicateColumn()
    {
        using SqliteConnection connection = Seed();

        IDictionary<string, object?> row = Assert.Single(connection.QueryPartialList(JoinSql));

        Assert.Equal(2, row.Count);
        Assert.Equal(10L, row["Id"]);
        Assert.Equal(1L, row["Id_2"]);
    }

    [Fact]
    public void QueryPartialList_KeepsBothValuesWhenTheNamesDifferOnlyInCase()
    {
        using SqliteConnection connection = Seed();

        IDictionary<string, object?> row = Assert.Single(connection.QueryPartialList(CaseDifferingSql));

        Assert.Equal(2, row.Count);
        Assert.Equal(10L, row["Id"]);
        Assert.Equal(1L, row["id_2"]);
    }

    [Fact]
    public async Task QueryPartialListAsync_KeepsBothValuesOfADuplicateColumn()
    {
        using SqliteConnection connection = Seed();

        IEnumerable<IDictionary<string, object?>> rows = await connection.QueryPartialListAsync(
            JoinSql, cancellationToken: TestContext.Current.CancellationToken);

        IDictionary<string, object?> row = Assert.Single(rows);

        Assert.Equal(2, row.Count);
        Assert.Equal(10L, row["Id"]);
        Assert.Equal(1L, row["Id_2"]);
    }

    /// <summary>
    /// The sibling untyped-row path. The finding named both sites and required that any fix cover
    /// both "so they cannot drift again" - this is the assertion that holds that.
    /// </summary>
    [Fact]
    public void TheDictionaryMapper_KeepsBothValuesToo()
    {
        using SqliteConnection connection = Seed();

        Dictionary<string, object> row =
            Assert.Single(connection.Query<Dictionary<string, object>>(JoinSql));

        Assert.Equal(2, row.Count);
        Assert.Equal(10L, row["Id"]);
        Assert.Equal(1L, row["Id_2"]);
    }

    // ------------------------------------------------------------------
    // What must not change
    // ------------------------------------------------------------------

    /// <summary>
    /// AUD-R25's actual fix - the case-insensitive lookup the comparer change was made for - has to
    /// survive this one.
    /// </summary>
    [Fact]
    public void CaseInsensitiveLookup_StillWorks()
    {
        using SqliteConnection connection = Seed();

        IDictionary<string, object?> row = Assert.Single(
            connection.QueryPartialList("SELECT Id, CustomerId FROM Orders"));

        Assert.Equal(10L, row["id"]);
        Assert.Equal(1L, row["CUSTOMERID"]);
    }

    [Fact]
    public void AResultSetWithoutDuplicates_IsUnaffected()
    {
        using SqliteConnection connection = Seed();

        IDictionary<string, object?> row = Assert.Single(
            connection.QueryPartialList("SELECT o.Id, c.Name FROM Orders o JOIN Customers c ON c.Id = o.CustomerId"));

        Assert.Equal(["Id", "Name"], row.Keys);
    }
}
