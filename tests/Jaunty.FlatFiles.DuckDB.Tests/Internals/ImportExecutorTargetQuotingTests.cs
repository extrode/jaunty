using System.Reflection;

using Jaunty.FlatFiles.DuckDB.Internals.Import;
using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// Regression tests (AUD-R24, batch 7): ImportExecutor's target-table existence probe built its
/// SQL as <c>SELECT * FROM "{tableName}" WHERE 0=1</c>, hardcoding double-quote quoting no matter
/// which <see cref="IImportDialect"/> was resolved. That contradicts SqlServerImportDialect's own
/// documented rationale for using [brackets] - double quotes only work when QUOTED_IDENTIFIER is
/// ON - and made this the one identifier-emitting path in the pipeline that ignored the dialect.
/// The probe now routes through <see cref="IQuotedIdentifierDialect"/> when the dialect offers it.
/// </summary>
public class ImportExecutorTargetQuotingTests
{
    private static readonly MethodInfo QuoteTargetIdentifier =
        typeof(ImportExecutor).GetMethod("QuoteTargetIdentifier", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string Quote(IImportDialect dialect, string identifier) =>
        (string)QuoteTargetIdentifier.Invoke(null, [dialect, identifier])!;

    [Fact]
    public void SqlServerDialect_QuotesWithBracketsNotDoubleQuotes()
    {
        string quoted = Quote(SqlServerImportDialect.Instance, "orders");

        Assert.Equal("[orders]", quoted);
        Assert.DoesNotContain("\"", quoted);
    }

    [Fact]
    public void SqlServerDialect_DoublesEmbeddedClosingBracket()
    {
        Assert.Equal("[orders]] DROP TABLE users; --]", Quote(SqlServerImportDialect.Instance, "orders] DROP TABLE users; --"));
    }

    [Theory]
    [InlineData("orders", "\"orders\"")]
    [InlineData("orders\" DROP TABLE users; --", "\"orders\"\" DROP TABLE users; --\"")]
    public void PostgreSqlDialect_QuotesWithDoubleQuotes(string identifier, string expected)
    {
        Assert.Equal(expected, Quote(PostgreSqlImportDialect.Instance, identifier));
    }

    [Theory]
    [InlineData("orders", "\"orders\"")]
    [InlineData("orders\" DROP TABLE users; --", "\"orders\"\" DROP TABLE users; --\"")]
    public void SqliteDialect_QuotesWithDoubleQuotes(string identifier, string expected)
    {
        Assert.Equal(expected, Quote(SqliteImportDialect.Instance, identifier));
    }

    // IQuotedIdentifierDialect is deliberately optional (IImportDialect is public and this
    // assembly targets netstandard2.0, so a default interface member isn't available). A
    // caller-supplied dialect that doesn't implement it must keep the previous SQL-standard
    // behaviour rather than fail.
    [Fact]
    public void DialectWithoutQuotingSupport_FallsBackToDoubleQuotes()
    {
        Assert.Equal("\"orders\"", Quote(new QuotingUnawareDialect(), "orders"));
    }

    [Fact]
    public void DialectWithoutQuotingSupport_StillEscapesEmbeddedDoubleQuote()
    {
        Assert.Equal(
            "\"orders\"\" DROP TABLE users; --\"",
            Quote(new QuotingUnawareDialect(), "orders\" DROP TABLE users; --"));
    }

    [Fact]
    public void AllBuiltInDialects_OptIntoQuotedIdentifierDialect()
    {
        Assert.IsAssignableFrom<IQuotedIdentifierDialect>(SqlServerImportDialect.Instance);
        Assert.IsAssignableFrom<IQuotedIdentifierDialect>(PostgreSqlImportDialect.Instance);
        Assert.IsAssignableFrom<IQuotedIdentifierDialect>(SqliteImportDialect.Instance);
    }

    private sealed class QuotingUnawareDialect : IImportDialect
    {
        public string MapClrTypeToSqlType(Type clrType) => "TEXT";

        public string GenerateInsertSql(
            string tableName,
            IReadOnlyList<string> columnNames,
            IReadOnlyList<string> parameterNames,
            ConflictStrategy conflictStrategy,
            string? keyColumnName) => string.Empty;

        public string GenerateCreateTableSql(
            string tableName,
            IReadOnlyList<(string Name, Type ClrType, bool IsPrimaryKey, bool IsNullable)> columns) => string.Empty;
    }
}
