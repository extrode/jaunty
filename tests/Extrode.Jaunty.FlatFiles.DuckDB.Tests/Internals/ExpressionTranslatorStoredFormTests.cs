using System.Linq.Expressions;

using DuckDB.NET.Data;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.FlatFiles.DuckDB.Internals;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R38-019 and AUD-R38-020: char and enum operands arrive promoted to int and were bound as
/// that int; the comparer argument of <c>Contains(source, value, comparer)</c> was dropped.
/// </summary>
public class ExpressionTranslatorStoredFormTests : IDisposable
{
    public enum Status { Open, Closed }

    public class GradedRow
    {
        public int Id { get; set; }
        public char Grade { get; set; }

        [EnumStorage(EnumStorage.String)]
        public Status State { get; set; }

        [EnumStorage(EnumStorage.Numeric)]
        public Status Code { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public class NullableRow
    {
        public int Id { get; set; }
        public char? Grade { get; set; }

        [EnumStorage(EnumStorage.String)]
        public Status? State { get; set; }
    }

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_r38_stored_{Guid.NewGuid():N}");
    private readonly DuckDb _db;

    public ExpressionTranslatorStoredFormTests()
    {
        Directory.CreateDirectory(_dataDir);
        var csvPath = Path.Combine(_dataDir, "graded.csv");

        using (var generator = new DuckDBConnection("DataSource=:memory:"))
        {
            generator.Open();
            using DuckDBCommand cmd = generator.CreateCommand();
            cmd.CommandText = $@"
                COPY (
                    SELECT * FROM (VALUES
                        (1, 'A', 'Open',   0, 'Alice'),
                        (2, 'B', 'Closed', 1, 'bob'),
                        (3, 'A', 'Closed', 1, 'Carol')
                    ) AS t(""Id"", ""Grade"", ""State"", ""Code"", ""Name"")
                ) TO '{csvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
            cmd.ExecuteNonQuery();
        }

        var options = new FlatFileOptions();
        options.AddCsv<GradedRow>(csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    private static (string Sql, object?[] Values) Translate<T>(Expression<Func<T, bool>> predicate)
    {
        var (sql, parameters) = ExpressionTranslator.Translate(predicate);
        return (sql, parameters.Select(p => p.Value).ToArray());
    }

    [Fact]
    public void CharComparison_BindsTheOneCharacterString()
    {
        var (sql, values) = Translate<GradedRow>(r => r.Grade == 'A');

        Assert.Equal("\"Grade\" = $1", sql);
        Assert.Equal(new object?[] { "A" }, values);
    }

    [Fact]
    public void CharComparison_WithTheMemberOnTheRight_BindsTheOneCharacterString()
    {
        var (sql, values) = Translate<GradedRow>(r => 'B' != r.Grade);

        Assert.Equal("\"Grade\" != $1", sql);
        Assert.Equal(new object?[] { "B" }, values);
    }

    [Fact]
    public void StringStoredEnumComparison_BindsTheName()
    {
        var (_, values) = Translate<GradedRow>(r => r.State == Status.Closed);

        Assert.Equal(new object?[] { "Closed" }, values);
    }

    [Fact]
    public void NumericStoredEnumComparison_BindsTheUnderlyingNumber()
    {
        var (_, values) = Translate<GradedRow>(r => r.Code == Status.Closed);

        Assert.Equal(new object?[] { 1 }, values);
    }

    [Fact]
    public void NullableCharAndEnum_BindTheStoredForm()
    {
        var (_, values) = Translate<NullableRow>(r => r.Grade == 'C' && r.State == Status.Open);

        Assert.Equal(new object?[] { "C", "Open" }, values);
    }

    [Fact]
    public void NullableEnumAgainstNull_StillBindsNothing()
    {
        var (sql, values) = Translate<NullableRow>(r => r.State == null);

        Assert.Equal("\"State\" IS NULL", sql);
        Assert.Empty(values);
    }

    [Fact]
    public void InClause_OverChars_BindsStrings()
    {
        char[] grades = ['A', 'C'];
        var (sql, values) = Translate<GradedRow>(r => grades.Contains(r.Grade));

        Assert.Equal("\"Grade\" IN ($1, $2)", sql);
        Assert.Equal(new object?[] { "A", "C" }, values);
    }

    [Fact]
    public void InClause_OverStringStoredEnums_BindsNames()
    {
        var states = new List<Status> { Status.Closed };
        var (_, values) = Translate<GradedRow>(r => states.Contains(r.State));

        Assert.Equal(new object?[] { "Closed" }, values);
    }

    [Fact]
    public void Delete_ByChar_MatchesTheRowsTheFileHolds()
    {
        Assert.Equal(2, _db.Delete<GradedRow>(r => r.Grade == 'A'));
    }

    [Fact]
    public void Delete_ByStringStoredEnum_MatchesTheRowsTheFileHolds()
    {
        Assert.Equal(2, _db.Delete<GradedRow>(r => r.State == Status.Closed));
    }

    [Fact]
    public void Delete_ByNumericStoredEnum_MatchesTheRowsTheFileHolds()
    {
        Assert.Equal(1, _db.Delete<GradedRow>(r => r.Code == Status.Open));
    }

    [Theory]
    [MemberData(nameof(IgnoreCaseComparers))]
    public void InClause_WithAnIgnoreCaseComparer_LowersBothSides(StringComparer comparer)
    {
        string[] names = ["ALICE"];
        var (sql, _) = Translate<GradedRow>(r => names.Contains(r.Name, comparer));

        Assert.Equal("lower(\"Name\") IN (lower($1))", sql);
    }

    public static TheoryData<StringComparer> IgnoreCaseComparers() => new()
    {
        StringComparer.OrdinalIgnoreCase,
        StringComparer.InvariantCultureIgnoreCase,
        StringComparer.CurrentCultureIgnoreCase,
    };

    [Fact]
    public void InClause_WithAnOrdinalOrDefaultOrNullComparer_StaysCaseSensitive()
    {
        string[] names = ["ALICE"];

        Assert.Equal("\"Name\" IN ($1)", Translate<GradedRow>(r => names.Contains(r.Name, StringComparer.Ordinal)).Sql);
        Assert.Equal("\"Name\" IN ($1)", Translate<GradedRow>(r => names.Contains(r.Name, EqualityComparer<string>.Default)).Sql);
        Assert.Equal("\"Name\" IN ($1)", Translate<GradedRow>(r => names.Contains(r.Name, null)).Sql);
    }

    [Fact]
    public void InClause_WithAnyOtherComparer_IsRejected()
    {
        string[] names = ["ALICE"];

        var ex = Assert.Throws<NotSupportedException>(() =>
            Translate<GradedRow>(r => names.Contains(r.Name, StringComparer.InvariantCulture)));

        Assert.StartsWith("Contains with the comparer", ex.Message);
        Assert.EndsWith(
            "has no SQL equivalent in flat file predicates. Use the default or an ordinal comparer, or one of StringComparer's ignore-case comparers.",
            ex.Message);
    }

    [Fact]
    public void Delete_WithAnIgnoreCaseComparer_MatchesEitherCasing()
    {
        string[] names = ["ALICE", "BOB"];

        Assert.Equal(2, _db.Delete<GradedRow>(r => names.Contains(r.Name, StringComparer.OrdinalIgnoreCase)));
    }
}
