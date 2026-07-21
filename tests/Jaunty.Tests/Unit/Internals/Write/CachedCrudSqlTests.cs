using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

namespace Jaunty.Tests.Unit.Internals.Write;

public class CachedCrudSqlTests
{
    private static ColumnMetadata IdentityColumn() =>
        new("Id", typeof(int), "id", isPrimaryKey: true, isIdentity: true, isComputed: false,
            getter: _ => null, setter: (_, _) => { });

    private static EntityMetadata Metadata() =>
        new("probe_table", null, [IdentityColumn()]);

    [Fact]
    public void InsertCommandText_HasIdentityKey_NonEmptyNonReturningLastInsertIdSql_UsesSemicolonSeparator()
    {
        var sql = new CachedCrudSql(
            insertSql: "INSERT INTO probe_table DEFAULT VALUES",
            updateSql: string.Empty,
            deleteSql: string.Empty,
            deleteByIdSql: string.Empty,
            upsertSql: string.Empty,
            lastInsertIdSql: "SELECT LAST_INSERT_ID()",
            metadata: Metadata(),
            supportsUpsert: false);

        Assert.Equal("INSERT INTO probe_table DEFAULT VALUES; SELECT LAST_INSERT_ID()", sql.InsertCommandText);
    }

    [Fact]
    public void InsertCommandText_HasIdentityKey_ReturningLastInsertIdSql_AppendsWithoutSemicolon()
    {
        var sql = new CachedCrudSql(
            insertSql: "INSERT INTO probe_table DEFAULT VALUES",
            updateSql: string.Empty,
            deleteSql: string.Empty,
            deleteByIdSql: string.Empty,
            upsertSql: string.Empty,
            lastInsertIdSql: "RETURNING id",
            metadata: Metadata(),
            supportsUpsert: false);

        Assert.Equal("INSERT INTO probe_table DEFAULT VALUES RETURNING id", sql.InsertCommandText);
    }

    // ComposeInsertCommandText used to fall into the "; {lastInsertIdSql}" branch even when
    // lastInsertIdSql was empty, producing a dangling "; " with nothing after it. Not reachable
    // via any currently-shipped dialect (identity dialects always supply a non-empty fragment),
    // but the empty case should still be handled defensively rather than emit malformed SQL.
    [Fact]
    public void InsertCommandText_HasIdentityKey_EmptyLastInsertIdSql_DoesNotProduceDanglingSeparator()
    {
        var sql = new CachedCrudSql(
            insertSql: "INSERT INTO probe_table DEFAULT VALUES",
            updateSql: string.Empty,
            deleteSql: string.Empty,
            deleteByIdSql: string.Empty,
            upsertSql: string.Empty,
            lastInsertIdSql: string.Empty,
            metadata: Metadata(),
            supportsUpsert: false);

        Assert.Equal("INSERT INTO probe_table DEFAULT VALUES", sql.InsertCommandText);
    }

    [Fact]
    public void InsertCommandText_NoIdentityKey_ReturnsInsertSqlUnchanged()
    {
        var nonIdentityColumn = new ColumnMetadata("Name", typeof(string), "name", isPrimaryKey: false, isIdentity: false, isComputed: false,
            getter: _ => null, setter: (_, _) => { });
        var metadata = new EntityMetadata("probe_table", null, [nonIdentityColumn]);

        var sql = new CachedCrudSql(
            insertSql: "INSERT INTO probe_table (name) VALUES (@Name)",
            updateSql: string.Empty,
            deleteSql: string.Empty,
            deleteByIdSql: string.Empty,
            upsertSql: string.Empty,
            lastInsertIdSql: "SELECT LAST_INSERT_ID()",
            metadata: metadata,
            supportsUpsert: false);

        Assert.Equal("INSERT INTO probe_table (name) VALUES (@Name)", sql.InsertCommandText);
    }
}
