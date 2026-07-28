#if NET8_0_OR_GREATER
using System.Data;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Extensions.Reflection.Dialects;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// AUD-R25: <c>SqlDialectFactory.GetDialect</c> runs every resolved dialect through the optional
/// bulk-copy enhancement step, which - once <c>UseNativeBulkCopy()</c> has been called - substitutes
/// a <c>*DialectWithBulkCopy</c> wrapper. Those wrappers <em>implement</em> <see cref="ISqlDialect"/>
/// and delegate to the engine dialect rather than deriving from it, so every
/// <c>dialect is SQLiteDialect</c>-style engine test silently stopped matching: <c>CsvImport</c>'s
/// dispatch switch fell through to its <c>NotSupportedException</c> arm for <em>all four</em>
/// supported engines, and <c>BulkInsert</c> only kept working because it carried a
/// <c>GetType().Name.Contains("SQLite")</c> fallback.
///
/// <para>
/// <see cref="IDialectWrapper"/> plus <see cref="SqlDialectFactory.Unwrap"/> make the engine
/// identity recoverable, and both call sites now unwrap before testing the type.
/// </para>
/// </summary>
[Collection("Dialect Factory State")]
public class DialectWrapperUnwrapTests
{
    // ------------------------------------------------------------------
    // The premise: the wrappers really are not the engine dialect
    // ------------------------------------------------------------------

    [Fact]
    public void BulkCopyWrappers_AreNotAssignableToTheEngineDialectTheyDecorate()
    {
        // This is the whole reason Unwrap has to exist. If any of these ever became a subclass,
        // the corresponding `dialect is XDialect` test would start matching on its own and this
        // assertion would fail loudly rather than the fix quietly becoming redundant.
        Assert.IsNotType<SQLiteDialect>(new SQLiteDialectWithBulkCopy());
        Assert.IsNotType<SqlServerDialect>(new SqlServerDialectWithBulkCopy());
        Assert.IsNotType<PostgreSqlDialect>(new PostgreSqlDialectWithBulkCopy());
        Assert.IsNotType<MySqlDialect>(new MySqlDialectWithBulkCopy());

        Assert.False(new SQLiteDialectWithBulkCopy() is SQLiteDialect);
        Assert.False(new SqlServerDialectWithBulkCopy() is SqlServerDialect);
        Assert.False(new PostgreSqlDialectWithBulkCopy() is PostgreSqlDialect);
        Assert.False(new MySqlDialectWithBulkCopy() is MySqlDialect);
    }

    // ------------------------------------------------------------------
    // Unwrap
    // ------------------------------------------------------------------

    [Fact]
    public void Unwrap_BulkCopyWrapper_ReturnsTheDecoratedEngineDialect()
    {
        Assert.IsType<SQLiteDialect>(SqlDialectFactory.Unwrap(new SQLiteDialectWithBulkCopy()));
        Assert.IsType<SqlServerDialect>(SqlDialectFactory.Unwrap(new SqlServerDialectWithBulkCopy()));
        Assert.IsType<PostgreSqlDialect>(SqlDialectFactory.Unwrap(new PostgreSqlDialectWithBulkCopy()));
        Assert.IsType<MySqlDialect>(SqlDialectFactory.Unwrap(new MySqlDialectWithBulkCopy()));
    }

    [Fact]
    public void Unwrap_BulkCopyWrapper_PreservesTheCallersOwnInstance()
    {
        // BulkCopyDialectFactory passes the caller's dialect into the wrapper rather than
        // constructing a stock one, so Unwrap must hand back that same instance - otherwise a
        // subclassed or state-carrying dialect would be silently replaced.
        var inner = new SQLiteDialect();
        Assert.Same(inner, SqlDialectFactory.Unwrap(new SQLiteDialectWithBulkCopy(inner)));
    }

    [Fact]
    public void Unwrap_NonWrapper_ReturnsTheSameInstance()
    {
        var dialect = new SQLiteDialect();
        Assert.Same(dialect, SqlDialectFactory.Unwrap(dialect));
    }

    [Fact]
    public void Unwrap_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SqlDialectFactory.Unwrap(null!));
    }

    [Fact]
    public void Unwrap_NestedWrappers_UnwrapsAllTheWayDown()
    {
        var engine = new SQLiteDialect();
        ISqlDialect nested = new PassThroughWrapper(new PassThroughWrapper(new PassThroughWrapper(engine)));

        Assert.Same(engine, SqlDialectFactory.Unwrap(nested));
    }

    [Fact]
    public void Unwrap_SelfReferentialWrapper_TerminatesInsteadOfLooping()
    {
        // InnerDialect is on a public interface, so a third-party dialect can return itself.
        // Unwrap must degrade to "couldn't unwrap further" rather than spin forever.
        var self = new SelfReferentialWrapper();
        Assert.Same(self, SqlDialectFactory.Unwrap(self));
    }

    [Fact]
    public void Unwrap_CyclicWrapperPair_TerminatesInsteadOfLooping()
    {
        var a = new MutableWrapper();
        var b = new MutableWrapper();
        a.Inner = b;
        b.Inner = a;

        // Any bounded result is acceptable; the assertion that matters is that this returns at all.
        ISqlDialect result = SqlDialectFactory.Unwrap(a);
        Assert.True(ReferenceEquals(result, a) || ReferenceEquals(result, b));
    }

    // ------------------------------------------------------------------
    // The defect this fixes, end to end through the real CsvImport dispatch
    // ------------------------------------------------------------------

    [Fact]
    public void ImportCsv_WhenDialectIsBulkCopyWrapped_StillRoutesToTheSqliteImporter()
    {
        // Before the fix this threw NotSupportedException("CSV import is not supported for
        // connection type: SqliteConnection") - on a connection that is fully supported - because
        // SQLiteDialectWithBulkCopy matched no arm of the dispatch switch.
        var csvPath = Path.Combine(Path.GetTempPath(), $"jaunty_r25_unwrap_{Guid.NewGuid():N}.csv");
        File.WriteAllText(csvPath, "Name,Age\nAda,36\nGrace,45\n");

        // Registering by connection type both installs the custom dialect and refreshes the
        // per-type cache, so this deterministically forces the wrapper down the production path
        // without depending on BulkCopyDialectFactory's process-wide one-way switch.
        SqlDialectFactory.RegisterDialect<SqliteConnection>(new SQLiteDialectWithBulkCopy());
        try
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            using (IDbCommand create = connection.CreateCommand())
            {
                create.CommandText = "CREATE TABLE r25_unwrap (Name TEXT, Age INTEGER);";
                create.ExecuteNonQuery();
            }

            // Confirm the dispatch really is seeing the wrapper, not a plain SQLiteDialect.
            ISqlDialect resolved = SqlDialectFactory.GetDialect(connection);
            Assert.IsType<SQLiteDialectWithBulkCopy>(resolved);
            Assert.IsType<SQLiteDialect>(SqlDialectFactory.Unwrap(resolved));

            long rows = connection.ImportCsv("r25_unwrap", csvPath);

            Assert.Equal(2, rows);

            using IDbCommand count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM r25_unwrap;";
            Assert.Equal(2L, Convert.ToInt64(count.ExecuteScalar()));
        }
        finally
        {
            // Restore the resolution this type would have had anyway.
            SqlDialectFactory.RegisterDialect<SqliteConnection>(new SQLiteDialect());
            File.Delete(csvPath);
        }
    }

    // ------------------------------------------------------------------
    // Test doubles
    // ------------------------------------------------------------------

    private sealed class PassThroughWrapper(ISqlDialect inner) : DelegatingDialect(inner), IDialectWrapper
    {
        public ISqlDialect InnerDialect { get; } = inner;
    }

    private sealed class SelfReferentialWrapper() : DelegatingDialect(new SQLiteDialect()), IDialectWrapper
    {
        public ISqlDialect InnerDialect => this;
    }

    private sealed class MutableWrapper() : DelegatingDialect(new SQLiteDialect()), IDialectWrapper
    {
        public ISqlDialect? Inner { get; set; }
        public ISqlDialect InnerDialect => Inner!;
    }

    /// <summary>
    /// Forwards every <see cref="ISqlDialect"/> member to an inner dialect so the test doubles above
    /// only have to declare the wrapping behaviour under test.
    /// </summary>
    private abstract class DelegatingDialect(ISqlDialect inner) : ISqlDialect
    {
        private readonly ISqlDialect _d = inner;

        public bool SupportsForeignKeyToggle => _d.SupportsForeignKeyToggle;
        public bool RequiresAutocommitForForeignKeyToggle => _d.RequiresAutocommitForForeignKeyToggle;
        public bool SupportsUpsert => _d.SupportsUpsert;
        public bool SupportsMultiRowInsert => _d.SupportsMultiRowInsert;
        public bool SupportsNativeBulkCopy => _d.SupportsNativeBulkCopy;
        public int MaxParametersPerStatement => _d.MaxParametersPerStatement;
        public string ParameterPrefix => _d.ParameterPrefix;
        public IBulkCopyProvider? CreateBulkCopyProvider() => _d.CreateBulkCopyProvider();
        public string GetDefaultSchema() => _d.GetDefaultSchema();
        public string EscapeTableName(string? schemaName, string tableName) => _d.EscapeTableName(schemaName, tableName);
        public string EscapeColumnName(string columnName) => _d.EscapeColumnName(columnName);
        public string EscapeStringLiteral(string value) => _d.EscapeStringLiteral(value);
        public string GetLastInsertIdSql(params string[] columnNames) => _d.GetLastInsertIdSql(columnNames);
        public string GetPagingSql(string baseSql, int offset, int fetchNext) => _d.GetPagingSql(baseSql, offset, fetchNext);
        public bool IsKeyword(string identifier) => _d.IsKeyword(identifier);
        public string GenerateCaseSensitiveLike(string c, string p, string e) => _d.GenerateCaseSensitiveLike(c, p, e);
        public string GenerateCaseInsensitiveLike(string c, string p, string e) => _d.GenerateCaseInsensitiveLike(c, p, e);
        public string GenerateCaseInsensitiveEquals(string c, string p) => _d.GenerateCaseInsensitiveEquals(c, p);
        public string FormatContainsPattern(string value) => _d.FormatContainsPattern(value);
        public string FormatStartsWithPattern(string value) => _d.FormatStartsWithPattern(value);
        public string FormatEndsWithPattern(string value) => _d.FormatEndsWithPattern(value);
        public string FormatBooleanLiteral(bool value) => _d.FormatBooleanLiteral(value);
        public string? GetDisableForeignKeyChecksSql() => _d.GetDisableForeignKeyChecksSql();
        public string? GetEnableForeignKeyChecksSql() => _d.GetEnableForeignKeyChecksSql();
        public string GenerateCoalesce(params string[] expressions) => _d.GenerateCoalesce(expressions);
        public string GenerateIsNull(string expression, string defaultExpression) => _d.GenerateIsNull(expression, defaultExpression);
        public string GenerateNullIf(string expression, string compareExpression) => _d.GenerateNullIf(expression, compareExpression);
        public string GenerateLength(string expression) => _d.GenerateLength(expression);
        public string GenerateUpper(string expression) => _d.GenerateUpper(expression);
        public string GenerateLower(string expression) => _d.GenerateLower(expression);
        public string GenerateTrim(string expression) => _d.GenerateTrim(expression);
        public string GenerateSubstring(string e, string s, string l) => _d.GenerateSubstring(e, s, l);
        public string GenerateYear(string expression) => _d.GenerateYear(expression);
        public string GenerateMonth(string expression) => _d.GenerateMonth(expression);
        public string GenerateDay(string expression) => _d.GenerateDay(expression);
        public string GenerateUpsertSql(string t, string[] ic, string[] ip, string[] uc, string[] up, string[] kc, string[] kp)
            => _d.GenerateUpsertSql(t, ic, ip, uc, up, kc, kp);
        public string GenerateRowNumber() => _d.GenerateRowNumber();
        public string GenerateRank() => _d.GenerateRank();
        public string GenerateDenseRank() => _d.GenerateDenseRank();
        public string GenerateNTile(int buckets) => _d.GenerateNTile(buckets);
        public string GenerateOverClause(string[]? partitionBy, (string column, bool descending)[]? orderBy) => _d.GenerateOverClause(partitionBy, orderBy);
        public string GenerateWindowAggregate(string function, string? expression) => _d.GenerateWindowAggregate(function, expression);
    }
}
#endif
