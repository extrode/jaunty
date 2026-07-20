#if NET8_0_OR_GREATER
using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Extensions.Reflection.BulkCopy;
using Jaunty.Extensions.Reflection.Dialects;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// Coverage for the Extensions.Reflection bulk-copy dialect layer (AUD-R11 batch-06): the four
/// *DialectWithBulkCopy wrappers and BulkCopyDialectFactory's Enable()/GetDialect() dispatch,
/// none of which were exercised anywhere in tests/ because no test ever called
/// UseNativeBulkCopy() or Enable() directly.
/// </summary>
[Collection("Dialect Factory State")]
public class BulkCopyDialectFactoryTests
{
    // ------------------------------------------------------------------
    // *DialectWithBulkCopy wrappers - direct instantiation, no shared state
    // ------------------------------------------------------------------

    [Fact]
    public void MySqlDialectWithBulkCopy_SupportsNativeBulkCopy_ReturnsProvider()
    {
        var wrapper = new MySqlDialectWithBulkCopy();
        Assert.True(wrapper.SupportsNativeBulkCopy);
        Assert.IsType<MySqlBulkCopyProvider>(wrapper.CreateBulkCopyProvider());
    }

    [Fact]
    public void PostgreSqlDialectWithBulkCopy_SupportsNativeBulkCopy_ReturnsProvider()
    {
        var wrapper = new PostgreSqlDialectWithBulkCopy();
        Assert.True(wrapper.SupportsNativeBulkCopy);
        Assert.IsType<PostgreSqlBulkCopyProvider>(wrapper.CreateBulkCopyProvider());
    }

    [Fact]
    public void SqlServerDialectWithBulkCopy_SupportsNativeBulkCopy_ReturnsProvider()
    {
        var wrapper = new SqlServerDialectWithBulkCopy();
        Assert.True(wrapper.SupportsNativeBulkCopy);
        Assert.IsType<SqlServerBulkCopyProvider>(wrapper.CreateBulkCopyProvider());
    }

    [Fact]
    public void SQLiteDialectWithBulkCopy_SupportsNativeBulkCopy_IsFalseWithNullProvider()
    {
        // PROD-120: SQLite intentionally has no native bulk copy provider - the former
        // SQLiteBulkCopyProvider measured ~16x slower than the loop-based BulkInsert path.
        // See the doc comment on CreateBulkCopyProvider() in the source file.
        var wrapper = new SQLiteDialectWithBulkCopy();
        Assert.False(wrapper.SupportsNativeBulkCopy);
        Assert.Null(wrapper.CreateBulkCopyProvider());
    }

    [Theory]
    [MemberData(nameof(WrapperDelegationCases))]
    public void Wrapper_DelegatesNonBulkCopyMembersToInnerDialect(ISqlDialect wrapper, ISqlDialect inner)
    {
        Assert.Equal(inner.ParameterPrefix, wrapper.ParameterPrefix);
        Assert.Equal(inner.SupportsForeignKeyToggle, wrapper.SupportsForeignKeyToggle);
        Assert.Equal(inner.SupportsUpsert, wrapper.SupportsUpsert);
        Assert.Equal(inner.SupportsMultiRowInsert, wrapper.SupportsMultiRowInsert);
        Assert.Equal(inner.MaxParametersPerStatement, wrapper.MaxParametersPerStatement);
        Assert.Equal(inner.GetDefaultSchema(), wrapper.GetDefaultSchema());
        Assert.Equal(inner.EscapeTableName(null, "orders"), wrapper.EscapeTableName(null, "orders"));
        Assert.Equal(inner.EscapeColumnName("name"), wrapper.EscapeColumnName("name"));
        Assert.Equal(inner.GetPagingSql("SELECT 1", 0, 10), wrapper.GetPagingSql("SELECT 1", 0, 10));
        Assert.Equal(inner.IsKeyword("select"), wrapper.IsKeyword("select"));
        Assert.Equal(inner.GenerateUpper("name"), wrapper.GenerateUpper("name"));
        Assert.Equal(inner.GenerateRowNumber(), wrapper.GenerateRowNumber());
    }

    public static IEnumerable<object[]> WrapperDelegationCases()
    {
        yield return new object[] { new MySqlDialectWithBulkCopy(), new MySqlDialect() };
        yield return new object[] { new PostgreSqlDialectWithBulkCopy(), new PostgreSqlDialect() };
        yield return new object[] { new SqlServerDialectWithBulkCopy(), new SqlServerDialect() };
        yield return new object[] { new SQLiteDialectWithBulkCopy(), new SQLiteDialect() };
    }

    // ------------------------------------------------------------------
    // BulkCopyDialectFactory.Enable() / GetDialect() dispatch
    // ------------------------------------------------------------------

    private sealed class UnrecognizedDialect : ISqlDialect
    {
        private readonly SQLiteDialect _inner = new();

        public bool SupportsNativeBulkCopy => false;
        public IBulkCopyProvider? CreateBulkCopyProvider() => null;
        public bool SupportsForeignKeyToggle => _inner.SupportsForeignKeyToggle;
        public bool SupportsUpsert => _inner.SupportsUpsert;
        public bool SupportsMultiRowInsert => _inner.SupportsMultiRowInsert;
        public int MaxParametersPerStatement => _inner.MaxParametersPerStatement;
        public string ParameterPrefix => _inner.ParameterPrefix;
        public string GetDefaultSchema() => _inner.GetDefaultSchema();
        public string EscapeTableName(string? schemaName, string tableName) => _inner.EscapeTableName(schemaName, tableName);
        public string EscapeColumnName(string columnName) => _inner.EscapeColumnName(columnName);
        public string GetLastInsertIdSql(params string[] columnNames) => _inner.GetLastInsertIdSql(columnNames);
        public string GetPagingSql(string baseSql, int offset, int fetchNext) => _inner.GetPagingSql(baseSql, offset, fetchNext);
        public bool IsKeyword(string identifier) => _inner.IsKeyword(identifier);
        public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar) => _inner.GenerateCaseSensitiveLike(columnName, parameterName, escapeChar);
        public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar) => _inner.GenerateCaseInsensitiveLike(columnName, parameterName, escapeChar);
        public string GenerateCaseInsensitiveEquals(string columnName, string parameterName) => _inner.GenerateCaseInsensitiveEquals(columnName, parameterName);
        public string FormatContainsPattern(string value) => _inner.FormatContainsPattern(value);
        public string FormatStartsWithPattern(string value) => _inner.FormatStartsWithPattern(value);
        public string FormatEndsWithPattern(string value) => _inner.FormatEndsWithPattern(value);
        public string? GetDisableForeignKeyChecksSql() => _inner.GetDisableForeignKeyChecksSql();
        public string? GetEnableForeignKeyChecksSql() => _inner.GetEnableForeignKeyChecksSql();
        public string GenerateCoalesce(params string[] expressions) => _inner.GenerateCoalesce(expressions);
        public string GenerateIsNull(string expression, string defaultExpression) => _inner.GenerateIsNull(expression, defaultExpression);
        public string GenerateNullIf(string expression, string compareExpression) => _inner.GenerateNullIf(expression, compareExpression);
        public string GenerateLength(string expression) => _inner.GenerateLength(expression);
        public string GenerateUpper(string expression) => _inner.GenerateUpper(expression);
        public string GenerateLower(string expression) => _inner.GenerateLower(expression);
        public string GenerateTrim(string expression) => _inner.GenerateTrim(expression);
        public string GenerateSubstring(string expression, string start, string length) => _inner.GenerateSubstring(expression, start, length);
        public string GenerateYear(string expression) => _inner.GenerateYear(expression);
        public string GenerateMonth(string expression) => _inner.GenerateMonth(expression);
        public string GenerateDay(string expression) => _inner.GenerateDay(expression);
        public string GenerateUpsertSql(string tableName, string[] insertColumns, string[] insertParams, string[] updateColumns, string[] updateParams, string[] keyColumns, string[] keyParams) => _inner.GenerateUpsertSql(tableName, insertColumns, insertParams, updateColumns, updateParams, keyColumns, keyParams);
        public string GenerateRowNumber() => _inner.GenerateRowNumber();
        public string GenerateRank() => _inner.GenerateRank();
        public string GenerateDenseRank() => _inner.GenerateDenseRank();
        public string GenerateNTile(int buckets) => _inner.GenerateNTile(buckets);
        public string GenerateOverClause(string[]? partitionBy, (string column, bool descending)[]? orderBy) => _inner.GenerateOverClause(partitionBy, orderBy);
        public string GenerateWindowAggregate(string function, string? expression) => _inner.GenerateWindowAggregate(function, expression);
    }

    [Fact]
    public void GetDialect_Enabled_WrapsKnownBuiltInDialects()
    {
        BulkCopyDialectFactory.Enable();
        try
        {
            Assert.IsType<SqlServerDialectWithBulkCopy>(BulkCopyDialectFactory.GetDialect(new SqlServerDialect()));
            Assert.IsType<PostgreSqlDialectWithBulkCopy>(BulkCopyDialectFactory.GetDialect(new PostgreSqlDialect()));
            Assert.IsType<MySqlDialectWithBulkCopy>(BulkCopyDialectFactory.GetDialect(new MySqlDialect()));
            Assert.IsType<SQLiteDialectWithBulkCopy>(BulkCopyDialectFactory.GetDialect(new SQLiteDialect()));
        }
        finally
        {
            BulkCopyDialectFactory.ResetForTests();
        }
    }

    [Fact]
    public void GetDialect_Enabled_PassesThroughUnrecognizedDialectUnchanged()
    {
        BulkCopyDialectFactory.Enable();
        try
        {
            var baseDialect = new UnrecognizedDialect();
            Assert.Same(baseDialect, BulkCopyDialectFactory.GetDialect(baseDialect));
        }
        finally
        {
            BulkCopyDialectFactory.ResetForTests();
        }
    }
}
#endif
