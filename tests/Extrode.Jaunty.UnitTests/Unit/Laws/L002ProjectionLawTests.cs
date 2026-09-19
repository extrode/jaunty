#if CSCHECK

using System.Data.SQLite;

using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Extensions.Reflection;

namespace Extrode.Jaunty.Tests.Unit.Laws;

/// <summary>
/// Proof of docs/laws/002-projection-never-throws-for-missing-column.md. For every subset of the
/// entity's columns, with or without a stray extra column, projection mapping never throws, maps
/// exactly the columns present, and touches no property whose column is absent.
/// </summary>
[Trait("Law", "L002")]
public class L002ProjectionLawTests : IDisposable
{
    private static readonly string[] Columns = ["id", "name", "value", "score"];

    private readonly SQLiteConnection _connection;

    public L002ProjectionLawTests()
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

    // 2^4 masks x 2 extra-column states = 32, small enough to enumerate outright rather than
    // sample: exhaustive over this fixture's fixed 4-column entity, not a claim about every
    // possible entity shape. See docs/laws/002-projection-never-throws-for-missing-column.md.
    private static IEnumerable<(bool[] Mask, bool Extra)> AllShapes()
    {
        for (int m = 0; m < 1 << Columns.Length; m++)
        {
            bool[] mask = [.. Enumerable.Range(0, Columns.Length).Select(i => (m & (1 << i)) != 0)];
            yield return (mask, false);
            yield return (mask, true);
        }
    }

    [Fact]
    public void ProjectionMapsWhatIsPresentAndNeverThrows()
    {
        foreach ((bool[] mask, bool extra) in AllShapes())
        {
            var selected = Columns.Where((_, i) => mask[i]).ToList();
            if (selected.Count == 0 && !extra) continue;

            var select = selected.Select(c => c).ToList();
            if (extra) select.Add("999 AS extra");

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT " + string.Join(", ", select) + " FROM law_items LIMIT 1";
            using var reader = cmd.ExecuteReader();
            reader.Read();

            var setters = MetadataCache<LawItem>.GetSetters(reader, MappingMode.Projection);

            Assert.Equal(selected.Count, setters.Length);
            var mapped = setters.Select(s => s.Context.PropertyName.ToLowerInvariant()).OrderBy(n => n).ToArray();
            Assert.Equal(selected.OrderBy(n => n).ToArray(), mapped);

            var item = new LawItem();
            foreach (var setter in setters) setter.Set(item, reader);
            Assert.Equal(mask[0] ? 1 : 0, item.Id);
            Assert.Equal(mask[1] ? "Alpha" : string.Empty, item.Name);
            Assert.Equal(mask[2] ? 10 : 0, item.Value);
            Assert.Equal(mask[3] ? 100 : 0, item.Score);
        }
    }
}

#endif
