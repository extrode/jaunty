using System.Data.SQLite;

using Jaunty.Attributes;
using Jaunty.Internals.Write;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Tests CrudSqlCache SQL generation for various entity configurations using SQLite dialect.
/// </summary>
public class CrudSqlCacheTests : IDisposable
{
    private readonly SQLiteConnection _connection;

    public CrudSqlCacheTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Test Entities

    [Table("simple_items")]
    public class SimpleItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("value")]
        public int Value { get; set; }
    }

    [Table("identity_items")]
    public class IdentityItem
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("value")]
        public int Value { get; set; }
    }

    [Table("computed_items")]
    public class ComputedItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("computed_value")]
        public string ComputedValue { get; set; } = string.Empty;
    }

    [Table("composite_key_items")]
    public class CompositeKeyItem
    {
        [Key]
        [Column("key1")]
        public int Key1 { get; set; }

        [Key]
        [Column("key2")]
        public int Key2 { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class NoKeyEntity
    {
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("value")]
        public int Value { get; set; }
    }

    #endregion

    #region Insert SQL

    [Fact]
    public void GetSql_SimpleItem_GeneratesInsertSql()
    {
        var sql = CrudSqlCache.GetSql<SimpleItem>(_connection);

        Assert.Contains("INSERT INTO", sql.InsertSql);
        Assert.Contains("simple_items", sql.InsertSql);
        Assert.Contains("@Name", sql.InsertSql);
        Assert.Contains("@Value", sql.InsertSql);
    }

    [Fact]
    public void GetSql_IdentityItem_ExcludesIdentityFromInsert()
    {
        var sql = CrudSqlCache.GetSql<IdentityItem>(_connection);

        Assert.Contains("INSERT INTO", sql.InsertSql);
        Assert.DoesNotContain("@Id", sql.InsertSql);
        Assert.Contains("@Name", sql.InsertSql);
        Assert.Contains("@Value", sql.InsertSql);
    }

    [Fact]
    public void GetSql_ComputedItem_ExcludesComputedFromInsert()
    {
        var sql = CrudSqlCache.GetSql<ComputedItem>(_connection);

        Assert.DoesNotContain("computed_value", sql.InsertSql);
        Assert.DoesNotContain("@ComputedValue", sql.InsertSql);
    }

    #endregion

    #region Update SQL

    [Fact]
    public void GetSql_SimpleItem_GeneratesUpdateWithWhereClause()
    {
        var sql = CrudSqlCache.GetSql<SimpleItem>(_connection);

        Assert.Contains("UPDATE", sql.UpdateSql);
        Assert.Contains("SET", sql.UpdateSql);
        Assert.Contains("WHERE", sql.UpdateSql);
        Assert.Contains("id = @Id", sql.UpdateSql);
    }

    [Fact]
    public void GetSql_SimpleItem_UpdateExcludesKeyColumns()
    {
        var sql = CrudSqlCache.GetSql<SimpleItem>(_connection);

        // The SET clause should not contain the key column
        var setClause = sql.UpdateSql.Split(new[] { "WHERE" }, StringSplitOptions.None)[0];
        Assert.DoesNotContain("id =", setClause);
    }

    [Fact]
    public void GetSql_IdentityItem_UpdateExcludesIdentityColumn()
    {
        var sql = CrudSqlCache.GetSql<IdentityItem>(_connection);

        var setClause = sql.UpdateSql.Split(new[] { "WHERE" }, StringSplitOptions.None)[0];
        Assert.DoesNotContain("@Id", setClause);
    }

    [Fact]
    public void GetSql_CompositeKey_UpdateHasMultipleWhereConditions()
    {
        var sql = CrudSqlCache.GetSql<CompositeKeyItem>(_connection);

        Assert.Contains("AND", sql.UpdateSql);
        Assert.Contains("key1 = @Key1", sql.UpdateSql);
        Assert.Contains("key2 = @Key2", sql.UpdateSql);
    }

    [Fact]
    public void GetSql_NoKeyEntity_UpdateSqlIsEmpty()
    {
        var sql = CrudSqlCache.GetSql<NoKeyEntity>(_connection);

        Assert.Equal(string.Empty, sql.UpdateSql);
    }

    #endregion

    #region Delete SQL

    [Fact]
    public void GetSql_SimpleItem_GeneratesDeleteSql()
    {
        var sql = CrudSqlCache.GetSql<SimpleItem>(_connection);

        Assert.Contains("DELETE FROM", sql.DeleteSql);
        Assert.Contains("simple_items", sql.DeleteSql);
        Assert.Contains("WHERE", sql.DeleteSql);
    }

    [Fact]
    public void GetSql_NoKeyEntity_DeleteSqlIsEmpty()
    {
        var sql = CrudSqlCache.GetSql<NoKeyEntity>(_connection);

        Assert.Equal(string.Empty, sql.DeleteSql);
    }

    #endregion

    #region Delete by ID SQL

    [Fact]
    public void GetSql_SingleKey_GeneratesDeleteByIdSql()
    {
        var sql = CrudSqlCache.GetSql<SimpleItem>(_connection);

        Assert.Contains("DELETE FROM", sql.DeleteByIdSql);
        Assert.Contains("@Id", sql.DeleteByIdSql);
    }

    [Fact]
    public void GetSql_CompositeKey_DeleteByIdSqlIsEmpty()
    {
        var sql = CrudSqlCache.GetSql<CompositeKeyItem>(_connection);

        Assert.Equal(string.Empty, sql.DeleteByIdSql);
    }

    #endregion

    #region Upsert SQL

    [Fact]
    public void GetSql_SQLite_SupportsUpsert()
    {
        var sql = CrudSqlCache.GetSql<SimpleItem>(_connection);

        Assert.True(sql.SupportsUpsert);
        Assert.Contains("ON CONFLICT", sql.UpsertSql);
    }

    [Fact]
    public void GetSql_SimpleItem_UpsertContainsDoUpdateSet()
    {
        var sql = CrudSqlCache.GetSql<SimpleItem>(_connection);

        Assert.Contains("DO UPDATE SET", sql.UpsertSql);
    }

    #endregion

    #region Metadata Properties

    [Fact]
    public void GetSql_SimpleItem_HasPrimaryKey()
    {
        var sql = CrudSqlCache.GetSql<SimpleItem>(_connection);

        Assert.True(sql.HasPrimaryKey);
        Assert.True(sql.HasSinglePrimaryKey);
    }

    [Fact]
    public void GetSql_IdentityItem_HasIdentityKey()
    {
        var sql = CrudSqlCache.GetSql<IdentityItem>(_connection);

        Assert.True(sql.HasIdentityKey);
    }

    [Fact]
    public void GetSql_CompositeKey_HasPrimaryKeyButNotSingle()
    {
        var sql = CrudSqlCache.GetSql<CompositeKeyItem>(_connection);

        Assert.True(sql.HasPrimaryKey);
        Assert.False(sql.HasSinglePrimaryKey);
        Assert.False(sql.HasIdentityKey);
    }

    [Fact]
    public void GetSql_NoKeyEntity_HasNoPrimaryKey()
    {
        var sql = CrudSqlCache.GetSql<NoKeyEntity>(_connection);

        Assert.False(sql.HasPrimaryKey);
        Assert.False(sql.HasSinglePrimaryKey);
        Assert.False(sql.HasIdentityKey);
    }

    #endregion

    #region LastInsertIdSql

    [Fact]
    public void GetSql_SQLite_LastInsertIdSql()
    {
        var sql = CrudSqlCache.GetSql<SimpleItem>(_connection);

        Assert.Equal("SELECT last_insert_rowid();", sql.LastInsertIdSql);
    }

    #endregion

    #region Caching

    [Fact]
    public void GetSql_SameType_ReturnsCachedInstance()
    {
        var sql1 = CrudSqlCache.GetSql<SimpleItem>(_connection);
        var sql2 = CrudSqlCache.GetSql<SimpleItem>(_connection);

        Assert.Same(sql1, sql2);
    }

    [Fact]
    public void GetSql_DifferentTypes_ReturnsDifferentInstances()
    {
        var sql1 = CrudSqlCache.GetSql<SimpleItem>(_connection);
        var sql2 = CrudSqlCache.GetSql<IdentityItem>(_connection);

        Assert.NotSame(sql1, sql2);
    }

    #endregion
}
