using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Providers.MySql;
using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

public class MySqlSchemaReaderTests
{
    [Fact]
    public void ShouldSkipDatabase_NoIncludeSchemas_ReturnsFalse()
    {
        var options = new SchemaReaderOptions();

        Assert.False(MySqlSchemaReader.ShouldSkipDatabase(options, "mydb"));
    }

    [Fact]
    public void ShouldSkipDatabase_IncludeSchemasContainsCurrentDatabase_ReturnsFalse()
    {
        var options = new SchemaReaderOptions { IncludeSchemas = ["mydb"] };

        Assert.False(MySqlSchemaReader.ShouldSkipDatabase(options, "mydb"));
    }

    [Fact]
    public void ShouldSkipDatabase_IncludeSchemasContainsCurrentDatabase_CaseInsensitive_ReturnsFalse()
    {
        var options = new SchemaReaderOptions { IncludeSchemas = ["MyDb"] };

        Assert.False(MySqlSchemaReader.ShouldSkipDatabase(options, "mydb"));
    }

    [Fact]
    public void ShouldSkipDatabase_IncludeSchemasExcludesCurrentDatabase_ReturnsTrue()
    {
        var options = new SchemaReaderOptions { IncludeSchemas = ["otherdb"] };

        Assert.True(MySqlSchemaReader.ShouldSkipDatabase(options, "mydb"));
    }

    [Fact]
    public void ShouldSkipDatabase_IncludeSchemasHasMultipleDatabasesIncludingCurrent_ReturnsFalse()
    {
        var options = new SchemaReaderOptions { IncludeSchemas = ["otherdb", "mydb"] };

        Assert.False(MySqlSchemaReader.ShouldSkipDatabase(options, "mydb"));
    }
}
