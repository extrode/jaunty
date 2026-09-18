#if CSCHECK

using System.Data.SQLite;

using CsCheck;

using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Extensions.Reflection;

namespace Extrode.Jaunty.Tests.Unit.Laws;

/// <summary>
/// Proof of docs/laws/001-strict-mapping-rejects-unmapped-property.md. For every subset of the
/// entity's columns, with or without a stray extra column, strict mapping throws exactly when the
/// result set is not the entity's full shape, and a missing property is named in the message.
/// </summary>
[Trait("Law", "L001")]
public class L001StrictMappingLawTests : IDisposable
{
    private static readonly string[] Columns = ["id", "name", "value", "score"];

    private readonly SQLiteConnection _connection;

    public L001StrictMappingLawTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE law_items (id INTEGER PRIMARY KEY, name TEXT NOT NULL, value INTEGER NOT NULL, score INTEGER NOT NULL);
            INSERT INTO law_items (id, name, value, score) VALUES (1, 'Alpha', 10, 100);";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    public class LawItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public int Score { get; set; }
    }

    private static readonly Gen<(bool[] Mask, bool Extra)> Shapes =
        Gen.Select(Gen.Bool.Array[Columns.Length], Gen.Bool);

    [Fact]
    public void StrictThrowsExactlyWhenTheShapeIsNotFull()
    {
        Shapes.Sample(shape =>
        {
            (bool[] mask, bool extra) = shape;
            var selected = Columns.Where((_, i) => mask[i]).ToList();
            if (selected.Count == 0 && !extra) return;

            var select = selected.Select(c => c).ToList();
            if (extra) select.Add("999 AS extra");

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT " + string.Join(", ", select) + " FROM law_items LIMIT 1";
            using var reader = cmd.ExecuteReader();
            reader.Read();

            bool missing = selected.Count < Columns.Length;
            if (!missing && !extra)
            {
                var setters = MetadataCache<LawItem>.GetSetters(reader, MappingMode.Strict);
                Assert.Equal(Columns.Length, setters.Length);
                return;
            }

            var ex = Assert.Throws<InvalidOperationException>(
                () => MetadataCache<LawItem>.GetSetters(reader, MappingMode.Strict));

            if (extra)
            {
                Assert.Contains("does not map to any property", ex.Message);
                return;
            }

            string firstMissing = Columns.Where((_, i) => !mask[i]).First();
            string property = char.ToUpperInvariant(firstMissing[0]) + firstMissing.Substring(1);
            Assert.Contains("has no matching column", ex.Message);
            Assert.Contains($"'{property}'", ex.Message);
        }, iter: 500);
    }
}

#endif
