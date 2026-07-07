using Jaunty.Internals.Write;

using Microsoft.Data.Sqlite;

namespace Jaunty.Fluent.SourceGen.Tests.Integration;

/// <summary>
/// Spec 003 (fluent NativeAOT-safe metadata) task T008: unit-level check that
/// CrudSqlCache's actionable failure message (User Story 4 / T011-T012) also names both
/// remedies, mirroring FluentMetadataCache's equivalent message. The successful-resolution
/// side of this tier is already proven end-to-end by FluentCrudSourceGenTests (T009) across
/// all 4 real dialects; this test covers the failure path specifically.
///
/// Calls CrudSqlCache.GetSql&lt;T&gt;() directly (internal, accessible via InternalsVisibleTo)
/// rather than through the public Insert&lt;T&gt;() API: InsertCore checks
/// WriteParameterCache&lt;T&gt;.InsertBinder first and throws its own, separate,
/// pre-existing "no parameter binder" message before ever reaching CrudSqlCache for a
/// fully-unmapped type - a different error path, out of scope for this spec (T011/T012 only
/// covers the metadata-resolution messages in FluentMetadataCache/CrudSqlCache).
/// </summary>
public sealed class CrudSqlCacheSourceGenTests
{
    [Fact]
    public void GetSql_UnmappedType_ThrowsActionableError()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");

        var ex = Assert.Throws<InvalidOperationException>(() => CrudSqlCache.GetSql<NotMappedEntity>(connection));

        Assert.Contains("source generator", ex.Message);
        Assert.Contains("UseReflectionMapping", ex.Message);
    }

    private class NotMappedEntity
    {
        public int Id { get; set; }
    }
}
