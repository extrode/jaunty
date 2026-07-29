using System.Data;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Interfaces;
using Jaunty.Internals.Read;
using Jaunty.Internals.Write;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R26 (batch 3, low/bug). Two public extension points were consumed with an unchecked
/// assumption instead of a validated contract, so a third-party implementation that got it wrong
/// failed with an opaque exception from deep inside a per-row lambda rather than a diagnostic
/// naming the hook.
///
/// <para>
/// <strong>ReflectionMultiMapperResolverN.</strong> <c>MultiEntityMapperN.CreateMapper</c> (all five
/// arities) called this public settable <c>Func&lt;Type[], IDataReader, Action&lt;object,
/// IDataRecord&gt;[]&gt;</c> and immediately indexed the returned array at <c>0..N-1</c> from inside
/// the per-row closures, with no length check. A resolver returning fewer than N delegates produced
/// an <c>IndexOutOfRangeException</c> once per row that mentioned neither the resolver, nor the
/// arity, nor the entity types. The in-repo implementation always returns exactly N, so nothing in
/// the suite exercised the failure.
/// </para>
///
/// <para>
/// <strong>ISqlDialect's foreign-key toggle pair.</strong>
/// <c>ForeignKeyToggleCoordinator.{Disable,Enable}{Sync,Async}</c> assigned
/// <c>dialect.GetDisableForeignKeyChecksSql()!</c> straight to <c>CommandText</c>. The
/// null-forgiving operator was load-bearing - <c>SqlServerDialect</c> genuinely returns null there -
/// and the only thing preventing a null command text is that every caller separately checks
/// <c>SupportsForeignKeyToggle</c> first. Those are two independent members of a public interface
/// with no invariant tying them together.
/// </para>
/// </summary>
public class ExtensionPointContractTests
{
    // ------------------------------------------------------------------
    // ReflectionMultiMapperResolverN
    // ------------------------------------------------------------------

    public class ResolverT1 { public int A { get; set; } }
    public class ResolverT2 { public int B { get; set; } }
    public class ResolverT3 { public int C { get; set; } }

    private static SqliteDataReader OpenReader(SqliteConnection connection)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 AS A, 2 AS B, 3 AS C";
        SqliteDataReader reader = command.ExecuteReader();
        reader.Read();
        return reader;
    }

    /// <summary>
    /// A short array is the case the finding describes: the closures index 0..N-1, so arity 3 with
    /// two delegates threw IndexOutOfRangeException per row, naming nothing useful.
    /// </summary>
    [Fact]
    public void AResolverReturningTooFewDelegates_IsNamed()
    {
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? original =
            JauntyConfig.ReflectionMultiMapperResolverN;

        try
        {
            JauntyConfig.ReflectionMultiMapperResolverN = (_, _) => new Action<object, IDataRecord>[]
            {
                (_, _) => { },
                (_, _) => { },
            };

            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            using SqliteDataReader reader = OpenReader(connection);

            var exception = Assert.Throws<InvalidOperationException>(
                () => global::Jaunty.Internals.Read.MultiEntityMapper<ResolverT1, ResolverT2, ResolverT3>.Build(reader));

            Assert.Contains("ReflectionMultiMapperResolverN", exception.Message, StringComparison.Ordinal);
            Assert.Contains("arity 3", exception.Message, StringComparison.Ordinal);
            Assert.Contains("ResolverT3", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            JauntyConfig.ReflectionMultiMapperResolverN = original;
        }
    }

    [Fact]
    public void AResolverReturningANullDelegate_IsNamedWithItsIndex()
    {
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? original =
            JauntyConfig.ReflectionMultiMapperResolverN;

        try
        {
            JauntyConfig.ReflectionMultiMapperResolverN = (_, _) => new Action<object, IDataRecord>[]
            {
                (_, _) => { },
                null!,
                (_, _) => { },
            };

            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            using SqliteDataReader reader = OpenReader(connection);

            var exception = Assert.Throws<InvalidOperationException>(
                () => global::Jaunty.Internals.Read.MultiEntityMapper<ResolverT1, ResolverT2, ResolverT3>.Build(reader));

            Assert.Contains("index 1", exception.Message, StringComparison.Ordinal);
            Assert.Contains("ResolverT2", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            JauntyConfig.ReflectionMultiMapperResolverN = original;
        }
    }

    // ------------------------------------------------------------------
    // ISqlDialect's SupportsForeignKeyToggle / Get*ForeignKeyChecksSql pair
    // ------------------------------------------------------------------

    /// <summary>
    /// A dialect whose two answers disagree: it claims the capability, then supplies no SQL. The
    /// caller-side <c>SupportsForeignKeyToggle</c> check waves it through, and before the fix the
    /// provider received a null CommandText.
    /// </summary>
    private sealed class DisagreeingDialect : ISqlDialect
    {
        public bool SupportsForeignKeyToggle => true;
        public string? GetDisableForeignKeyChecksSql() => null;
        public string? GetEnableForeignKeyChecksSql() => null;

        // Everything else throws: ForeignKeyToggleCoordinator must reach the contradiction on the
        // three members above without consulting anything else. A member reached here would show
        // up as NotSupportedException rather than the diagnostic under test.
        public string GetDefaultSchema() => throw new NotSupportedException();
        public string EscapeTableName(string? schemaName, string tableName) => throw new NotSupportedException();
        public string EscapeColumnName(string columnName) => throw new NotSupportedException();
        public string EscapeStringLiteral(string value) => throw new NotSupportedException();
        public string GetLastInsertIdSql(params string[] columnNames) => throw new NotSupportedException();
        public string GetPagingSql(string baseSql, int offset, int fetchNext) => throw new NotSupportedException();
        public bool IsKeyword(string identifier) => throw new NotSupportedException();
        public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar) => throw new NotSupportedException();
        public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar) => throw new NotSupportedException();
        public string GenerateCaseInsensitiveEquals(string columnName, string parameterName) => throw new NotSupportedException();
        public string FormatContainsPattern(string value) => throw new NotSupportedException();
        public string FormatStartsWithPattern(string value) => throw new NotSupportedException();
        public string FormatEndsWithPattern(string value) => throw new NotSupportedException();
        public string FormatBooleanLiteral(bool value) => throw new NotSupportedException();
        public string GenerateCoalesce(params string[] expressions) => throw new NotSupportedException();
        public string GenerateIsNull(string expression, string defaultExpression) => throw new NotSupportedException();
        public string GenerateNullIf(string expression, string compareExpression) => throw new NotSupportedException();
        public string GenerateLength(string expression) => throw new NotSupportedException();
        public string GenerateUpper(string expression) => throw new NotSupportedException();
        public string GenerateLower(string expression) => throw new NotSupportedException();
        public string GenerateTrim(string expression) => throw new NotSupportedException();
        public string GenerateSubstring(string expression, string start, string length) => throw new NotSupportedException();
        public string GenerateYear(string expression) => throw new NotSupportedException();
        public string GenerateMonth(string expression) => throw new NotSupportedException();
        public string GenerateDay(string expression) => throw new NotSupportedException();
        public string GenerateUpsertSql(string tableName, string[] insertColumns, string[] insertParams, string[] updateColumns, string[] updateParams, string[] keyColumns, string[] keyParams) => throw new NotSupportedException();
        public string GenerateRowNumber() => throw new NotSupportedException();
        public string GenerateRank() => throw new NotSupportedException();
        public string GenerateDenseRank() => throw new NotSupportedException();
        public string GenerateNTile(int buckets) => throw new NotSupportedException();
        public string GenerateOverClause(string[]? partitionBy, (string column, bool descending)[]? orderBy) => throw new NotSupportedException();
        public string GenerateWindowAggregate(string function, string? expression) => throw new NotSupportedException();
        public IBulkCopyProvider? CreateBulkCopyProvider() => throw new NotSupportedException();
        public string ParameterPrefix => throw new NotSupportedException();
        public bool RequiresAutocommitForForeignKeyToggle => throw new NotSupportedException();
        public bool SupportsUpsert => throw new NotSupportedException();
        public bool SupportsMultiRowInsert => throw new NotSupportedException();
        public int MaxParametersPerStatement => throw new NotSupportedException();
        public bool SupportsNativeBulkCopy => throw new NotSupportedException();
    }

    [Fact]
    public void ADialectThatClaimsTheToggleButSuppliesNoSql_IsNamed()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var exception = Assert.Throws<InvalidOperationException>(
            () => ForeignKeyToggleCoordinator.DisableSync(connection, new DisagreeingDialect(), null));

        Assert.Contains("DisagreeingDialect", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SupportsForeignKeyToggle", exception.Message, StringComparison.Ordinal);
        Assert.Contains("GetDisableForeignKeyChecksSql", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEnableHalfIsGuardedToo()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var exception = Assert.Throws<InvalidOperationException>(
            () => ForeignKeyToggleCoordinator.EnableSync(connection, new DisagreeingDialect(), null));

        Assert.Contains("GetEnableForeignKeyChecksSql", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The control: a dialect that answers consistently must still toggle. SQLite's toggle SQL is
    /// real, so this also proves the guard is not simply rejecting everything.
    /// </summary>
    [Fact]
    public void AConsistentDialectStillToggles()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
        Assert.True(dialect.SupportsForeignKeyToggle);

        ForeignKeyToggleCoordinator.DisableSync(connection, dialect, null);
        ForeignKeyToggleCoordinator.EnableSync(connection, dialect, null);
    }
}
