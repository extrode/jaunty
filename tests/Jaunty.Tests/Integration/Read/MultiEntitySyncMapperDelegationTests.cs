using System.Data.SQLite;

using Jaunty.Core;

using Xunit;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// AUD-R35-060. The <c>if (options.Mapper is not null)</c> delegation added by AUD-R33-004 and
/// AUD-R34-004 is hand-copied per arity across five sync core families, and was tested on the sync
/// side at arity 2 only - <c>MultiEntityCustomMapperTests</c> and
/// <c>MultiEntityCustomMapperEmptyResultTests</c> both use
/// <c>CommandOptions&lt;(Author, Book)&gt;.WithMapper</c> exclusively, and the one higher-arity
/// mapper case in the suite drives the async twin. That left the arity 3-7 delegation sites
/// unexecuted, including each arity-specific <c>describeType</c> lambda whose wording the
/// empty-result tests pin at arity 2 only.
/// <para>
/// Hand-copied per arity is the shape most likely to carry a silent transcription error, and the
/// two defects these branches exist to prevent - a discarded mapper, and a tuple of nulls standing
/// in for an empty result - both shipped undetected, twice.
/// </para>
/// </summary>
public class MultiEntitySyncMapperDelegationTests
{
    internal sealed class PartA
    {
        public long Id { get; set; }
    }
    internal sealed class PartB
    {
        public long Id { get; set; }
    }
    internal sealed class PartC
    {
        public long Id { get; set; }
    }
    internal sealed class PartD
    {
        public long Id { get; set; }
    }
    internal sealed class PartE
    {
        public long Id { get; set; }
    }
    internal sealed class PartF
    {
        public long Id { get; set; }
    }
    internal sealed class PartG
    {
        public long Id { get; set; }
    }
    private static SQLiteConnection Open()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE mapper_rows (id INTEGER PRIMARY KEY); INSERT INTO mapper_rows (id) VALUES (1), (2)";
        cmd.ExecuteNonQuery();

        return connection;
    }

    private const string OneRow = "SELECT id AS Id FROM mapper_rows WHERE id = 1";
    private const string TwoRows = "SELECT id AS Id FROM mapper_rows ORDER BY id";
    private const string NoRows = "SELECT id AS Id FROM mapper_rows WHERE id = -1";

    // ------------------------------------------------------------------
    // Arity 3
    // ------------------------------------------------------------------

    [Fact]
    public void Query_Arity3_HonoursTheMapper()
    {
        using var connection = Open();

        int calls = 0;
        var options = CommandOptions<(PartA, PartB, PartC)>.WithMapper(reader =>
        {
            calls++;
            return (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 });
        });

        var rows = connection.Query<PartA, PartB, PartC>(OneRow, options);

        Assert.Equal(1, calls);
        Assert.Equal(10L, rows[0].Item1.Id);
        Assert.Equal(12L, rows[0].Item3.Id);
    }

    [Fact]
    public void QueryStream_Arity3_HonoursTheMapper()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }));

        var rows = connection.QueryStream<PartA, PartB, PartC>(OneRow, options).ToList();

        Assert.Single(rows);
        Assert.Equal(10L, rows[0].Item1.Id);
    }

    [Fact]
    public void QueryFirstOrDefault_Arity3_WithMapper_ReturnsNullOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }));

        Assert.Null(connection.QueryFirstOrDefault<PartA, PartB, PartC>(NoRows, options));
    }

    [Fact]
    public void QuerySingleOrDefault_Arity3_WithMapper_ReturnsNullOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }));

        Assert.Null(connection.QuerySingleOrDefault<PartA, PartB, PartC>(NoRows, options));
    }

    /// <summary>
    /// The arity-specific describeType lambda, which nothing above arity 2 had ever invoked.
    /// </summary>
    [Fact]
    public void QuerySingle_Arity3_WithMapper_NamesEveryTypeWhenMoreThanOneRow()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }));

        var ex = Assert.Throws<InvalidOperationException>(
            () => connection.QuerySingle<PartA, PartB, PartC>(TwoRows, options));

        Assert.Contains("(PartA, PartB, PartC)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryFirst_Arity3_WithMapper_ThrowsOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }));

        Assert.Throws<InvalidOperationException>(
            () => connection.QueryFirst<PartA, PartB, PartC>(NoRows, options));
    }

    // ------------------------------------------------------------------
    // Arity 4
    // ------------------------------------------------------------------

    [Fact]
    public void Query_Arity4_HonoursTheMapper()
    {
        using var connection = Open();

        int calls = 0;
        var options = CommandOptions<(PartA, PartB, PartC, PartD)>.WithMapper(reader =>
        {
            calls++;
            return (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 });
        });

        var rows = connection.Query<PartA, PartB, PartC, PartD>(OneRow, options);

        Assert.Equal(1, calls);
        Assert.Equal(10L, rows[0].Item1.Id);
        Assert.Equal(13L, rows[0].Item4.Id);
    }

    [Fact]
    public void QueryStream_Arity4_HonoursTheMapper()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }));

        var rows = connection.QueryStream<PartA, PartB, PartC, PartD>(OneRow, options).ToList();

        Assert.Single(rows);
        Assert.Equal(10L, rows[0].Item1.Id);
    }

    [Fact]
    public void QueryFirstOrDefault_Arity4_WithMapper_ReturnsNullOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }));

        Assert.Null(connection.QueryFirstOrDefault<PartA, PartB, PartC, PartD>(NoRows, options));
    }

    [Fact]
    public void QuerySingleOrDefault_Arity4_WithMapper_ReturnsNullOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }));

        Assert.Null(connection.QuerySingleOrDefault<PartA, PartB, PartC, PartD>(NoRows, options));
    }

    /// <summary>
    /// The arity-specific describeType lambda, which nothing above arity 2 had ever invoked.
    /// </summary>
    [Fact]
    public void QuerySingle_Arity4_WithMapper_NamesEveryTypeWhenMoreThanOneRow()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }));

        var ex = Assert.Throws<InvalidOperationException>(
            () => connection.QuerySingle<PartA, PartB, PartC, PartD>(TwoRows, options));

        Assert.Contains("(PartA, PartB, PartC, PartD)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryFirst_Arity4_WithMapper_ThrowsOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }));

        Assert.Throws<InvalidOperationException>(
            () => connection.QueryFirst<PartA, PartB, PartC, PartD>(NoRows, options));
    }

    // ------------------------------------------------------------------
    // Arity 5
    // ------------------------------------------------------------------

    [Fact]
    public void Query_Arity5_HonoursTheMapper()
    {
        using var connection = Open();

        int calls = 0;
        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE)>.WithMapper(reader =>
        {
            calls++;
            return (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 });
        });

        var rows = connection.Query<PartA, PartB, PartC, PartD, PartE>(OneRow, options);

        Assert.Equal(1, calls);
        Assert.Equal(10L, rows[0].Item1.Id);
        Assert.Equal(14L, rows[0].Item5.Id);
    }

    [Fact]
    public void QueryStream_Arity5_HonoursTheMapper()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }));

        var rows = connection.QueryStream<PartA, PartB, PartC, PartD, PartE>(OneRow, options).ToList();

        Assert.Single(rows);
        Assert.Equal(10L, rows[0].Item1.Id);
    }

    [Fact]
    public void QueryFirstOrDefault_Arity5_WithMapper_ReturnsNullOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }));

        Assert.Null(connection.QueryFirstOrDefault<PartA, PartB, PartC, PartD, PartE>(NoRows, options));
    }

    [Fact]
    public void QuerySingleOrDefault_Arity5_WithMapper_ReturnsNullOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }));

        Assert.Null(connection.QuerySingleOrDefault<PartA, PartB, PartC, PartD, PartE>(NoRows, options));
    }

    /// <summary>
    /// The arity-specific describeType lambda, which nothing above arity 2 had ever invoked.
    /// </summary>
    [Fact]
    public void QuerySingle_Arity5_WithMapper_NamesEveryTypeWhenMoreThanOneRow()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }));

        var ex = Assert.Throws<InvalidOperationException>(
            () => connection.QuerySingle<PartA, PartB, PartC, PartD, PartE>(TwoRows, options));

        Assert.Contains("(PartA, PartB, PartC, PartD, PartE)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryFirst_Arity5_WithMapper_ThrowsOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }));

        Assert.Throws<InvalidOperationException>(
            () => connection.QueryFirst<PartA, PartB, PartC, PartD, PartE>(NoRows, options));
    }

    // ------------------------------------------------------------------
    // Arity 6
    // ------------------------------------------------------------------

    [Fact]
    public void Query_Arity6_HonoursTheMapper()
    {
        using var connection = Open();

        int calls = 0;
        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE, PartF)>.WithMapper(reader =>
        {
            calls++;
            return (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }, new PartF { Id = 15 });
        });

        var rows = connection.Query<PartA, PartB, PartC, PartD, PartE, PartF>(OneRow, options);

        Assert.Equal(1, calls);
        Assert.Equal(10L, rows[0].Item1.Id);
        Assert.Equal(15L, rows[0].Item6.Id);
    }

    [Fact]
    public void QueryStream_Arity6_HonoursTheMapper()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE, PartF)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }, new PartF { Id = 15 }));

        var rows = connection.QueryStream<PartA, PartB, PartC, PartD, PartE, PartF>(OneRow, options).ToList();

        Assert.Single(rows);
        Assert.Equal(10L, rows[0].Item1.Id);
    }

    [Fact]
    public void QueryFirstOrDefault_Arity6_WithMapper_ReturnsNullOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE, PartF)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }, new PartF { Id = 15 }));

        Assert.Null(connection.QueryFirstOrDefault<PartA, PartB, PartC, PartD, PartE, PartF>(NoRows, options));
    }

    [Fact]
    public void QuerySingleOrDefault_Arity6_WithMapper_ReturnsNullOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE, PartF)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }, new PartF { Id = 15 }));

        Assert.Null(connection.QuerySingleOrDefault<PartA, PartB, PartC, PartD, PartE, PartF>(NoRows, options));
    }

    /// <summary>
    /// The arity-specific describeType lambda, which nothing above arity 2 had ever invoked.
    /// </summary>
    [Fact]
    public void QuerySingle_Arity6_WithMapper_NamesEveryTypeWhenMoreThanOneRow()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE, PartF)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }, new PartF { Id = 15 }));

        var ex = Assert.Throws<InvalidOperationException>(
            () => connection.QuerySingle<PartA, PartB, PartC, PartD, PartE, PartF>(TwoRows, options));

        Assert.Contains("(PartA, PartB, PartC, PartD, PartE, PartF)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryFirst_Arity6_WithMapper_ThrowsOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE, PartF)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }, new PartF { Id = 15 }));

        Assert.Throws<InvalidOperationException>(
            () => connection.QueryFirst<PartA, PartB, PartC, PartD, PartE, PartF>(NoRows, options));
    }

    // ------------------------------------------------------------------
    // Arity 7
    // ------------------------------------------------------------------

    [Fact]
    public void Query_Arity7_HonoursTheMapper()
    {
        using var connection = Open();

        int calls = 0;
        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE, PartF, PartG)>.WithMapper(reader =>
        {
            calls++;
            return (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }, new PartF { Id = 15 }, new PartG { Id = 16 });
        });

        var rows = connection.Query<PartA, PartB, PartC, PartD, PartE, PartF, PartG>(OneRow, options);

        Assert.Equal(1, calls);
        Assert.Equal(10L, rows[0].Item1.Id);
        Assert.Equal(16L, rows[0].Item7.Id);
    }

    [Fact]
    public void QueryStream_Arity7_HonoursTheMapper()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE, PartF, PartG)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }, new PartF { Id = 15 }, new PartG { Id = 16 }));

        var rows = connection.QueryStream<PartA, PartB, PartC, PartD, PartE, PartF, PartG>(OneRow, options).ToList();

        Assert.Single(rows);
        Assert.Equal(10L, rows[0].Item1.Id);
    }

    [Fact]
    public void QueryFirstOrDefault_Arity7_WithMapper_ReturnsNullOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE, PartF, PartG)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }, new PartF { Id = 15 }, new PartG { Id = 16 }));

        Assert.Null(connection.QueryFirstOrDefault<PartA, PartB, PartC, PartD, PartE, PartF, PartG>(NoRows, options));
    }

    [Fact]
    public void QuerySingleOrDefault_Arity7_WithMapper_ReturnsNullOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE, PartF, PartG)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }, new PartF { Id = 15 }, new PartG { Id = 16 }));

        Assert.Null(connection.QuerySingleOrDefault<PartA, PartB, PartC, PartD, PartE, PartF, PartG>(NoRows, options));
    }

    /// <summary>
    /// The arity-specific describeType lambda, which nothing above arity 2 had ever invoked.
    /// </summary>
    [Fact]
    public void QuerySingle_Arity7_WithMapper_NamesEveryTypeWhenMoreThanOneRow()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE, PartF, PartG)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }, new PartF { Id = 15 }, new PartG { Id = 16 }));

        var ex = Assert.Throws<InvalidOperationException>(
            () => connection.QuerySingle<PartA, PartB, PartC, PartD, PartE, PartF, PartG>(TwoRows, options));

        Assert.Contains("(PartA, PartB, PartC, PartD, PartE, PartF, PartG)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryFirst_Arity7_WithMapper_ThrowsOnAnEmptyResult()
    {
        using var connection = Open();

        var options = CommandOptions<(PartA, PartB, PartC, PartD, PartE, PartF, PartG)>.WithMapper(reader => (new PartA { Id = 10 }, new PartB { Id = 11 }, new PartC { Id = 12 }, new PartD { Id = 13 }, new PartE { Id = 14 }, new PartF { Id = 15 }, new PartG { Id = 16 }));

        Assert.Throws<InvalidOperationException>(
            () => connection.QueryFirst<PartA, PartB, PartC, PartD, PartE, PartF, PartG>(NoRows, options));
    }
}
