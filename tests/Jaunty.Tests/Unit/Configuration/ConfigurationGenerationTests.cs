using System.Data;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Extensions.Reflection;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Configuration;

/// <summary>
/// AUD-R26 (batch 4). <c>JauntyConfig.Reset()</c> could not restore the write path.
///
/// <para>
/// <c>WriteParameterCache&lt;T&gt;</c> is a static generic whose constructor snapshotted
/// <c>JauntyConfig.Reflection{Insert,Update,Delete}BinderResolver</c> <em>once per entity type for
/// the process lifetime</em>. If that constructor happened to run while the resolvers were null -
/// which <c>Reset()</c> guarantees - then <c>Insert&lt;T&gt;</c>, <c>Update&lt;T&gt;</c> and
/// <c>Delete&lt;T&gt;</c> were permanently broken for that <c>T</c>, and re-registering the resolver
/// had no effect ever again. The read path did not have this problem, because
/// <c>DrDispatcher</c> consults <c>ReflectionMapperResolver</c> per resolution: two halves of one
/// configuration surface, one recoverable and one not.
/// </para>
///
/// <para>
/// It is a wider shape than the one entry point. Every cache derived from configuration had it:
/// <c>CrudSqlCache</c>, <c>MultiRowInsertCache</c>, <c>FluentMetadataCache</c>,
/// <c>MetadataCache&lt;T&gt;</c> and the six resolver caches in <c>JauntyReflectionExtensions</c>
/// all keyed on the entity type alone and never on the configuration they were built from. So the
/// fix is one mechanism rather than one patch: a configuration generation counter that every
/// mutator bumps and every derived cache validates against.
/// </para>
/// </summary>
[Collection("Type Handler Operations")]
public class ConfigurationGenerationTests : IDisposable
{
    public ConfigurationGenerationTests() => JauntyReflectionExtensions.UseReflectionMapping();

    /// <summary>
    /// These tests deliberately leave configuration in a non-default state, so every one of them
    /// hands the process back the way the rest of the suite expects to find it.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.Reset();
        JauntyReflectionExtensions.UseReflectionMapping();
    }

    // Each test owns its entity type. The defect is per-T and permanent, so a type another test has
    // already written would be poisoned (or already healed) before the test under test ran.

    [Table("reset_write_path")]
    public class ResetWritePathWidget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Table("reset_read_path")]
    public class ResetReadPathWidget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Table("resolver_rename")]
    public class ResolverRenameWidget
    {
        [Key]
        public int Id { get; set; }
        public string? WidgetName { get; set; }
    }

    [Table("reset_bulk_path")]
    public class ResetBulkPathWidget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Table("bulk_resolver_rename")]
    public class BulkRenameWidget
    {
        [Key]
        public int Id { get; set; }
        public string? WidgetName { get; set; }
    }

    [Table("steady_state")]
    public class SteadyStateWidget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Table("observed_write")]
    public class ObservedWriteWidget
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private static SqliteConnection OpenWith(string schema)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using SqliteCommand create = connection.CreateCommand();
        create.CommandText = schema;
        create.ExecuteNonQuery();
        return connection;
    }

    private static string? ScalarText(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() as string;
    }

    // ------------------------------------------------------------------
    // The finding, reproduced end to end
    // ------------------------------------------------------------------

    /// <summary>
    /// The measured sequence from the finding: warm the write path while the resolvers are cleared,
    /// re-register them, and write again. The second write used to fail with the same
    /// "No parameter binder found" exception as the first, forever.
    /// </summary>
    [Fact]
    public void ReRegisteringTheBinderResolverAfterResetRestoresTheWritePath()
    {
        using SqliteConnection connection =
            OpenWith("CREATE TABLE reset_write_path (Id INTEGER PRIMARY KEY, Name TEXT);");

        JauntyConfig.Reset();

        // This is the poisoning touch: WriteParameterCache<ResetWritePathWidget>'s constructor runs
        // here, with every binder resolver null.
        Assert.Throws<InvalidOperationException>(
            () => connection.Insert(new ResetWritePathWidget { Id = 1, Name = "before" }));

        JauntyReflectionExtensions.UseReflectionMapping();

        connection.Insert(new ResetWritePathWidget { Id = 2, Name = "after" });

        Assert.Equal("after", ScalarText(connection, "SELECT Name FROM reset_write_path WHERE Id = 2;"));
    }

    /// <summary>
    /// The control the finding used, and the reason it could tell the failure was constructor
    /// ordering rather than a missing resolver: the read path recovers across the same
    /// <c>Reset()</c>. It must keep doing so.
    /// </summary>
    [Fact]
    public void TheReadPathRecoversAcrossResetToo()
    {
        using SqliteConnection connection = OpenWith(
            "CREATE TABLE reset_read_path (Id INTEGER PRIMARY KEY, Name TEXT);" +
            "INSERT INTO reset_read_path (Id, Name) VALUES (1, 'row');");

        JauntyConfig.Reset();

        Assert.ThrowsAny<Exception>(
            () => connection.Query<ResetReadPathWidget>("SELECT Id, Name FROM reset_read_path;"));

        JauntyReflectionExtensions.UseReflectionMapping();

        List<ResetReadPathWidget> rows =
            connection.Query<ResetReadPathWidget>("SELECT Id, Name FROM reset_read_path;").ToList();

        Assert.Equal("row", Assert.Single(rows).Name);
    }

    /// <summary>
    /// The bulk write path resolves its own binder and its own SQL, so it gets its own pass through
    /// the same sequence.
    /// </summary>
    [Fact]
    public void ReRegisteringTheBinderResolverAfterResetRestoresTheBulkWritePath()
    {
        using SqliteConnection connection =
            OpenWith("CREATE TABLE reset_bulk_path (Id INTEGER PRIMARY KEY, Name TEXT);");

        JauntyConfig.Reset();

        var rows = new List<ResetBulkPathWidget>
        {
            new() { Id = 1, Name = "a" },
            new() { Id = 2, Name = "b" },
        };

        Assert.ThrowsAny<Exception>(() => connection.BulkInsert(rows));

        JauntyReflectionExtensions.UseReflectionMapping();

        Assert.Equal(2, connection.BulkInsert(rows));
        Assert.Equal("b", ScalarText(connection, "SELECT Name FROM reset_bulk_path WHERE Id = 2;"));
    }

    // ------------------------------------------------------------------
    // The same shape reached through the caches downstream of the resolvers
    // ------------------------------------------------------------------

    /// <summary>
    /// The deeper half. Changing <c>ColumnNameResolver</c> changes the column a property maps to,
    /// which changes the metadata, which changes the generated INSERT - but the metadata was cached
    /// per entity type in four places between the resolver and the SQL
    /// (<c>MetadataCache&lt;T&gt;</c>, <c>JauntyReflectionExtensions</c>'s resolver caches,
    /// <c>CrudSqlCache</c> and the compiled binder), none of which knew the resolver had moved.
    ///
    /// <para>
    /// The table carries both spellings so the assertion is decisive rather than merely
    /// non-throwing: after the resolver is registered the value must land in <c>widget_name</c> and
    /// <c>WidgetName</c> must be left null.
    /// </para>
    /// </summary>
    [Fact]
    public void ChangingTheColumnNameResolverAfterAnEntityHasBeenWrittenChangesWhereItsColumnsGo()
    {
        using SqliteConnection connection = OpenWith(
            "CREATE TABLE resolver_rename (Id INTEGER PRIMARY KEY, WidgetName TEXT, widget_name TEXT);");

        // First write with no resolver: the property name is the column name.
        connection.Insert(new ResolverRenameWidget { Id = 1, WidgetName = "pascal" });

        Assert.Equal("pascal", ScalarText(connection, "SELECT WidgetName FROM resolver_rename WHERE Id = 1;"));
        Assert.Null(ScalarText(connection, "SELECT widget_name FROM resolver_rename WHERE Id = 1;"));

        JauntyConfig.ColumnNameResolver = static name =>
            name == nameof(ResolverRenameWidget.WidgetName) ? "widget_name" : name;

        connection.Insert(new ResolverRenameWidget { Id = 2, WidgetName = "snake" });

        Assert.Equal("snake", ScalarText(connection, "SELECT widget_name FROM resolver_rename WHERE Id = 2;"));
        Assert.Null(ScalarText(connection, "SELECT WidgetName FROM resolver_rename WHERE Id = 2;"));
    }

    /// <summary>
    /// The same observation on the bulk path, which builds its own statement through
    /// <c>MultiRowInsertCache</c> rather than through <c>CrudSqlCache</c>. That cache is keyed on
    /// (entity, connection, batch size) and carried a comment saying a layout key was unnecessary
    /// "because the column set/order for T never varies across calls" - which is precisely what a
    /// column-name resolver varies, and the comment named the consequence: SQL whose
    /// <c>@col_row</c> parameter names no longer match the getters built for the new layout.
    /// </summary>
    [Fact]
    public void TheBulkPathFollowsAColumnNameResolverChangeToo()
    {
        using SqliteConnection connection = OpenWith(
            "CREATE TABLE bulk_resolver_rename (Id INTEGER PRIMARY KEY, WidgetName TEXT, widget_name TEXT);");

        connection.BulkInsert(new List<BulkRenameWidget> { new() { Id = 1, WidgetName = "pascal" } });

        Assert.Equal("pascal", ScalarText(connection, "SELECT WidgetName FROM bulk_resolver_rename WHERE Id = 1;"));

        JauntyConfig.ColumnNameResolver = static name =>
            name == nameof(BulkRenameWidget.WidgetName) ? "widget_name" : name;

        connection.BulkInsert(new List<BulkRenameWidget> { new() { Id = 2, WidgetName = "snake" } });

        Assert.Equal("snake", ScalarText(connection, "SELECT widget_name FROM bulk_resolver_rename WHERE Id = 2;"));
        Assert.Null(ScalarText(connection, "SELECT WidgetName FROM bulk_resolver_rename WHERE Id = 2;"));
    }

    /// <summary>
    /// <c>MultiRowInsertCache</c> gets its own direct test rather than an end-to-end one, because
    /// <c>BulkInsert</c> routes SQLite to the prepared loop deliberately - multi-row VALUES binds
    /// quadratically in Microsoft.Data.Sqlite, measured ~16x slower (PROD-120) - so no test using
    /// this suite's in-memory SQLite can reach the multi-row path at all.
    ///
    /// <para>
    /// Reaching it end to end would mean a live PostgreSQL, MySQL or SQL Server connection plus a
    /// table carrying both column spellings; calling the cache with the two metadata shapes directly
    /// observes the same thing without pretending an offline test covered a networked path. The key
    /// is identical across both calls - same entity, same connection type, same batch size - which
    /// is the defect: the cache had no way to tell the two apart.
    /// </para>
    /// </summary>
    [Fact]
    public void TheMultiRowInsertStatementFollowsAColumnNameResolverChange()
    {
        var dialect = new PostgreSqlDialect();

        string before = global::Jaunty.Internals.Write.MultiRowInsertCache.GetOrBuild(
            typeof(BulkRenameWidget), typeof(SqliteConnection), 2, MetadataFor<BulkRenameWidget>(), dialect);

        Assert.Contains("WidgetName", before, StringComparison.Ordinal);

        JauntyConfig.ColumnNameResolver = static name =>
            name == nameof(BulkRenameWidget.WidgetName) ? "widget_name" : name;

        string after = global::Jaunty.Internals.Write.MultiRowInsertCache.GetOrBuild(
            typeof(BulkRenameWidget), typeof(SqliteConnection), 2, MetadataFor<BulkRenameWidget>(), dialect);

        Assert.Contains("widget_name", after, StringComparison.Ordinal);
        Assert.DoesNotContain("\"WidgetName\"", after, StringComparison.Ordinal);
    }

    private static global::Jaunty.Internals.Entity.EntityMetadata MetadataFor<T>() =>
        (global::Jaunty.Internals.Entity.EntityMetadata)JauntyConfig.ReflectionTableMetadataResolver!.Invoke(typeof(T));

    // ------------------------------------------------------------------
    // "Resets all configuration options" - AUD-R26 batch 4, low/consistency
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>Reset()</c>'s summary says it resets <em>all</em> configuration options and its remarks
    /// say it is intended for test cleanup, but it reset only <c>JauntyConfig</c>'s own fields.
    /// <c>BulkCopyConfiguration</c> has six public settable statics and its own <c>Reset()</c> that
    /// this one never called, so a batch size or a disabled-native-bulk-copy flag set in one test
    /// stayed in effect for every test after it.
    /// </summary>
    [Fact]
    public void ResetRestoresBulkCopyConfigurationToo()
    {
        BulkCopyConfiguration.DefaultBatchSize = 42;
        BulkCopyConfiguration.EnableNativeBulkCopy = false;
        BulkCopyConfiguration.MinimumRowsForNativeBulkCopy = 7;

        JauntyConfig.Reset();

        Assert.Equal(10000, BulkCopyConfiguration.DefaultBatchSize);
        Assert.True(BulkCopyConfiguration.EnableNativeBulkCopy);
        Assert.Equal(100, BulkCopyConfiguration.MinimumRowsForNativeBulkCopy);
    }

    /// <summary>
    /// The other surviving surface: a dialect registered through the public
    /// <c>SqlDialectFactory.RegisterDialect</c> had no reset of any kind, so a custom dialect
    /// registered by one test governed every connection of that type name for the rest of the
    /// process.
    /// </summary>
    [Fact]
    public void ResetDropsCustomDialectRegistrations()
    {
        SqlDialectFactory.RegisterDialect("SqliteConnection", new PostgreSqlDialect());

        using (var probe = new SqliteConnection("Data Source=:memory:"))
            Assert.IsType<PostgreSqlDialect>(SqlDialectFactory.Unwrap(SqlDialectFactory.GetDialect(probe)));

        JauntyConfig.Reset();

        using (var probe = new SqliteConnection("Data Source=:memory:"))
            Assert.IsType<SQLiteDialect>(SqlDialectFactory.Unwrap(SqlDialectFactory.GetDialect(probe)));
    }

    // ------------------------------------------------------------------
    // What the mechanism must not do
    // ------------------------------------------------------------------

    /// <summary>
    /// Rebuilding on a generation change is only affordable because it does not happen when nothing
    /// changed. Configuration is normally written once at startup and read for the life of the
    /// process, so the steady state has to be a hit: the same entity resolved twice with no
    /// configuration write in between must hand back the identical cached artefacts.
    /// </summary>
    [Fact]
    public void NothingIsRebuiltWhileConfigurationIsUnchanged()
    {
        Action<IDbCommand, SteadyStateWidget>? first =
            global::Jaunty.Internals.Write.WriteParameterCache<SteadyStateWidget>.InsertBinder;
        Action<IDbCommand, SteadyStateWidget>? second =
            global::Jaunty.Internals.Write.WriteParameterCache<SteadyStateWidget>.InsertBinder;

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    /// <summary>
    /// And the converse: a configuration write must be observed by the next read, not by some later
    /// one. Reference inequality is the whole mechanism in one assertion.
    /// </summary>
    [Fact]
    public void AConfigurationWriteIsObservedByTheVeryNextRead()
    {
        Action<IDbCommand, ObservedWriteWidget>? before =
            global::Jaunty.Internals.Write.WriteParameterCache<ObservedWriteWidget>.InsertBinder;
        Assert.NotNull(before);

        JauntyConfig.ColumnNameResolver = static name => name;

        Action<IDbCommand, ObservedWriteWidget>? after =
            global::Jaunty.Internals.Write.WriteParameterCache<ObservedWriteWidget>.InsertBinder;

        Assert.NotNull(after);
        Assert.NotSame(before, after);
    }
}
