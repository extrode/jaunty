using System.Data;

using Jaunty.Interfaces;
using Jaunty.SourceGenerator.Tests.Entities;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// AUD-R25 (B8-5): the generator infers <c>IsIdentity</c> for an int/long key that carries no
/// explicit <c>[DatabaseGenerated]</c>, and the reflection path does not - so the same entity
/// produces different INSERT SQL depending on which mapper resolves it.
///
/// <para>
/// The half of that finding which is not a matter of convention is fixed and covered here. Applied
/// per property, as it was, the inference marked <em>every</em> int key column identity, so a
/// composite-key entity had all its key columns dropped from <c>InsertColumns</c> and from
/// <c>BindInsert</c> and the generated INSERT wrote a row with no key values at all. No database has
/// two identity columns, so no schema makes that emission correct. It now applies only to a
/// single-key entity, and only to the inference - an explicit <c>[DatabaseGenerated]</c> is always
/// honoured, even on a composite-key entity.
/// </para>
///
/// <para>
/// The single-key divergence from the reflection path is left standing deliberately. Converging it
/// is a product decision, not an audit fix: dropping the inference changes the SQL of every
/// source-generated entity with a conventional key, and adopting it on the reflection side changes
/// the SQL of every reflection-mapped one - and existing tests rely on both behaviours
/// (<c>FluentCrudSourceGenTests</c> inserts into real IDENTITY/SERIAL/AUTO_INCREMENT tables without
/// the attribute; <c>CoreIntegrationTestBase</c> inserts a <c>Category</c> with a client-assigned
/// key and deletes it by that key). What this file adds is that the divergence is now pinned on
/// both sides rather than merely true - see
/// <c>Jaunty.Tests.Unit.Internals.ReflectionIdentityInferenceTests</c> for the other half.
/// </para>
/// </summary>
public sealed class GeneratedIdentityInferenceTests
{
    [Fact]
    public void CompositeKey_NeitherKeyColumnIsInferredIdentity()
    {
        // The defect: both of these were marked identity, so both were dropped from the INSERT.
        IEntityMetadataSource source = new GenCompositeKeyEntity();

        EntityColumnInfo orderId = Assert.Single(source.Columns, c => c.ColumnName == "order_id");
        EntityColumnInfo productId = Assert.Single(source.Columns, c => c.ColumnName == "product_id");

        Assert.True(orderId.IsPrimaryKey);
        Assert.True(productId.IsPrimaryKey);
        Assert.False(orderId.IsIdentity);
        Assert.False(productId.IsIdentity);
    }

    [Fact]
    public void CompositeKey_BothKeyColumnsAreStillBoundOnInsert()
    {
        // The consequence that actually reached the database: with both keys inferred identity,
        // BindInsert bound neither, and the row went in with no key values.
        var command = new StubCommand();

        GenCompositeKeyEntity.BindInsert(command, new GenCompositeKeyEntity { OrderId = 11, ProductId = 22, Quantity = 3 });

        Assert.Contains(command.Bound, p => p.ParameterName == "@order_id" && Equals(p.Value, 11));
        Assert.Contains(command.Bound, p => p.ParameterName == "@product_id" && Equals(p.Value, 22));
    }

    [Fact]
    public void CompositeKey_InsertColumnsIncludesBothKeys()
    {
        IReadOnlyList<string> insertColumns = [.. GenCompositeKeyEntity.InsertColumns.Select(c => c.ColumnName)];

        Assert.Contains("order_id", insertColumns);
        Assert.Contains("product_id", insertColumns);
        Assert.Contains("quantity", insertColumns);
    }

    [Fact]
    public void SingleKey_IsStillInferredIdentity()
    {
        // The convention is narrowed, not withdrawn - this is the shape it was always meant for,
        // and the shape FluentCrudSourceGenTests inserts with against real auto-increment tables.
        IEntityMetadataSource source = new GenSingleKeyEntity();

        EntityColumnInfo id = Assert.Single(source.Columns, c => c.ColumnName == "id");

        Assert.True(id.IsPrimaryKey);
        Assert.True(id.IsIdentity);
    }

    [Fact]
    public void SingleKey_InsertColumnsStillOmitsTheInferredIdentity()
    {
        IReadOnlyList<string> insertColumns = [.. GenSingleKeyEntity.InsertColumns.Select(c => c.ColumnName)];

        Assert.DoesNotContain("id", insertColumns);
        Assert.Contains("name", insertColumns);
    }

    [Fact]
    public void CompositeKey_AnExplicitDatabaseGeneratedIsStillHonoured()
    {
        // Only the *inference* is withdrawn for a composite key. Someone who wrote the attribute
        // meant it, and a two-column key with one generated part is a real schema.
        IEntityMetadataSource source = new GenExplicitCompositeEntity();

        EntityColumnInfo id = Assert.Single(source.Columns, c => c.ColumnName == "id");
        EntityColumnInfo tenantId = Assert.Single(source.Columns, c => c.ColumnName == "tenant_id");

        Assert.True(id.IsIdentity);
        Assert.False(tenantId.IsIdentity);
    }

    [Fact]
    public void CompositeKey_AnExplicitDatabaseGeneratedIsStillOmittedFromInsert()
    {
        IReadOnlyList<string> insertColumns = [.. GenExplicitCompositeEntity.InsertColumns.Select(c => c.ColumnName)];

        Assert.DoesNotContain("id", insertColumns);
        Assert.Contains("tenant_id", insertColumns);
        Assert.Contains("name", insertColumns);
    }

    private sealed class StubParameter : IDbDataParameter
    {
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        public string ParameterName { get; set; } = "";
        public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
    }

    private sealed class StubCommand : IDbCommand
    {
        public List<StubParameter> Bound { get; } = [];

        public string CommandText { get; set; } = "";
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; } = CommandType.Text;
        public IDbConnection? Connection { get; set; }
        public IDataParameterCollection Parameters { get; }
        public IDbTransaction? Transaction { get; set; }
        public UpdateRowSource UpdatedRowSource { get; set; }

        public StubCommand() => Parameters = new Collection(Bound);

        public void Cancel() { }
        public IDbDataParameter CreateParameter() => new StubParameter();
        public void Dispose() { }
        public int ExecuteNonQuery() => 0;
        public IDataReader ExecuteReader() => throw new NotSupportedException();
        public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
        public object? ExecuteScalar() => null;
        public void Prepare() { }

        private sealed class Collection(List<StubParameter> bound) : List<object>, IDataParameterCollection
        {
            public object this[string parameterName]
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public bool Contains(string parameterName) => false;
            public int IndexOf(string parameterName) => -1;
            public void RemoveAt(string parameterName) { }

            public new int Add(object value)
            {
                if (value is StubParameter parameter)
                    bound.Add(parameter);

                base.Add(value);
                return Count - 1;
            }
        }
    }
}
