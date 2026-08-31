using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.CodeGeneration;
using Jaunty.Scaffolding.Providers.SQLite;
using Jaunty.Scaffolding.Schema;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// AUD-R26: <c>EscapeStringLiteral</c> escaped backslashes and double quotes but no control
/// characters, and a regular C# string literal cannot span a line. A quoted identifier may
/// contain one, so the generator could emit a file the user cannot compile.
/// </summary>
public class EntityCodeGeneratorEscapingTests
{
    private static string Generate(string tableName, string columnName, string schemaName = "")
    {
        var table = new TableSchema
        {
            SchemaName = schemaName,
            TableName = tableName,
            Columns =
            [
                new ColumnSchema
                {
                    ColumnName = columnName,
                    DataType = "TEXT",
                    IsNullable = true,
                    IsPrimaryKey = false,
                    IsIdentity = false,
                    IsComputed = false,
                    OrdinalPosition = 1
                }
            ],
            PrimaryKey = null,
            ForeignKeys = []
        };

        return new EntityCodeGenerator(new SQLiteTypeMapper())
            .GenerateEntity(table, new CodeGeneratorOptions { Namespace = "N" });
    }

    /// <summary>
    /// The real assertion: whatever the identifier contains, the file has to parse. Anything
    /// weaker just re-states the escaping rules the code already applies.
    /// </summary>
    private static void AssertParses(string code)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
        Diagnostic[] errors = tree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            errors.Length == 0,
            "Generated code does not parse:\n" +
            string.Join("\n", errors.Select(e => "  " + e)) +
            "\n--- generated ---\n" + code);
    }

    [Theory]
    [InlineData("line1\nline2")]
    [InlineData("carriage\rreturn")]
    [InlineData("tab\there")]
    [InlineData("crlf\r\npair")]
    [InlineData("null\0char")]
    [InlineData("vertical\vtab")]
    [InlineData("form\ffeed")]
    [InlineData("bell\a")]
    [InlineData("backspace\b")]
    [InlineData("next\u0085line")]
    [InlineData("line\u2028separator")]
    [InlineData("paragraph\u2029separator")]
    [InlineData("escape\u001bchar")]
    public void ColumnNameWithAControlCharacter_StillGeneratesParseableCode(string columnName)
        => AssertParses(Generate("items", columnName));

    [Theory]
    [InlineData("table\nname")]
    [InlineData("table\rname")]
    [InlineData("table\u2028name")]
    public void TableNameWithAControlCharacter_StillGeneratesParseableCode(string tableName)
        => AssertParses(Generate(tableName, "value"));

    [Fact]
    public void SchemaNameWithAControlCharacter_StillGeneratesParseableCode()
        => AssertParses(Generate("items", "value", schemaName: "sch\nema"));

    /// <summary>
    /// The characters that were already handled must keep working - this is the regression guard
    /// on the rewrite, not on the original bug.
    /// </summary>
    [Theory]
    [InlineData("back\\slash")]
    [InlineData("quote\"inside")]
    [InlineData("both\\\"together")]
    public void ColumnNameWithABackslashOrQuote_IsStillEscaped(string columnName)
        => AssertParses(Generate("items", columnName));

    [Fact]
    public void AnOrdinaryColumnName_IsUnchangedInTheAttribute()
    {
        var code = Generate("items", "first_name");

        AssertParses(code);
        Assert.Contains("[Jaunty.Attributes.Column(\"first_name\")]", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// The escaped form must still round-trip to the original identifier - escaping that merely
    /// stripped the offending character would compile but map to a column that does not exist.
    /// </summary>
    [Fact]
    public void TheEscapedLiteral_RoundTripsToTheOriginalIdentifier()
    {
        var code = Generate("items", "line1\nline2");

        AssertParses(code);
        Assert.Contains("[Jaunty.Attributes.Column(\"line1\\nline2\")]", code, StringComparison.Ordinal);
    }
}
