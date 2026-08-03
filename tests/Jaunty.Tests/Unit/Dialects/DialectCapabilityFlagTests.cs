using Jaunty.Configuration;
using Jaunty.Dialects;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// AUD-R35-160 and AUD-R35-161. Five members on <see cref="MySqlDialect"/> and
/// <see cref="PostgreSqlDialect"/> had no direct assertion anywhere in <c>tests/</c>:
/// <c>RequiresAutocommitForForeignKeyToggle</c>, <c>SupportsMultiRowInsert</c>,
/// <c>SupportsNativeBulkCopy</c>, <c>CreateBulkCopyProvider</c> and <c>ParameterPrefix</c>. The only
/// hits were interface re-implementations in test doubles and assertions against the
/// <c>*DialectWithBulkCopy</c> wrappers, which deliberately answer the bulk-copy pair the other way -
/// so the base dialects' answers were pinned by nothing.
/// </summary>
public class DialectCapabilityFlagTests
{
    private readonly MySqlDialect _mySql = new();
    private readonly PostgreSqlDialect _postgres = new();
    private readonly SqlServerDialect _sqlServer = new();
    private readonly SQLiteDialect _sqlite = new();

    [Fact]
    public void MySql_ReportsItsCapabilities()
    {
        Assert.Equal("@", _mySql.ParameterPrefix);
        Assert.False(_mySql.RequiresAutocommitForForeignKeyToggle);
        Assert.True(_mySql.SupportsMultiRowInsert);
        Assert.False(_mySql.SupportsNativeBulkCopy);
        Assert.Null(_mySql.CreateBulkCopyProvider());
    }

    [Fact]
    public void PostgreSql_ReportsItsCapabilities()
    {
        Assert.Equal("@", _postgres.ParameterPrefix);
        Assert.False(_postgres.RequiresAutocommitForForeignKeyToggle);
        Assert.True(_postgres.SupportsMultiRowInsert);
        Assert.False(_postgres.SupportsNativeBulkCopy);
        Assert.Null(_postgres.CreateBulkCopyProvider());
    }

    /// <summary>
    /// The pair the wrappers override answers <see langword="false"/> on every base dialect, so a
    /// missing forward in a wrapper is a downgrade rather than a silent upgrade.
    /// </summary>
    [Fact]
    public void NoBaseDialectClaimsNativeBulkCopy()
    {
        ISqlDialect[] dialects = [_mySql, _postgres, _sqlServer, _sqlite];

        Assert.All(dialects, d =>
        {
            Assert.False(d.SupportsNativeBulkCopy);
            Assert.Null(d.CreateBulkCopyProvider());
        });
    }

    [Fact]
    public void OnlySqliteRequiresAutocommitForTheForeignKeyToggle()
    {
        Assert.True(_sqlite.RequiresAutocommitForForeignKeyToggle);
        Assert.False(_sqlServer.RequiresAutocommitForForeignKeyToggle);
        Assert.False(_mySql.RequiresAutocommitForForeignKeyToggle);
        Assert.False(_postgres.RequiresAutocommitForForeignKeyToggle);
    }

    /// <summary>
    /// AUD-R35-159. The MySQL class comment claimed a null default schema while
    /// <c>GetDefaultSchema()</c> returns the empty string, which is what the existing test pins.
    /// </summary>
    [Fact]
    public void TheDefaultSchemasAreWhatTheClassCommentsSay()
    {
        Assert.Equal(string.Empty, _mySql.GetDefaultSchema());
        Assert.Equal("public", _postgres.GetDefaultSchema());
        Assert.Equal("dbo", _sqlServer.GetDefaultSchema());
        Assert.Equal(string.Empty, _sqlite.GetDefaultSchema());
    }
}
