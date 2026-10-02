using Extrode.Jaunty.Scaffolding.Abstractions;
using Extrode.Jaunty.Scaffolding.Configuration;

using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Unit;

public class ScaffolderReaderOptionsTests
{
    [Fact]
    public void ToReaderOptions_EmptyFilters_BecomeNull()
    {
        SchemaReaderOptions result = Scaffolder.ToReaderOptions(new ScaffoldOptions());

        Assert.Null(result.IncludeTables);
        Assert.Null(result.ExcludeTables);
        Assert.Null(result.IncludeSchemas);
    }

    [Fact]
    public void ToReaderOptions_PopulatedFilters_PassThroughUnchanged()
    {
        var options = new ScaffoldOptions
        {
            IncludeTables = ["a", "b"],
            ExcludeTables = ["c"],
            IncludeSchemas = ["s"]
        };

        SchemaReaderOptions result = Scaffolder.ToReaderOptions(options);

        Assert.Same(options.IncludeTables, result.IncludeTables);
        Assert.Same(options.ExcludeTables, result.ExcludeTables);
        Assert.Same(options.IncludeSchemas, result.IncludeSchemas);
    }

    [Fact]
    public void ToReaderOptions_EachFilterIsIndependent()
    {
        var onlyTables = new ScaffoldOptions { IncludeTables = ["a"] };
        var onlyExclude = new ScaffoldOptions { ExcludeTables = ["c"] };
        var onlySchemas = new ScaffoldOptions { IncludeSchemas = ["s"] };

        SchemaReaderOptions tables = Scaffolder.ToReaderOptions(onlyTables);
        SchemaReaderOptions exclude = Scaffolder.ToReaderOptions(onlyExclude);
        SchemaReaderOptions schemas = Scaffolder.ToReaderOptions(onlySchemas);

        Assert.Equal(["a"], tables.IncludeTables);
        Assert.Null(tables.ExcludeTables);
        Assert.Null(tables.IncludeSchemas);
        Assert.Null(exclude.IncludeTables);
        Assert.Equal(["c"], exclude.ExcludeTables);
        Assert.Null(exclude.IncludeSchemas);
        Assert.Null(schemas.IncludeTables);
        Assert.Null(schemas.ExcludeTables);
        Assert.Equal(["s"], schemas.IncludeSchemas);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToReaderOptions_ForeignKeyFlag_PassesThrough(bool includeForeignKeys)
    {
        SchemaReaderOptions result = Scaffolder.ToReaderOptions(new ScaffoldOptions { IncludeForeignKeys = includeForeignKeys });

        Assert.Equal(includeForeignKeys, result.IncludeForeignKeys);
    }

    [Fact]
    public void TryGetValue_NoCandidatePresent_ReturnsFalseWithEmptyValue()
    {
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["other"] = "x" };

        bool found = Scaffolder.TryGetValue(keys, out string value, "first", "second");

        Assert.False(found);
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void TryGetValue_LaterCandidatePresent_ReturnsItsValue()
    {
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["second"] = "two" };

        bool found = Scaffolder.TryGetValue(keys, out string value, "first", "second");

        Assert.True(found);
        Assert.Equal("two", value);
    }

    [Fact]
    public void TryGetValue_SeveralCandidatesPresent_FirstCandidateWins()
    {
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["first"] = "one", ["second"] = "two" };

        bool found = Scaffolder.TryGetValue(keys, out string value, "first", "second");

        Assert.True(found);
        Assert.Equal("one", value);
    }
}
