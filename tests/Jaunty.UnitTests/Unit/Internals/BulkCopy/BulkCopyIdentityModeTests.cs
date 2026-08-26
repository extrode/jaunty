using System.Reflection;

using Jaunty.Attributes;

using Jaunty.Internals.BulkCopy;
using Jaunty.Internals.Entity;

namespace Jaunty.Tests.Unit.Internals.BulkCopy;

/// <summary>
/// AUD-R26-061 (round 26, batch 6, low/consistency - and the finding understated it).
///
/// <para>
/// The finding says three of <c>BulkCopyOptions</c>' seven settable members - <c>IdentityMode</c>,
/// <c>CheckConstraints</c>, <c>TableLock</c> - are read by the SQL Server provider and by no one
/// else, so <c>BulkCopyIdentityMode.KeepIdentity</c> works on SQL Server and is silently dropped on
/// PostgreSQL and MySQL. The first half is true. The second is not: <c>KeepIdentity</c> is a no-op
/// on <em>every</em> provider, SQL Server included.
/// </para>
///
/// <para>
/// The reason is upstream of all four providers. <c>BulkInsert</c> streams entities through
/// <c>EntityDataReader&lt;T&gt;</c>, which exposes <c>EntityMetadata.InsertColumns</c> - and
/// <c>InsertColumns</c> is built as "not identity and not computed". The identity value is
/// therefore never in the data stream, and no provider-side flag can preserve a value it was never
/// sent. <c>SqlBulkCopyOptions.KeepIdentity</c> tells SQL Server not to reseed for incoming identity
/// values; with the column absent from <c>ApplyColumnMappings</c>' mapping there are none, so the
/// server generates them exactly as it would have anyway. <c>SqlServerBulkCopyProvider</c>'s own
/// <c>ApplyColumnMappings</c> summary states the premise outright: the destination table has columns
/// absent from the source, "e.g. an IDENTITY key".
/// </para>
///
/// <para>
/// These tests pin the structural fact rather than the round-trip, so they hold without a server.
/// Making <c>KeepIdentity</c> work means changing which columns <c>EntityDataReader</c> streams,
/// which changes the SQL every provider emits - a feature, not a doc fix - so it is recorded and
/// carried forward, and these tests are what would have to change deliberately to do it.
/// </para>
/// </summary>
public class BulkCopyIdentityModeTests
{
    private class IdentityEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
    }

    private static EntityMetadata IdentityMetadata()
    {
        PropertyInfo id = typeof(IdentityEntity).GetProperty(nameof(IdentityEntity.Id))!;
        PropertyInfo name = typeof(IdentityEntity).GetProperty(nameof(IdentityEntity.Name))!;
        PropertyInfo quantity = typeof(IdentityEntity).GetProperty(nameof(IdentityEntity.Quantity))!;

        var columns = new List<ColumnMetadata>
        {
            new(id, "Id", isPrimaryKey: true, databaseGeneratedOption: DatabaseGeneratedOption.Identity),
            new(name, "Name", isPrimaryKey: false, databaseGeneratedOption: null),
            new(quantity, "Quantity", isPrimaryKey: false, databaseGeneratedOption: null),
        };

        return new EntityMetadata("identity_entities", null, columns);
    }

    [Fact]
    public void InsertColumns_ExcludeTheIdentityColumn()
    {
        EntityMetadata metadata = IdentityMetadata();

        Assert.DoesNotContain(metadata.InsertColumns, c => c.ColumnName == "Id");
        Assert.Equal(new[] { "Name", "Quantity" }, metadata.InsertColumns.Select(c => c.ColumnName).ToArray());
    }

    /// <summary>
    /// The step that makes <c>KeepIdentity</c> unreachable: what the providers receive has no
    /// identity column in it, on every provider, before any option is consulted.
    /// </summary>
    [Fact]
    public void EntityDataReader_DoesNotStreamTheIdentityColumn()
    {
        var entities = new List<IdentityEntity>
        {
            new() { Id = 41, Name = "kept?", Quantity = 7 }
        };

        using var reader = new EntityDataReader<IdentityEntity>(entities, IdentityMetadata());

        Assert.Equal(2, reader.FieldCount);

        var names = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
            names.Add(reader.GetName(i));

        Assert.DoesNotContain("Id", names);

        // And the value the caller set is nowhere in the row it would send.
        Assert.True(reader.Read());
        var values = new List<object?>();
        for (int i = 0; i < reader.FieldCount; i++)
            values.Add(reader.GetValue(i));

        Assert.DoesNotContain(41, values.OfType<int>());
    }

    /// <summary>
    /// A non-identity primary key is a different case and must keep flowing - the exclusion is on
    /// <c>IsIdentity</c>, not on <c>IsPrimaryKey</c>. Without this, "fixing" identity handling by
    /// widening the filter would silently start or stop sending assigned keys.
    /// </summary>
    [Fact]
    public void EntityDataReader_StillStreamsAnAssignedPrimaryKey()
    {
        PropertyInfo id = typeof(IdentityEntity).GetProperty(nameof(IdentityEntity.Id))!;
        PropertyInfo name = typeof(IdentityEntity).GetProperty(nameof(IdentityEntity.Name))!;

        var metadata = new EntityMetadata("assigned_key_entities", null, new List<ColumnMetadata>
        {
            new(id, "Id", isPrimaryKey: true, databaseGeneratedOption: null),
            new(name, "Name", isPrimaryKey: false, databaseGeneratedOption: null),
        });

        var entities = new List<IdentityEntity> { new() { Id = 41, Name = "assigned" } };
        using var reader = new EntityDataReader<IdentityEntity>(entities, metadata);

        Assert.Equal(2, reader.FieldCount);
        Assert.Equal("Id", reader.GetName(0));
        Assert.True(reader.Read());
        Assert.Equal(41, reader.GetValue(0));
    }

    /// <summary>
    /// A computed column is excluded for a different reason than an identity column - the database
    /// owns the value outright - and that exclusion is not what AUD-R26-061 is about. Pinned so the
    /// two stay distinguishable.
    /// </summary>
    [Fact]
    public void EntityDataReader_ExcludesComputedColumnsToo()
    {
        PropertyInfo name = typeof(IdentityEntity).GetProperty(nameof(IdentityEntity.Name))!;
        PropertyInfo quantity = typeof(IdentityEntity).GetProperty(nameof(IdentityEntity.Quantity))!;

        var metadata = new EntityMetadata("computed_entities", null, new List<ColumnMetadata>
        {
            new(name, "Name", isPrimaryKey: false, databaseGeneratedOption: null),
            new(quantity, "Quantity", isPrimaryKey: false, databaseGeneratedOption: DatabaseGeneratedOption.Computed),
        });

        using var reader = new EntityDataReader<IdentityEntity>(new List<IdentityEntity> { new() }, metadata);

        Assert.Equal(1, reader.FieldCount);
        Assert.Equal("Name", reader.GetName(0));
    }
}
